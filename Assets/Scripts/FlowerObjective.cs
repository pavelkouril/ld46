﻿using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FlowerObjective : MonoBehaviour
{
    [Range(0.0f, 1.0f)]
    public float _ObjectiveProgress = 0.0f;

    public Transform _Beam;

    public Vector3 GridPos;

    // Start is called before the first frame update
    void Start()
    {
        _ObjectiveProgress = 0.0f;
    }

    // Update is called once per frame
    void Update()
    {
        int i = 0;
        foreach (Transform child in transform)
        {
            Flower f = child.gameObject.GetComponent<Flower>();
            if (f)
            {
                f._Blossom = _ObjectiveProgress;
            }
        }

        if (_ObjectiveProgress >= 1.0f)
        {
            _Beam.gameObject.SetActive(true);
        }
    }

    internal void StartTransition()
    {
        StartCoroutine(Trans());
    }

    private IEnumerator Trans()
    {
        while (_ObjectiveProgress < 1)
        {
            _ObjectiveProgress += Time.deltaTime / 5f;
            yield return new WaitForEndOfFrame();
        }
    }
}
