using System.Collections.Generic;
using UnityEngine;

public class TerrainRender : MonoBehaviour
{
    public Material Material;
    public Material Material2;
    public ComputeShader ComputeShader;
    // public QuadTreeConfig TreeConfig;
    public QuadTreeData TreeData;
    public TerrainData TerrainData;
    public Texture2D NormalMap;
    int TILE_RESOLUTION;
    int PATCH_VERT_RESOLUTION;
    QuadTreeBuilder _quadTree;
    Mesh _tileMesh;
    Mesh _halfTileMesh;
    Mesh _boundMesh;

    private Material _boundMaterial;
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
        #region Copy MaterialData
        for (int i = 0; i < 4; i++)
        {
            var layer = TerrainData.terrainLayers[i];
            Material.SetTexture("_Splat" + i, layer.diffuseTexture);
            Material.SetTextureOffset("_Splat" + i, layer.tileOffset);
            Material.SetTextureScale("_Splat" + i, (Vector2.one * 2048.0f) / layer.tileSize);
            Material.SetTexture("_Normal" + i, layer.normalMapTexture);
            Material.SetTexture("_Mask" + i, layer.maskMapTexture);
            Material.SetVector("_DiffuseRemapScale"+i, layer.diffuseRemapMax);
            Material.SetVector("_MaskMapRemapOffset"+i, layer.maskMapRemapMin);
            Material.SetVector("_MaskMapRemapScale"+i, layer.maskMapRemapMax);
            Material.SetFloat("_Metallic"+i, layer.metallic);
            Material.SetFloat("_Smoothness"+i, layer.smoothness);
            Material.SetFloat("_NormalScale"+i, layer.normalScale);
            Material.SetFloat("_LayerHasMask" + i, layer.maskMapTexture == null ? 0 : 1);
        }
        Material.SetTexture("_Control", TerrainData.alphamapTextures[0]);
        Material.SetTexture("_TerrainNormalmapTexture", NormalMap);
        Material.EnableKeyword("_NORMALMAP");
        Material.EnableKeyword("_MASKMAP");

        for (int i = 0; i < 4; i++)
        {
            var layer = TerrainData.terrainLayers[i];
            Material2.SetTexture("_Splat" + i, layer.diffuseTexture);
            Material2.SetTextureOffset("_Splat" + i, layer.tileOffset);
            Material2.SetTextureScale("_Splat" + i, (Vector2.one * 2048.0f) / layer.tileSize);
            Material2.SetTexture("_Normal" + i, layer.normalMapTexture);
            Material2.SetTexture("_Mask" + i, layer.maskMapTexture);
            Material2.SetVector("_DiffuseRemapScale"+i, layer.diffuseRemapMax);
            Material2.SetVector("_MaskMapRemapOffset"+i, layer.maskMapRemapMin);
            Material2.SetVector("_MaskMapRemapScale"+i, layer.maskMapRemapMax);            
            Material2.SetFloat("_Metallic"+i, layer.metallic);
            Material2.SetFloat("_Smoothness"+i, layer.smoothness);
            Material2.SetFloat("_NormalScale"+i, layer.normalScale);
            Material2.SetFloat("_LayerHasMask" + i, layer.maskMapTexture == null ? 0 : 1);
        }
        Material2.SetTexture("_Control", TerrainData.alphamapTextures[0]);
        Material2.SetTexture("_TerrainNormalmapTexture", NormalMap);
        Material2.EnableKeyword("_NORMALMAP");
        Material2.EnableKeyword("_MASKMAP");
        #endregion
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

        // generate half tile mesh
        {
            _boundMesh = new Mesh();
            //设置顶点
            _boundMesh.vertices = new Vector3[]
            {   new Vector3(0, 0, 0),
                new Vector3(1, 0, 0),
                new Vector3(1, 1, 0),
                new Vector3(0, 1, 0),
                new Vector3(0, 1, 1),
                new Vector3(1, 1, 1),
                new Vector3(1, 0, 1),
                new Vector3(0, 0, 1),
            };
            //设置三角形顶点顺序，顺时针设置
            _boundMesh.triangles = new int[]
            {
                0, 2, 1,
                0, 3, 2,
                3, 4, 2,
                4, 5, 2,
                4, 7, 5,
                7, 6, 5,
                7, 0, 1,
                6, 7, 1,
                4, 3, 0,
                4, 0, 7,
                2, 5, 6,
                2, 6, 1
            };
            _boundMaterial = new Material(Shader.Find("Unlit/NewUnlitShader"));
            _boundMaterial.enableInstancing = true;
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
        //保证Editor的预览
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

        #region  debug heightbounds

        // var matrixs = new List<Matrix4x4>();
        // for (int i = 0; i < _quadTree.FinalPatch1.Length; i++)
        // {
        //     var patch = _quadTree.FinalPatch1[i];
        //     var scale = 1 << ((int)patch.lod + 5);
        //     var mat = Matrix4x4.TRS(new Vector3(patch.position.x, patch.minmax.x, patch.position.y), Quaternion.identity,
        //         new Vector3(scale, patch.minmax.y - patch.minmax.x, scale));
        //     matrixs.Add(mat);
        // }
        //
        // Graphics.DrawMesh(_boundMesh, matrixs[0], Material, 0);
        // Graphics.DrawMeshInstanced(_boundMesh, 0, _boundMaterial, matrixs);
        //
        // matrixs.Clear();
        // for (int i = 0; i < _quadTree.FinalPatch2.Length; i++)
        // {
        //     var patch = _quadTree.FinalPatch2[i];
        //     var scale = 1 << ((int)patch.lod + 5 - 1);
        //     var mat = Matrix4x4.TRS(new Vector3(patch.position.x, patch.minmax.x, patch.position.y), Quaternion.identity,
        //         new Vector3(scale, patch.minmax.y - patch.minmax.x, scale));
        //     matrixs.Add(mat);
        // }
        // Graphics.DrawMeshInstanced(_boundMesh, 0, _boundMaterial, matrixs);
        //
        #endregion
    }

    private void OnDestroy()
    {
        _quadTree.Dispose();
    }
}