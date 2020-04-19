using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Flower : MonoBehaviour
{
    [Range(0.0f, 1.0f)]
    public float _Blossom = 0.0f;

    private List<float> bsOffsets;

    void UpdateBlendShape(GameObject obj, float blossom)
    {
        if (obj.GetComponent<SkinnedMeshRenderer>() != null)
        {
            SkinnedMeshRenderer skinnedMesh = obj.GetComponent<SkinnedMeshRenderer>();

            if (blossom < 0.333f)
            {
                float weight = Mathf.Clamp01(1.0f - blossom * 3.0f) * 100.0f;
                skinnedMesh.SetBlendShapeWeight(0, 0.0f);
                skinnedMesh.SetBlendShapeWeight(1, 100.0f - weight);
                skinnedMesh.SetBlendShapeWeight(2, weight);
            }
            else if (blossom < 0.666f)
            {
                float weight = Mathf.Clamp01(1.0f - (blossom - 0.333f) * 3.0f) * 100.0f;
                skinnedMesh.SetBlendShapeWeight(0, 100.0f - weight);
                skinnedMesh.SetBlendShapeWeight(1, weight);
                skinnedMesh.SetBlendShapeWeight(2, 0.0f);
            }
            else
            {
                float weight = Mathf.Clamp01(1.0f - (blossom - 0.666f) * 3.0f) * 100.0f;
                skinnedMesh.SetBlendShapeWeight(0, weight);
                skinnedMesh.SetBlendShapeWeight(1, 0.0f);
                skinnedMesh.SetBlendShapeWeight(2, 0.0f);
            }
        }
    }

    // Start is called before the first frame update
    void Start()
    {
        bsOffsets = new List<float>();

        foreach (Transform child in transform)
        {
            bsOffsets.Add(UnityEngine.Random.Range(0.0f, 25.0f) * 0.01f);
        }
    }

    // Update is called once per frame
    void Update()
    {
        int i = 0;
        foreach (Transform child in transform)
        {
            UpdateBlendShape(child.gameObject, _Blossom - bsOffsets[i]);
            i++;
        }
    }
}
