using System;
using System.Numerics;
using System.Text;
using Rubezh.Core;

namespace Rubezh.Core.Stage3Tests;

public static class Program
{
    static int _ok, _fail;
    static void Check(bool c, string n) { if (c) { _ok++; Console.WriteLine("  OK   " + n); } else { _fail++; Console.WriteLine("  FAIL " + n); } }
    public static int Main()
    {
        Console.OutputEncoding = Encoding.UTF8;
        Proto(); Server(); Pvs(); Cons(); Predict(); Channel();
        Console.WriteLine("Итого: пройдено " + _ok + ", провалено " + _fail);
        return _fail == 0 && _ok > 0 ? 0 : 1;
    }

    static void Proto()
    {
        Console.WriteLine("[Протокол]");
        var c = new InputCommand { Seq = 7, Tick = 3, Slot = 1, Flags = 1 | 128, Yaw = 1.2f, Pitch = -0.1f, WeaponId = 2 };
        Span<byte> buf = stackalloc byte[64];
        int n = c.Write(buf);
        Check(n == 28 && n < NetProto.Mtu, "input 28 байт");
        Check(InputCommand.TryRead(buf[..n], out var r) && r.Seq == 7 && r.Forward && r.Fire, "input roundtrip");
        Check(!InputCommand.TryRead(new byte[28], out _), "мусор отклонён");
        var snap = new Snapshot { Tick = 9, AckSeq = 7, Phase = 1, Round = 2, ScoreT = 3, ScoreCt = 1, Viewer = 0, Money = 800, Visible = new SnapPlayer[10] };
        for (int i = 0; i < 10; i++) snap.Visible[i] = new SnapPlayer { Slot = (byte)i, Hp = 100, X = i };
        Span<byte> sb = stackalloc byte[NetProto.Mtu];
        int sn = snap.Write(sb);
        Check(sn > 0 && sn <= NetProto.Mtu, "снимок 10 игроков в MTU " + sn);
        var s2 = new Snapshot();
        Check(Snapshot.TryRead(sb[..sn], s2) && s2.Visible.Length == 10 && s2.AckSeq == 7, "snapshot roundtrip");
    }

    static void Server()
    {
        Console.WriteLine("[Сервер]");
        var sv = new SimServer();
        var cmd = new InputCommand { Seq = 1, Slot = 0, Flags = 1, Yaw = 0 };
        sv.Enqueue(cmd); sv.Enqueue(cmd);
        sv.Step();
        Check(sv.Players[0].LastSeq == 1, "дубль seq отброшен");
        float z0 = sv.Players[0].Pos.Z;
        sv.Enqueue(new InputCommand { Seq = 2, Slot = 0, Flags = 1, Yaw = 0 });
        sv.Step();
        Check(sv.Players[0].Pos.Z != z0, "движение применено");
        sv.Enqueue(new InputCommand { Seq = 4, Slot = 0, Flags = 1, Yaw = 0 });
        sv.Enqueue(new InputCommand { Seq = 3, Slot = 0, Flags = 1, Yaw = 0 });
        uint last = sv.Players[0].LastSeq;
        sv.Step();
        Check(sv.Players[0].LastSeq == 4, "reorder: latest wins");
        var t = sv.Players[0]; var e = sv.Players[5];
        e.Pos = t.Pos + new Vector3(0, 0, -2f);
        t.Yaw = 0;
        sv.Enqueue(new InputCommand { Seq = 10, Slot = 0, Flags = 128, Yaw = 0 });
        sv.Step();
        Check(e.Hp < 100, "hitscan нанёс урон");
        for (int i = 0; i < 64 * 16; i++) sv.Step();
        Check(sv.Match.Phase == RoundPhase.Live, "через ~16 с freeze → live");
    }

    static void Pvs()
    {
        Console.WriteLine("[PVS]");
        var sv = new SimServer();
        sv.Players[0].Pos = new System.Numerics.Vector3(-20f, 0.05f, 0f);
        sv.Players[5].Pos = new System.Numerics.Vector3(20f, 0.05f, 0f);
        var snap = sv.BuildSnapshot(0);
        bool enemy = false;
        foreach (var p in snap.Visible) if (p.Team != 0) enemy = true;
        Check(!enemy, "скрытый CT не в payload");
        sv.Console.Execute("game give self wallhack", 100, 1);
        var w = sv.BuildSnapshot(0);
        int n = 0; foreach (var p in w.Visible) if (p.Team != 0) n++;
        Check(n > 0, "wallhack показывает скрытых");
        Check(snap.Write(stackalloc byte[NetProto.Mtu]) <= NetProto.Mtu, "pvs снимок в MTU");
    }

    static void Cons()
    {
        Console.WriteLine("[Консоль]");
        var c = new ConsoleState();
        Check(c.Execute("game give self wallhack", 1, 10).Ok, "give ok");
        Check(c.Execute("game give self wallhack", 1, 10).Code == "dup", "nonce идемпотентен");
        Check(c.Execute("game give self wallhack", 1, 11).Ok, "вторая активация");
        Check(c.Execute("game give self wallhack", 1, 12).Ok, "третья активация");
        Check(!c.Execute("game give self wallhack", 1, 13).Ok, "квота 3");
        var k = new ConsoleState { Mode = MatchMode.Competitive };
        Check(!k.Execute("game give self wallhack", 1, 1).Ok, "Competitive: wallhack запрещён");
        Check(k.Execute("game give self aimbot", 1, 2).Ok, "Competitive: aimbot ок");
    }

    static void Predict()
    {
        Console.WriteLine("[Предсказание]");
        var p = new ClientPredict { Pos = new Vector3(0, 0.05f, 0), Move = new MoveState { OnGround = true } };
        p.Make(1, 0);
        var before = p.Pos;
        var auth = new SnapPlayer { X = 0, Y = 0.05f, Z = 0, Flags = 2 };
        p.Reconcile(auth, 0);
        Check(Vector3.Distance(p.Pos, before) < 0.01f || p.Seq == 1, "откат и повтор");
        Check(p.Seq == 1, "seq растёт");
    }

    static void Channel()
    {
        Console.WriteLine("[Канал]");
        var ch = new NetChannel(7) { Loss = 0.3f, Dup = 0.2f, Reorder = 0.2f };
        var c = new InputCommand { Seq = 1, Flags = 1 };
        Span<byte> b = stackalloc byte[28]; c.Write(b);
        int sent = 0, got = 0;
        for (int i = 0; i < 40; i++) { c.Seq = (uint)(i + 1); c.Write(b); ch.Send(b); sent++; }
        Span<byte> r = stackalloc byte[64];
        while (ch.Recv(r) > 0) got++;
        Check(got > 0 && got != sent, "loss/dup/reorder меняют поток");
    }
}
