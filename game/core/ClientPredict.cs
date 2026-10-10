using System;
using System.Collections.Generic;
using System.Numerics;

namespace Rubezh.Core;

/// <summary>Клиентское предсказание: история ввода, откат к ack, повтор неподтверждённого.</summary>
public sealed class ClientPredict
{
    public Vector3 Pos;
    public MoveState Move;
    public float Yaw;
    public uint Seq;
    readonly InputCommand[] _hist = new InputCommand[64];
    int _n;
    readonly MovementSettings _cfg = new();

    public InputCommand Make(byte flags, float yaw, byte slot = 0)
    {
        Seq++;
        var c = new InputCommand { Seq = Seq, Slot = slot, Flags = flags, Yaw = yaw };
        _hist[_n++ % 64] = c;
        if (_n > 64) _n = 64 + (_n % 64);
        Apply(c);
        return c;
    }

    public void Reconcile(in SnapPlayer auth, uint ack)
    {
        Pos = new Vector3(auth.X, auth.Y, auth.Z);
        Yaw = auth.Yaw;
        Move.OnGround = (auth.Flags & 2) != 0;
        Move.Velocity = default;
        int count = Math.Min(_n, 64);
        for (int i = 0; i < count; i++)
        {
            var c = _hist[i];
            if (c.Seq <= ack) continue;
            Apply(c);
        }
    }

    void Apply(in InputCommand c)
    {
        var inp = c.ToMove();
        MovementCore.Step(ref Move, inp, _cfg, NetProto.TickDt);
        Pos += Move.Velocity * NetProto.TickDt;
        if (Pos.Y <= 0.05f) { Pos.Y = 0.05f; Move.OnGround = true; Move.Velocity.Y = 0; }
        Yaw = c.Yaw;
    }
}

public struct NetMsg
{
    public byte[] Data;
    public int Delay;
}

/// <summary>Канал-эмулятор: loss, reorder, duplicates. Только для тестов.</summary>
public sealed class NetChannel
{
    public float Loss, Dup, Reorder;
    readonly List<NetMsg> _q = new();
    uint _rng;

    public NetChannel(uint seed = 1) { _rng = seed == 0 ? 1u : seed; }

    public void Send(ReadOnlySpan<byte> d)
    {
        if (Next() < Loss) return;
        var copy = d.ToArray();
        _q.Add(new NetMsg { Data = copy, Delay = Next() < Reorder ? 1 : 0 });
        if (Next() < Dup) _q.Add(new NetMsg { Data = copy, Delay = 0 });
    }

    public int Recv(Span<byte> dst)
    {
        for (int i = 0; i < _q.Count; i++)
        {
            var m = _q[i];
            if (m.Delay > 0) { m.Delay--; _q[i] = m; continue; }
            int n = Math.Min(dst.Length, m.Data.Length);
            m.Data.AsSpan(0, n).CopyTo(dst);
            _q.RemoveAt(i);
            return n;
        }
        return 0;
    }

    float Next()
    {
        _rng ^= _rng << 13; _rng ^= _rng >> 17; _rng ^= _rng << 5;
        return (_rng & 0xFFFFFF) / 16777216f;
    }
}
