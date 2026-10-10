using System;
using System.Buffers.Binary;

namespace Rubezh.Core;

public static class NetProto
{
    public const uint Magic = 0x31485A52; // RZH1
    public const int Mtu = 1200;
    public const int TickHz = 64;
    public const float TickDt = 1f / TickHz;
    public const byte Input = 1, Snapshot = 2, Event = 3, Console = 4;
}

public struct InputCommand
{
    public uint Seq, Tick;
    public byte Slot, Flags, WeaponId, Use;
    public float Yaw, Pitch;
    public uint Nonce;

    public bool Forward => (Flags & 1) != 0;
    public bool Back => (Flags & 2) != 0;
    public bool Left => (Flags & 4) != 0;
    public bool Right => (Flags & 8) != 0;
    public bool Jump => (Flags & 16) != 0;
    public bool Crouch => (Flags & 32) != 0;
    public bool Walk => (Flags & 64) != 0;
    public bool Fire => (Flags & 128) != 0;

    public int Write(Span<byte> d)
    {
        if (d.Length < 28) return 0;
        BinaryPrimitives.WriteUInt32LittleEndian(d, NetProto.Magic);
        d[4] = NetProto.Input;
        BinaryPrimitives.WriteUInt16LittleEndian(d[5..], 28);
        BinaryPrimitives.WriteUInt32LittleEndian(d[7..], Seq);
        BinaryPrimitives.WriteUInt32LittleEndian(d[11..], Tick);
        d[15] = Slot; d[16] = Flags; d[17] = WeaponId; d[18] = Use;
        BinaryPrimitives.WriteSingleLittleEndian(d[19..], Yaw);
        BinaryPrimitives.WriteSingleLittleEndian(d[23..], Pitch);
        d[27] = (byte)(Nonce & 255);
        return 28;
    }

    public static bool TryRead(ReadOnlySpan<byte> d, out InputCommand c)
    {
        c = default;
        if (d.Length < 28 || BinaryPrimitives.ReadUInt32LittleEndian(d) != NetProto.Magic || d[4] != NetProto.Input) return false;
        if (BinaryPrimitives.ReadUInt16LittleEndian(d[5..]) < 28) return false;
        c.Seq = BinaryPrimitives.ReadUInt32LittleEndian(d[7..]);
        c.Tick = BinaryPrimitives.ReadUInt32LittleEndian(d[11..]);
        c.Slot = d[15]; c.Flags = d[16]; c.WeaponId = d[17]; c.Use = d[18];
        c.Yaw = BinaryPrimitives.ReadSingleLittleEndian(d[19..]);
        c.Pitch = BinaryPrimitives.ReadSingleLittleEndian(d[23..]);
        c.Nonce = d[27];
        if (float.IsNaN(c.Yaw) || float.IsNaN(c.Pitch) || float.IsInfinity(c.Yaw) || float.IsInfinity(c.Pitch)) return false;
        return true;
    }

    public MoveInput ToMove()
    {
        float f = 0, r = 0;
        if (Forward) f += 1f; if (Back) f -= 1f;
        if (Right) r += 1f; if (Left) r -= 1f;
        return new MoveInput { Forward = f, Right = r, Yaw = Yaw, JumpPressed = Jump, Crouch = Crouch, Walk = Walk };
    }
}

public struct SnapPlayer
{
    public byte Slot, Team, Hp, Flags;
    public float X, Y, Z, Yaw;
}

/// <summary>Персональный снимок: скрытые противники в payload не входят.</summary>
public sealed class Snapshot
{
    public uint Tick, AckSeq;
    public byte Phase, Round, ScoreT, ScoreCt, Viewer;
    public ushort Money;
    public SnapPlayer[] Visible = Array.Empty<SnapPlayer>();

    public int Write(Span<byte> d)
    {
        int n = Visible.Length;
        int need = 22 + n * 20;
        if (d.Length < need || need > NetProto.Mtu) return 0;
        BinaryPrimitives.WriteUInt32LittleEndian(d, NetProto.Magic);
        d[4] = NetProto.Snapshot;
        BinaryPrimitives.WriteUInt16LittleEndian(d[5..], (ushort)need);
        BinaryPrimitives.WriteUInt32LittleEndian(d[7..], Tick);
        BinaryPrimitives.WriteUInt32LittleEndian(d[11..], AckSeq);
        d[15] = Phase; d[16] = Round; d[17] = ScoreT; d[18] = ScoreCt; d[19] = Viewer;
        BinaryPrimitives.WriteUInt16LittleEndian(d[20..], Money);
        int o = 22;
        for (int i = 0; i < n; i++)
        {
            var p = Visible[i];
            d[o] = p.Slot; d[o+1] = p.Team; d[o+2] = p.Hp; d[o+3] = p.Flags;
            BinaryPrimitives.WriteSingleLittleEndian(d[(o+4)..], p.X);
            BinaryPrimitives.WriteSingleLittleEndian(d[(o+8)..], p.Y);
            BinaryPrimitives.WriteSingleLittleEndian(d[(o+12)..], p.Z);
            BinaryPrimitives.WriteSingleLittleEndian(d[(o+16)..], p.Yaw);
            o += 20;
        }
        return need;
    }

    public static bool TryRead(ReadOnlySpan<byte> d, Snapshot s)
    {
        if (d.Length < 22 || BinaryPrimitives.ReadUInt32LittleEndian(d) != NetProto.Magic || d[4] != NetProto.Snapshot) return false;
        int need = BinaryPrimitives.ReadUInt16LittleEndian(d[5..]);
        if (need > d.Length || need > NetProto.Mtu) return false;
        s.Tick = BinaryPrimitives.ReadUInt32LittleEndian(d[7..]);
        s.AckSeq = BinaryPrimitives.ReadUInt32LittleEndian(d[11..]);
        s.Phase = d[15]; s.Round = d[16]; s.ScoreT = d[17]; s.ScoreCt = d[18]; s.Viewer = d[19];
        s.Money = BinaryPrimitives.ReadUInt16LittleEndian(d[20..]);
        int n = (need - 22) / 20;
        if (s.Visible.Length != n) s.Visible = new SnapPlayer[n];
        int o = 22;
        for (int i = 0; i < n; i++)
        {
            s.Visible[i] = new SnapPlayer {
                Slot = d[o], Team = d[o+1], Hp = d[o+2], Flags = d[o+3],
                X = BinaryPrimitives.ReadSingleLittleEndian(d[(o+4)..]),
                Y = BinaryPrimitives.ReadSingleLittleEndian(d[(o+8)..]),
                Z = BinaryPrimitives.ReadSingleLittleEndian(d[(o+12)..]),
                Yaw = BinaryPrimitives.ReadSingleLittleEndian(d[(o+16)..])
            };
            o += 20;
        }
        return true;
    }
}
