using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Utilities : MonoBehaviour
{
    /// <summary>
    /// 获取最小的包围球
    /// </summary>
    /// <param name="normals"></param>
    public static Vector4 MinimumBoundingSphere(Vector3[] points)
    {
        var center = points[0];
        float radius = 0;

        for (int i = 1; i < points.Length; i++)
        {
            var p = points[i];
            var distance = Vector3.Distance(center, p);
            if (distance > radius)
            {
                //计算出一个大的包围圆
                var d = distance - radius;
                var dir = (p - center).normalized;
                radius += d / 2.0f;
                center += dir * (d / 2.0f);
            }
        }
        return new Vector4(center.x, center.y, center.z, radius);
    }

    
    /// <summary>
    /// 求法线向量并集 圆锥 Calculate the normal cone
    /// </summary>
    public static Vector4 CaclNormalCone(Vector3[] normals)
    {
        // 1. Normalized center point of minimum bounding sphere of unit normals == conic axis
        var s = minSphere(normals, normals.Length, new Vector3[] { }, 0);
        // 半圆锥的sin
        float sinCone = 1;
        //球内 没必要聚合了
        var d = Vector3.Distance(Vector3.zero, new Vector3(s.x, s.y, s.z));
        if (d > s.w)
        {
            sinCone = s.w / d;
        }
        var normalCone = new Vector3(s.x, s.y, s.z).normalized;
        return new Vector4(normalCone.x, normalCone.y, normalCone.z, sinCone);
    }
    

    /// <summary>
    /// Welzl's algorithm 总能找到三个点勾成的球，包含所有的点
    /// </summary>
    /// <param name="points"></param>
    /// <param name="count"></param>
    /// <param name="bnd"></param>
    /// <param name="nb"></param>
    /// <returns></returns>
    static Vector4 minSphere(Vector3[] points, int count, Vector3[] bnd, int nb)
    {
        if (count == 1)
        {
            if (0 == nb) return sphere1pt(points[0]);
            if (1 == nb) return sphere2pts(points[0], bnd[0]);
        }
        if (count == 0)
        {
            if (1 == nb) return sphere1pt(bnd[0]);
            if (2 == nb) return sphere2pts(bnd[0], bnd[1]);
        }
        if (nb == 3)
            return sphere3pts(bnd[0], bnd[1], bnd[2]);

        var s = minSphere(points, count - 1, bnd, nb);
        if (Inside(s, points[count - 1]))
        {
            return s;
        }

        bnd[nb] = points[count - 1];
        nb++;
        return minSphere(points, count - 1, bnd, nb);
    }

    private static bool Inside(Vector4 s, Vector3 p)
    {
        if (Vector3.Distance(p, new Vector3(s.x, s.y, s.z)) <= s.w)
        {
            return true;
        }
        return false;
    }
    
    private static Vector4 sphere1pt(Vector3 p)
    {
        return new Vector4(p.x, p.y, p.z, 0);
    }
    private static Vector4 sphere2pts(Vector3 p1, Vector3 p2)
    {
        var p = (p1 + p2) / 2;
        var r = Vector3.Distance(p1, p2) / 2;
        return new Vector4(p.x, p.y, p.z, r);
    }
    ///三个点的外接球 算法来自chatgpt
    private static Vector4 sphere3pts(Vector3 p1, Vector3 p2, Vector3 p3) {
        
        // 计算三角形的法向量
        var normal = Vector3.Cross(p2 - p1, p3 - p1);

        // 计算三角形的重心
        var centroid = (p1 + p2 + p3);
        centroid = new Vector3(centroid.x / 3.0f, centroid.y / 3.0f, centroid.z / 3.0f);
        // 计算垂足和外接圆心
        Vector3 foot_point_12 = p1 + Vector3.Dot((p2 - p1), normal) / Vector3.Dot(normal, normal) * normal;
        Vector3 foot_point_13 = p1 + Vector3.Dot((p3 - p1), normal) / Vector3.Dot(normal, normal) * normal;
        
        Vector3 circum_center = centroid + Vector3.Scale(Vector3.Cross(foot_point_12 - centroid,
            foot_point_13 - centroid) / Vector3.Dot(normal, normal), normal);

        // 计算外接圆半径
        float circum_radius = Vector3.Distance(circum_center, p1);
        
        return new Vector4(circum_center.x, circum_center.y, circum_center.z, circum_radius);
    }
}
