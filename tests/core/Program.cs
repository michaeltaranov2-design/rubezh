using System;
using System.Collections.Generic;
using System.Numerics;
using Rubezh.Core;

namespace Rubezh.Core.Tests;

/// <summary>Самодостаточный раннер: без NuGet-зависимостей, код выхода 0 только при успехе.</summary>
public static class Program
{
    static int _passed, _failed;
    const float Dt = 1f / 64f;

    static void Check(bool ok, string name)
    {
        if (ok) { _passed++; Console.WriteLine("  OK   " + name); }
        else { _failed++; Console.WriteLine("  FAIL " + name); }
    }

    public static int Main()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Run("Движение", MovementTests);
        Run("Оружие", WeaponTests);
        Run("Урон", DamageTests);
        Run("Карта", MapTests);
        Run("Аллокации", AllocationTests);
        Console.WriteLine();
        Console.WriteLine("Итого: пройдено " + _passed + ", провалено " + _failed);
        return _failed == 0 && _passed > 0 ? 0 : 1;
    }

    static void Run(string name, Action body)
    {
        Console.WriteLine("[" + name + "]");
        try { body(); }
        catch (Exception e) { _failed++; Console.WriteLine("  FAIL исключение: " + e); }
    }

    static float Horizontal(Vector3 v) => MathF.Sqrt(v.X * v.X + v.Z * v.Z);

    static void MovementTests()
    {
        var cfg = new MovementSettings();
        var s = new MoveState { OnGround = true, Velocity = new Vector3(0, 0, -6f) };
        var none = new MoveInput();
        for (int i = 0; i < 64; i++) MovementCore.Step(ref s, none, cfg, Dt);
        Check(Horizontal(s.Velocity) < 1e-3f, "трение останавливает игрока за секунду");

        s = new MoveState { OnGround = true };
        var fwd = new MoveInput { Forward = 1 };
        for (int i = 0; i < 128; i++) MovementCore.Step(ref s, fwd, cfg, Dt);
        float top = Horizontal(s.Velocity);
        Check(top <= cfg.MaxSpeed + 1e-3f && top > cfg.MaxSpeed - 0.05f, "разгон до максимальной скорости без превышения");
        Check(s.Velocity.Z < 0, "yaw=0 смотрит в -Z (соглашение Godot)");

        s = new MoveState { OnGround = true };
        var diag = new MoveInput { Forward = 1, Right = 1 };
        for (int i = 0; i < 128; i++) MovementCore.Step(ref s, diag, cfg, Dt);
        Check(Horizontal(s.Velocity) <= cfg.MaxSpeed + 1e-3f, "диагональ не быстрее прямой");

        s = new MoveState { OnGround = true };
        var crouch = new MoveInput { Forward = 1, Crouch = true };
        for (int i = 0; i < 128; i++) MovementCore.Step(ref s, crouch, cfg, Dt);
        Check(Horizontal(s.Velocity) <= cfg.MaxSpeed * cfg.CrouchSpeedFactor + 1e-3f, "присед ограничивает скорость");

        s = new MoveState { OnGround = true };
        MovementCore.Step(ref s, new MoveInput { JumpPressed = true }, cfg, Dt);
        Check(!s.OnGround && s.Velocity.Y > 0, "прыжок отрывает от земли");

        s = new MoveState { OnGround = true, Velocity = new Vector3(0, 0, -8f) };
        MovementCore.Step(ref s, new MoveInput { JumpPressed = true, Yaw = 0 }, cfg, Dt);
        Check(MathF.Abs(Horizontal(s.Velocity) - 8f) < 1e-3f, "bunnyhop: в кадр прыжка трение не применяется");

        s = new MoveState { OnGround = false, Velocity = new Vector3(0, 0, -cfg.MaxSpeed) };
        for (int i = 0; i < 64; i++)
        {
            float yaw = MathF.Atan2(-s.Velocity.X, -s.Velocity.Z);
            MovementCore.Step(ref s, new MoveInput { Right = 1, Yaw = yaw }, cfg, Dt);
        }
        Check(Horizontal(s.Velocity) > 7.5f, "air-strafe набирает скорость выше максимальной");

        s = new MoveState { OnGround = false, Velocity = new Vector3(0, 0, -9f) };
        MovementCore.Step(ref s, new MoveInput { Forward = 1 }, cfg, Dt);
        Check(Horizontal(s.Velocity) <= 9f + 1e-3f, "в воздухе нельзя ускоряться прямо вперёд");

        s = new MoveState { OnGround = false };
        MovementCore.Step(ref s, none, cfg, Dt);
        Check(s.Velocity.Y < 0, "гравитация действует в воздухе");

        var limited = new MovementSettings { BhopSpeedLimit = 7f };
        s = new MoveState { OnGround = false, Velocity = new Vector3(0, 0, -20f) };
        MovementCore.Step(ref s, none, limited, Dt);
        Check(Horizontal(s.Velocity) <= 14f + 1e-3f, "лимит скорости bhop соблюдается");
        s = new MoveState { OnGround = true, Velocity = new Vector3(0, 0, -20f) };
        MovementCore.Step(ref s, new MoveInput { JumpPressed = true }, limited, Dt);
        Check(Horizontal(s.Velocity) <= 7f + 1e-3f, "лимит в кадр прыжка");

        bool thrown = false;
        try { MovementCore.Step(ref s, none, cfg, 0f); } catch (ArgumentOutOfRangeException) { thrown = true; }
        Check(thrown, "недопустимый dt отклоняется");
    }

    static void WeaponTests()
    {
        var rifle = WeaponCatalog.Rifle;
        var st = WeaponCore.Create(rifle, 1);
        int shots = 0;
        for (int i = 0; i < 64; i++)
        {
            WeaponCore.Update(ref st, rifle, i * Dt);
            if (WeaponCore.TryFire(ref st, rifle, i * Dt, true, 0, true, out _)) shots++;
        }
        Check(shots == 10, "винтовка: 600 выстрелов/мин при тике 64 Гц (получено " + shots + ")");
        Check(st.Ammo == rifle.MagazineSize - 10, "расход патронов");

        var pistol = WeaponCatalog.Pistol;
        var ps = WeaponCore.Create(pistol, 1);
        int pshots = 0;
        for (int i = 0; i < 64; i++)
            if (WeaponCore.TryFire(ref ps, pistol, i * Dt, true, 0, true, out _)) pshots++;
        Check(pshots == 1, "полуавтомат стреляет один раз за нажатие");
        WeaponCore.TryFire(ref ps, pistol, 1.0, false, 0, true, out _);
        Check(WeaponCore.TryFire(ref ps, pistol, 1.1, true, 0, true, out _), "повторное нажатие разрешает выстрел");

        st = WeaponCore.Create(rifle, 2);
        double t = 0;
        for (int i = 0; i < rifle.MagazineSize; i++) { WeaponCore.TryFire(ref st, rifle, t, true, 0, true, out _); t += 0.2; }
        Check(st.Ammo == 0, "магазин опустошается");
        Check(!WeaponCore.TryFire(ref st, rifle, t, true, 0, true, out _), "пустой магазин не стреляет");
        Check(WeaponCore.StartReload(ref st, rifle, t), "перезарядка начинается");
        Check(!WeaponCore.TryFire(ref st, rifle, t + 1.0, true, 0, true, out _), "во время перезарядки выстрела нет");
        WeaponCore.Update(ref st, rifle, t + rifle.ReloadSeconds + 0.01);
        Check(st.Ammo == rifle.MagazineSize && !st.Reloading, "перезарядка восполняет магазин");
        Check(!WeaponCore.StartReload(ref st, rifle, t + 5), "полный магазин не перезаряжается");

        var a = WeaponCore.Create(rifle, 77);
        var b = WeaponCore.Create(rifle, 77);
        bool same = true;
        for (int i = 0; i < 10; i++)
        {
            WeaponCore.TryFire(ref a, rifle, i * 0.2, true, 3f, true, out var ra);
            WeaponCore.TryFire(ref b, rifle, i * 0.2, true, 3f, true, out var rb);
            same &= ra.SpreadPitchDeg == rb.SpreadPitchDeg && ra.SpreadYawDeg == rb.SpreadYawDeg;
        }
        Check(same, "одинаковый seed даёт одинаковый разброс (для предсказания на сервере)");

        st = WeaponCore.Create(rifle, 3);
        WeaponCore.TryFire(ref st, rifle, 0, true, 0, true, out var first);
        Check(first.KickPitchDeg == rifle.RecoilPitch[0] && first.KickYawDeg == 0f, "первый выстрел использует начало паттерна отдачи");
        for (int i = 1; i < 5; i++) WeaponCore.TryFire(ref st, rifle, i * rifle.FireInterval, true, 0, true, out _);
        Check(st.ShotIndex == 5, "индекс паттерна растёт при очереди");
        WeaponCore.TryFire(ref st, rifle, 0.5 + 1.0, true, 0, true, out var afterPause);
        Check(afterPause.KickPitchDeg == rifle.RecoilPitch[0], "паттерн сбрасывается после паузы");

        float maxStand = 0, maxRun = 0, maxAir = 0;
        var w = WeaponCore.Create(rifle, 9);
        for (int i = 0; i < 200; i++)
        {
            WeaponCore.TryFire(ref w, rifle, i * 1.0, true, 0, true, out var r0);
            maxStand = MathF.Max(maxStand, MathF.Sqrt(r0.SpreadPitchDeg * r0.SpreadPitchDeg + r0.SpreadYawDeg * r0.SpreadYawDeg));
            WeaponCore.TryFire(ref w, rifle, i * 1.0 + 0.5, true, 6.35f, true, out var r1);
            maxRun = MathF.Max(maxRun, MathF.Sqrt(r1.SpreadPitchDeg * r1.SpreadPitchDeg + r1.SpreadYawDeg * r1.SpreadYawDeg));
            WeaponCore.Update(ref w, rifle, i * 1.0 + 0.6);
            if (w.Ammo < 4) { WeaponCore.StartReload(ref w, rifle, i * 1.0 + 0.6); WeaponCore.Update(ref w, rifle, i * 1.0 + 0.6 + rifle.ReloadSeconds); }
        }
        var air = WeaponCore.Create(rifle, 10);
        for (int i = 0; i < 25; i++)
        {
            WeaponCore.TryFire(ref air, rifle, i * 1.0, true, 0, false, out var r2);
            maxAir = MathF.Max(maxAir, MathF.Sqrt(r2.SpreadPitchDeg * r2.SpreadPitchDeg + r2.SpreadYawDeg * r2.SpreadYawDeg));
        }
        Check(maxStand <= rifle.BaseSpreadDeg + 1e-3f, "стоячий разброс не выше базового");
        Check(maxRun > maxStand * 3f, "в движении разброс растёт");
        Check(maxAir > maxRun, "в воздухе разброс больше, чем в движении");
    }

    static void DamageTests()
    {
        var r = WeaponCatalog.Rifle;
        int head = WeaponCore.Damage(r, HitZone.Head, 10);
        int chest = WeaponCore.Damage(r, HitZone.Chest, 10);
        int stomach = WeaponCore.Damage(r, HitZone.Stomach, 10);
        int legs = WeaponCore.Damage(r, HitZone.Legs, 10);
        Check(head > stomach && stomach > chest && chest > legs, "урон по зонам: голова > живот > грудь > ноги");
        Check(head >= 100, "выстрел винтовки в голову смертелен без брони");
        Check(WeaponCore.Damage(r, HitZone.Chest, 10) >= WeaponCore.Damage(r, HitZone.Chest, 60), "урон падает с дистанцией");
        Check(WeaponCore.Damage(r, HitZone.Legs, 8000) >= 1, "урон не опускается ниже 1");
    }

    static void MapTests()
    {
        var obstacles = MapUzel.Boxes;
        int blocked = 0;
        for (int i = 0; i < MapUzel.Edges.Length; i += 2)
            if (MapValidator.SegmentBlocked(MapUzel.Waypoints[MapUzel.Edges[i]], MapUzel.Waypoints[MapUzel.Edges[i + 1]], obstacles, MapUzel.BotRadius)) blocked++;
        Check(blocked == 0, "рёбра навигации не проходят сквозь стены (" + blocked + ")");

        var spawns = new List<Vector3>(MapUzel.TSpawns);
        spawns.AddRange(MapUzel.CtSpawns);
        bool spawnsClear = true, spacing = true;
        for (int i = 0; i < spawns.Count; i++)
        {
            if (MapValidator.SegmentBlocked(spawns[i], spawns[i], obstacles, MapUzel.BotRadius)) spawnsClear = false;
            for (int j = i + 1; j < spawns.Count; j++)
                if (Vector3.Distance(spawns[i], spawns[j]) < 1.0f) spacing = false;
        }
        Check(spawnsClear, "точки появления свободны");
        Check(spacing, "точки появления не пересекаются");
        Check(MapUzel.TSpawns.Length == 5 && MapUzel.CtSpawns.Length == 5, "по 5 точек появления на команду (5v5)");

        bool wpClear = true;
        foreach (var wp in MapUzel.Waypoints)
            if (MapValidator.SegmentBlocked(wp, wp, obstacles, MapUzel.BotRadius)) wpClear = false;
        Check(wpClear, "точки маршрута свободны");

        var seen = new bool[MapUzel.Waypoints.Length];
        var queue = new Queue<int>();
        queue.Enqueue(0); seen[0] = true;
        while (queue.Count > 0)
            foreach (int n in MapUzel.Neighbors[queue.Dequeue()])
                if (!seen[n]) { seen[n] = true; queue.Enqueue(n); }
        Check(Array.TrueForAll(seen, x => x), "граф маршрутов связный");

        Check(MapValidator.SegmentBlocked(new Vector3(-6, 1, 14), new Vector3(-6, 1, 10), obstacles, MapUzel.BotRadius),
              "контроль: проверка обнаруживает проход сквозь стену");
        Check(MapValidator.InsideXZ(MapUzel.Waypoints[11], MapUzel.SiteAMin, MapUzel.SiteAMax), "точка A внутри зоны A");
        Check(MapValidator.InsideXZ(MapUzel.Waypoints[4], MapUzel.SiteBMin, MapUzel.SiteBMax), "точка B внутри зоны B");
    }

    static void AllocationTests()
    {
        var cfg = new MovementSettings();
        var rifle = WeaponCatalog.Rifle;
        var s = new MoveState { OnGround = true };
        var w = WeaponCore.Create(rifle, 5);
        var input = new MoveInput { Forward = 1, Right = 0.5f };
        double t = 0;
        void Loop(int n)
        {
            for (int i = 0; i < n; i++)
            {
                input.Yaw = i * 0.01f;
                input.JumpPressed = (i & 31) == 0;
                MovementCore.Step(ref s, input, cfg, Dt);
                if (s.Velocity.Y < 0) { s.Velocity.Y = 0; s.OnGround = true; }
                WeaponCore.Update(ref w, rifle, t);
                WeaponCore.TryFire(ref w, rifle, t, true, 3f, s.OnGround, out _);
                if (w.Ammo == 0) WeaponCore.StartReload(ref w, rifle, t);
                t += Dt;
            }
        }
        Loop(5000);
        long before = GC.GetAllocatedBytesForCurrentThread();
        Loop(20000);
        long delta = GC.GetAllocatedBytesForCurrentThread() - before;
        Check(delta == 0, "игровой тик без выделений памяти (выделено байт: " + delta + ")");
    }
}
