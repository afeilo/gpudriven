
using Unity.Mathematics;
using UnityEngine;

public class MeshClusterData : ScriptableObject
{
    public Vector3[] vertices;
    public int[] indices;
    public Bounds[] clusterBounds;
}