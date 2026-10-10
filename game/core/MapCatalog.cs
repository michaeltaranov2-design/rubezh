using System;

namespace Rubezh.Core;

public static class MapCatalog
{
    public static MapDef[] All() => new[] { MapKlin.Build(), MapDef.FromUzel(), MapHorda.Build() };
    public static MapDef Get(string id)
    {
        foreach (var m in All()) if (m.Id == id) return m;
        throw new ArgumentException("неизвестная карта: " + id);
    }
}

public static class MapChecks
{
    public static string? Validate(MapDef m)
    {
        if (m.TSpawns.Length != 5 || m.CtSpawns.Length != 5) return "нужно 5+5 точек появления";
        if (m.Waypoints.Length < 6 || m.Edges.Length < 8 || (m.Edges.Length & 1) != 0) return "мало маршрутов";
        if (m.Neighbors.Length != m.Waypoints.Length) m.BuildNeighbors();
        var seen = new bool[m.Waypoints.Length];
        var q = new int[m.Waypoints.Length]; int nq = 0;
        seen[0] = true; q[nq++] = 0;
        for (int i = 0; i < nq; i++)
            foreach (int nb in m.Neighbors[q[i]])
                if (!seen[nb]) { seen[nb] = true; q[nq++] = nb; }
        if (nq != m.Waypoints.Length) return "граф маршрутов несвязный";
        for (int i = 0; i < m.Edges.Length; i += 2)
            if (Hits(m, m.Waypoints[m.Edges[i]], m.Waypoints[m.Edges[i+1]]))
                return "ребро проходит сквозь стену";
        foreach (var s in m.TSpawns)
            if (Hits(m, s, s)) return "T-спавн в стене";
        foreach (var s in m.CtSpawns)
            if (Hits(m, s, s)) return "CT-спавн в стене";
        if (!MapValidator.InsideXZ(m.Waypoints[Nearest(m, m.SiteAMin)], m.SiteAMin, m.SiteAMax) &&
            !SiteHasWp(m, true)) return "нет точки маршрута на A";
        if (!SiteHasWp(m, false)) return "нет точки маршрута на B";
        return null;
    }

    static bool Hits(MapDef m, System.Numerics.Vector3 a, System.Numerics.Vector3 b)
    {
        foreach (var box in m.Boxes)
            if (MapValidator.SegmentHitsBox(a, b, box, m.BotRadius)) return true;
        return false;
    }

    static bool SiteHasWp(MapDef m, bool a)
    {
        var min = a ? m.SiteAMin : m.SiteBMin; var max = a ? m.SiteAMax : m.SiteBMax;
        foreach (var w in m.Waypoints) if (MapValidator.InsideXZ(w, min, max)) return true;
        return false;
    }

    static int Nearest(MapDef m, System.Numerics.Vector3 p)
    {
        int b = 0; float d = float.MaxValue;
        for (int i = 0; i < m.Waypoints.Length; i++)
        { float x = System.Numerics.Vector3.Distance(m.Waypoints[i], p); if (x < d) { d = x; b = i; } }
        return b;
    }
}
