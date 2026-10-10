using System;
using System.Numerics;

namespace Rubezh.Core;

public readonly struct MapBox
{
    public readonly Vector3 Center, Size;
    public readonly int Material;
    public readonly bool Solid;

    public MapBox(float cx, float cy, float cz, float sx, float sy, float sz, int material, bool solid = true)
    {
        Center = new Vector3(cx, cy, cz);
        Size = new Vector3(sx, sy, sz);
        Material = material;
        Solid = solid;
    }

    public Vector3 Min => Center - Size * 0.5f;
    public Vector3 Max => Center + Size * 0.5f;
}

public static class MapMaterial
{
    public const int Floor = 0, Wall = 1, Crate = 2, SiteA = 3, SiteB = 4, Outer = 5, Count = 6;
}

/// <summary>
/// Оригинальная greybox-карта «Узел» 64×48 м. T — юг (+Z), CT — север (−Z),
/// точка A — восток, B — запад, середина с укрытием. Планировка собственная.
/// </summary>
public static class MapUzel
{
    public const string Id = "uzel_greybox";
    public const float BotRadius = 0.4f;
    public static readonly Vector3 BoundsMin = new(-32f, -1f, -24f);
    public static readonly Vector3 BoundsMax = new(32f, 8f, 24f);

    public static readonly MapBox[] Boxes =
    {
        new(0f, -0.25f, 0f, 64f, 0.5f, 48f, MapMaterial.Floor),
        new(-23f, 1.5f, 12f, 18f, 3f, 0.5f, MapMaterial.Wall),
        new(-6f, 1.5f, 12f, 4f, 3f, 0.5f, MapMaterial.Wall),
        new(7f, 1.5f, 12f, 6f, 3f, 0.5f, MapMaterial.Wall),
        new(24f, 1.5f, 12f, 16f, 3f, 0.5f, MapMaterial.Wall),
        new(-25f, 1.5f, -12f, 14f, 3f, 0.5f, MapMaterial.Wall),
        new(-8f, 1.5f, -12f, 8f, 3f, 0.5f, MapMaterial.Wall),
        new(8f, 1.5f, -12f, 8f, 3f, 0.5f, MapMaterial.Wall),
        new(25f, 1.5f, -12f, 14f, 3f, 0.5f, MapMaterial.Wall),
        new(-4f, 1.5f, -6f, 0.5f, 3f, 8f, MapMaterial.Wall),
        new(-4f, 1.5f, 7f, 0.5f, 3f, 6f, MapMaterial.Wall),
        new(4f, 1.5f, -7f, 0.5f, 3f, 6f, MapMaterial.Wall),
        new(4f, 1.5f, 6f, 0.5f, 3f, 8f, MapMaterial.Wall),
        new(0f, 0.75f, 0f, 1.5f, 1.5f, 1.5f, MapMaterial.Crate),
        new(20f, 0.6f, -2f, 1.2f, 1.2f, 1.2f, MapMaterial.Crate),
        new(21.2f, 0.6f, -2f, 1.2f, 1.2f, 1.2f, MapMaterial.Crate),
        new(24f, 0.5f, 3f, 2f, 1f, 1f, MapMaterial.Crate),
        new(-20f, 0.6f, 1f, 1.2f, 1.2f, 1.2f, MapMaterial.Crate),
        new(-22f, 1f, -3f, 2f, 2f, 2f, MapMaterial.Crate),
        new(-17f, 0.6f, 4f, 1.2f, 1.2f, 1.2f, MapMaterial.Crate),
        new(0f, 2f, -24.25f, 65f, 4f, 0.5f, MapMaterial.Outer),
        new(0f, 2f, 24.25f, 65f, 4f, 0.5f, MapMaterial.Outer),
        new(-32.25f, 2f, 0f, 0.5f, 4f, 49f, MapMaterial.Outer),
        new(32.25f, 2f, 0f, 0.5f, 4f, 49f, MapMaterial.Outer),
        new(20f, 0.01f, 0f, 10f, 0.02f, 10f, MapMaterial.SiteA, false),
        new(-20f, 0.01f, 0f, 10f, 0.02f, 10f, MapMaterial.SiteB, false),
    };

    public static readonly Vector3 SiteAMin = new(15f, 0f, -5f), SiteAMax = new(25f, 3f, 5f);
    public static readonly Vector3 SiteBMin = new(-25f, 0f, -5f), SiteBMax = new(-15f, 3f, 5f);

    public static readonly Vector3[] TSpawns =
    {
        new(-3f, 0.05f, 20f), new(-1f, 0.05f, 20f), new(1f, 0.05f, 20f), new(3f, 0.05f, 20f), new(0f, 0.05f, 22f)
    };

    public static readonly Vector3[] CtSpawns =
    {
        new(-3f, 0.05f, -20f), new(-1f, 0.05f, -20f), new(1f, 0.05f, -20f), new(3f, 0.05f, -20f), new(0f, 0.05f, -22f)
    };

    public static readonly Vector3[] Waypoints =
    {
        new(0f, 0f, 18f), new(-11f, 0f, 15f), new(-11f, 0f, 8f), new(-20f, 0f, 6f), new(-18f, 0f, -1f),
        new(-15f, 0f, -8f), new(0f, 0f, -18f), new(0f, 0f, 8f), new(0f, 0f, -8f), new(2.5f, 0f, 0f),
        new(13f, 0f, 8f), new(20f, 0f, 4f), new(15f, 0f, -8f), new(11f, 0f, 15f), new(-15f, 0f, -14f),
        new(15f, 0f, -14f)
    };

    /// <summary>Пары индексов неориентированных рёбер.</summary>
    public static readonly int[] Edges =
    {
        0, 1, 1, 2, 2, 3, 3, 4, 4, 5, 5, 14, 14, 6, 0, 7, 7, 9, 9, 8, 8, 6,
        0, 13, 13, 10, 10, 11, 11, 12, 12, 15, 15, 6
    };

    public static readonly int[][] Neighbors = BuildNeighbors();

    static int[][] BuildNeighbors()
    {
        var counts = new int[Waypoints.Length];
        for (int i = 0; i < Edges.Length; i++) counts[Edges[i]]++;
        var result = new int[Waypoints.Length][];
        for (int i = 0; i < result.Length; i++) result[i] = new int[counts[i]];
        var fill = new int[Waypoints.Length];
        for (int i = 0; i < Edges.Length; i += 2)
        {
            int a = Edges[i], b = Edges[i + 1];
            result[a][fill[a]++] = b;
            result[b][fill[b]++] = a;
        }
        return result;
    }
}

/// <summary>Проверки проходимости карты (используются тестами и редактором карт).</summary>
public static class MapValidator
{
    public static bool SegmentHitsBox(Vector3 a, Vector3 b, in MapBox box, float radius)
    {
        if (!box.Solid || box.Max.Y <= 0.3f) return false;
        Vector3 min = box.Min, max = box.Max;
        float t0 = 0f, t1 = 1f;
        if (!Slab(a.X, b.X - a.X, min.X - radius, max.X + radius, ref t0, ref t1)) return false;
        return Slab(a.Z, b.Z - a.Z, min.Z - radius, max.Z + radius, ref t0, ref t1);
    }

    public static bool SegmentBlocked(Vector3 a, Vector3 b, MapBox[] boxes, float radius)
    {
        for (int i = 0; i < boxes.Length; i++)
            if (SegmentHitsBox(a, b, boxes[i], radius)) return true;
        return false;
    }

    static bool Slab(float p, float d, float min, float max, ref float t0, ref float t1)
    {
        if (MathF.Abs(d) < 1e-9f) return p >= min && p <= max;
        float u = (min - p) / d, v = (max - p) / d;
        if (u > v) (u, v) = (v, u);
        t0 = MathF.Max(t0, u);
        t1 = MathF.Min(t1, v);
        return t0 <= t1;
    }

    public static bool InsideXZ(Vector3 p, Vector3 min, Vector3 max) =>
        p.X >= min.X && p.X <= max.X && p.Z >= min.Z && p.Z <= max.Z;
}
