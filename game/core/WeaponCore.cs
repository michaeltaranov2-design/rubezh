using System;
using System.Numerics;

namespace Rubezh.Core;

public enum HitZone { Head, Chest, Stomach, Legs }

public sealed class WeaponDef
{
    public string Id = "";
    public string DisplayName = "";
    public int Damage;
    public float RoundsPerMinute;
    public int MagazineSize;
    public float ReloadSeconds;
    public bool Automatic;
    public float BaseSpreadDeg, MoveSpreadDeg, AirSpreadDeg;
    public float RecoilResetSeconds;
    public float[] RecoilPitch = Array.Empty<float>();
    public float[] RecoilYaw = Array.Empty<float>();
    public float RangeMeters;
    public float FalloffPer10m;
    public float HeadMultiplier = 4f, ChestMultiplier = 1f, StomachMultiplier = 1.25f, LegsMultiplier = 0.75f;
    public double FireInterval => 60.0 / RoundsPerMinute;
}

public static class WeaponCatalog
{
    public static readonly WeaponDef Rifle = Build(new WeaponDef
    {
        Id = "rifle_gran", DisplayName = "Винтовка «Грань»", Damage = 34, RoundsPerMinute = 600f, MagazineSize = 30,
        ReloadSeconds = 2.4f, Automatic = true, BaseSpreadDeg = 0.12f, MoveSpreadDeg = 2.2f, AirSpreadDeg = 6f,
        RecoilResetSeconds = 0.35f, RangeMeters = 150f, FalloffPer10m = 0.02f
    }, 0.55f, 9, 0.35f);

    public static readonly WeaponDef Pistol = Build(new WeaponDef
    {
        Id = "pistol_klyn", DisplayName = "Пистолет «Клин»", Damage = 30, RoundsPerMinute = 400f, MagazineSize = 12,
        ReloadSeconds = 2.2f, Automatic = false, BaseSpreadDeg = 0.25f, MoveSpreadDeg = 1.2f, AirSpreadDeg = 4f,
        RecoilResetSeconds = 0.25f, RangeMeters = 80f, FalloffPer10m = 0.06f
    }, 1.1f, 12, 0.1f);

    /// <summary>Паттерн отдачи: сильный подъём первых выстрелов, затем плато и боковое раскачивание.</summary>
    static WeaponDef Build(WeaponDef d, float kick, int riseShots, float sway)
    {
        d.RecoilPitch = new float[d.MagazineSize];
        d.RecoilYaw = new float[d.MagazineSize];
        for (int i = 0; i < d.MagazineSize; i++)
        {
            d.RecoilPitch[i] = i < riseShots ? kick : kick * 0.2f;
            d.RecoilYaw[i] = i < 3 ? 0f : sway * MathF.Sin(i * 0.65f);
        }
        return d;
    }
}

public struct WeaponState
{
    public int Ammo;
    public int ShotIndex;
    public double NextFireTime;
    public double LastShotTime;
    public double ReloadEndTime;
    public bool Reloading;
    public bool TriggerWasDown;
    public uint Rng;

    public static WeaponState Create(WeaponDef d, uint seed) => new()
    {
        Ammo = d.MagazineSize,
        LastShotTime = double.NegativeInfinity,
        Rng = seed == 0 ? 0x9E3779B9u : seed
    };
}

public struct ShotResult
{
    public float KickPitchDeg, KickYawDeg, SpreadPitchDeg, SpreadYawDeg;
}

/// <summary>Детерминированная стрельба: одинаковый seed даёт одинаковый разброс (нужно для сервера и предсказания).</summary>
public static class WeaponCore
{
    public static WeaponState Create(WeaponDef d, uint seed) => WeaponState.Create(d, seed);

    public static void Update(ref WeaponState s, WeaponDef d, double now)
    {
        if (s.Reloading && now >= s.ReloadEndTime)
        {
            s.Reloading = false;
            s.Ammo = d.MagazineSize;
            s.ShotIndex = 0;
        }
        if (now - s.LastShotTime >= d.RecoilResetSeconds) s.ShotIndex = 0;
    }

    public static bool StartReload(ref WeaponState s, WeaponDef d, double now)
    {
        if (s.Reloading || s.Ammo >= d.MagazineSize) return false;
        s.Reloading = true;
        s.ReloadEndTime = now + d.ReloadSeconds;
        return true;
    }

    public static bool TryFire(ref WeaponState s, WeaponDef d, double now, bool triggerDown, float horizontalSpeed, bool onGround, out ShotResult shot)
    {
        shot = default;
        bool pressedNow = triggerDown && !s.TriggerWasDown;
        s.TriggerWasDown = triggerDown;
        Update(ref s, d, now);
        if (!triggerDown || s.Reloading || now < s.NextFireTime || s.Ammo <= 0) return false;
        if (!d.Automatic && !pressedNow) return false;

        float spread = d.BaseSpreadDeg + (onGround ? d.MoveSpreadDeg * Math.Clamp(horizontalSpeed / 6.35f, 0f, 1f) : d.AirSpreadDeg);
        float angle = NextFloat(ref s.Rng) * MathF.Tau;
        float radius = spread * MathF.Sqrt(NextFloat(ref s.Rng));
        shot.SpreadPitchDeg = radius * MathF.Sin(angle);
        shot.SpreadYawDeg = radius * MathF.Cos(angle);

        int i = Math.Min(s.ShotIndex, d.RecoilPitch.Length - 1);
        if (i >= 0)
        {
            shot.KickPitchDeg = d.RecoilPitch[i];
            shot.KickYawDeg = d.RecoilYaw[i];
        }
        double interval = d.FireInterval;
        s.NextFireTime = now - s.NextFireTime < interval ? s.NextFireTime + interval : now + interval;
        s.Ammo--;
        s.ShotIndex++;
        s.LastShotTime = now;
        return true;
    }

    public static int Damage(WeaponDef d, HitZone zone, float distance)
    {
        float m = zone switch
        {
            HitZone.Head => d.HeadMultiplier,
            HitZone.Chest => d.ChestMultiplier,
            HitZone.Stomach => d.StomachMultiplier,
            _ => d.LegsMultiplier
        };
        float falloff = MathF.Pow(1f - d.FalloffPer10m, MathF.Max(distance, 0f) / 10f);
        return Math.Max(1, (int)MathF.Round(d.Damage * m * falloff));
    }

    /// <summary>xorshift32 → [0, 1).</summary>
    public static float NextFloat(ref uint state)
    {
        uint x = state;
        x ^= x << 13;
        x ^= x >> 17;
        x ^= x << 5;
        state = x;
        return (x >> 8) * (1f / 16777216f);
    }
}
