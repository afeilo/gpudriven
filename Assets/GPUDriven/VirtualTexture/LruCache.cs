using System.Collections.Generic;
using UnityEngine;

namespace VirtualTexture
{
    public class LruCache
    {
        public class NodeInfo
        {
            public int id = 0;
            public int x = -1;//坐标
            public int y = -1;
            public bool isLoading = false;
            public NodeInfo Next { get; set; }
            public NodeInfo Prev { get; set; }
            /// <summary>
            /// hashcode
            /// </summary>
            /// <returns></returns>

            public override int GetHashCode()
            {
                return id;
            }
        }

        class NodeComparer : IEqualityComparer<NodeInfo>
        {
            public bool Equals(NodeInfo x, NodeInfo y)
            {
                return x.GetHashCode() == y.GetHashCode();
            }

            public int GetHashCode(NodeInfo node)
            {
                return node.GetHashCode();
            }
        }

        private HashSet<NodeInfo> nodeInfos;

        private int nodeCount;
        private int rowCount;
        private int allCount;
        private NodeInfo [] allNodes;
        private NodeInfo head = null;
        private NodeInfo tail = null;
        private NodeInfo temp = new NodeInfo();

        public int First { get { return head.id; } }

        public void Init(int rowCount)
        {
            this.rowCount = rowCount;
            nodeInfos = new HashSet<NodeInfo>(new NodeComparer());
            nodeCount = rowCount * rowCount;
        }

        /// <summary>
        /// 根据id获取node
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public NodeInfo GetNode(int id)
        {
            temp.id = id;
            NodeInfo node;
            nodeInfos.TryGetValue(temp, out node);
            return node;
        }

        /// <summary>
        /// 激活一个node
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public NodeInfo SetActive(int id)
        {
            temp.id = id;
            NodeInfo node = null;
            if (nodeInfos.Contains(temp))
            {
                nodeInfos.TryGetValue(temp, out node);
                if (node != tail)
                {
                    Remove(node);
                    AddLast(node);
                }
            }
            else
            {
                if (allCount >= nodeCount)
                {
                    node = new NodeInfo() {id = id, x = head.x, y = head.y};
                    nodeInfos.Remove(head);
                    AddLast(node);
                    RemoveFirst();

                }
                else
                {
                    node = new NodeInfo() {id = id, x = (allCount) % rowCount, y = allCount / rowCount};
                    AddLast(node);
                    allCount = allCount + 1;
                }
                nodeInfos.Add(node);
            }
            return node;
        }

        private void AddLast(NodeInfo node)
        {
            if (null == tail)
            {
                tail = node;
                head = node;
                return;
            }
            var lastTail = tail;
            lastTail.Next = node;
            tail = node;
            node.Prev = lastTail;
        }

        private void RemoveFirst()
        {
            var firstNode = head.Next;
            firstNode.Prev = null;
            head = firstNode;
        }

        private void Remove(NodeInfo node)
        {
            if (head == node)
            {
                head = node.Next;
            }
            else
            {
                node.Prev.Next = node.Next;
                node.Next.Prev = node.Prev;
            }
        }
    }
    
}