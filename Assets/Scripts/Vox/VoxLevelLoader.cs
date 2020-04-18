using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CsharpVoxReader;
using System;
using CsharpVoxReader.Chunks;


public class VoxLevelLoader : MonoBehaviour
{
    public static bool IsTerrain(Color32 c) => (c.r == 0 && c.b == 0 && c.g != 0) || (c.r == 100 && c.g == 50 && c.b == 0);

    public static bool IsFluidInput(Color32 c) => c.r == 0 && c.g == 0 && c.b == 255;

    private VoxelGrid _grid;
    private Data _inputData;

    public class Data : IVoxLoader
    {
        public uint[] Palette { get; private set; }
        public byte[,,] Indices { get; private set; }
        public Vector3Int Size { get; private set; }


        public void LoadModel(int sizeX, int sizeY, int sizeZ, byte[,,] data)
        {
            Indices = data;
            Size = new Vector3Int(sizeX, sizeY, sizeZ);

        }

        public void LoadPalette(uint[] palette)
        {
            Palette = palette;
        }

        public void NewGroupNode(int id, Dictionary<string, byte[]> attributes, int[] childrenIds)
        {
        }

        public void NewLayer(int id, Dictionary<string, byte[]> attributes)
        {
        }

        public void NewMaterial(int id, Dictionary<string, byte[]> attributes)
        {
        }

        public void NewShapeNode(int id, Dictionary<string, byte[]> attributes, int[] modelIds, Dictionary<string, byte[]>[] modelsAttributes)
        {
        }

        public void NewTransformNode(int id, int childNodeId, int layerId, Dictionary<string, byte[]>[] framesAttributes)
        {
        }

        public void SetMaterialOld(int paletteId, MaterialOld.MaterialTypes type, float weight, MaterialOld.PropertyBits property, float normalized)
        {
        }

        public void SetModelCount(int count)
        {
        }
    }

    private void Awake()
    {
        _grid = GetComponent<VoxelGrid>();
    }

    private void Start()
    {
        _inputData = new Data();
        VoxReader reader = new VoxReader("Assets/Levels/level0.vox", _inputData);

        reader.Read();

        _grid.Setup(_inputData);

        // DebugInput(loader);
    }

    /*private static void DebugInput(Data loader)
    {
        for (int x = 0; x < loader.Size.x; x++)
        {
            for (int y = 0; y < loader.Size.y; y++)
            {
                for (int z = 0; z < loader.Size.z; z++)
                {
                    var idx = loader.Indices[x, y, z];
                    if (idx != 0)
                    {
                        var color = loader.Palette[idx].ToColor();

                        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        //cube.transform.localScale = new Vector3(0.1f, 0.1f, 0.1f);
                        cube.transform.position = new Vector3(x, y, z);

                        cube.GetComponent<MeshRenderer>().material.SetColor("_Color", color);
                    }
                }
            }
        }
    }*/
}
