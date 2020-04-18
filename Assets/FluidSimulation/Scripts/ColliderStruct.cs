using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Struct that is mapped on the structuredbuffer struct for collision field generation
/// </summary>
public struct CollisionFieldCollider
{
    public Vector3 position;
    public Vector3 velocity;
    public float size;
    public float pad; // padding based on nvidia recommendations to structuredbuffers

    public CollisionFieldCollider(Vector3 position, Vector3 velocity, float size)
    {
        this.position = position;
        this.size = size;
        this.velocity = velocity;
        this.pad = 0;
    }
}
