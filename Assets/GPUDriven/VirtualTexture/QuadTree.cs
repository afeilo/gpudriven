using System;
using UnityEngine;

/// <summary>
/// 四叉树
/// </summary>
public class QuadTree
{

    public struct SelectNode
    {
        public Node node;
        public byte chooseBit;
    }

    public class Node
    {
        /// <summary>
        /// 位置
        /// </summary>
        public int x, y;

        /// <summary>
        /// 格子尺寸
        /// </summary>
        public int size;

        /// <summary>
        /// 父节点
        /// </summary>
        public Node parent;

        public Vector2 offset;

        /// <summary>
        /// 地块加载路径
        /// </summary>
        public string path;

    }

    
}