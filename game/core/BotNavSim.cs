using System;
using System.Numerics;

namespace Rubezh.Core;

/// <summary>Симуляция ботов по графу карты: 20 раундов без Godot.</summary>
public static class BotNavSim
{
    public struct Report
    {
        public string MapId;
        public int Rounds, Stuck, Falls, Plants;
        public bool Ok => Stuck == 0 && Falls == 0 && Rounds == 20;
    }

    public static Report Run(MapDef map, int rounds = 20, uint seed = 1)
    {
        var r = new Report { MapId = map.Id, Rounds = rounds };
        var err = MapChecks.Validate(map);
        if (err != null) throw new InvalidOperationException(map.Id + ": " + err);
        var rng = seed == 0 ? 0x9E3779B9u : seed;
        var match = new MatchState();
        for (int round = 0; round < rounds; round++)
        {
            int tWp = Nearest(map, map.TSpawns[0]);
            int ctWp = Nearest(map, map.CtSpawns[0]);
            int siteA = SiteWp(map, true);
            int siteB = SiteWp(map, false);
            int goal = (rng = Next(rng)) % 2 == 0 ? siteA : siteB;
            match.Tick(15f); // пропускаем фриз, чтобы раунд был в Live и закладка была возможна
            float t = 0;
            bool planted = false;
            int lastT = tWp, lastCt = ctWp;
            int stallT = 0, stallCt = 0;
            while (t < 90f && !planted)
            {
                t += 0.25f;
                int nt = StepToward(map, tWp, goal, ref rng);
                if (nt == tWp && tWp != goal) stallT++; else stallT = 0;
                tWp = nt;
                int nct = StepToward(map, ctWp, goal, ref rng);
                if (nct == ctWp && ctWp != goal) stallCt++; else stallCt = 0;
                ctWp = nct;
                if (stallT > 40 || stallCt > 40) { r.Stuck++; break; }
                if (!Inside(map, map.Waypoints[tWp]) || !Inside(map, map.Waypoints[ctWp])) { r.Falls++; break; }
                if (tWp == goal)
                {
                    match.BeginPlant(3.1f);
                    planted = match.BombPlanted;
                    if (planted) r.Plants++;
                }
                lastT = tWp; lastCt = ctWp;
            }
            match.Tick(0.016f);
            if (match.Phase == RoundPhase.Bomb) match.BeginDefuse(7.1f);
            match.Tick(0.016f);
            if (match.Phase == RoundPhase.End) match.NextRound();
            else
            {
                match.Kill(TeamId.T); match.Kill(TeamId.T); match.Kill(TeamId.T); match.Kill(TeamId.T); match.Kill(TeamId.T);
                if (match.Phase == RoundPhase.End) match.NextRound();
            }
        }
        return r;
    }

    static int StepToward(MapDef map, int from, int goal, ref uint rng)
    {
        if (from == goal) return from;
        var n = map.Neighbors[from];
        if (n.Length == 0) return from;
        int best = n[0];
        float bd = Vector3.Distance(map.Waypoints[best], map.Waypoints[goal]);
        for (int i = 1; i < n.Length; i++)
        {
            float d = Vector3.Distance(map.Waypoints[n[i]], map.Waypoints[goal]);
            if (d < bd) { bd = d; best = n[i]; }
        }
        if ((rng = Next(rng)) % 7 == 0) best = n[(int)(rng % (uint)n.Length)];
        return best;
    }

    static int SiteWp(MapDef map, bool a)
    {
        var min = a ? map.SiteAMin : map.SiteBMin; var max = a ? map.SiteAMax : map.SiteBMax;
        for (int i = 0; i < map.Waypoints.Length; i++)
            if (MapValidator.InsideXZ(map.Waypoints[i], min, max)) return i;
        return 0;
    }

    static int Nearest(MapDef map, Vector3 p)
    {
        int b = 0; float d = float.MaxValue;
        for (int i = 0; i < map.Waypoints.Length; i++)
        { float x = Vector3.Distance(map.Waypoints[i], p); if (x < d) { d = x; b = i; } }
        return b;
    }

    static bool Inside(MapDef map, Vector3 p) =>
        p.X >= map.BoundsMin.X && p.X <= map.BoundsMax.X && p.Z >= map.BoundsMin.Z && p.Z <= map.BoundsMax.Z;

    static uint Next(uint x) { x ^= x << 13; x ^= x >> 17; x ^= x << 5; return x; }
}
