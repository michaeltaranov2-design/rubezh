using System;
using System.Numerics;

namespace Rubezh.Core;

public sealed class MapDef
{
    public required string Id;
    public required string DisplayName;
    public required string SizeClass;
    public required string Author;
    public required string License;
    public float BotRadius = 0.4f;
    public Vector3 BoundsMin, BoundsMax;
    public MapBox[] Boxes = Array.Empty<MapBox>();
    public Vector3[] TSpawns = Array.Empty<Vector3>();
    public Vector3[] CtSpawns = Array.Empty<Vector3>();
    public Vector3[] Waypoints = Array.Empty<Vector3>();
    public int[] Edges = Array.Empty<int>();
    public int[][] Neighbors = Array.Empty<int[]>();
    public Vector3 SiteAMin, SiteAMax, SiteBMin, SiteBMax;
    public int TriangleBudget;
    public int MemoryBudgetKb;

    public void BuildNeighbors()
    {
        var counts = new int[Waypoints.Length];
        for (int i = 0; i < Edges.Length; i++) counts[Edges[i]]++;
        Neighbors = new int[Waypoints.Length][];
        for (int i = 0; i < Neighbors.Length; i++) Neighbors[i] = new int[counts[i]];
        var fill = new int[Waypoints.Length];
        for (int i = 0; i < Edges.Length; i += 2)
        {
            int a = Edges[i], b = Edges[i + 1];
            Neighbors[a][fill[a]++] = b;
            Neighbors[b][fill[b]++] = a;
        }
    }

    public static MapDef FromUzel()
    {
        var m = new MapDef
        {
            Id = MapUzel.Id, DisplayName = "Узел", SizeClass = "medium",
            Author = "Рубеж", License = "CC0-1.0",
            BoundsMin = MapUzel.BoundsMin, BoundsMax = MapUzel.BoundsMax,
            Boxes = MapUzel.Boxes, TSpawns = MapUzel.TSpawns, CtSpawns = MapUzel.CtSpawns,
            Waypoints = MapUzel.Waypoints, Edges = MapUzel.Edges,
            SiteAMin = MapUzel.SiteAMin, SiteAMax = MapUzel.SiteAMax,
            SiteBMin = MapUzel.SiteBMin, SiteBMax = MapUzel.SiteBMax,
            TriangleBudget = 8000, MemoryBudgetKb = 24 * 1024
        };
        m.BuildNeighbors();
        return m;
    }
}
