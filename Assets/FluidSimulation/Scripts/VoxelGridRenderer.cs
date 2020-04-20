using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class VoxelGridRenderer : MonoBehaviour
{
    public Material Material;

    [Range(1, 1024)] 
    public int _TextureSize = 512;
    private int _OldTextureSize = 0;

    public Camera _Camera;

    public ReflectionProbe _Probe;

    [Header("Internal")]
    private RenderTexture _RefractionRT = null;
    private Camera _RefractionCamera = null;

    private bool _IsRendering = false;

    private VoxelGrid _grid;

    private void Awake()
    {
        _grid = GetComponent<VoxelGrid>();
    }

    private void Start()
    {

    }

    private void OnDisable()
    {
        if (_RefractionRT)
        {
            DestroyImmediate(_RefractionRT);
            _RefractionRT = null;
        }

        if (_RefractionCamera)
        {
            DestroyImmediate(_RefractionCamera.gameObject);
            _RefractionCamera = null;
        }
    }

    void CreateObjects()
    {
        if (_RefractionCamera == null)
        {
            GameObject go = new GameObject("Refraction_Camera_" + GetInstanceID(), typeof(Camera), typeof(Skybox));
            _RefractionCamera = go.GetComponent<Camera>();
            _RefractionCamera.enabled = false;
            _RefractionCamera.transform.position = transform.position;
            _RefractionCamera.transform.rotation = transform.rotation;
            go.hideFlags = HideFlags.DontSave;
        }

        if (_RefractionRT || _TextureSize != _OldTextureSize)
        {
            if (_RefractionRT)
            {
                DestroyImmediate(_RefractionRT);
            }
            _RefractionRT = new RenderTexture(_TextureSize, _TextureSize, 24);
            _RefractionRT.name = "RFR_RTColor_" + GetInstanceID();
            _RefractionRT.isPowerOfTwo = true;
            _RefractionRT.hideFlags = HideFlags.DontSave;
            _RefractionRT.wrapMode = TextureWrapMode.Repeat;

            _OldTextureSize = _TextureSize;
        }
    }

    void UpdateCameraModes(Camera src, Camera dest)
    {
        if (dest == null)
            return;

        dest.clearFlags = src.clearFlags;
        dest.backgroundColor = src.backgroundColor;
        if (src.clearFlags == CameraClearFlags.Skybox)
        {
            Skybox sky = src.GetComponent<Skybox>();
            Skybox mysky = dest.GetComponent<Skybox>();
            if (!sky || !sky.material)
            {
                mysky.enabled = false;
            }
            else
            {
                mysky.enabled = true;
                mysky.material = sky.material;
            }
        }
        dest.farClipPlane = src.farClipPlane;
        dest.nearClipPlane = src.nearClipPlane;
        dest.orthographic = src.orthographic;
        dest.fieldOfView = src.fieldOfView;
        dest.aspect = src.aspect;
        dest.orthographicSize = src.orthographicSize;
    }

    private void OnWillRenderObject()
    {
        Camera cam = _Camera;
        if (!Application.isPlaying)
        {
            cam = Camera.current;
        }

        if (cam == null)
        {
            return;
        }
               
        if (_IsRendering)
        {
            return;
        }

        _IsRendering = true;

        CreateObjects();
        UpdateCameraModes(cam, _RefractionCamera);

        _RefractionCamera.worldToCameraMatrix = cam.worldToCameraMatrix;
        _RefractionCamera.projectionMatrix = cam.projectionMatrix;
        //_RefractionCamera.cullingMask = ~(1 << 4);
        _RefractionCamera.depthTextureMode = DepthTextureMode.Depth;
        _RefractionCamera.renderingPath = RenderingPath.DeferredShading;
        _RefractionCamera.targetTexture = _RefractionRT;
        _RefractionCamera.allowMSAA = false;
        _RefractionCamera.Render();

        _IsRendering = false;
    }

    /// <summary>
    /// The rendering of MC needs to be in OnRenderObject, due to the undocumented requirement of DrawProceduralIndirect
    /// </summary>
    private void OnRenderObject()
    {
        if (Application.isPlaying)
        {
            Material.SetPass(0);
            if (_IsRendering)
            {
                Material.SetFloat("_Clip", 0.0f);
            }
            else
            {
                Material.SetFloat("_Clip", 1.0f);
            }
            Material.SetBuffer("triangles", _grid.AppendVertexBuffer);
            Material.SetMatrix("model", transform.localToWorldMatrix);
            Material.SetTexture("_RefractionTex", _RefractionRT);
            Material.SetTexture("_ReflectionTex", _Probe.texture);
            Graphics.DrawProceduralIndirectNow(MeshTopology.Triangles, _grid.ArgBuffer);
        }
    }
}
