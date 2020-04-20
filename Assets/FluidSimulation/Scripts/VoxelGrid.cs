using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

/// <summary>
/// The class taking care of the sand simulation and its rendering
/// </summary>
public class VoxelGrid : MonoBehaviour
{
    public struct Vertex
    {
        public Vector4 vPosition;
        public Vector4 vNormal;
    };

    [SerializeField]
    private Material _terrainMaterial;

    [SerializeField]
    private GameObject _objectiveFlower;

    public float[] CollisionField { get; private set; }

    public byte[] GrassMask { get; private set; }

    public Vector3Int GpuTexturesResolution { get; private set; }

    public Vector3Int GridResolution => GpuTexturesResolution - Vector3Int.one * 2;

    public Mesh TerrainMesh { get; private set; }

    public delegate void TerrainChangedEvt(VoxelGrid grid);

    public event TerrainChangedEvt OnTerrainChanged;

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
    private int _kernelDiffusion;
    private int _kernelOverflow;
    private int _kernelCheckFlowers;

    private int _kernelMC;
    private int _kernelTripleCount;

    // marching cube buffers
    public ComputeBuffer AppendVertexBuffer { get; private set; }
    public ComputeBuffer AppendVertexBufferTerrain { get; private set; }
    public ComputeBuffer ArgBuffer { get; private set; }
    public ComputeBuffer ArgBufferTerrain { get; private set; }

    public ComputeBuffer FlowersPositionsBuffer { get; private set; }

    public ComputeBuffer FlowersWateredBuffer { get; private set; }

    public List<Vector3> Flowers { get; private set; }

    private bool _simStarted;
    //private int _remainingFluid = 15;
    private int _remainingFluid = 250;

    private MeshFilter _terrainMeshFilter;
    private MeshRenderer _terrainMeshRenderer;
    private MeshCollider _terrainMeshCollider;

    private bool _shouldDig = true;
    [SerializeField]
    private Text _lmbModeText;

    private void Awake()
    {
        _kernelResetTextures = Shader.FindKernel("ResetTextures");
        _kernelAddFluid = Shader.FindKernel("AddFluid");
        _kernelForceApplication = Shader.FindKernel("ForceApplication");
        _kernelForcePropagation = Shader.FindKernel("ForcePropagation");
        _kernelAdvection = Shader.FindKernel("Advection");
        _kernelOverflow = Shader.FindKernel("Overflow");
        _kernelDiffusion = Shader.FindKernel("Diffusion");
        _kernelCheckFlowers = Shader.FindKernel("CheckFlowers");
        _kernelMC = MarchingCubesShader.FindKernel("MarchingCubes");
        _kernelTripleCount = MarchingCubesShader.FindKernel("TripleCount");

        _terrainMeshFilter = gameObject.AddComponent<MeshFilter>();
        TerrainMesh = new Mesh();
        _terrainMeshFilter.sharedMesh = TerrainMesh;
        _terrainMeshRenderer = gameObject.AddComponent<MeshRenderer>();
        _terrainMeshRenderer.sharedMaterial = _terrainMaterial;
        _terrainMeshCollider = gameObject.AddComponent<MeshCollider>();
        _terrainMeshCollider.convex = false;
        _terrainMeshCollider.sharedMesh = TerrainMesh;
    }

    public void Setup(VoxLevelLoader.Data data)
    {
        // make border
        GpuTexturesResolution = data.Size + new Vector3Int(2, 2, 2);

        transform.localScale = new Vector3(data.Size.x / 10f, data.Size.y / 10f, data.Size.z / 10f);

        CollisionField = new float[data.Size.x * data.Size.y * data.Size.z];
        GrassMask = new byte[data.Size.x * data.Size.y];

        Flowers = new List<Vector3>();

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
                    if (VoxLevelLoader.IsGrass(color))
                    {
                        GrassMask[x + data.Size.x * z] = color.g;
                    }
                    if (VoxLevelLoader.IsFlower(color))
                    {
                        Flowers.Add(new Vector3(x, y, z) + Vector3.one);
                    }
                }
            }
        }

        FlowersPositionsBuffer = new ComputeBuffer(Flowers.Count, sizeof(float) * 3, ComputeBufferType.Structured);
        FlowersPositionsBuffer.SetData(Flowers);
        FlowersWateredBuffer = new ComputeBuffer(Flowers.Count, sizeof(float) * 3, ComputeBufferType.Structured);
        Shader.SetInt("_flowerCount", Flowers.Count);

        AppendVertexBuffer = new ComputeBuffer(GpuTexturesResolution.x * GpuTexturesResolution.y * GpuTexturesResolution.z * 5, sizeof(float) * 24, ComputeBufferType.Append);
        AppendVertexBufferTerrain = new ComputeBuffer(GpuTexturesResolution.x * GpuTexturesResolution.y * GpuTexturesResolution.z * 5, sizeof(float) * 24, ComputeBufferType.Append);
        ArgBuffer = new ComputeBuffer(4, sizeof(int), ComputeBufferType.IndirectArguments);
        ArgBufferTerrain = new ComputeBuffer(4, sizeof(int), ComputeBufferType.IndirectArguments);

        MarchingCubesShader.SetInt("_gridSizeX", GpuTexturesResolution.x);
        MarchingCubesShader.SetInt("_gridSizeY", GpuTexturesResolution.y);
        MarchingCubesShader.SetInt("_gridSizeZ", GpuTexturesResolution.z);
        MarchingCubesShader.SetFloat("_isoLevel", 0.0001f);

        CollisionTexture = new Texture3D(GpuTexturesResolution.x, GpuTexturesResolution.y, GpuTexturesResolution.z, UnityEngine.Experimental.Rendering.GraphicsFormat.R32_SFloat, UnityEngine.Experimental.Rendering.TextureCreationFlags.None);

        foreach (var f in Flowers)
        {
            var go = GameObject.Instantiate(_objectiveFlower);
            go.transform.position = f * 0.1f - transform.position - transform.localScale / 2.0f;
        }

        ToGPUCollisionField();

        ResetTextures();
    }

    private int Vector3IntPosToLinearized(Vector3Int pos)
    {
        return pos.x + GridResolution.x * (pos.y + GridResolution.y * pos.z);
    }

    public void RemoveTerrain(Vector3Int position)
    {
        // remove the terrain at the given position - and also remove grass at this point, since we are destroying the topmost level always
        for (int i = -1; i <= 1; i++)
        {
            for (int j = -1; j <= 0; j++)
            {
                for (int k = -1; k <= 1; k++)
                {
                    var newPos = position + new Vector3Int(i, j, k);
                    if (newPos.x <= 0 || newPos.x >= GridResolution.x - 1 || newPos.y <= 0 || newPos.y >= GridResolution.y - 1 || newPos.z <= 0 || newPos.z >= GridResolution.z - 1)
                    {
                        continue;
                    }

                    if (Flowers.Any(p => p.x == newPos.x && p.z == newPos.z))
                    {
                        continue;
                    }

                    CollisionField[Vector3IntPosToLinearized(newPos)] = 0;
                    GrassMask[newPos.x + GridResolution.x * newPos.z] = 0;
                }
            }
        }

        // regenerate collision field
        ToGPUCollisionField();
    }

    public void AddTerrain(Vector3Int position)
    {
        // remove the terrain at the given position - and also remove grass at this point, since we are destroying the topmost level always
        for (int i = -1; i <= 1; i++)
        {
            for (int j = -1; j <= 0; j++)
            {
                for (int k = -1; k <= 1; k++)
                {
                    var newPos = position + new Vector3Int(i, j, k);
                    if (newPos.x <= 0 || newPos.x >= GridResolution.x - 1 || newPos.y <= 0 || newPos.y >= GridResolution.y - 1 || newPos.z <= 0 || newPos.z >= GridResolution.z - 1)
                    {
                        continue;
                    }

                    if (Flowers.Any(p => p.x == newPos.x && p.z == newPos.z))
                    {
                        continue;
                    }

                    CollisionField[Vector3IntPosToLinearized(newPos)] = 1;
                    GrassMask[newPos.x + GridResolution.x * newPos.z] = 0;
                }
            }
        }

        // regenerate collision field
        ToGPUCollisionField();
    }

    public void ToGPUCollisionField()
    {
        var textureData = new float[GpuTexturesResolution.x * GpuTexturesResolution.y * GpuTexturesResolution.z];

        for (int x = 0; x < GpuTexturesResolution.x; x++)
        {
            for (int y = 0; y < GpuTexturesResolution.y; y++)
            {
                for (int z = 0; z < GpuTexturesResolution.z; z++)
                {
                    int flatIndexDst = x + GpuTexturesResolution.x * (y + GpuTexturesResolution.y * z);
                    if (x == 0 || y == 0 || z == 0 || x == GpuTexturesResolution.x - 1 || y == GpuTexturesResolution.y - 1 || z == GpuTexturesResolution.z - 1)
                    {
                        textureData[flatIndexDst] = 1;
                        continue;
                    }

                    // src data is without border, so we need to adjust calc for that
                    int flatIndexSrc = (x - 1) + GridResolution.x * ((y - 1) + GridResolution.y * (z - 1));
                    textureData[flatIndexDst] = CollisionField[flatIndexSrc];
                }
            }
        }

        CollisionTexture.SetPixelData(textureData, 0);
        CollisionTexture.Apply(false, false);

        // and also get new mesh from the GPU to replace the current one
        ComputeCollisionFieldMesh();
    }

    public void ComputeCollisionFieldMesh()
    {
        var terrainTexture = new Texture3D(GpuTexturesResolution.x, GpuTexturesResolution.y, GpuTexturesResolution.z, UnityEngine.Experimental.Rendering.GraphicsFormat.R32_SFloat, UnityEngine.Experimental.Rendering.TextureCreationFlags.None);

        var textureData = new float[GpuTexturesResolution.x * GpuTexturesResolution.y * GpuTexturesResolution.z];

        for (int x = 0; x < GpuTexturesResolution.x; x++)
        {
            for (int y = 0; y < GpuTexturesResolution.y; y++)
            {
                for (int z = 0; z < GpuTexturesResolution.z; z++)
                {
                    int flatIndexDst = x + GpuTexturesResolution.x * (y + GpuTexturesResolution.y * z);
                    if (x == 0 || y == 0 || z == 0 || x == GpuTexturesResolution.x - 1 || y == GpuTexturesResolution.y - 1 || z == GpuTexturesResolution.z - 1)
                    {
                        // textureData[flatIndexDst] = 1;
                        continue;
                    }

                    // src data is without border, so we need to adjust calc for that
                    int flatIndexSrc = (x - 1) + (GpuTexturesResolution.x - 2) * ((y - 1) + (GpuTexturesResolution.y - 2) * (z - 1));
                    textureData[flatIndexDst] = CollisionField[flatIndexSrc];
                }
            }
        }

        terrainTexture.SetPixelData(textureData, 0);
        terrainTexture.Apply(false, true);

        MarchingCubes(terrainTexture, AppendVertexBufferTerrain);

        // slow af but idgaf now
        int[] args = new int[] { 0, 1, 0, 0 };
        ArgBufferTerrain.SetData(args);
        ComputeBuffer.CopyCount(AppendVertexBufferTerrain, ArgBufferTerrain, 0);
        ArgBufferTerrain.GetData(args);
        int triCount = args[0] * 3;

        AsyncGPUReadback.Request(AppendVertexBufferTerrain, (request) =>
        {
            Vector3[] vert = new Vector3[triCount * 3];
            Vector3[] normals = new Vector3[triCount * 3];
            int[] tris = new int[triCount * 3];
            var data = request.GetData<Vertex>();
            for (int i = 0; i < triCount * 3; i++)
            {
                vert[i] = data[i].vPosition;
                normals[i] = data[i].vNormal;
                tris[i] = i;
            }

            TerrainMesh.Clear();
            TerrainMesh.MarkDynamic();
            TerrainMesh.vertices = vert;
            TerrainMesh.normals = normals;
            TerrainMesh.triangles = tris;

            TerrainMesh.UploadMeshData(false);

            _terrainMeshCollider.sharedMesh = TerrainMesh;

            OnTerrainChanged?.Invoke(this);
        });
    }

    /// <summary>
    /// A simulation step of the simulation
    /// </summary>
    private void FixedUpdate()
    {
        Shader.SetFloat("_deltaTime", Time.fixedDeltaTime);
        if (!_simStarted)
        {
            return;
        }

        AddFluid();

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

        Diffusion();

        MarchingCubes(densityTexture, AppendVertexBuffer);
        FixArgBuffer();
    }

    public void StartWater()
    {
        _simStarted = true;
    }

    public void ToggleLmbMode()
    {
        _shouldDig = !_shouldDig;
        _lmbModeText.text = _shouldDig ? "DIG" : "ADD";
    }

    private void Update()
    {
        if (Input.GetMouseButtonUp(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                var pos = (hit.point - transform.position + (transform.localScale / 2)) * 10;
                if (_shouldDig)
                {
                    RemoveTerrain(new Vector3Int(Mathf.FloorToInt(pos.x), Mathf.FloorToInt(pos.y), Mathf.FloorToInt(pos.z)));
                }
                else
                {
                    AddTerrain(new Vector3Int(Mathf.FloorToInt(pos.x), Mathf.FloorToInt(pos.y), Mathf.FloorToInt(pos.z)));
                }
            }
        }
    }

    private void MarchingCubes(Texture dataTex, ComputeBuffer vertexBuffer)
    {
        MarchingCubesShader.SetBuffer(_kernelMC, "triangleRW", vertexBuffer);
        MarchingCubesShader.SetTexture(_kernelMC, "_densityTexture", dataTex);
        vertexBuffer.SetCounterValue(0);

        MarchingCubesShader.Dispatch(_kernelMC, GpuTexturesResolution.x / 8, GpuTexturesResolution.y / 8, GpuTexturesResolution.z / 8);


        MarchingCubesShader.SetBuffer(_kernelTripleCount, "argBuffer", ArgBuffer);
        MarchingCubesShader.Dispatch(_kernelTripleCount, 1, 1, 1);
    }

    private void FixArgBuffer()
    {
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

        Shader.Dispatch(_kernelResetTextures, GpuTexturesResolution.x / 8, GpuTexturesResolution.y / 8, GpuTexturesResolution.z / 8);
    }

    private void AddFluid()
    {
        if (_remainingFluid <= 0)
        {
            return;
        }
        _remainingFluid--;
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

        Shader.Dispatch(_kernelForceApplication, GpuTexturesResolution.x / 8, GpuTexturesResolution.y / 8, GpuTexturesResolution.z / 8);

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

        Shader.Dispatch(_kernelForcePropagation, GpuTexturesResolution.x / 8, GpuTexturesResolution.y / 8, GpuTexturesResolution.z / 8);

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

        Shader.Dispatch(_kernelAdvection, GpuTexturesResolution.x / 8, GpuTexturesResolution.y / 8, GpuTexturesResolution.z / 8);

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

        Shader.Dispatch(_kernelOverflow, GpuTexturesResolution.x / 8, GpuTexturesResolution.y / 8, GpuTexturesResolution.z / 8);

        RenderTexture.ReleaseTemporary(densityTexture);

        densityTexture = tempDensity;
    }

    private void Diffusion()
    {
        var tempDensity = CreateTemporaryRT(RenderTextureFormat.RFloat);

        Shader.SetTexture(_kernelDiffusion, "density", densityTexture);
        Shader.SetTexture(_kernelDiffusion, "collision", CollisionTexture);
        Shader.SetTexture(_kernelDiffusion, "densityRW", tempDensity);

        Shader.Dispatch(_kernelDiffusion, GpuTexturesResolution.x / 8, 8, GpuTexturesResolution.z / 8);

        RenderTexture.ReleaseTemporary(densityTexture);

        densityTexture = tempDensity;
    }

    private void CheckFlowers()
    {
        Shader.SetBuffer(_kernelCheckFlowers, "flowersWatered", FlowersWateredBuffer);
        Shader.SetBuffer(_kernelCheckFlowers, "flowersPositions", FlowersPositionsBuffer);

        Shader.Dispatch(_kernelCheckFlowers, 1, 1, 1);

        AsyncGPUReadback.Request(FlowersWateredBuffer, (request) =>
        {
            var data = request.GetData<byte>();
        });
    }

    private RenderTexture CreateTemporaryRT(RenderTextureFormat format)
    {
        var temp = RenderTexture.GetTemporary(GpuTexturesResolution.x, GpuTexturesResolution.y, 0, format);
        temp.dimension = UnityEngine.Rendering.TextureDimension.Tex3D;
        temp.volumeDepth = GpuTexturesResolution.z;
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
        ArgBufferTerrain.Release();
        AppendVertexBufferTerrain.Release();
    }
}