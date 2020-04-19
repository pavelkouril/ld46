using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The class taking care of the sand simulation and its rendering
/// </summary>
public class VoxelGrid : MonoBehaviour
{
    public float[] CollisionField;

    public Vector3Int Resolution { get; private set; }

    public ComputeShader Shader;

    public ComputeShader MarchingCubesShader;

    // the render textures used to hold the voxel values
    private RenderTexture densityTexture;
    private RenderTexture velocityTexture;
    private RenderTexture forceTexture;
    private Texture3D CollisionTexture;
    private RenderTexture rbVelocityTexture;
    private RenderTexture mlhTexture;

    // soil and marching cubes compute shaders kernels
    private int _kernelResetTextures;
    private int _kernelAddFluid;
    private int _kernelCollisionField;
    private int _kernelForceApplication;
    private int _kernelForcePropagation;
    private int _kernelAdvection;
    private int _kernelSoilSlippage;
    private int _kernelOverflow;

    private int _kernelMC;
    private int _kernelTripleCount;

    // marching cube buffers
    public ComputeBuffer AppendVertexBuffer { get; private set; }
    public ComputeBuffer ArgBuffer { get; private set; }

    private bool _fluidGenerated;

    private void Awake()
    {
        _kernelResetTextures = Shader.FindKernel("ResetTextures");
        _kernelAddFluid = Shader.FindKernel("AddFluid");
        _kernelForceApplication = Shader.FindKernel("ForceApplication");
        _kernelForcePropagation = Shader.FindKernel("ForcePropagation");
        _kernelAdvection = Shader.FindKernel("Advection");
        _kernelOverflow = Shader.FindKernel("Overflow");
        _kernelMC = MarchingCubesShader.FindKernel("MarchingCubes");
        _kernelTripleCount = MarchingCubesShader.FindKernel("TripleCount");
    }

    public void Setup(VoxLevelLoader.Data data)
    {
        // make border
        Resolution = data.Size + new Vector3Int(2, 2, 2);

        CollisionField = new float[data.Size.x * data.Size.y * data.Size.z];

        for (int x = 0; x < data.Size.x; x++)
        {
            for (int y = 0; y < data.Size.y; y++)
            {
                for (int z = 0; z < data.Size.z; z++)
                {
                    int flatIndex = x + data.Size.x * (y + data.Size.y * z);
                    //bytes[flatIndex] = 1;

                    var item = data.Indices[x, y, z];
                    var color = data.Palette[item].ToColor();
                    if (VoxLevelLoader.IsTerrain(color))
                    {
                        CollisionField[flatIndex] = 1;
                    }
                    if (VoxLevelLoader.IsFluidInput(color))
                    {
                        Shader.SetInt("_FluidInputX", x + 1);
                        Shader.SetInt("_FluidInputY", y + 1);
                        Shader.SetInt("_FluidInputZ", z + 1);
                    }
                }
            }
        }

        AppendVertexBuffer = new ComputeBuffer(Resolution.x * Resolution.y * Resolution.z * 5, sizeof(float) * 24, ComputeBufferType.Append);
        ArgBuffer = new ComputeBuffer(4, sizeof(int), ComputeBufferType.IndirectArguments);

        MarchingCubesShader.SetInt("_gridSizeX", Resolution.x);
        MarchingCubesShader.SetInt("_gridSizeY", Resolution.y);
        MarchingCubesShader.SetInt("_gridSizeZ", Resolution.z);
        MarchingCubesShader.SetFloat("_isoLevel", 0.5f);

        MarchingCubesShader.SetBuffer(_kernelMC, "triangleRW", AppendVertexBuffer);

        CollisionTexture = new Texture3D(Resolution.x, Resolution.y, Resolution.z, UnityEngine.Experimental.Rendering.GraphicsFormat.R32_SFloat, UnityEngine.Experimental.Rendering.TextureCreationFlags.None);

        ToGPUCollisionField();

        ResetTextures();

        _fluidGenerated = true;
    }

    public void ToGPUCollisionField()
    {
        var textureData = new float[Resolution.x * Resolution.y * Resolution.z];

        for (int x = 0; x < Resolution.x; x++)
        {
            for (int y = 0; y < Resolution.y; y++)
            {
                for (int z = 0; z < Resolution.z; z++)
                {
                    int flatIndexDst = x + Resolution.x * (y + Resolution.y * z);
                    if (x == 0 || y == 0 || z == 0 || x == Resolution.x - 1 || y == Resolution.y - 1 || z == Resolution.z - 1)
                    {
                        textureData[flatIndexDst] = 1;
                        continue;
                    }

                    // src data is without border, so we need to adjust calc for that
                    int flatIndexSrc = (x - 1) + (Resolution.x - 2) * ((y - 1) + (Resolution.y - 2) * (z - 1));
                    textureData[flatIndexDst] = CollisionField[flatIndexSrc];
                }
            }
        }

        CollisionTexture.SetPixelData(textureData, 0);
        CollisionTexture.Apply(false, false);
    }

    /// <summary>
    /// A simulation step of the simulation
    /// </summary>
    private void FixedUpdate()
    {
        Shader.SetFloat("_deltaTime", Time.fixedDeltaTime);
        if (!_fluidGenerated)
        {
            return;
        }
        /*
        ForceApplication();
        for (var i = 0; i < 4; i++)
        {
            ForcePropagation();
        }

        Advection();

        for (var i = 0; i < 4; i++)
        {
            Overflow();
        }
        */
        MarchingCubes();
    }

    private void MarchingCubes()
    {
        MarchingCubesShader.SetTexture(_kernelMC, "_densityTexture", CollisionTexture);
        AppendVertexBuffer.SetCounterValue(0);

        MarchingCubesShader.Dispatch(_kernelMC, Resolution.x / 8, Resolution.y / 8, Resolution.z / 8);

        int[] args = new int[] { 0, 1, 0, 0 };
        ArgBuffer.SetData(args);

        ComputeBuffer.CopyCount(AppendVertexBuffer, ArgBuffer, 0);

        MarchingCubesShader.SetBuffer(_kernelTripleCount, "argBuffer", ArgBuffer);
        MarchingCubesShader.Dispatch(_kernelTripleCount, 1, 1, 1);
    }

    private void ResetTextures()
    {
        densityTexture = CreateTemporaryRT(RenderTextureFormat.RHalf);
        velocityTexture = CreateTemporaryRT(RenderTextureFormat.ARGBFloat);
        forceTexture = CreateTemporaryRT(RenderTextureFormat.ARGBFloat);

        Shader.SetTexture(_kernelResetTextures, "collision", CollisionTexture);
        Shader.SetTexture(_kernelResetTextures, "densityRW", densityTexture);
        Shader.SetTexture(_kernelResetTextures, "velocityRW", velocityTexture);
        Shader.SetTexture(_kernelResetTextures, "forceRW", forceTexture);

        Shader.Dispatch(_kernelResetTextures, Resolution.x / 8, Resolution.y / 8, Resolution.z / 8);
    }

    private void AddFluid()
    {
        Shader.SetTexture(_kernelAddFluid, "densityRW", densityTexture);
        Shader.Dispatch(_kernelAddFluid, 1, 1, 1);
    }

    private void ForceApplication()
    {
        var tempForce = CreateTemporaryRT(RenderTextureFormat.ARGBFloat);

        Shader.SetTexture(_kernelForceApplication, "force", forceTexture);
        Shader.SetTexture(_kernelForceApplication, "forceRW", tempForce);
        //Shader.SetTexture(_kernelForceApplication, "rbVelocity", rbVelocityTexture);
        Shader.SetTexture(_kernelForceApplication, "density", densityTexture);
        Shader.SetTexture(_kernelForceApplication, "collision", CollisionTexture);

        Shader.Dispatch(_kernelForceApplication, Resolution.x / 8, Resolution.y / 8, Resolution.z / 8);

        RenderTexture.ReleaseTemporary(forceTexture);
        forceTexture = tempForce;
    }

    private void ForcePropagation()
    {
        var tempForce = CreateTemporaryRT(RenderTextureFormat.ARGBFloat);
        var tempVelocity = CreateTemporaryRT(RenderTextureFormat.ARGBFloat);
        var tempDensity = CreateTemporaryRT(RenderTextureFormat.RFloat);

        Shader.SetTexture(_kernelForcePropagation, "force", forceTexture);
        Shader.SetTexture(_kernelForcePropagation, "forceRW", tempForce);
        Shader.SetTexture(_kernelForcePropagation, "density", densityTexture);
        Shader.SetTexture(_kernelForcePropagation, "densityRW", tempDensity);
        Shader.SetTexture(_kernelForcePropagation, "velocity", velocityTexture);
        Shader.SetTexture(_kernelForcePropagation, "velocityRW", tempVelocity);
        Shader.SetTexture(_kernelForcePropagation, "collision", CollisionTexture);

        Shader.Dispatch(_kernelForcePropagation, Resolution.x / 8, Resolution.y / 8, Resolution.z / 8);

        RenderTexture.ReleaseTemporary(forceTexture);
        RenderTexture.ReleaseTemporary(densityTexture);
        RenderTexture.ReleaseTemporary(velocityTexture);

        forceTexture = tempForce;
        densityTexture = tempDensity;
        velocityTexture = tempVelocity;
    }

    private void Advection()
    {
        var tempForce = CreateTemporaryRT(RenderTextureFormat.ARGBFloat);
        var tempVelocity = CreateTemporaryRT(RenderTextureFormat.ARGBFloat);
        var tempDensity = CreateTemporaryRT(RenderTextureFormat.RFloat);

        Shader.SetTexture(_kernelAdvection, "force", forceTexture);
        Shader.SetTexture(_kernelAdvection, "forceRW", tempForce);
        Shader.SetTexture(_kernelAdvection, "density", densityTexture);
        Shader.SetTexture(_kernelAdvection, "densityRW", tempDensity);
        Shader.SetTexture(_kernelAdvection, "velocity", velocityTexture);
        Shader.SetTexture(_kernelAdvection, "velocityRW", tempVelocity);
        Shader.SetTexture(_kernelAdvection, "collision", CollisionTexture);

        Shader.Dispatch(_kernelAdvection, Resolution.x / 8, Resolution.y / 8, Resolution.z / 8);

        RenderTexture.ReleaseTemporary(forceTexture);
        RenderTexture.ReleaseTemporary(densityTexture);
        RenderTexture.ReleaseTemporary(velocityTexture);

        forceTexture = tempForce;
        densityTexture = tempDensity;
        velocityTexture = tempVelocity;
    }

    private void Overflow()
    {
        var tempDensity = CreateTemporaryRT(RenderTextureFormat.RFloat);

        Shader.SetTexture(_kernelOverflow, "collision", CollisionTexture);
        Shader.SetTexture(_kernelOverflow, "density", densityTexture);
        Shader.SetTexture(_kernelOverflow, "densityRW", tempDensity);

        Shader.Dispatch(_kernelOverflow, Resolution.x / 8, Resolution.y / 8, Resolution.z / 8);

        RenderTexture.ReleaseTemporary(densityTexture);

        densityTexture = tempDensity;
    }

    private void SoilSlippage()
    {
        var tempDensity = CreateTemporaryRT(RenderTextureFormat.RFloat);

        Shader.SetTexture(_kernelSoilSlippage, "density", densityTexture);
        Shader.SetTexture(_kernelSoilSlippage, "collision", CollisionTexture);
        Shader.SetTexture(_kernelSoilSlippage, "densityRW", tempDensity);

        Shader.Dispatch(_kernelSoilSlippage, Resolution.x / 8, 8, Resolution.z / 8);

        RenderTexture.ReleaseTemporary(densityTexture);

        densityTexture = tempDensity;
    }

    private RenderTexture CreateTemporaryRT(RenderTextureFormat format)
    {
        var temp = RenderTexture.GetTemporary(Resolution.x, Resolution.y, 0, format);
        temp.dimension = UnityEngine.Rendering.TextureDimension.Tex3D;
        temp.volumeDepth = Resolution.z;
        temp.enableRandomWrite = true;
        temp.Create();
        return temp;
    }

    /// <summary>
    /// The buffers need to be released
    /// </summary>
    private void OnDestroy()
    {
        AppendVertexBuffer.Release();
        ArgBuffer.Release();
    }
}