using System;
using System.Numerics;

namespace Rubezh.Core;

public enum GrenadeKind { Frag, Flash, Smoke, Molotov }

public readonly struct GrenadeDef
{
    public readonly GrenadeKind Kind;
    public readonly string Id, DisplayName;
    public readonly int Price, Damage;
    public readonly float Fuse, Radius, Speed, FlashDuration, SmokeDuration;
    public GrenadeDef(GrenadeKind kind, string id, string name, int price, float fuse, float radius, int damage, float speed,
                      float flashDuration = 0f, float smokeDuration = 0f)
    {
        Kind = kind; Id = id; DisplayName = name; Price = price; Fuse = fuse; Radius = radius; Damage = damage; Speed = speed;
        FlashDuration = flashDuration; SmokeDuration = smokeDuration;
    }
}

public static class GrenadeCatalog
{
    public static readonly GrenadeDef Frag = new(GrenadeKind.Frag, "grenade_frag", "Осколочная «Сота»", 300, 1.6f, 4.5f, 98, 18f);
    public static readonly GrenadeDef Flash = new(GrenadeKind.Flash, "grenade_flash", "Световая «Луч»", 200, 1.4f, 6f, 0, 19f, flashDuration: 2.5f);
    public static readonly GrenadeDef Smoke = new(GrenadeKind.Smoke, "grenade_smoke", "Дымовая «Туман»", 300, 2.0f, 4.2f, 0, 16f, smokeDuration: 12f);
    public static readonly GrenadeDef Molotov = new(GrenadeKind.Molotov, "grenade_molotov", "Зажигательная «Жар»", 400, 1.8f, 3.2f, 12, 15f);
    public static GrenadeDef Get(GrenadeKind k) => k switch
    {
        GrenadeKind.Flash => Flash,
        GrenadeKind.Smoke => Smoke,
        GrenadeKind.Molotov => Molotov,
        _ => Frag
    };
}

public struct ThrownGrenade
{
    public GrenadeKind Kind;
    public Vector3 Position, Velocity;
    public float FuseLeft;
    public int OwnerTeam;
    public bool Alive;
}

public static class GrenadeCore
{
    public static ThrownGrenade Throw(GrenadeDef def, Vector3 origin, Vector3 dir, int team)
    {
        dir = Vector3.Normalize(dir);
        return new ThrownGrenade
        {
            Kind = def.Kind, Position = origin, Velocity = dir * def.Speed + new Vector3(0, 2.4f, 0),
            FuseLeft = def.Fuse, OwnerTeam = team, Alive = true
        };
    }

    public static bool Tick(ref ThrownGrenade g, float dt, float gravity = 20.3f)
    {
        if (!g.Alive) return false;
        g.Velocity.Y -= gravity * dt;
        g.Position += g.Velocity * dt;
        if (g.Position.Y < 0.08f)
        {
            g.Position.Y = 0.08f;
            g.Velocity.Y *= -0.25f;
            g.Velocity.X *= 0.55f;
            g.Velocity.Z *= 0.55f;
        }
        g.FuseLeft -= dt;
        if (g.FuseLeft > 0f) return false;
        g.Alive = false;
        return true;
    }

    public static int BlastDamage(GrenadeDef def, Vector3 center, Vector3 target)
    {
        if (def.Damage <= 0) return 0;
        float d = Vector3.Distance(center, target);
        if (d >= def.Radius) return 0;
        float k = 1f - d / def.Radius;
        return Math.Max(1, (int)MathF.Round(def.Damage * k * k));
    }
}
