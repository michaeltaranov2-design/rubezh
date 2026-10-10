using Godot;
using Rubezh.Core;

namespace Rubezh.Game;

public interface IDamageable
{
    int Team { get; }
    bool Alive { get; }
    void ApplyDamage(int amount, HitZone zone, Node3D? attacker);
}

public struct HitInfo
{
    public bool Hit, Kill, Friendly;
    public HitZone Zone;
    public int Damage;
    public float Distance;
    public Godot.Vector3 Position;
    public IDamageable? Target;
}

public static class Combat
{
    static PhysicsRayQueryParameters3D? _q;
    public static ImpactPool? Pool;

    public static void Bind(ImpactPool impacts) => Pool = impacts;

    public static void Release()
    {
        _q = null;
        Pool = null;
    }

    public static HitInfo Fire(World3D world, Node3D attacker, int team, WeaponDef def, Godot.Vector3 origin, Godot.Vector3 dir, Rid exclude)
    {
        HitInfo info = default;
        _q ??= new PhysicsRayQueryParameters3D();
        _q.From = origin;
        _q.To = origin + dir * def.RangeMeters;
        _q.CollisionMask = Layers.FireMask;
        _q.Exclude = new Godot.Collections.Array<Rid> { exclude };
        var hit = world.DirectSpaceState.IntersectRay(_q);
        if (hit.Count == 0) return info;
        info.Hit = true;
        info.Position = (Godot.Vector3)hit["position"];
        info.Distance = origin.DistanceTo(info.Position);
        var collider = hit["collider"].AsGodotObject();
        if (collider is HitboxArea hb && hb.Target is IDamageable dmg)
        {
            info.Zone = hb.Zone;
            info.Target = dmg;
            info.Friendly = dmg.Team == team;
            if (!info.Friendly && dmg.Alive)
            {
                info.Damage = WeaponCore.Damage(def, hb.Zone, info.Distance);
                bool wasAlive = dmg.Alive;
                dmg.ApplyDamage(info.Damage, hb.Zone, attacker);
                info.Kill = wasAlive && !dmg.Alive;
            }
        }
        Pool?.Spawn(info.Position);
        return info;
    }
}
