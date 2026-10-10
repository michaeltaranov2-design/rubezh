using System.Numerics;

namespace Rubezh.Core;

/// <summary>Компактная карта «Клин» 40×32 м. Два коротких маршрута на A и B, без тупиков.</summary>
public static class MapKlin
{
    public const string Id = "klin_greybox";
    public static MapDef Build()
    {
        var m = new MapDef
        {
            Id = Id, DisplayName = "Клин", SizeClass = "compact", Author = "Рубеж", License = "CC0-1.0",
            BoundsMin = new(-20f, -1f, -16f), BoundsMax = new(20f, 8f, 16f),
            TriangleBudget = 5000, MemoryBudgetKb = 16 * 1024,
            SiteAMin = new(8f, 0f, -4f), SiteAMax = new(16f, 3f, 4f),
            SiteBMin = new(-16f, 0f, -4f), SiteBMax = new(-8f, 3f, 4f),
            Boxes = new MapBox[]
            {
                new(0, -0.25f, 0, 40, 0.5f, 32, MapMaterial.Floor),
                new(-14, 1.5f, 8, 12, 3, 0.5f, MapMaterial.Wall),
                new(0, 1.5f, 8, 6, 3, 0.5f, MapMaterial.Wall),
                new(14, 1.5f, 8, 12, 3, 0.5f, MapMaterial.Wall),
                new(-14, 1.5f, -8, 12, 3, 0.5f, MapMaterial.Wall),
                new(0, 1.5f, -8, 6, 3, 0.5f, MapMaterial.Wall),
                new(14, 1.5f, -8, 12, 3, 0.5f, MapMaterial.Wall),
                new(-3, 1.5f, 0, 0.5f, 3, 8, MapMaterial.Wall),
                new(3, 1.5f, 0, 0.5f, 3, 8, MapMaterial.Wall),
                new(12, 0.6f, 0, 1.2f, 1.2f, 1.2f, MapMaterial.Crate),
                new(-12, 0.6f, 0, 1.2f, 1.2f, 1.2f, MapMaterial.Crate),
                new(0, 2, -16.25f, 41, 4, 0.5f, MapMaterial.Outer),
                new(0, 2, 16.25f, 41, 4, 0.5f, MapMaterial.Outer),
                new(-20.25f, 2, 0, 0.5f, 4, 33, MapMaterial.Outer),
                new(20.25f, 2, 0, 0.5f, 4, 33, MapMaterial.Outer),
                new(12, 0.01f, 0, 8, 0.02f, 8, MapMaterial.SiteA, false),
                new(-12, 0.01f, 0, 8, 0.02f, 8, MapMaterial.SiteB, false)
            },
            TSpawns = new Vector3[] { new(-2, 0.05f, 12), new(0, 0.05f, 12), new(2, 0.05f, 12), new(-1, 0.05f, 13.5f), new(1, 0.05f, 13.5f) },
            CtSpawns = new Vector3[] { new(-2, 0.05f, -12), new(0, 0.05f, -12), new(2, 0.05f, -12), new(-1, 0.05f, -13.5f), new(1, 0.05f, -13.5f) },
            Waypoints = new Vector3[]
            {
                new(0, 0, 11), new(5.5f, 0, 10), new(5.5f, 0, 6), new(12, 0, 2.5f), new(5.5f, 0, 0),
                new(-5.5f, 0, 10), new(-5.5f, 0, 6), new(-12, 0, 2.5f), new(-5.5f, 0, 0),
                new(5.5f, 0, -6), new(5.5f, 0, -10), new(0, 0, -11), new(-5.5f, 0, -6), new(-5.5f, 0, -10)
            },
            Edges = new[] { 0,1, 1,2, 2,3, 3,4, 4,9, 9,10, 10,11, 11,13, 13,12, 12,8, 8,7, 7,6, 6,5, 5,0, 2,9, 6,12 }
        };
        m.BuildNeighbors();
        return m;
    }
}

/// <summary>Большая карта «Хорда» 96×64 м. Три маршрута: A, середина, B.</summary>
public static class MapHorda
{
    public const string Id = "horda_greybox";
    public static MapDef Build()
    {
        var m = new MapDef
        {
            Id = Id, DisplayName = "Хорда", SizeClass = "large", Author = "Рубеж", License = "CC0-1.0",
            BoundsMin = new(-48f, -1f, -32f), BoundsMax = new(48f, 8f, 32f),
            TriangleBudget = 14000, MemoryBudgetKb = 48 * 1024,
            SiteAMin = new(24f, 0f, -8f), SiteAMax = new(36f, 3f, 8f),
            SiteBMin = new(-36f, 0f, -8f), SiteBMax = new(-24f, 3f, 8f),
            Boxes = new MapBox[]
            {
                new(0, -0.25f, 0, 96, 0.5f, 64, MapMaterial.Floor),
                new(-32, 1.5f, 16, 24, 3, 0.5f, MapMaterial.Wall),
                new(-8, 1.5f, 16, 8, 3, 0.5f, MapMaterial.Wall),
                new(8, 1.5f, 16, 8, 3, 0.5f, MapMaterial.Wall),
                new(32, 1.5f, 16, 24, 3, 0.5f, MapMaterial.Wall),
                new(-32, 1.5f, -16, 24, 3, 0.5f, MapMaterial.Wall),
                new(-8, 1.5f, -16, 8, 3, 0.5f, MapMaterial.Wall),
                new(8, 1.5f, -16, 8, 3, 0.5f, MapMaterial.Wall),
                new(32, 1.5f, -16, 24, 3, 0.5f, MapMaterial.Wall),
                new(-6, 1.5f, 0, 0.5f, 3, 16, MapMaterial.Wall),
                new(6, 1.5f, 0, 0.5f, 3, 16, MapMaterial.Wall),
                new(0, 0.75f, 0, 2, 1.5f, 2, MapMaterial.Crate),
                new(28, 0.6f, 0, 1.4f, 1.2f, 1.4f, MapMaterial.Crate),
                new(30, 0.6f, 3, 1.2f, 1.2f, 1.2f, MapMaterial.Crate),
                new(-28, 0.6f, 0, 1.4f, 1.2f, 1.4f, MapMaterial.Crate),
                new(-30, 0.6f, -3, 1.2f, 1.2f, 1.2f, MapMaterial.Crate),
                new(0, 2, -32.25f, 97, 4, 0.5f, MapMaterial.Outer),
                new(0, 2, 32.25f, 97, 4, 0.5f, MapMaterial.Outer),
                new(-48.25f, 2, 0, 0.5f, 4, 65, MapMaterial.Outer),
                new(48.25f, 2, 0, 0.5f, 4, 65, MapMaterial.Outer),
                new(30, 0.01f, 0, 12, 0.02f, 12, MapMaterial.SiteA, false),
                new(-30, 0.01f, 0, 12, 0.02f, 12, MapMaterial.SiteB, false)
            },
            TSpawns = new Vector3[] { new(-4, 0.05f, 26), new(-2, 0.05f, 26), new(0, 0.05f, 26), new(2, 0.05f, 26), new(4, 0.05f, 26) },
            CtSpawns = new Vector3[] { new(-4, 0.05f, -26), new(-2, 0.05f, -26), new(0, 0.05f, -26), new(2, 0.05f, -26), new(4, 0.05f, -26) },
            Waypoints = new Vector3[]
            {
                new(0, 0, 24), new(-16, 0, 22), new(-16, 0, 10), new(-26, 0, 6), new(-16, 0, -10), new(-16, 0, -22),
                new(0, 0, -24), new(16, 0, -22), new(16, 0, -10), new(26, 0, -6),
                new(0, 0, 10), new(2.5f, 0, 0), new(0, 0, -10), new(16, 0, 22), new(16, 0, 10)
            },
            Edges = new[] { 0,1, 1,2, 2,3, 3,4, 4,5, 5,6, 6,7, 7,8, 8,9, 0,13, 13,14, 14,9, 14,8, 0,10, 10,11, 11,12, 12,6, 2,4 }
        };
        m.BuildNeighbors();
        return m;
    }
}
