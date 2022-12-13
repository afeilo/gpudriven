using System;

[Serializable]
public struct QuadTreeConfig
{
    /// <summary>
    /// 中心位置x,y
    /// </summary>
    public int x, y;

    public int maxLevel; // 12 = 4096
    public int startLevel; // 10 = 1024
    public int endLevel; // 5 = 32
    public float lerpValua; //过渡区范围
    public int lodRange; //决定lod过度范围 
}