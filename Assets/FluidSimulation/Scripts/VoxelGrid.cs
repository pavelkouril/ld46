using System;
using System.Collections;
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

    public byte[] CollisionField { get; private set; }

    public byte[] GrassMask { get; private set; }

    public Vector3Int GpuTexturesResolution { get; private set; }

    public Vector3Int GridResolution => GpuTexturesResolution - Vector3Int.one * 2;

    public Mesh TerrainMesh { get; private set; }

    public delegate void TerrainChangedEvt(VoxelGrid grid);

    public event TerrainChangedEvt OnTerrainChanged;

    public ComputeShader Shader;

    public ComputeShader MarchingCubesShader;

    public ReflectionProbe _Probe;

    // the render textures used to hold the voxel values
    private RenderTexture densityTexture;
    private Texture3D CollisionTexture;

    // soil and marching cubes compute shaders kernels
    private int _kernelResetTextures;
    private int _kernelAddFluid;
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

    public List<FlowerObjective> Flowers { get; private set; }

    private bool _simStarted;
    //private int _remainingFluid = 15;
    public int _remainingFluid;
    public int _remainigTerraform;

    private int _remainigTerraformCurr;
    private int _remainigFluidCurr;


    private MeshFilter _terrainMeshFilter;
    private MeshRenderer _terrainMeshRenderer;
    private MeshCollider _terrainMeshCollider;

    private bool _shouldDig = true;
    [SerializeField]
    private Text _lmbModeText;
    [SerializeField]
    private GameObject _victoryScreen;

    private AsyncGPUReadbackRequest _flowersRequest;

    [SerializeField]
    private GameObject _arrow;

    public Text remainTerraformText;

    public AudioClip _clip;

    public Button digbutton;

    private void Awake()
    {
        _kernelResetTextures = Shader.FindKernel("ResetTextures");
        _kernelAddFluid = Shader.FindKernel("AddFluid");
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

    private VoxLevelLoader.Data _levelData;

    public void Setup(VoxLevelLoader.Data data, bool spawnArrow = true)
    {
        _levelData = data;

        // make border
        GpuTexturesResolution = data.Size + new Vector3Int(2, 2, 2);

        transform.localScale = new Vector3(data.Size.x / 10f, data.Size.y / 10f, data.Size.z / 10f);

        CollisionField = new byte[data.Size.x * data.Size.y * data.Size.z];
        GrassMask = new byte[data.Size.x * data.Size.y];

        Flowers = new List<FlowerObjective>();
        var FlowerPos = new List<Vector3Int>();

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

                        if (spawnArrow)
                        {
                            var go = GameObject.Instantiate(_arrow);
                            go.transform.localScale = new Vector3(0.1f, 0.25f, 0.1f);
                            go.transform.position = new Vector3(x, y, z) * 0.1f - transform.position - transform.localScale / 2.0f;
                            go.GetComponentInChildren<ArrowScript>()._Renderer = GetComponent<VoxelGridRenderer>();
                        }

                        CollisionField[flatIndex] = 0;
                    }
                    if (VoxLevelLoader.IsGrass(color))
                    {
                        GrassMask[x + data.Size.x * z] = color.g;
                    }
                    if (VoxLevelLoader.IsFlower(color))
                    {
                        var f = new Vector3Int(x, y, z) + Vector3Int.one;
                        FlowerPos.Add(f);
                        var go = GameObject.Instantiate(_objectiveFlower);
                        go.transform.position = new Vector3(f.x, f.y, f.z) * 0.1f - transform.position - transform.localScale / 2.0f;
                        var flow = go.GetComponent<FlowerObjective>();
                        flow.GridPos = new Vector3(x, y, z);
                        Flowers.Add(flow);
                    }
                }
            }
        }

        FlowersPositionsBuffer = new ComputeBuffer(Flowers.Count, sizeof(int) * 3, ComputeBufferType.Structured);
        FlowersPositionsBuffer.SetData(FlowerPos);
        FlowersWateredBuffer = new ComputeBuffer(Flowers.Count, sizeof(int), ComputeBufferType.Structured);
        FlowersWateredBuffer.SetData(new int[Flowers.Count]);
        Shader.SetInt("_flowerCount", Flowers.Count);

        AppendVertexBuffer = new ComputeBuffer(GpuTexturesResolution.x * GpuTexturesResolution.y * GpuTexturesResolution.z * 5, sizeof(float) * 24, ComputeBufferType.Append);
        AppendVertexBufferTerrain = new ComputeBuffer(GpuTexturesResolution.x * GpuTexturesResolution.y * GpuTexturesResolution.z * 5, sizeof(float) * 24, ComputeBufferType.Append);
        ArgBuffer = new ComputeBuffer(4, sizeof(int), ComputeBufferType.IndirectArguments);
        ArgBufferTerrain = new ComputeBuffer(4, sizeof(int), ComputeBufferType.IndirectArguments);

        MarchingCubesShader.SetInt("_gridSizeX", GpuTexturesResolution.x);
        MarchingCubesShader.SetInt("_gridSizeY", GpuTexturesResolution.y);
        MarchingCubesShader.SetInt("_gridSizeZ", GpuTexturesResolution.z);

        Shader.SetInt("_GridY", GpuTexturesResolution.y);

        MarchingCubesShader.SetFloat("_isoLevel", 0.0001f);

        CollisionTexture = new Texture3D(GpuTexturesResolution.x, GpuTexturesResolution.y, GpuTexturesResolution.z, UnityEngine.Experimental.Rendering.GraphicsFormat.R32_SFloat, UnityEngine.Experimental.Rendering.TextureCreationFlags.None);
        _remainigTerraformCurr = _remainigTerraform;
        _remainigFluidCurr = _remainingFluid;
        remainTerraformText.text = "REMAINING TERRAFORMS " + _remainigTerraformCurr;

        ToGPUCollisionField();

        ResetTextures();
    }

    private int Vector3IntPosToLinearized(Vector3Int pos)
    {
        return pos.x + GridResolution.x * (pos.y + GridResolution.y * pos.z);
    }

    public void RemoveTerrain(Vector3Int position)
    {
        bool changed = false;
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

                    if (Flowers.Any(p => (p.GridPos.x >= newPos.x - 1 && p.GridPos.x <= newPos.x + 1) && (p.GridPos.z >= newPos.z - 1 && p.GridPos.z <= newPos.z + 1)))
                    {
                        continue;
                    }
                    changed = true;
                    CollisionField[Vector3IntPosToLinearized(newPos)] = 0;
                    GrassMask[newPos.x + GridResolution.x * newPos.z] = 0;
                }
            }
        }

        if (changed)
        {
            _remainigTerraformCurr--;
            remainTerraformText.text = "REMAINING TERRAFORMS " + _remainigTerraformCurr;
            AudioSource.PlayClipAtPoint(_clip, Vector3.zero);
        }

        // regenerate collision field
        ToGPUCollisionField();
    }

    public void AddTerrain(Vector3Int position)
    {
        bool changed = false;
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

                    if (Flowers.Any(p => (p.GridPos.x >= newPos.x - 1 && p.GridPos.x <= newPos.x + 1) && (p.GridPos.z >= newPos.z - 1 && p.GridPos.z <= newPos.z + 1)))
                    {
                        continue;
                    }
                    changed = true;

                    CollisionField[Vector3IntPosToLinearized(newPos)] = 1;
                    GrassMask[newPos.x + GridResolution.x * newPos.z] = 0;

                }
            }
        }

        if (changed)
        {
            _remainigTerraformCurr--;
            remainTerraformText.text = "REMAINING TERRAFORMS " + _remainigTerraformCurr;
            AudioSource.PlayClipAtPoint(_clip, Vector3.zero);
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

            _Probe.RenderProbe();
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

        Advection();

        Diffusion();

        Overflow();

        MarchingCubes(densityTexture, AppendVertexBuffer);
        FixArgBuffer();

        CheckFlowers();
    }

    public void StartWater()
    {
        _simStarted = true;
        digbutton.gameObject.SetActive(false);
    }

    public void ToggleLmbMode()
    {
        _shouldDig = !_shouldDig;
        _lmbModeText.text = _shouldDig ? "DIG" : "ADD";
    }

    public void Quit()
    {
        Application.Quit(0);
    }

    public void ResetGrid()
    {
        digbutton.gameObject.SetActive(true);
        _remainigFluidCurr = _remainingFluid;
        _remainigTerraformCurr = _remainigTerraform;

        foreach (var f in Flowers)
        {
            GameObject.Destroy(f.gameObject);
        }

        Flowers.Clear();

        AppendVertexBuffer.Release();
        ArgBuffer.Release();
        ArgBufferTerrain.Release();
        AppendVertexBufferTerrain.Release();
        FlowersWateredBuffer.Release();
        FlowersPositionsBuffer.Release();

        Setup(_levelData, false);
    }

    private void Update()
    {
        if (_victoryScreen.activeInHierarchy)
        {
            if (Input.GetKeyUp(KeyCode.Escape))
            {
                Quit();
            }
        }

        if (_simStarted)
        {
            return;
        }

        if (Input.GetMouseButtonUp(0) && _remainigTerraformCurr > 0)
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
        densityTexture = CreateTemporaryRT();

        Shader.SetTexture(_kernelResetTextures, "densityRW", densityTexture);

        Shader.Dispatch(_kernelResetTextures, GpuTexturesResolution.x / 8, GpuTexturesResolution.y / 8, GpuTexturesResolution.z / 8);
    }

    private void AddFluid()
    {
        if (_remainigFluidCurr <= 0)
        {
            return;
        }
        _remainigFluidCurr--;
        Shader.SetTexture(_kernelAddFluid, "collision", CollisionTexture);
        Shader.SetTexture(_kernelAddFluid, "densityRW", densityTexture);

        Shader.Dispatch(_kernelAddFluid, 1, 1, 1);
    }

    private void Advection()
    {
        Shader.SetTexture(_kernelAdvection, "densityRW", densityTexture);
        Shader.SetTexture(_kernelAdvection, "collision", CollisionTexture);

        Shader.Dispatch(_kernelAdvection, GpuTexturesResolution.x / 8, 1, GpuTexturesResolution.z / 8);
    }

    private void Overflow()
    {
        Shader.SetTexture(_kernelOverflow, "collision", CollisionTexture);
        Shader.SetTexture(_kernelOverflow, "densityRW", densityTexture);

        Shader.Dispatch(_kernelOverflow, GpuTexturesResolution.x / 8, 1, GpuTexturesResolution.z / 8);
    }

    private void Diffusion()
    {
        var tempDensity = CreateTemporaryRT();

        Shader.SetTexture(_kernelDiffusion, "density", densityTexture);
        Shader.SetTexture(_kernelDiffusion, "collision", CollisionTexture);
        Shader.SetTexture(_kernelDiffusion, "densityRW", tempDensity);

        Shader.Dispatch(_kernelDiffusion, 1, GpuTexturesResolution.x / 8, 1);

        RenderTexture.ReleaseTemporary(densityTexture);

        densityTexture = tempDensity;
    }

    private void CheckFlowers()
    {

        if (!_flowersRequest.done)
        {
            return;
        }

        if (!_flowersRequest.hasError)
        {
            bool areAllDone = true;
            var flowersWatered = _flowersRequest.GetData<int>();
            for (int i = 0; i < Flowers.Count; i++)
            {
                if (flowersWatered[i] != 0)
                {
                    Flowers[i].StartTransition();
                }
                else
                {
                    areAllDone = false;
                }
            }

            if (areAllDone)
            {
                StartVicScreen();
            }
        }

        Shader.SetTexture(_kernelCheckFlowers, "density", densityTexture);
        Shader.SetBuffer(_kernelCheckFlowers, "flowersWatered", FlowersWateredBuffer);
        Shader.SetBuffer(_kernelCheckFlowers, "flowersPositions", FlowersPositionsBuffer);

        Shader.Dispatch(_kernelCheckFlowers, 1, 1, 1);

        _flowersRequest = AsyncGPUReadback.Request(FlowersWateredBuffer);
    }

    private bool _isRunning;

    internal void StartVicScreen()
    {
        if (!_isRunning)
        {
            _isRunning = true;
            StartCoroutine(Trans());
        }
    }

    private IEnumerator Trans()
    {
        yield return new WaitForSeconds(2f);
        _victoryScreen.SetActive(true);

    }

    private RenderTexture CreateTemporaryRT()
    {
        var temp = RenderTexture.GetTemporary(GpuTexturesResolution.x, GpuTexturesResolution.y, 0, UnityEngine.Experimental.Rendering.GraphicsFormat.R32_SFloat);
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
        FlowersWateredBuffer.Release();
        FlowersPositionsBuffer.Release();
    }
}