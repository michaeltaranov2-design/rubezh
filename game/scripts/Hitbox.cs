using Godot;
using Rubezh.Core;

namespace Rubezh.Game;

public sealed partial class HitboxArea : Area3D
{
    public HitZone Zone;
    public IDamageable? Target;
}

public sealed class HitboxRig
{
    readonly HitboxArea[] _areas = new HitboxArea[4];
    readonly float[] _baseY = { 0.45f, 0.95f, 1.25f, 1.58f };

    public HitboxRig(Node3D parent, IDamageable target)
    {
        Add(parent, target, 0, HitZone.Legs, new BoxShape3D { Size = new Godot.Vector3(0.38f, 0.7f, 0.32f) });
        Add(parent, target, 1, HitZone.Stomach, new BoxShape3D { Size = new Godot.Vector3(0.4f, 0.28f, 0.28f) });
        Add(parent, target, 2, HitZone.Chest, new BoxShape3D { Size = new Godot.Vector3(0.42f, 0.34f, 0.3f) });
        Add(parent, target, 3, HitZone.Head, new SphereShape3D { Radius = 0.14f });
    }

    void Add(Node3D parent, IDamageable target, int i, HitZone zone, Shape3D shape)
    {
        var a = new HitboxArea { Zone = zone, Target = target, Monitoring = false, Monitorable = true };
        a.CollisionLayer = Layers.Hitbox;
        a.CollisionMask = 0;
        a.Position = new Godot.Vector3(0f, _baseY[i], 0f);
        a.AddChild(new CollisionShape3D { Shape = shape });
        parent.AddChild(a);
        _areas[i] = a;
    }

    public void SetCrouch(bool crouch)
    {
        float k = crouch ? 0.62f : 1f;
        for (int i = 0; i < _areas.Length; i++)
            _areas[i].Position = new Godot.Vector3(0f, _baseY[i] * k, 0f);
    }

    public void SetEnabled(bool on)
    {
        uint layer = on ? Layers.Hitbox : 0u;
        for (int i = 0; i < _areas.Length; i++) _areas[i].CollisionLayer = layer;
    }

    public Godot.Vector3 ChestWorld => _areas[2].GlobalPosition;
    public Godot.Vector3 HeadWorld => _areas[3].GlobalPosition;
}
