using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Rendering;
using UnityEngine.Serialization;
using VirtualTexture;

/// <summary>
///  步骤：
///  1、Active Node
///  2、分配PageTable的位置（LRUCache）
///  3、加载Lod图片
///  4、烘焙到PageTable上
///  5、更新LookUp表
/// </summary>
public class VTPageTable : MonoBehaviour
{
    /// <summary>
    /// 平铺数量(x*x)
    /// </summary>
    public int m_RegionSize;

    /// <summary>
    /// Tile间距 单个的
    /// </summary>
    public int m_TilePadding;

    /// <summary>
    /// Tile尺寸
    /// </summary>
    public int mTileSize;

    public Shader mDrawTextureShader;

    /// <summary>
    /// 加上padding的总长度
    /// </summary>
    public int TileSizeWithPadding
    {
        get { return mTileSize + m_TilePadding; }
    }

    /// <summary>
    /// 页面长宽
    /// </summary>
    public int PageSize
    {
        get { return m_RegionSize * TileSizeWithPadding; }
    }

    /// <summary>
    /// 平铺贴图对象
    /// </summary>
    public RenderTexture mTileTexture;

    /// <summary>
    /// LookUp table 大小由可视范围以及能够切分到多少个 格子绝对 比如说可视范围是4096*4096 单个cell能表现的最大范围是32*32 则lookuptable的大小为128 * 128
    /// </summary>
    public Texture2D mLookupTexture;

    /// <summary>
    /// 合并texture的材质
    /// </summary>
    private Material _drawTextureMaterial;

    /// <summary>
    /// 当前激活的Page
    /// </summary>
    private LruCache _lruCache;

    /// <summary>
    /// 四叉树节点相关
    /// </summary>
    private QuadTreeData _quadTreeData;

    //叶节点尺寸
    private int _topGridSize;

    //节点lod深度
    private int _maxLodLevel;

    private static Mesh _fullscreenMesh;

    public static Mesh fullscreenMesh
    {
        get
        {
            if (_fullscreenMesh != null)
                return _fullscreenMesh;

            float topV = 1.0f;
            float bottomV = 0.0f;

            _fullscreenMesh = new Mesh {name = "Fullscreen Quad"};
            _fullscreenMesh.SetVertices(new List<Vector3>
            {
                new Vector3(-1.0f, -1.0f, 0.0f),
                new Vector3(-1.0f, 1.0f, 0.0f),
                new Vector3(1.0f, -1.0f, 0.0f),
                new Vector3(1.0f, 1.0f, 0.0f)
            });

            _fullscreenMesh.SetUVs(0, new List<Vector2>
            {
                new Vector2(0.0f, bottomV),
                new Vector2(0.0f, topV),
                new Vector2(1.0f, bottomV),
                new Vector2(1.0f, topV)
            });

            _fullscreenMesh.SetIndices(new[] {0, 1, 2, 2, 1, 3}, MeshTopology.Triangles, 0, false);
            _fullscreenMesh.UploadMeshData(true);
            return _fullscreenMesh;
        }
    }

    void Awake()
    {
        _lruCache = new LruCache();
        _lruCache.Init(m_RegionSize);

        mTileTexture = new RenderTexture(PageSize, PageSize, 0);
        mTileTexture.format = RenderTextureFormat.R16;
        mTileTexture.useMipMap = false;
        mTileTexture.filterMode = FilterMode.Point;
        mTileTexture.wrapMode = TextureWrapMode.Clamp;


        Shader.SetGlobalTexture("_VTTiledTex", mTileTexture);
    }

    public void SetQuadTreeData(QuadTreeData config)
    {
        _quadTreeData = config;
        _topGridSize = 1 << config.startLevel;
        _maxLodLevel = config.startLevel - config.endLevel;
        
        int lengthX = 1 << (config.mapLevel.x - config.endLevel);
        int lengthZ = 1 << (config.mapLevel.y - config.endLevel);
        
        mLookupTexture = new Texture2D(lengthX, lengthZ, TextureFormat.RGBAHalf, 0, true);
        Shader.SetGlobalTexture("_LookupTex", mLookupTexture);
        Shader.SetGlobalVector("_LookupParam", new Vector4(m_RegionSize, mTileSize, TileSizeWithPadding, 1 << config.endLevel));
    }

    public void ActiveNodes(RenderPatch[] patchs, bool isHalf)
    {
        for (int i = 0; i < patchs.Length; i++)
        {
            ActiveNode(patchs[i], isHalf);
            DrawLookupTable(patchs[i], isHalf);
        }

        mLookupTexture.Apply();
    }


    int getHashCode(RenderPatch patch)
    {
        var code = _quadTreeData.GetHeightHashCode((int) patch.position.x, (int) patch.position.y, (int) patch.lod);
        var deep = _quadTreeData.GetHeightDeep((int)patch.lod);
        return packDeepHashCode(code, deep);
    }

    int packDeepHashCodeByLod(int code, int lod)
    {
        var deep = _quadTreeData.GetDeep(lod);
        return packDeepHashCode(code, deep);
    }
    
    int packDeepHashCode(int code, int deep)
    {
        return (code << 8) + deep;
    }
    
    int getHeightHashCode(RenderPatch patch)
    {
        return _quadTreeData.GetHeightHashCode((int) patch.position.x, (int) patch.position.y, (int) patch.lod);
    }
    int getLodHashCode(int x, int y, int lod)
    {
        return _quadTreeData.GetLodHashCode(x, y, lod);
    }

    private string GetPath(RenderPatch patch)
    {
        return _quadTreeData.GetHeightMapPath((int) patch.position.x, (int) patch.position.y, (int) patch.lod);
    }
    public async void ActiveNode(RenderPatch patch, bool isHalf)
    {
        int key = getHashCode(patch);
        string path = GetPath(patch);
        if (path == null)
        {
            return;
            // node = node.parent;
        }

        int hashCode = key;
        var lruNode = _lruCache.GetNode(hashCode);
        if (null != lruNode)
        {
            _lruCache.SetActive(hashCode);
            return;
        }

        lruNode = _lruCache.SetActive(hashCode);
        //加载对应Node;
        var handle = Addressables.LoadAssetAsync<Texture2D>(path);
        await handle.Task;
        //烘焙对应Node；
        var texture2d = handle.Result;
        lruNode = _lruCache.GetNode(hashCode);
        if (null == lruNode)
        {
            return;
        }

        DrawTexture(texture2d, mTileTexture,
            new RectInt(lruNode.x * TileSizeWithPadding, lruNode.y * TileSizeWithPadding, mTileSize, mTileSize));
        Addressables.Release(handle);
    }

    private void DrawLookupTable(RenderPatch patch, bool isHalf)
    {
        var lod = patch.lod;
        int caclLod = (int) (isHalf ? lod - 1 : lod);
        int hashCode = getLodHashCode((int) patch.position.x, (int) patch.position.y, caclLod);
        var lruNode = isHalf ? null : _lruCache.GetNode(packDeepHashCodeByLod(hashCode, (int)lod));
        // var lruNode = _lruCache.GetNode(packDeepHashCodeByLod(hashCode, caclLod));
        var nodeOffsetScale = new Vector4(0, 0, 1, 1);
        while (lruNode == null)
        {
            var y = (hashCode >> 0) & 1;
            var x = (hashCode >> 1) & 1;

            nodeOffsetScale.x = x * 0.5f + nodeOffsetScale.x * 0.5f;
            nodeOffsetScale.y = y * 0.5f + nodeOffsetScale.y * 0.5f;
            nodeOffsetScale.z = nodeOffsetScale.z * 0.5f;
            nodeOffsetScale.w = nodeOffsetScale.w * 0.5f;
            hashCode >>= 2;
            caclLod++;
            lruNode = _lruCache.GetNode(packDeepHashCodeByLod(hashCode, caclLod));
        }

        var color = new Color(nodeOffsetScale.x, nodeOffsetScale.y, nodeOffsetScale.z,
            lruNode.x * m_RegionSize + lruNode.y);
        var minPageSize = 1 << _quadTreeData.endLevel;
        var pageOffset = 1 << ((int) (isHalf ? lod - 1 : lod));
        var startX = (int) (patch.position.x / minPageSize);
        var startZ = (int) (patch.position.y / minPageSize);

        DrawLookupTextureRect(startX, startX + pageOffset, startZ, startZ + pageOffset, color);
    }


    void DrawLookupTextureRect(int _i, int _i_end, int _j, int _j_end, Color color)
    {
        for (int x = _i; x < _i_end; x++)
        {
            if (x < 0)
                continue;
            if (x >= mLookupTexture.width)
                continue;
            for (int y = _j; y < _j_end; y++)
            {
                if (y < 0)
                    continue;
                if (y >= mLookupTexture.height)
                    continue;

                mLookupTexture.SetPixel(x, y, color);
            }
        }
    }

    private void DrawTexture(Texture source, RenderTexture target, RectInt position)
    {
        if (source == null || target == null || mDrawTextureShader == null)
            return;

        // 初始化绘制材质
        if (_drawTextureMaterial == null)
            _drawTextureMaterial = new Material(mDrawTextureShader);

        // 构建变换矩阵
        float l = position.x * 2.0f / target.width - 1;
        float r = (position.x + position.width) * 2.0f / target.width - 1;
        float b = position.y * 2.0f / target.height - 1;
        float t = (position.y + position.height) * 2.0f / target.height - 1;

        var mat = new Matrix4x4();
        mat.m00 = r - l;
        mat.m03 = l;
        mat.m11 = t - b;
        mat.m13 = b;
        mat.m23 = -1;
        mat.m33 = 1;

        // 绘制贴图
        _drawTextureMaterial.SetMatrix(Shader.PropertyToID("_ImageMVP"), GL.GetGPUProjectionMatrix(mat, true));

        target.DiscardContents();
        Graphics.Blit(source, target, _drawTextureMaterial);


        //// 绘制贴图
        //_drawTextureMaterial.SetMatrix(Shader.PropertyToID("_ImageMVP"), GL.GetGPUProjectionMatrix(mat, true));
        //_drawTextureMaterial.SetTexture("_MainTex", source);
        //var tempCB = CommandBufferPool.Get("VTDraw");
        //tempCB.SetRenderTarget(target, RenderBufferLoadAction.Load, RenderBufferStoreAction.Store, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.DontCare);
        //tempCB.DrawMesh(fullscreenMesh, Matrix4x4.identity, _drawTextureMaterial, 0, 0);
        //Graphics.ExecuteCommandBuffer(tempCB);//DEBUG
        //CommandBufferPool.Release(tempCB);
    }
}