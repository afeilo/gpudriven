using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public class QuadTreeData : ScriptableObject
{
    /// <summary>
    /// 大地图的最大尺寸
    /// </summary>
    public Vector2Int mapLevel;
    /// <summary>
    /// 四叉树最大深度
    /// </summary>
    public int startLevel = 0; // 10 = 1024
    /// <summary>
    /// 四叉树最小深度
    /// </summary>
    public int endLevel = 0; // 5 = 32

    /// <summary>
    /// 深度图尺寸
    /// </summary>
    public int heightMapLevel = 0;
    
    /// <summary>
    /// lod范围
    /// </summary>
    public int lodRange = 1;
    
    /// <summary>
    /// 过度区间
    /// </summary>
    [Range(0,1)]
    public float lerpValue = 0.5f;

    /// <summary>
    /// 高度图放大倍数
    /// </summary>
    public float heightMapScale = 0;

    /// <summary>
    /// 高度图深度
    /// </summary>
    public int heightMapDeep = 0;

    public Texture2D heightBoundsTexture;
    public QuadTreeLevelConfig[] configs;
    /// <summary>
    /// 四叉树实例
    /// </summary>
    [Serializable]
    public struct QuadTreeLevelConfig
    {
        /// <summary>
        /// 中心位置x,y
        /// </summary>
        public int x, y;
        /// <summary>
        /// heightmap的Depth level 超过了深度就没有高度图了
        /// </summary>
        public int heightMapDepthLevel;
        /// <summary>
        /// 高度图位置
        /// </summary>
        public string heightMapRootPath;
    }


    private NodeTree[] topTreeArray;

    public class Node
    {
        
    }

    /// <summary>
    /// 每一层的node列表，每一层展开为线性树
    /// </summary>
    class NodeLevel
    {
        public Node[] Nodes;
    }

    /// <summary>
    /// 节点树  
    /// </summary>
    class NodeTree
    {
        public NodeLevel[] NodeLevels;
    }
    
    public void BuildTree()
    {
        var treeNodes = new List<List<Node>>();
        
        int lengthX = 1 << (mapLevel.x - startLevel);
        int lengthZ = 1 << (mapLevel.y - startLevel);
        int topSize = 1 << startLevel;
        
        topTreeArray = new NodeTree[lengthX * lengthZ];
        for (int i = 0; i < lengthX; i++)
        {
            for (int j = 0; j < lengthZ; j++)
            {
                int index = i * lengthZ + j;
                var heightConfig = configs[index];
                var nodeLevels = new NodeTree();
                topTreeArray[i * lengthZ + j] = nodeLevels;
            }
        }
    }

    void BuildTreeLevel(NodeTree tree, QuadTreeLevelConfig levelConfig)
    {
        var depth = (startLevel - endLevel);
        tree.NodeLevels = new NodeLevel[depth];

        int count = 1;
        for (int lod = depth; lod >= 0; lod--)
        {
            var nodes = new Node[count];
            for (int i = 0; i < count; i++)
            {
                nodes[i] = new Node();
            }
            tree.NodeLevels[lod].Nodes = nodes;
            count *= 4;
        }
    }

    private StringBuilder sb;
    public string GetHeightMapPath(int x, int y, int lod)
    {
        var key = GetHeightTreeCode(x, y, lod);
        var heightConfig = GetGeightConfig(x, y);
        return GetHeightMapPath(heightConfig.heightMapRootPath + key);
    }

    public QuadTreeLevelConfig GetGeightConfig(int x, int y)
    {
        int lengthZ = 1 << (mapLevel.y - startLevel);
        int topSize = 1 << startLevel;
        int offsetX = x / topSize;
        int offsetZ = y / topSize;
        int index = offsetX * lengthZ + offsetZ;
        return configs[index];
    }
    
    public string GetHeightTreeCode(int x, int y, int lod)
    {
        if (null == sb)
            sb = new StringBuilder();
        else
            sb.Clear();
        var depth = GetHeightDeep(lod);
        var code = GetHeightHashCode(x, y, lod);
        for (int i = depth * 2 - 1; i >= 0; i--)
        {
            sb.Append((code >> i) & 1);
        }
        var key = sb.ToString();
        return key;
    }

    public int GetHeightDeep(int lod)
    {
        return Mathf.Min(heightMapDeep, (startLevel - endLevel) - lod);
    }

    //返回lod的树深度
    public int GetDeep(int lod)
    {
        return (startLevel - endLevel) - lod;
    }
    
    public int GetHeightHashCode(int x, int y, int lod)
    {
        uint mx = (uint) (x >> endLevel);
        uint my = (uint) (y >> endLevel);
        var depth = Mathf.Min(heightMapDeep, (startLevel - endLevel) - lod);
        var code = UTools.EncodeMorton2(mx, my);
        var maxDepth = startLevel - endLevel;
        var key = code >> (maxDepth - depth) * 2;
        // Debug.Log(x + "_" + y + "_" + depth + "_" +  Convert.ToString(code,2) + "_" + Convert.ToString(key,2));
        return (int)key;
    }
    
    public int GetLodHashCode(int x, int y, int lod)
    {
        uint mx = (uint) (x >> (endLevel + lod));
        uint my = (uint) (y >> (endLevel + lod));
        var code = UTools.EncodeMorton2(mx, my);
        // Debug.Log(x + "_" + y + "_" + depth + "_" +  Convert.ToString(code,2) + "_" + Convert.ToString(key,2));
        return (int)code;
    }
    
    string GetFormatCode(uint x, uint y, int depth)
    {
        var code = GetCode(x, y);
        if (null == sb)
            sb = new StringBuilder();
        else
            sb.Clear();
        var maxDepth = startLevel - endLevel;
        for (int i = 1; i <= depth * 2; i++)
        {
            sb.Append((code & (1 << (maxDepth * 2 - i))) > 0 ? 1 : 0);
        }
        return sb.ToString();
    }

    long GetCode(uint x, uint y)
    {
        var code = UTools.EncodeMorton2(x, y);
        return code;
    }
    
    public static string GetHeightMapPath(string name)
    {
        return string.Format(@"Assets/ClipmapExport/HeightMap/{0}.asset", name);
    }
    
}
