using System;
using System.Numerics;

namespace Rubezh.Core;

public sealed class SimPlayer
{
    public byte Slot;
    public TeamId Team;
    public int Hp = 100;
    public Vector3 Pos;
    public float Yaw;
    public MoveState Move;
    public uint LastSeq;
    public uint AccountId;
    public bool Alive => Hp > 0;
}

/// <summary>Авторитетный сервер 64 Гц без Godot: ввод, движение, hitscan, персональные снимки.</summary>
public sealed class SimServer
{
    public readonly MatchState Match = new();
    public readonly MapDef Map;
    public readonly SimPlayer[] Players = new SimPlayer[10];
    public readonly ConsoleState Console = new();
    public readonly MovementSettings MoveCfg = new();
    public uint Tick;
    readonly InputCommand[] _pending = new InputCommand[10];
    readonly bool[] _has = new bool[10];
    readonly SnapPlayer[] _buf = new SnapPlayer[10];

    public SimServer(MapDef? map = null)
    {
        Map = map ?? MapDef.FromUzel();
        for (int i = 0; i < 10; i++)
        {
            bool t = i < 5;
            var spawns = t ? Map.TSpawns : Map.CtSpawns;
            int si = t ? i : i - 5;
            if (si >= spawns.Length) si = spawns.Length - 1;
            var p = new SimPlayer { Slot = (byte)i, Team = t ? TeamId.T : TeamId.Ct, AccountId = (uint)(100 + i), Pos = spawns[si], Yaw = t ? 0f : MathF.PI };
            p.Move.OnGround = true;
            Players[i] = p;
        }
    }

    public void Enqueue(in InputCommand cmd)
    {
        if (cmd.Slot >= 10) return;
        if (cmd.Seq <= Players[cmd.Slot].LastSeq) return;
        if (_has[cmd.Slot] && cmd.Seq <= _pending[cmd.Slot].Seq) return;
        _pending[cmd.Slot] = cmd;
        _has[cmd.Slot] = true;
    }

    public void Step()
    {
        Tick++;
        float dt = NetProto.TickDt;
        for (int i = 0; i < 10; i++)
        {
            if (!_has[i]) continue;
            var cmd = _pending[i];
            _has[i] = false;
            var p = Players[i];
            if (!p.Alive) continue;
            p.LastSeq = cmd.Seq;
            p.Yaw = cmd.Yaw;
            var inp = cmd.ToMove();
            MovementCore.Step(ref p.Move, inp, MoveCfg, dt);
            var next = p.Pos + p.Move.Velocity * dt;
            if (!Blocked(p.Pos, next)) p.Pos = next;
            else { p.Move.Velocity.X = 0; p.Move.Velocity.Z = 0; }
            if (p.Pos.Y <= 0.05f) { p.Pos.Y = 0.05f; p.Move.OnGround = true; p.Move.Velocity.Y = 0; }
            if (cmd.Fire) TryHitscan(p);
            if (cmd.Use == 1) Match.BeginPlant(dt);
            if (cmd.Use == 2) Match.BeginDefuse(dt);
        }
        Match.Tick(dt);
        if (Match.Phase == RoundPhase.End)
        {
            Match.NextRound();
            Console.OnRoundEnd();
        }
    }

    public bool Blocked(Vector3 a, Vector3 b)
    {
        foreach (var box in Map.Boxes)
            if (box.Solid && MapValidator.SegmentHitsBox(a, b, box, Map.BotRadius)) return true;
        return false;
    }

    void TryHitscan(SimPlayer shooter)
    {
        float sy = MathF.Sin(shooter.Yaw), cy = MathF.Cos(shooter.Yaw);
        var dir = new Vector3(-sy, 0, -cy);
        var eye = shooter.Pos + new Vector3(0, 1.55f, 0);
        SimPlayer? hit = null; float best = 80f;
        for (int i = 0; i < 10; i++)
        {
            var t = Players[i];
            if (!t.Alive || t.Slot == shooter.Slot || t.Team == shooter.Team) continue;
            var to = t.Pos + new Vector3(0, 1.25f, 0) - eye;
            float dist = to.Length();
            if (dist < 0.2f || dist > best) continue;
            to /= dist;
            if (Vector3.Dot(dir, to) < 0.92f) continue;
            if (Blocked(eye, eye + to * dist)) continue;
            best = dist; hit = t;
        }
        if (hit == null) return;
        hit.Hp -= 34;
        if (hit.Hp <= 0) { hit.Hp = 0; Match.Kill(hit.Team); }
    }

    public Snapshot BuildSnapshot(byte viewer)
    {
        var v = Players[viewer];
        int n = 0;
        for (int i = 0; i < 10; i++)
        {
            var p = Players[i];
            if (!p.Alive && i != viewer) continue;
            bool ally = p.Team == v.Team || i == viewer;
            if (!ally && !Console.WallhackOn && !CanSee(v, p)) continue;
            _buf[n++] = new SnapPlayer
            {
                Slot = p.Slot, Team = (byte)p.Team, Hp = (byte)Math.Clamp(p.Hp, 0, 255),
                Flags = (byte)((p.Alive ? 1 : 0) | (p.Move.OnGround ? 2 : 0)),
                X = p.Pos.X, Y = p.Pos.Y, Z = p.Pos.Z, Yaw = p.Yaw
            };
        }
        var vis = new SnapPlayer[n];
        for (int i = 0; i < n; i++) vis[i] = _buf[i];
        return new Snapshot
        {
            Tick = Tick, AckSeq = v.LastSeq, Phase = (byte)Match.Phase, Round = (byte)Match.RoundIndex,
            ScoreT = (byte)Match.ScoreT, ScoreCt = (byte)Match.ScoreCt, Viewer = viewer,
            Money = (ushort)(v.Team == TeamId.T ? Match.EcoT.Money : Match.EcoCt.Money),
            Visible = vis
        };
    }

    public bool CanSee(SimPlayer a, SimPlayer b)
    {
        var from = a.Pos + new Vector3(0, 1.55f, 0);
        var to = b.Pos + new Vector3(0, 1.4f, 0);
        return !Blocked(from, to);
    }
}
