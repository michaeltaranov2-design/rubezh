using System;
using Godot;
using Rubezh.Core;

namespace Rubezh.Game;

/// <summary>ENet-хост поверх RZH1. Сервер: Listen. Клиент: Connect. Lobby-токен не даёт admin.</summary>
public partial class NetHost : Node
{
    public DedicatedLoop Loop = null!;
    public bool IsServer;
    public int LocalSlot = -1;
    public string Token = "";
    public ClientPredict Predict = new();
    ENetMultiplayerPeer? _peer;
    readonly int[] _peerSlot = new int[64];
    readonly byte[] _buf = new byte[NetProto.Mtu];
    bool _authed;

    public Error Listen(int port = DedicatedLoop.GamePort)
    {
        Loop ??= new DedicatedLoop(MapRuntime.Current);
        IsServer = true;
        for (int i = 0; i < _peerSlot.Length; i++) _peerSlot[i] = -1;
        _peer = new ENetMultiplayerPeer();
        var e = _peer.CreateServer(port, 16);
        if (e != Error.Ok) return e;
        Multiplayer.MultiplayerPeer = _peer;
        return Error.Ok;
    }

    public Error Connect(string host, int port = DedicatedLoop.GamePort)
    {
        IsServer = false;
        _peer = new ENetMultiplayerPeer();
        var e = _peer.CreateClient(host, port);
        if (e != Error.Ok) return e;
        Multiplayer.MultiplayerPeer = _peer;
        return Error.Ok;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_peer == null) return;
        Pump();
        if (IsServer) Loop.TickAccum((float)delta);
        if (IsServer) SendSnaps();
        else SendLocalInput();
    }

    void Pump()
    {
        if (_peer == null) return;
        while (_peer.GetAvailablePacketCount() > 0)
        {
            int from = _peer.GetPacketPeer();
            var pkt = _peer.GetPacket();
            Handle(from, pkt);
        }
        if (!IsServer && !_authed && _peer.GetConnectionStatus() == MultiplayerPeer.ConnectionStatus.Connected && Token.Length > 0)
            SendAuth();
    }

    void Handle(int from, byte[] pkt)
    {
        if (pkt == null || pkt.Length < 5) return;
        byte t = pkt[4];
        if (IsServer)
        {
            if (t == NetAuth.Type && NetAuth.TryRead(pkt, out var tok))
            {
                var slot = Loop.Authorize(tok);
                if (slot == null || from < 0 || from >= _peerSlot.Length) return;
                _peerSlot[from] = slot.Value;
                return;
            }
            if (t == NetProto.Input && InputCommand.TryRead(pkt, out var cmd))
            {
                int slot = from < _peerSlot.Length ? _peerSlot[from] : -1;
                if (slot >= 0) Loop.PushInput(slot, cmd);
            }
            return;
        }
        if (t == NetProto.Snapshot)
        {
            var snap = new Snapshot();
            if (!Snapshot.TryRead(pkt, snap)) return;
            _authed = true;
            LocalSlot = snap.Viewer;
            foreach (var p in snap.Visible)
                if (p.Slot == LocalSlot) { Predict.Reconcile(p, snap.AckSeq); break; }
        }
    }

    void SendAuth()
    {
        int n = NetAuth.Write(_buf, Token);
        if (n > 0) { _peer!.SetTargetPeer(1); _peer.PutPacket(_buf.AsSpan(0, n).ToArray()); _authed = true; }
    }

    void SendLocalInput()
    {
        if (LocalSlot < 0 || _peer == null) return;
        byte flags = 0;
        if (Input.IsActionPressed("move_forward")) flags |= 1;
        if (Input.IsActionPressed("move_back")) flags |= 2;
        if (Input.IsActionPressed("move_left")) flags |= 4;
        if (Input.IsActionPressed("move_right")) flags |= 8;
        if (Input.IsActionPressed("jump")) flags |= 16;
        if (Input.IsActionPressed("crouch")) flags |= 32;
        if (Input.IsActionPressed("walk")) flags |= 64;
        if (Input.IsActionPressed("fire")) flags |= 128;
        var cmd = Predict.Make(flags, Predict.Yaw, (byte)LocalSlot);
        int n = cmd.Write(_buf);
        if (n > 0) { _peer.SetTargetPeer(1); _peer.PutPacket(_buf.AsSpan(0, n).ToArray()); }
    }

    void SendSnaps()
    {
        if (_peer == null) return;
        for (int peer = 2; peer < _peerSlot.Length; peer++)
        {
            int slot = _peerSlot[peer];
            if (slot < 0) continue;
            var snap = Loop.Snapshot((byte)slot);
            int n = snap.Write(_buf);
            if (n <= 0) continue;
            _peer.SetTargetPeer(peer);
            _peer.PutPacket(_buf.AsSpan(0, n).ToArray());
        }
    }
}
