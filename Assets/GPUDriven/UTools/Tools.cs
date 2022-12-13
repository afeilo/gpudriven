using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UTools{
    
    /// <summary>
    ///  计算x,y的morton Code
    /// </summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <returns></returns>
    public static uint EncodeMorton2(uint x, uint y)
    {
        return (Part1By1(x) << 1) + Part1By1(y);
    }
    
    // "Insert" a 0 bit after each of the 16 low bits of x
    static uint Part1By1(uint x)
    {
        x &= 0x0000ffff; // x = ---- ---- ---- ---- fedc ba98 7654 3210
        x = (x ^ (x << 8)) & 0x00ff00ff; // x = ---- ---- fedc ba98 ---- ---- 7654 3210
        x = (x ^ (x << 4)) & 0x0f0f0f0f; // x = ---- fedc ---- ba98 ---- 7654 ---- 3210
        x = (x ^ (x << 2)) & 0x33333333; // x = --fe --dc --ba --98 --76 --54 --32 --10
        x = (x ^ (x << 1)) & 0x55555555; // x = -f-e -d-c -b-a -9-8 -7-6 -5-4 -3-2 -1-0
        return x;
    }
}
