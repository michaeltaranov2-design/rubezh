using System;
using System.Buffers.Binary;
using System.Text;

namespace Rubezh.Core;

/// <summary>Dedicated-цикл: аккаунт → слот, ввод без подмены slot, тик 64 Гц.</summary>
public sealed class DedicatedLoop
{
    public const int GamePort = 24567;
    public readonly SimServer Sim;
    public readonly AccountServer Accounts;
    float _acc;
    readonly bool[] _bound = new bool[10];

    public DedicatedLoop(MapDef? map = null)
    {
        Sim = new SimServer(map);
        Accounts = new AccountServer();
    }

    public int? Authorize(string token)
    {
        var slot = Accounts.Join(token);
        if (slot == null) return null;
        var s = Accounts.Validate(token);
        if (s == null) return null;
        Sim.Players[slot.Value].AccountId = s.Value.AccountId;
        _bound[slot.Value] = true;
        return slot;
    }

    public bool PushInput(int slot, in InputCommand cmd)
    {
        if ((uint)slot >= 10 || !_bound[slot]) return false;
        if (cmd.Slot != (byte)slot) return false;
        Sim.Enqueue(cmd);
        return true;
    }

    public int TickAccum(float dt)
    {
        if (dt < 0f) dt = 0f;
        if (dt > 0.25f) dt = 0.25f;
        _acc += dt;
        int n = 0;
        while (_acc >= NetProto.TickDt && n < 8)
        {
            Sim.Step();
            _acc -= NetProto.TickDt;
            n++;
        }
        return n;
    }

    public Snapshot Snapshot(byte slot) => Sim.BuildSnapshot(slot);
}

public static class NetAuth
{
    public const byte Type = 5;
    public static int Write(Span<byte> d, string token)
    {
        var t = Encoding.UTF8.GetBytes(token ?? "");
        int n = 7 + t.Length;
        if (d.Length < n || n > NetProto.Mtu) return 0;
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(d, NetProto.Magic);
        d[4] = Type;
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(d[5..], (ushort)n);
        t.CopyTo(d[7..]);
        return n;
    }
    public static bool TryRead(ReadOnlySpan<byte> d, out string token)
    {
        token = "";
        if (d.Length < 7 || System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(d) != NetProto.Magic || d[4] != Type) return false;
        int n = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(d[5..]);
        if (n > d.Length || n < 7) return false;
        token = Encoding.UTF8.GetString(d.Slice(7, n - 7));
        return token.Length > 0;
    }
}
