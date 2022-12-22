using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.SceneManagement;

public class TerrainDataEditor : EditorWindow
{
    public const string EXPORT_ROOT = "Assets/ClipmapExport";
    public const string DATA_PATH = EXPORT_ROOT + "/Data";
    public const string HEIGHT_PATH = EXPORT_ROOT + "/HeightMap";
    public const string NORMAL_PATH = EXPORT_ROOT + "/NormalMap";
    public const string HEIGHT_GO_PATH = EXPORT_ROOT + "/HeightMapPrefab";
    public const string CONFIG_NAME = "quad_tree_config.asset";
    public const string HEIGHT_TEST_SHADER_PATH = "Assets/Clipmap/Shaders/HeightMapTest.shader";

    [MenuItem("TerrainData/BuildTerrain")]
    private static void ShowWindow()
    {
        EditorWindow.CreateWindow<TerrainDataEditor>();
    }

    private List<Terrain> terrains = new List<Terrain>();
    private QuadTreeData treeData;
    private List<GameObject> rootGameObject = new List<GameObject>();
    private string outputInfo = "";
    private Dictionary<int, int> Quick2PowDic;
    private GameObject temp;
    private Mesh mesh;
    private Shader shader;
    private bool genHeightPrefab = false;
    private Texture2D HeightBoundTexture;
    private void Awake()
    {
        if (null == treeData)
        {
            var path = Path.Combine(DATA_PATH, CONFIG_NAME);
            var data = AssetDatabase.LoadAssetAtPath<QuadTreeData>(path);
            treeData = new QuadTreeData();
            if (null != data)
            {
                CopyQuadTreeData(data, treeData);
            }
        }
    }

    private void OnGUI()
    {
        // 创建Base Settings
        GUILayout.Space(20);
        GUILayout.Label("数据生成", EditorStyles.boldLabel); // 创建一个粗体 Label


        GUILayout.Space(10);
        GUILayout.BeginVertical();
        GUILayout.Label("地表索引：");
        var length = rootGameObject.Count;
        SceneManager.GetActiveScene().GetRootGameObjects(rootGameObject);
        if (length != rootGameObject.Count)
        {
            terrains.Clear();
            foreach (var go in rootGameObject)
            {
                var t = go.GetComponent<Terrain>();
                if (t != null)
                    terrains.Add(t);
            }
        }

        Bounds mapBounds = new Bounds();
        foreach (var terrain in terrains)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(terrain.gameObject.name, GUILayout.Width(80));
            EditorGUILayout.ObjectField("", terrain, typeof(Terrain), true);
            var pos = terrain.transform.position;
            var bounds = terrain.terrainData.bounds;
            treeData.heightMapScale = Mathf.Max(treeData.heightMapScale, terrain.terrainData.heightmapScale.y);
            GUILayout.Label(string.Format("bounds：({0},{1},{2},{3})", bounds.min.x, bounds.min.z, bounds.max.x,
                bounds.max.z));
            GUILayout.EndHorizontal();
            mapBounds = bounds;
        }

        GUILayout.Label(string.Format("大地图尺寸:({0}, {1})", mapBounds.size.x, mapBounds.size.z));
        GUILayout.Space(10);
        GUILayout.Label("配置：");

        GUILayout.BeginHorizontal();
        GUILayout.Label(String.Format("四叉树根节点尺寸:{0}", Mathf.Pow(2, treeData.startLevel)), GUILayout.Width(150));
        treeData.startLevel = EditorGUILayout.IntSlider("", treeData.startLevel, 1, 12, GUILayout.Width(300));
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label(String.Format("四叉树叶节点尺寸:{0}", Mathf.Pow(2, treeData.endLevel)), GUILayout.Width(150));
        treeData.endLevel = EditorGUILayout.IntSlider("", treeData.endLevel, 1, 6, GUILayout.Width(300));
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label(String.Format("高度图分割尺寸:{0}", Mathf.Pow(2, treeData.heightMapLevel) + 1), GUILayout.Width(150));
        treeData.heightMapLevel = EditorGUILayout.IntSlider("", treeData.heightMapLevel, 1, 10, GUILayout.Width(300));
        GUILayout.EndHorizontal();

        genHeightPrefab = EditorGUILayout.Toggle("是否生成高度图预制体", genHeightPrefab);
        GUILayout.Space(20);
        if (GUILayout.Button("导出"))
        {
            if (null == Quick2PowDic)
            {
                Quick2PowDic = new Dictionary<int, int>();
                for (int i = 0; i < 20; i++)
                {
                    Quick2PowDic.Add((int) Mathf.Pow(2, i), i);
                }
            }

            if (!Directory.Exists(EXPORT_ROOT))
            {
                Directory.CreateDirectory(EXPORT_ROOT);
            }

            if (!Directory.Exists(HEIGHT_PATH))
            {
                Directory.CreateDirectory(HEIGHT_PATH);
            }

            if (genHeightPrefab && Directory.Exists(HEIGHT_GO_PATH))
            {
                Directory.Delete(HEIGHT_GO_PATH, true);
            }

            if (!Directory.Exists(HEIGHT_GO_PATH))
            {
                Directory.CreateDirectory(HEIGHT_GO_PATH);
            }

            if (!Directory.Exists(NORMAL_PATH))
            {
                Directory.CreateDirectory(NORMAL_PATH);
            }

            if (null == shader)
            {
                shader = AssetDatabase.LoadAssetAtPath<Shader>(HEIGHT_TEST_SHADER_PATH);
            }

            var path = Path.Combine(DATA_PATH, CONFIG_NAME);
            var data = AssetDatabase.LoadAssetAtPath<QuadTreeData>(path);
            if (null == data)
            {
                data = ScriptableObject.CreateInstance<QuadTreeData>();
                if (!Directory.Exists(DATA_PATH))
                    Directory.CreateDirectory(DATA_PATH);
                AssetDatabase.CreateAsset(treeData, path);
                AssetDatabase.Refresh();
            }

            var topSize = 1 << data.startLevel;
            var heightSize = 1 << data.heightMapLevel;
            CopyQuadTreeData(treeData, data);
            List<QuadTreeData.QuadTreeLevelConfig> levelConfigs = new List<QuadTreeData.QuadTreeLevelConfig>();

            // ExportTestMesh(heightSize);

            foreach (var terrain in terrains)
            {
                var bounds = terrain.terrainData.bounds;
                int boundDepthX = -1;
                int boundDepthZ = -1;
                Quick2PowDic.TryGetValue((int) bounds.size.x, out boundDepthX);
                Quick2PowDic.TryGetValue((int) bounds.size.z, out boundDepthZ);

                if (-1 == boundDepthX || -1 == boundDepthZ)
                {
                    EditorUtility.DisplayDialog("Warning", "地形长宽不是2的幂次", "确定");
                    return;
                }

                var xc = boundDepthX - data.startLevel;
                var zc = boundDepthZ - data.startLevel;

                if (xc < 0 || zc < 0)
                {
                    EditorUtility.DisplayDialog("Warning", "地形长宽小于四叉树根节点尺寸", "确定");
                    return;
                }

                var pos = terrain.transform.localPosition;

                var sourceHeight = terrain.terrainData.heightmapResolution - 1;
                var sourceHeightDepth = -1;
                Quick2PowDic.TryGetValue(sourceHeight, out sourceHeightDepth);

                int xHeightMaxDepth = sourceHeightDepth - xc - data.heightMapLevel;
                int zHeightMaxDepth = sourceHeightDepth - zc - data.heightMapLevel;
                ///高度图四叉树深度
                var heightQuadDepth = Mathf.Min(xHeightMaxDepth, zHeightMaxDepth);
                int xcount = 1 << xc;
                int zcount = 1 << zc;

                ExportNormalMap(terrain, "normal_" + terrain.name);
                ExportQuadTreeHeightMap(terrain, terrain.terrainData.heightmapResolution - 1, 0, 0,
                    0, 0, "height_" + terrain.name, Vector3.zero, new Vector3Int(-1, -1, -1));
                HeightBoundTexture = new Texture2D(1 << (boundDepthX - data.heightMapLevel),
                    1 << (boundDepthZ - data.heightMapLevel), TextureFormat.RGB24, true, true);
                int maxLod = heightQuadDepth; //最大lod
                for (int i = 0; i < xcount; i++)
                {
                    for (int j = 0; j < zcount; j++)
                    {
                        var qtl = new QuadTreeData.QuadTreeLevelConfig();
                        var cx = (int) pos.x + i * topSize;
                        var cy = (int) pos.z + j * topSize;
                        qtl.x = cx + topSize / 2; //中心点
                        qtl.y = cy + topSize / 2;
                        qtl.heightMapRootPath = "height" + levelConfigs.Count;
                        qtl.heightMapDepthLevel = heightQuadDepth;
                        levelConfigs.Add(qtl);

                        //分割
                        ExportQuadTreeHeightMap(terrain, heightSize, i * (1 << xHeightMaxDepth) * heightSize,
                            j * (1 << zHeightMaxDepth) * heightSize,
                            xHeightMaxDepth, zHeightMaxDepth, qtl.heightMapRootPath, new Vector3(qtl.x, qtl.y, 1), new Vector3Int(i,j,maxLod));
                    }
                }

                // for (int l = 1; l < HeightBoundTexture.mipmapCount; l++)
                // {
                //     var w = HeightBoundTexture.width >> l;
                //     var h = HeightBoundTexture.height >> l;
                //     for (int i = 0; i < w; i++)
                //     {
                //         for (int j = 0; j < h; j++)
                //         {
                //             var c1 = HeightBoundTexture.GetPixel(i, j, l - 1);
                //             var c2 = HeightBoundTexture.GetPixel(i + 1, j, l - 1);
                //             var c3 = HeightBoundTexture.GetPixel(i, j + 1, l - 1);
                //             var c4 = HeightBoundTexture.GetPixel(i + 1, j + 1, l - 1);
                //             var min = Mathf.Min(c1.r, c2.r, c3.r, c4.r);
                //             var max = Mathf.Max(c1.g, c2.g, c3.g, c4.g);
                //             // HeightBoundTexture.SetPixel(i, j, new Color(min, max , 0, 1), l);
                //             HeightBoundTexture.SetPixel(i, j, new Color(l %2 == 0 ? 1 : 0, l %2 != 0 ? 1 : 0 , 0, 1), l);
                //         }
                //     }
                // }
                //
                HeightBoundTexture.Apply(false);
                AssetDatabase.CreateAsset(HeightBoundTexture, HEIGHT_PATH + "/heightBounds.asset");
                AssetDatabase.Refresh();
                
                data.mapLevel = new Vector2Int(boundDepthX, boundDepthZ);
            }

            data.configs = levelConfigs.ToArray();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        GUILayout.Label(outputInfo);

        GUILayout.EndVertical();
    }

    void ExportNormalMap(Terrain terrain, string key)
    {
        var _renderTex = terrain.normalmapTexture;
        Debug.Log(_renderTex.format);
        int width = _renderTex.width;
        int height = _renderTex.height;

        Texture2D rt2d = new Texture2D(width, height, TextureFormat.RGB24, false);
        for (int w = 0; w < width; w++)
        {
            for (int h = 0; h < height; h++)
            {
                var _normal = terrain.terrainData.GetInterpolatedNormal(w / (float) width, h / (float) height)
                    .normalized;
                rt2d.SetPixel(w, h, new Color((_normal.x + 1) / 2.0f, (_normal.y + 1) / 2.0f, (_normal.z + 1) / 2.0f));
            }
        }

        // RenderTexture.active = _renderTex;
        // rt2d.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        // rt2d.Apply();

        string path = NORMAL_PATH + "/" + key + ".asset";
        AssetDatabase.CreateAsset(rt2d, path);
        AssetDatabase.Refresh();
    }

    void ExportQuadTreeHeightMap(Terrain terrain, int heightSize, int offsetX, int offsetZ, int skipDepthX,
        int skipDepthZ, string key, Vector3 t, Vector3Int heightBoundsInfo)
    {
        var skipx = 1 << skipDepthX;
        var skipz = 1 << skipDepthZ;
        Texture2D texture2D = new Texture2D(heightSize + 1, heightSize + 1, TextureFormat.RHalf, false);
        // int heightSize = 129;
        float maxHeight = float.MinValue;
        float minHeight = float.MaxValue;
        for (int i = 0; i <= heightSize; i++)
        {
            for (int j = 0; j <= heightSize; j++)
            {
                var x = offsetX + skipx * i;
                var y = offsetZ + skipz * j;
                var height = terrain.terrainData.GetHeight(x, y);
                if (height > treeData.heightMapScale)
                {
                    Debug.LogError("hightmap scale is so small");
                }

                height = height / treeData.heightMapScale;
                maxHeight = height > maxHeight ? height : maxHeight;
                minHeight = height < minHeight ? height : minHeight;
                texture2D.SetPixel(i, j, new Color(height, 0, 0, 1));
            }
        }

        if (heightBoundsInfo.z >= 0)
        {
            Debug.Log(heightBoundsInfo.z +":"+ heightBoundsInfo.x +", "+heightBoundsInfo.y + " :(" +minHeight * 500+ "," +maxHeight * 500 +")");
            HeightBoundTexture.SetPixel(heightBoundsInfo.x, heightBoundsInfo.y, new Color(minHeight, maxHeight, 0, 0),
                heightBoundsInfo.z);
        }

        texture2D.Apply();
        string path = HEIGHT_PATH + "/" + key + ".asset";
        AssetDatabase.CreateAsset(texture2D, path);
        AssetDatabase.Refresh();

        #region 生成gameObject

        if (genHeightPrefab)
        {
            var go = new GameObject(key);
            go.transform.localPosition = new Vector3(t.x, 0, t.y);
            go.transform.localScale = new Vector3(skipx, 1, skipz);
            var mr = go.AddComponent<MeshRenderer>();
            var mf = go.AddComponent<MeshFilter>();
            Material material = new Material(shader);
            material.name = "mat_" + key;
            material.SetFloat("_HeightScale", treeData.heightMapScale);
            material.SetTexture("_HeightMap", AssetDatabase.LoadAssetAtPath<Texture2D>(path));
            var matPath = HEIGHT_GO_PATH + "/" + material.name + ".mat";
            AssetDatabase.CreateAsset(material, matPath);
            mr.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            mf.sharedMesh = mesh;
            PrefabUtility.CreatePrefab(HEIGHT_GO_PATH + "/" + go.name + ".prefab", go);
            GameObject.DestroyImmediate(go);
        }

        #endregion


        skipDepthX--;
        skipDepthZ--;
        if (skipDepthX < 0 || skipDepthZ < 0)
            return;

        int offsetAddX = heightSize * (1 << skipDepthX);
        int offsetAddZ = heightSize * (1 << skipDepthZ);
        ExportQuadTreeHeightMap(terrain, heightSize, offsetX, offsetZ + offsetAddZ, skipDepthX, skipDepthZ, key + "01",
            new Vector3(t.x, t.y + offsetAddZ, t.z / 2.0f), new Vector3Int(heightBoundsInfo.x * 2, heightBoundsInfo.y * 2 + 1, heightBoundsInfo.z - 1));
        ExportQuadTreeHeightMap(terrain, heightSize, offsetX + offsetAddX, offsetZ + offsetAddZ, skipDepthX, skipDepthZ,
            key + "11", new Vector3(t.x + offsetAddX, t.y + offsetAddZ, t.z / 2.0f), new Vector3Int(heightBoundsInfo.x * 2 + 1, heightBoundsInfo.y * 2 + 1, heightBoundsInfo.z - 1));
        ExportQuadTreeHeightMap(terrain, heightSize, offsetX, offsetZ, skipDepthX, skipDepthZ, key + "00",
            new Vector3(t.x, t.y, t.z / 2.0f), new Vector3Int(heightBoundsInfo.x * 2, heightBoundsInfo.y * 2, heightBoundsInfo.z - 1));
        ExportQuadTreeHeightMap(terrain, heightSize, offsetX + offsetAddX, offsetZ, skipDepthX, skipDepthZ, key + "10",
            new Vector3(t.x + offsetAddX, t.y, t.z / 2.0f), new Vector3Int(heightBoundsInfo.x * 2 + 1, heightBoundsInfo.y * 2, heightBoundsInfo.z - 1));
    }

    void ExportTexture(Texture t, int W, int H, Vector2Int scale, GraphicsFormat format)
    {
        var _renderTex = new RenderTexture(W, H, 0, format); //GraphicsFormat.R8G8B8A8_UNorm);
        Material mat = new Material(Shader.Find("Unlit/Blit"));
        mat.SetTextureScale("_MainTex", new Vector2(0.5f + 0.5f / t.width, 0.5f + 0.5f / t.height));
        mat.SetTextureOffset("_MainTex",
            new Vector2((0.5f - 0.5f / t.width) * scale.x, (0.5f - 0.5f / t.height) * scale.y));
        mat.mainTexture = t;
        mat.SetPass(0);


        Graphics.SetRenderTarget(_renderTex);
        GL.Clear(true, true, Color.white);
        GL.PushMatrix();
        GL.LoadOrtho();

        GL.Begin(GL.QUADS);

        Vector2 v1 = Vector2.zero;
        Vector2 v2 = Vector2.right;
        Vector2 v3 = Vector2.one;
        Vector2 v4 = Vector2.up;

        //init vertex position
        Vector3 p1 = Vector3.zero;
        Vector3 p2 = Vector3.right;
        Vector3 p3 = new Vector3(1, 1, 0);
        Vector3 p4 = Vector3.up;
        GL.TexCoord(v1);
        GL.Vertex(p1);

        GL.TexCoord(v4);
        GL.Vertex(p4);

        GL.TexCoord(v3);
        GL.Vertex(p3);

        GL.TexCoord(v2);
        GL.Vertex(p2);

        GL.End();
        GL.PopMatrix();
        GL.Flush();
        Graphics.SetRenderTarget(null);
        int width = _renderTex.width;
        int height = _renderTex.height;
        Texture2D rt2d = new Texture2D(width, height, TextureFormat.ARGB32, false);
        RenderTexture.active = _renderTex;
        rt2d.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        rt2d.Apply();


        // var file = File.Open("Assets/SplitMap/split2_" + m + "_" + n + ".png", FileMode.Create);
        // var binary = new BinaryWriter(file);
        // binary.Write(rt2d.EncodeToPNG());
        // file.Close();
    }

    void ExportTestMesh(int resolution)
    {
        {
            resolution = Mathf.Min(128, resolution);
            int TILE_RESOLUTION = resolution;
            int PATCH_VERT_RESOLUTION = resolution + 1;
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

            var tileMesh = new Mesh();
            tileMesh.SetVertices(vertices);
            tileMesh.SetUVs(0, uvs);
            tileMesh.SetIndices(indices, MeshTopology.Triangles, 0);
            var meshPath = Path.Combine(EXPORT_ROOT, "tile_mesh.asset");
            AssetDatabase.CreateAsset(tileMesh, meshPath);
            AssetDatabase.Refresh();
            mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
        }
    }

    int patch2d(int x, int y, int gap)
    {
        return y * gap + x;
    }

    public void CopyQuadTreeData(QuadTreeData src, QuadTreeData dest)
    {
        dest.mapLevel = src.mapLevel;
        dest.startLevel = src.startLevel;
        dest.endLevel = src.endLevel;
        dest.heightMapLevel = src.heightMapLevel;
        dest.heightMapScale = src.heightMapScale;
    }
}