using System.Collections.Generic;
using UnityEngine;

public class TerrainRender : MonoBehaviour
{
    public Material Material;
    public Material Material2;
    public ComputeShader ComputeShader;
    // public QuadTreeConfig TreeConfig;
    public QuadTreeData TreeData;
    int TILE_RESOLUTION;
    int PATCH_VERT_RESOLUTION;
    QuadTreeBuilder _quadTree;
    Mesh _tileMesh;
    Mesh _halfTileMesh;
    // Start is called before the first frame update

    private VTPageTable _vtPageTable;
    VTPageTable VtPageTable
    {
        get
        {
            if (null == _vtPageTable)
                _vtPageTable = GetComponent<VTPageTable>();
            return _vtPageTable;
        }
    }
    
    private void OnEnable()
    {
        _quadTree = new QuadTreeBuilder(ComputeShader, TreeData);
        VtPageTable.SetQuadTreeData(TreeData);
        #region create mesh
        TILE_RESOLUTION = 1 << TreeData.endLevel;
        PATCH_VERT_RESOLUTION = TILE_RESOLUTION + 1;
        // generate tile mesh
        {
            List<Vector3> vertices = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            for (int y = 0; y < PATCH_VERT_RESOLUTION; y++)
            {
                for (int x = 0; x < PATCH_VERT_RESOLUTION; x++)
                {
                    vertices.Add(new Vector3(x, 0, y));
                    uvs.Add(new Vector2((float) x / TILE_RESOLUTION, (float) y / TILE_RESOLUTION));
                }
            }

            List<int> indices = new List<int>(TILE_RESOLUTION * TILE_RESOLUTION * 6);
            for (int y = 0; y < TILE_RESOLUTION; y++)
            {
                for (int x = 0; x < TILE_RESOLUTION; x++)
                {
                    indices.Add(patch2d(x, y, PATCH_VERT_RESOLUTION));
                    indices.Add(patch2d(x, y + 1, PATCH_VERT_RESOLUTION));
                    indices.Add(patch2d(x + 1, y + 1, PATCH_VERT_RESOLUTION));
                    indices.Add(patch2d(x, y, PATCH_VERT_RESOLUTION));
                    indices.Add(patch2d(x + 1, y + 1, PATCH_VERT_RESOLUTION));
                    indices.Add(patch2d(x + 1, y, PATCH_VERT_RESOLUTION));
                }
            }

            _tileMesh = new Mesh();
            _tileMesh.SetVertices(vertices);
            _tileMesh.SetUVs(0, uvs);
            _tileMesh.SetIndices(indices, MeshTopology.Triangles, 0);
        }
        // generate half tile mesh
        {
            var resolution = TILE_RESOLUTION / 2;
            List<Vector3> vertices = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            for (int y = 0; y < resolution + 1; y++)
            {
                for (int x = 0; x < resolution + 1; x++)
                {
                    vertices.Add(new Vector3(x, 0, y));
                    uvs.Add(new Vector2((float) x / resolution, (float) y / resolution));
                }
            }

            List<int> indices = new List<int>(resolution * resolution * 6);
            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    indices.Add(patch2d(x, y, resolution + 1));
                    indices.Add(patch2d(x, y + 1, resolution + 1));
                    indices.Add(patch2d(x + 1, y + 1, resolution + 1));
                    indices.Add(patch2d(x, y, resolution + 1));
                    indices.Add(patch2d(x + 1, y + 1, resolution + 1));
                    indices.Add(patch2d(x + 1, y, resolution + 1));
                }
            }

            _halfTileMesh = new Mesh();
            _halfTileMesh.SetVertices(vertices);
            _halfTileMesh.SetUVs(0, uvs);
            _halfTileMesh.SetIndices(indices, MeshTopology.Triangles, 0);
        }

        #endregion
        _quadTree.IndirectArgsBuffer1.SetData(new uint[]{_tileMesh.GetIndexCount(0),0,0,0,0});
        _quadTree.IndirectArgsBuffer2.SetData(new uint[]{_halfTileMesh.GetIndexCount(0),0,0,0,0});
    }

    int patch2d(int x, int y, int gap)
    {
        return y * gap + x;
    }

    // Update is called once per frame
    void Update()
    {
        
        Material.SetFloat("_MeshResolution", TILE_RESOLUTION);
        Material.SetFloat("_UVResolution", TILE_RESOLUTION);
        Material.SetFloat("_LerpValue", TreeData.lerpValue);
        Material.SetInt("_LerpRange", TreeData.lodRange);
        Material2.SetFloat("_UVResolution", TILE_RESOLUTION / 2);
        Material2.SetFloat("_MeshResolution", TILE_RESOLUTION);
        Material2.SetFloat("_LerpValue", TreeData.lerpValue);
        Material2.SetInt("_LerpRange", TreeData.lodRange);
        
        var t = Camera.main.transform.position;
        Shader.SetGlobalVector("_CameraPosition", t);
        _quadTree.Select(Camera.main);
        VtPageTable.ActiveNodes(_quadTree.FinalPatch1, false);
        VtPageTable.ActiveNodes(_quadTree.FinalPatch2, true);
        //回读buffer到CPU 做资源加载
        
        Material.SetBuffer("PatchList", _quadTree.FinalNodeList1);
        Graphics.DrawMeshInstancedIndirect(_tileMesh, 0, Material, new Bounds(Vector3.zero, Vector3.one * 10240),
            _quadTree.IndirectArgsBuffer1);
        Material2.SetBuffer("PatchList", _quadTree.FinalNodeList2);
        Graphics.DrawMeshInstancedIndirect(_halfTileMesh, 0, Material2, new Bounds(Vector3.zero, Vector3.one * 10240),
            _quadTree.IndirectArgsBuffer2);
    }

    private void OnDestroy()
    {
        _quadTree.Dispose();
    }
}