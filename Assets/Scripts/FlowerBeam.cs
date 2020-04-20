using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FlowerBeam : MonoBehaviour
{
    public float alpha = 0.0f;
    float targetAlpha = 1.0f;
    float alphaInc = 1.0f;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (enabled)
        {
            transform.Rotate(new Vector3(0.0f, 1.0f, 0.0f), Time.deltaTime * 32.0f);

            foreach (Transform child in transform)
            {
                Material m = child.gameObject.GetComponent<Renderer>().material;
                if (m)
                {
                    Color c = m.GetColor(Shader.PropertyToID("_Color"));
                    c.a = alpha;
                    m.SetColor(Shader.PropertyToID("_Color"), c);
                }
            }

            if (alpha < targetAlpha)
            {
                alpha += alphaInc * Time.deltaTime;
            }
            else if (alpha > targetAlpha)
            {
                alpha -= alphaInc * Time.deltaTime;
            }
        }
    }
}
