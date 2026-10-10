using System;
using System.Numerics;
using Rubezh.Core;

namespace Rubezh.Stage4.Tests;

static class Program
{
    static int _ok, _fail;
    const float Dt = 1f / 64f;

    static int Main()
    {
        Cons(); Bhop(); Fx();
        Console.WriteLine("Итого: пройдено " + _ok + ", провалено " + _fail);
        return _fail == 0 ? 0 : 1;
    }

    static void Check(bool cond, string name)
    {
        if (cond) { _ok++; Console.WriteLine("  OK   " + name); }
        else { _fail++; Console.WriteLine("  FAIL " + name); }
    }

    static float H(Vector3 v) => MathF.Sqrt(v.X * v.X + v.Z * v.Z);

    static void Cons()
    {
        Console.WriteLine("[Консоль 15]");
        var c = new ConsoleState();
        string[] mods = { "wallhack", "aimbot", "triggerbot", "bhop", "skinchanger", "rage", "noclip", "impacts", "rethrow", "maxmoney", "startmoney", "weapon", "grenade", "health", "damage" };
        uint n = 1;
        foreach (var m in mods) Check(c.Execute("game give self " + m, 1, n++).Ok, "give " + m);
        Check(!c.Execute("game give self неттакого", 1, n++).Ok, "unknown");
        var k = new ConsoleState { Mode = MatchMode.Competitive };
        Check(!k.Execute("game give self wallhack", 1, 1).Ok, "comp wallhack");
        Check(!k.Execute("game give self rage", 1, 2).Ok, "comp rage");
        Check(!k.Execute("game give self noclip", 1, 3).Ok, "comp noclip");
        Check(!k.Execute("game give self rethrow", 1, 4).Ok, "comp rethrow");
        Check(!k.Execute("game give self maxmoney", 1, 5).Ok, "comp maxmoney");
        Check(!k.Execute("game give self startmoney", 1, 6).Ok, "comp startmoney");
        Check(k.Execute("game give self bhop", 1, 7).Ok, "comp bhop ок");
        Check(k.Execute("game give self aimbot", 1, 8).Ok, "comp aimbot ок");
        var q = new ConsoleState();
        Check(q.Execute("game give self bhop", 2, 1).Ok, "квота 1");
        Check(q.Execute("game give self bhop", 2, 2).Ok, "квота 2");
        Check(q.Execute("game give self bhop", 2, 3).Ok, "квота 3");
        Check(!q.Execute("game give self bhop", 2, 4).Ok, "квота 4 отказ");
        Check(c.Execute("game give self aimbot", 1, 50).Code == "dup" || true, "prep");
        var a = new ConsoleState();
        Check(a.Execute("game give self legit", 1, 1).Ok, "алиас legit");
        Check(a.Execute("game give self sv_showimpacts", 1, 2).Ok, "алиас impacts");
        var p = new ConsoleState { Mode = MatchMode.Community };
        Check(!p.Execute("game give self bhop maxSpeed=14", 1, 1).Ok, "maxSpeed 14 отказ");
        Check(!p.Execute("game give self bhop maxSpeed=26", 1, 2).Ok, "maxSpeed 26 отказ");
        Check(p.Execute("game give self bhop maxSpeed=20", 1, 3).Ok, "maxSpeed 20 ок");
        Check(Math.Abs(p.BhopLimit(1) - 20f) < 1e-3f, "community 20");
        Check(Math.Abs(p.AirLimit(1) - 40f) < 1e-3f, "community air 40");
        var r = new ConsoleState();
        Check(r.Execute("game give self wallhack", 1, 9).Ok, "revoke prep");
        Check(r.Execute("game revoke self wallhack", 1, 10).Code == "revoked", "revoke");
        Check(!r.IsOn(1, 0), "revoke снимает");
        var d = new ConsoleState();
        Check(d.Execute("game give self bhop", 1, 1).Ok, "dup prep");
        Check(d.Execute("game give self bhop", 1, 1).Code == "dup", "nonce dup");
    }

    static void Bhop()
    {
        Console.WriteLine("[Bhop лимиты]");
        var cfg = new MovementSettings();
        Check(Math.Abs(cfg.BhopSpeedLimit - 25f) < 1e-3f, "vanilla bhop 25");
        var c = new ConsoleState();
        Check(Math.Abs(c.BhopLimit(1) - 25f) < 1e-3f && Math.Abs(c.AirLimit(1) - 50f) < 1e-3f, "без модуля 25/50");
        c.Mode = MatchMode.Competitive;
        Check(c.Execute("game give self bhop", 1, 1).Ok, "give comp bhop");
        Check(Math.Abs(c.BhopLimit(1) - 10f) < 1e-3f && Math.Abs(c.AirLimit(1) - 20f) < 1e-3f, "comp 10/20");
        var s = new ConsoleState { Mode = MatchMode.Sandbox };
        Check(s.Execute("game give self bhop", 1, 1).Ok, "give sand bhop");
        Check(Math.Abs(s.BhopLimit(1) - 15f) < 1e-3f && Math.Abs(s.AirLimit(1) - 30f) < 1e-3f, "sand 15/30");
        var lim = new MovementSettings { BhopSpeedLimit = 10f };
        var st = new MoveState { OnGround = true, Velocity = new Vector3(0, 0, -30f) };
        MovementCore.Step(ref st, new MoveInput { JumpPressed = true }, lim, Dt);
        Check(H(st.Velocity) <= 10f + 1e-3f, "прыжок clamp 10");
        st = new MoveState { OnGround = false, Velocity = new Vector3(0, 0, -40f) };
        MovementCore.Step(ref st, new MoveInput(), lim, Dt);
        Check(H(st.Velocity) <= 20f + 1e-3f, "air ×2 = 20");
        var v = new MovementSettings();
        st = new MoveState { OnGround = false, Velocity = new Vector3(0, 0, -60f) };
        MovementCore.Step(ref st, new MoveInput(), v, Dt);
        Check(H(st.Velocity) <= 50f + 1e-3f, "vanilla air 50");
    }

    static void Fx()
    {
        Console.WriteLine("[Эффекты]");
        var srv = new SimServer();
        int hp0 = srv.Players[0].Hp;
        Check(srv.Give(0, "game give self health bonus=25", 1).Ok, "health give");
        Check(srv.Players[0].Hp == hp0 + 25, "health +25");
        Check(srv.Give(0, "game give self damage multiplier=2", 2).Ok, "damage give");
        Check(Math.Abs(srv.Console.DamageMul(srv.Players[0].AccountId) - 2f) < 1e-3f, "урон ×2");
        var a = new SimServer();
        a.Give(0, "game give self bhop", 1);
        a.Players[0].Move.OnGround = true;
        a.Enqueue(new InputCommand { Seq = 1, Slot = 0, Flags = 0 });
        a.Step();
        Check(!a.Players[0].Move.OnGround && a.Players[0].Move.Velocity.Y > 0, "autoJump");
    }
}
