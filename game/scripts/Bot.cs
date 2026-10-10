using System;
using Godot;
using Rubezh.Core;

namespace Rubezh.Game;

public partial class Bot : CharacterBody3D, IDamageable
{
    public int Team { get; private set; }
    public bool Alive => _hp > 0;
    public bool AiEnabled = true;
    public Player? Target;

    int _hp = 100;
    float _yaw, _pitch;
    Godot.Vector3 _spawn;
    double _respawnAt, _nextThink, _seeSince, _stuckFor;
    bool _sees;
    int _wp = -1, _prevWp = -1;
    uint _rng;
    MoveState _move;
    readonly MovementSettings _cfg = new();
    WeaponState _wpn;
    HitboxRig _rig = null!;
    MeshInstance3D _body = null!;
    const float ViewCos = 0.55f, React = 0.22f, Turn = 2.4f, Range = 42f;

    public override void _Ready()
    {
        CollisionLayer = Layers.Body;
        CollisionMask = Layers.World | Layers.Body;
        FloorSnapLength = 0.2f;
        var cap = new CapsuleShape3D { Radius = 0.4f, Height = 1.8f };
        AddChild(new CollisionShape3D { Shape = cap, Position = new Godot.Vector3(0, 0.9f, 0) });
        _body = new MeshInstance3D { Mesh = new CapsuleMesh { Radius = 0.4f, Height = 1.8f } };
        _body.Position = new Godot.Vector3(0, 0.9f, 0);
        _body.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        AddChild(_body);
        _rig = new HitboxRig(this, this);
    }

    public void Init(int team, Godot.Vector3 spawn, float yaw, Player target, uint seed)
    {
        Team = team;
        _spawn = spawn;
        _yaw = yaw;
        Target = target;
        _rng = seed == 0 ? 0x9E3779B9u : seed;
        var mat = new StandardMaterial3D { AlbedoColor = team == 1 ? new Color(0.25f, 0.4f, 0.75f) : new Color(0.75f, 0.45f, 0.2f) };
        _body.MaterialOverride = mat;
        Respawn();
    }

    public void ApplyDamage(int amount, HitZone zone, Node3D? from)
    {
        if (!Alive) return;
        _hp -= amount;
        if (_hp > 0) return;
        _hp = 0;
        Visible = false;
        CollisionLayer = 0;
        _rig.SetEnabled(false);
        _respawnAt = Time.GetTicksMsec() / 1000.0 + 3.0;
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        if (!Alive)
        {
            if (Time.GetTicksMsec() / 1000.0 >= _respawnAt) Respawn();
            return;
        }
        var input = new MoveInput { Yaw = _yaw };
        if (AiEnabled) Think(ref input, dt);
        _move.OnGround = IsOnFloor();
        MovementCore.Step(ref _move, input, _cfg, dt);
        Velocity = Conv.G(_move.Velocity);
        MoveAndSlide();
        _move.Velocity = Conv.N(Velocity);
        Rotation = new Godot.Vector3(0, _yaw, 0);
        HandleFire();
    }

    void Think(ref MoveInput input, float dt)
    {
        double now = Time.GetTicksMsec() / 1000.0;
        if (now >= _nextThink)
        {
            _nextThink = now + 0.1;
            _sees = CanSeeTarget();
            if (_sees && _seeSince <= 0) _seeSince = now;
            if (!_sees) _seeSince = 0;
        }
        if (_sees && Target != null && Target.Alive)
        {
            Godot.Vector3 to = Target.GlobalPosition + new Godot.Vector3(0, 1.4f, 0) - (GlobalPosition + new Godot.Vector3(0, 1.55f, 0));
            float wantYaw = Mathf.Atan2(-to.X, -to.Z);
            _yaw = Mathf.LerpAngle(_yaw, wantYaw, MathF.Min(1f, Turn * dt));
            float horiz = new Godot.Vector2(to.X, to.Z).Length();
            _pitch = Mathf.Clamp(Mathf.Atan2(to.Y, horiz), -1.2f, 1.2f);
            input.Yaw = _yaw;
            if (horiz > 8f) input.Forward = 1f;
            return;
        }
        Patrol(ref input, dt);
    }

    void Patrol(ref MoveInput input, float dt)
    {
        var map = MapRuntime.Current;
        if (map.Waypoints.Length == 0) return;
        if (_wp < 0 || _wp >= map.Waypoints.Length) _wp = NearestWp();
        var wp = Conv.G(map.Waypoints[_wp]);
        Godot.Vector3 d = wp - GlobalPosition; d.Y = 0;
        if (d.Length() < 1.2f) PickNext();
        else
        {
            float want = Mathf.Atan2(-d.X, -d.Z);
            _yaw = Mathf.LerpAngle(_yaw, want, MathF.Min(1f, Turn * dt));
            _pitch = Mathf.MoveToward(_pitch, 0, dt);
            input.Yaw = _yaw;
            input.Forward = 1f;
        }
        float hs = new Godot.Vector2(Velocity.X, Velocity.Z).Length();
        _stuckFor = hs < 0.15f ? _stuckFor + dt : 0;
        if (_stuckFor > 1.5f) { _stuckFor = 0; PickNext(); }
    }

    void HandleFire()
    {
        if (!_sees || Target == null || !Target.Alive) return;
        if (Time.GetTicksMsec() / 1000.0 - _seeSince < React) return;
        Godot.Vector3 eye = GlobalPosition + new Godot.Vector3(0, 1.55f, 0);
        Godot.Vector3 dir = Aim.Forward(_yaw, _pitch);
        double now = Time.GetTicksMsec() / 1000.0;
        WeaponCore.Update(ref _wpn, WeaponCatalog.Rifle, now);
        if (!WeaponCore.TryFire(ref _wpn, WeaponCatalog.Rifle, now, true, MovementCore.HorizontalSpeed(_move.Velocity), _move.OnGround, out var shot))
        {
            if (_wpn.Ammo == 0) WeaponCore.StartReload(ref _wpn, WeaponCatalog.Rifle, now);
            return;
        }
        dir = Aim.ApplyKick(dir, shot.SpreadPitchDeg + shot.KickPitchDeg, shot.SpreadYawDeg + shot.KickYawDeg);
        Combat.Fire(GetWorld3D(), this, Team, WeaponCatalog.Rifle, eye, dir, GetRid());
    }

    bool CanSeeTarget()
    {
        if (Target == null || !Target.Alive) return false;
        Godot.Vector3 eye = GlobalPosition + new Godot.Vector3(0, 1.55f, 0);
        Godot.Vector3 to = Target.GlobalPosition + new Godot.Vector3(0, 1.4f, 0) - eye;
        if (to.Length() > Range) return false;
        Godot.Vector3 fwd = Aim.Forward(_yaw, 0);
        if (fwd.Dot(to.Normalized()) < ViewCos) return false;
        var q = PhysicsRayQueryParameters3D.Create(eye, eye + to);
        q.CollisionMask = Layers.World;
        q.Exclude = new Godot.Collections.Array<Rid> { GetRid() };
        return GetWorld3D().DirectSpaceState.IntersectRay(q).Count == 0;
    }

    int NearestWp()
    {
        int best = 0; float bestD = float.MaxValue;
        var p = Conv.N(GlobalPosition);
        var wps = MapRuntime.Current.Waypoints;
        for (int i = 0; i < wps.Length; i++)
        {
            float d = System.Numerics.Vector3.Distance(p, wps[i]);
            if (d < bestD) { bestD = d; best = i; }
        }
        return best;
    }

    void PickNext()
    {
        var n = MapRuntime.Current.Neighbors;
        if (_wp < 0 || _wp >= n.Length || n[_wp].Length == 0) return;
        var nb = n[_wp];
        int pick = nb[(int)(WeaponCore.NextFloat(ref _rng) * nb.Length) % nb.Length];
        if (nb.Length > 1 && pick == _prevWp) pick = nb[(pick + 1) % nb.Length];
        _prevWp = _wp; _wp = pick;
    }

    void Respawn()
    {
        _hp = 100;
        GlobalPosition = _spawn;
        Velocity = Godot.Vector3.Zero;
        _move = new MoveState { OnGround = true };
        _wpn = WeaponCore.Create(WeaponCatalog.Rifle, _rng ^ 0xA5A5A5A5u);
        Visible = true;
        CollisionLayer = Layers.Body;
        _rig.SetEnabled(true);
        _wp = NearestWp();
        _sees = false; _seeSince = 0;
    }
}
