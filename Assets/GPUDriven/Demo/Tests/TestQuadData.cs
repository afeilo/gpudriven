using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

public class TestQuadData
{
    // A Test behaves as an ordinary method
    [Test]
    public void TestQuadDataSimplePasses()
    {
        var code = EncodeMorton2(3, 1);
        Debug.Log (Convert.ToString(code,2));
        // Use the Assert class to test conditions
    }
    
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

    // A UnityTest behaves like a coroutine in Play Mode. In Edit Mode you can use
    // `yield return null;` to skip a frame.
    [UnityTest]
    public IEnumerator TestQuadDataWithEnumeratorPasses()
    {
        // Use the Assert class to test conditions.
        // Use yield to skip a frame.
        yield return null;
    }
}
