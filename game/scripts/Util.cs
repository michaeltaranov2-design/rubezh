using Godot;
using Rubezh.Core;

namespace Rubezh.Game;

/// <summary>Слои столкновений. Луч стрельбы: World | Hitbox. Тело не участвует в попаданиях.</summary>
public static class Layers
{
    public const uint World = 1u;
    public const uint Body = 2u;
    public const uint Hitbox = 4u;
    public const uint FireMask = World | Hitbox;
}

public static class Conv
{
    public static Godot.Vector3 G(System.Numerics.Vector3 v) => new(v.X, v.Y, v.Z);
    public static System.Numerics.Vector3 N(Godot.Vector3 v) => new(v.X, v.Y, v.Z);
}

public static class Aim
{
    public static Godot.Vector3 Forward(float yaw, float pitch)
    {
        float cy = Mathf.Cos(yaw), sy = Mathf.Sin(yaw);
        float cp = Mathf.Cos(pitch), sp = Mathf.Sin(pitch);
        return new Godot.Vector3(-sy * cp, sp, -cy * cp);
    }

    public static Godot.Vector3 ApplyKick(Godot.Vector3 dir, float pitchDeg, float yawDeg)
    {
        Godot.Vector3 r = dir.Cross(Godot.Vector3.Up);
        if (r.LengthSquared() < 1e-6f) r = Godot.Vector3.Right;
        else r = r.Normalized();
        Godot.Vector3 u = r.Cross(dir).Normalized();
        return (dir + u * Mathf.DegToRad(pitchDeg) + r * Mathf.DegToRad(yawDeg)).Normalized();
    }
}

public static class Viewmodel
{
    public static Node3D Load(string id, bool pistol)
    {
        string path = "res://assets/weapons/" + id + "_LOD0.glb";
        if (ResourceLoader.Exists(path))
        {
            var packed = ResourceLoader.Load<PackedScene>(path);
            if (packed != null)
            {
                var n = packed.Instantiate<Node3D>();
                n.Position = new Godot.Vector3(0.18f, -0.22f, -0.42f);
                n.RotationDegrees = new Godot.Vector3(2f, 180f, 0f);
                DisableShadows(n);
                return n;
            }
        }
        var box = new MeshInstance3D();
        box.Mesh = new BoxMesh { Size = pistol ? new Godot.Vector3(0.05f, 0.08f, 0.22f) : new Godot.Vector3(0.06f, 0.08f, 0.5f) };
        box.Position = new Godot.Vector3(0.18f, -0.22f, -0.42f);
        box.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        return box;
    }

    public static AnimationPlayer? FindAnim(Node n)
    {
        if (n is AnimationPlayer ap) return ap;
        foreach (Node c in n.GetChildren())
        {
            var f = FindAnim(c);
            if (f != null) return f;
        }
        return null;
    }

    static void DisableShadows(Node n)
    {
        if (n is GeometryInstance3D g) g.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        foreach (Node c in n.GetChildren()) DisableShadows(c);
    }
}
