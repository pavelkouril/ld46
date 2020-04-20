using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ArrowScript : MonoBehaviour
{
    public VoxelGridRenderer _Renderer;

    private Renderer rend;

    void Start()
    {
        rend = GetComponent<Renderer>();
    }

    // Update is called once per frame
    void Update()
    {
        rend.material.SetTexture("_RefractionTex", _Renderer._RefractionRT);
        rend.material.SetTexture("_ReflectionTex", _Renderer._Probe.texture);
    }
}
