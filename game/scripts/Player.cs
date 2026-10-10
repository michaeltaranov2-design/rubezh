using Godot;
using Rubezh.Core;

namespace Rubezh.Game;

public partial class Player : CharacterBody3D, IDamageable
{
    const string AMove = "move_forward", ABack = "move_back", ALeft = "move_left", ARight = "move_right";
    const string AJump = "jump", ACrouch = "crouch", AWalk = "walk", AFire = "fire", AReload = "reload";
    const string ASlot1 = "slot1", ASlot2 = "slot2";

    public int Team { get; private set; } = 0;
    public bool Alive => _hp > 0;
    public int Kills { get; private set; }
    public Camera3D Cam { get; private set; } = null!;
    public Hud Hud { get; set; } = null!;

    int _hp = 100;
    float _yaw, _pitch;
    float _punchPitch, _punchYaw;
    bool _jumpQueued, _crouched;
    int _slot;
    double _respawnAt;
    MoveState _move;
    readonly MovementSettings _cfg = new();
    WeaponState _rifle, _pistol;
    Node3D _vmRifle = null!, _vmPistol = null!;
    AnimationPlayer? _animRifle, _animPistol;
    HitboxRig _hit = null!;
    CollisionShape3D _bodyShape = null!;
    CapsuleShape3D _standCap = null!, _crouchCap = null!;
    Camera3D _cam = null!;

    public override void _Ready()
    {
        CollisionLayer = Layers.Body;
        CollisionMask = Layers.World | Layers.Body;
        FloorSnapLength = 0.2f;
        _standCap = new CapsuleShape3D { Radius = 0.4f, Height = 1.8f };
        _crouchCap = new CapsuleShape3D { Radius = 0.4f, Height = 1.2f };
        _bodyShape = new CollisionShape3D { Shape = _standCap };
        _bodyShape.Position = new Vector3(0, 0.9f, 0);
        AddChild(_bodyShape);
        _cam = new Camera3D { Fov = GameSettings.Fov };
        _cam.Position = new Vector3(0, 1.62f, 0);
        AddChild(_cam);
        _cam.MakeCurrent();
        Cam = _cam;
        _hit = new HitboxRig(this, this);
        _rifle = WeaponCore.Create(WeaponCatalog.Rifle, 11);
        _pistol = WeaponCore.Create(WeaponCatalog.Pistol, 13);
        var vm = new Node3D { Name = "View" };
        _cam.AddChild(vm);
        vm.Position = new Vector3(0.18f, -0.16f, -0.32f);
        _vmRifle = Viewmodel.Load("rifle_gran", false);
        _vmPistol = Viewmodel.Load("pistol_klyn", true);
        vm.AddChild(_vmRifle);
        vm.AddChild(_vmPistol);
        _animRifle = Viewmodel.FindAnim(_vmRifle);
        _animPistol = Viewmodel.FindAnim(_vmPistol);
        SelectSlot(0);
        if (!DisplayServer.WindowGetMode().ToString().Contains("headless", System.StringComparison.OrdinalIgnoreCase))
            Input.MouseMode = Input.MouseModeEnum.Captured;
    }

    public void SpawnAt(System.Numerics.Vector3 p, float yaw) => Place(Conv.G(p), yaw);

    public void Place(Godot.Vector3 p, float yaw)
    {
        GlobalPosition = p;
        _yaw = yaw;
        Rotation = new Vector3(0, _yaw, 0);
    }

    public void ApplyDamage(int amount, HitZone zone, Node3D? from)
    {
        if (_hp <= 0) return;
        _hp = Mathf.Max(0, _hp - amount);
        if (_hp == 0)
        {
            _hit.SetEnabled(false);
            _respawnAt = Time.GetTicksMsec() / 1000.0 + 2.0;
            Hud?.SetDead(true);
        }
    }

    public void AddKill() => Kills++;

    public override void _UnhandledInput(InputEvent e)
    {
        if (e is InputEventMouseMotion m && Input.MouseMode == Input.MouseModeEnum.Captured)
        {
            _yaw -= m.Relative.X * GameSettings.Sensitivity * 0.0022f;
            _pitch = Mathf.Clamp(_pitch - m.Relative.Y * GameSettings.Sensitivity * 0.0022f, -1.2f, 1.2f);
        }
        if (e.IsActionPressed(AJump)) _jumpQueued = true;
        if (e is InputEventKey k && k.Pressed && k.Keycode == Key.Escape)
            Input.MouseMode = Input.MouseModeEnum.Visible;
        if (e is InputEventMouseButton mb && mb.Pressed && Input.MouseMode != Input.MouseModeEnum.Captured)
            Input.MouseMode = Input.MouseModeEnum.Captured;
    }

    public override void _Process(double delta)
    {
        Rotation = new Vector3(0, _yaw, 0);
        _cam.Rotation = new Vector3(_pitch + _punchPitch, 0, 0);
        _punchPitch = Mathf.MoveToward(_punchPitch, 0, (float)delta * 8f);
        _punchYaw = Mathf.MoveToward(_punchYaw, 0, (float)delta * 8f);
        TickHud();
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        double now = Time.GetTicksMsec() / 1000.0;
        if (_hp <= 0)
        {
            if (now >= _respawnAt) Respawn();
            return;
        }
        bool wantCrouch = Input.IsActionPressed(ACrouch);
        if (_crouched && !wantCrouch && !CanStand()) wantCrouch = true;
        if (wantCrouch != _crouched) SetCrouch(wantCrouch);
        var input = new MoveInput
        {
            Forward = Input.GetActionStrength(AMove) - Input.GetActionStrength(ABack),
            Right = Input.GetActionStrength(ARight) - Input.GetActionStrength(ALeft),
            Yaw = _yaw,
            JumpPressed = _jumpQueued,
            Crouch = _crouched,
            Walk = Input.IsActionPressed(AWalk)
        };
        _jumpQueued = false;
        _move.OnGround = IsOnFloor();
        _move.Velocity = Conv.N(Velocity);
        MovementCore.Step(ref _move, input, _cfg, dt);
        Velocity = Conv.G(_move.Velocity);
        MoveAndSlide();
        _move.Velocity = Conv.N(Velocity);
        _move.OnGround = IsOnFloor();
        TickWeapons(now);
    }

    void SetCrouch(bool on)
    {
        _crouched = on;
        _bodyShape.Shape = on ? _crouchCap : _standCap;
        _bodyShape.Position = new Vector3(0, on ? 0.6f : 0.9f, 0);
        _cam.Position = new Vector3(0, on ? 1.05f : 1.62f, 0);
        _hit.SetCrouch(on);
    }

    bool CanStand()
    {
        var q = new PhysicsShapeQueryParameters3D
        {
            Shape = _standCap,
            Transform = new Transform3D(Basis.Identity, GlobalPosition + new Vector3(0, 0.9f, 0)),
            CollisionMask = Layers.World,
            Exclude = new Godot.Collections.Array<Rid> { GetRid() }
        };
        return GetWorld3D().DirectSpaceState.IntersectShape(q, 1).Count == 0;
    }

    void TickWeapons(double now)
    {
        if (Input.IsActionJustPressed(ASlot1)) SelectSlot(0);
        if (Input.IsActionJustPressed(ASlot2)) SelectSlot(1);
        var def = _slot == 0 ? WeaponCatalog.Rifle : WeaponCatalog.Pistol;
        ref WeaponState st = ref _slot == 0 ? ref _rifle : ref _pistol;
        WeaponCore.Update(ref st, def, now);
        if (Input.IsActionJustPressed(AReload)) WeaponCore.StartReload(ref st, def, now);
        bool fire = Input.IsActionPressed(AFire);
        float speed = MovementCore.HorizontalSpeed(_move.Velocity);
        if (WeaponCore.TryFire(ref st, def, now, fire, speed, _move.OnGround, out var shot))
        {
            _punchPitch += Mathf.DegToRad(shot.KickPitchDeg);
            var dir = Aim.ApplyKick(Aim.Forward(_yaw, _pitch), shot.SpreadPitchDeg + shot.KickPitchDeg, shot.SpreadYawDeg + shot.KickYawDeg);
            var eye = GlobalPosition + new Vector3(0, _crouched ? 1.05f : 1.62f, 0);
            var info = Combat.Fire(GetWorld3D(), this, Team, def, eye, dir, GetRid());
            if (info.Kill) AddKill();
            PlayClip("fire");
        }
        if (st.Ammo == 0 && !st.Reloading) WeaponCore.StartReload(ref st, def, now);
    }

    void SelectSlot(int slot)
    {
        _slot = slot;
        _vmRifle.Visible = slot == 0;
        _vmPistol.Visible = slot == 1;
        PlayClip("idle");
    }

    void PlayClip(string name)
    {
        var ap = _slot == 0 ? _animRifle : _animPistol;
        if (ap == null || !ap.HasAnimation(name)) return;
        ap.Play(name);
        if (name != "idle" && ap.HasAnimation("idle")) ap.Queue("idle");
    }

    void TickHud()
    {
        if (Hud == null) return;
        var def = _slot == 0 ? WeaponCatalog.Rifle : WeaponCatalog.Pistol;
        var st = _slot == 0 ? _rifle : _pistol;
        Hud.SetStatus(def.DisplayName, st.Ammo, def.MagazineSize, st.Reloading, _hp, Kills);
    }

    void Respawn()
    {
        _hp = 100;
        _move = default;
        Velocity = Vector3.Zero;
        _rifle = WeaponCore.Create(WeaponCatalog.Rifle, 11);
        _pistol = WeaponCore.Create(WeaponCatalog.Pistol, 13);
        _hit.SetEnabled(true);
        SetCrouch(false);
        SpawnAt(MapUzel.TSpawns[0], 0f);
        Hud?.SetDead(false);
    }
}
