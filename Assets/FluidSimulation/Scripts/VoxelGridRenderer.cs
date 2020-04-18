using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VoxelGridRenderer : MonoBehaviour
{
    public Material Material;

    private VoxelGrid _grid;

    private void Awake()
    {
        _grid = GetComponent<VoxelGrid>();
    }

    private void Start()
    {

    }

    /// <summary>
    /// The rendering of MC needs to be in OnRenderObject, due to the undocumented requirement of DrawProceduralIndirect
    /// </summary>
    private void OnRenderObject()
    {
        Material.SetPass(0);
        Material.SetBuffer("triangles", _grid.AppendVertexBuffer);
        Material.SetMatrix("model", transform.localToWorldMatrix);
        Graphics.DrawProceduralIndirectNow(MeshTopology.Triangles, _grid.ArgBuffer);
    }
}
