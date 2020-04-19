using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GrassTile : MonoBehaviour
{
    [Range(0.0F, 1.0f)]
    public float _Cutoff = 0.55f;

    public float _InstanceSize = 10.0f;

    public Vector2 _InstanceOffset = new Vector2(0.0f, 0.0f);

    [Range(0.0F, 1.0f)]
    public float _InstanceSizeCutout = 0.1f;

    public Texture2D _HeightMap;
    public int _HeightMapSize = 64;

    public VoxelGrid _Grid;

    private Renderer _renderer;

    public void UpdateHeightmap(VoxelGrid grid)
    {
        float[] data = new float[_HeightMapSize * _HeightMapSize];

        Vector3 basePoint = transform.position;

        basePoint.x -= _InstanceSize / 2.0f;
        basePoint.z -= _InstanceSize / 2.0f;
        basePoint.y += 10.0f;

        float step = _InstanceSize / _HeightMapSize;

        for (int j = 0; j < _HeightMapSize; j++)
        {
            for (int i = 0; i < _HeightMapSize; i++)
            {
                Vector3 origin = new Vector3(basePoint.x + step * i, basePoint.y, basePoint.z + step * j);
                Vector3 direction = new Vector3(0.0f, -1.0f, 0.0f);

                RaycastHit testResult = new RaycastHit();
                if (Physics.Raycast(new Ray(origin, direction), out testResult))
                {
                    data[i + j * _HeightMapSize] = origin.y - testResult.distance;
                }
                else
                {
                    data[i + j * _HeightMapSize] = 0.0f;
                }
            }
        }

        _HeightMap.SetPixelData<float>(data, 0);
        _HeightMap.Apply();
    }

    // Start is called before the first frame update
    void Start()
    {
        _renderer = GetComponent<Renderer>();
        _HeightMap = new Texture2D(_HeightMapSize, _HeightMapSize, TextureFormat.RFloat, false);

        UpdateHeightmap(null);

        _Grid.OnTerrainChanged += UpdateHeightmap;
    }

    // Update is called once per frame
    void Update()
    {
        _renderer.material.SetFloat("_AlphaCutoff", _Cutoff);
        _renderer.material.SetFloat("_Size", _InstanceSize);
        _renderer.material.SetVector("_Position", new Vector4(_InstanceOffset.x, _InstanceOffset.y, 0.0f, 0.0f));
        _renderer.material.SetFloat("_SizeCutoff", _InstanceSizeCutout);
        _renderer.material.SetTexture("_HeightTex", _HeightMap);
    }
}
