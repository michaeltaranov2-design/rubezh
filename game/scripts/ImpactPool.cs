using Godot;

namespace Rubezh.Game;

public partial class ImpactPool : Node3D
{
    const int Cap = 24;
    readonly MeshInstance3D[] _items = new MeshInstance3D[Cap];
    readonly double[] _until = new double[Cap];
    int _next;
    StandardMaterial3D _mat = null!;

    public override void _Ready()
    {
        _mat = new StandardMaterial3D { AlbedoColor = new Color(1f, 0.85f, 0.3f), EmissionEnabled = true, Emission = new Color(1f, 0.7f, 0.2f), EmissionEnergyMultiplier = 2f };
        var mesh = new SphereMesh { Radius = 0.04f, Height = 0.08f, RadialSegments = 8, Rings = 4 };
        for (int i = 0; i < Cap; i++)
        {
            var mi = new MeshInstance3D { Mesh = mesh, MaterialOverride = _mat, Visible = false };
            mi.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
            AddChild(mi);
            _items[i] = mi;
        }
    }

    public void Spawn(Vector3 pos)
    {
        var mi = _items[_next];
        mi.GlobalPosition = pos;
        mi.Visible = true;
        _until[_next] = Time.GetTicksMsec() / 1000.0 + 0.12;
        _next = (_next + 1) % Cap;
    }

    public override void _Process(double delta)
    {
        double now = Time.GetTicksMsec() / 1000.0;
        for (int i = 0; i < Cap; i++)
            if (_items[i].Visible && now >= _until[i]) _items[i].Visible = false;
    }
}
