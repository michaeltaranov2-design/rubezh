using Godot;
using Rubezh.Core;

namespace Rubezh.Game;

public static class MapBuilder
{
    static StandardMaterial3D[]? _mats;

    public static Node3D Build(Node parent)
    {
        var root = new Node3D { Name = "MapUzel" };
        parent.AddChild(root);
        var mats = Mats();
        foreach (var box in MapUzel.Boxes)
        {
            var mesh = new BoxMesh { Size = Conv.G(box.Size) };
            var mi = new MeshInstance3D { Mesh = mesh, MaterialOverride = mats[box.Material] };
            mi.Position = Conv.G(box.Center);
            mi.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
            root.AddChild(mi);
            if (!box.Solid) continue;
            var body = new StaticBody3D();
            body.CollisionLayer = Layers.World;
            body.CollisionMask = 0;
            var col = new CollisionShape3D { Shape = new BoxShape3D { Size = Conv.G(box.Size) } };
            body.AddChild(col);
            body.Position = Conv.G(box.Center);
            root.AddChild(body);
        }
        AddSiteLabel(root, new Vector3(20, 2.4f, 0), "A");
        AddSiteLabel(root, new Vector3(-20, 2.4f, 0), "B");
        AddLight(root);
        AddWorldEnv(root);
        return root;
    }

    static void AddSiteLabel(Node3D root, Vector3 pos, string text)
    {
        var l = new Label3D { Text = text, FontSize = 64, PixelSize = 0.02f, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled };
        l.Modulate = text == "A" ? new Color(0.95f, 0.55f, 0.2f) : new Color(0.3f, 0.55f, 0.95f);
        l.Position = pos;
        root.AddChild(l);
    }

    static void AddLight(Node3D root)
    {
        var sun = new DirectionalLight3D { Name = "Sun" };
        sun.RotationDegrees = new Vector3(-50, 35, 0);
        sun.LightEnergy = 0.9f;
        sun.ShadowEnabled = GameSettings.Quality != "low";
        sun.DirectionalShadowMaxDistance = 40f;
        root.AddChild(sun);
    }

    static void AddWorldEnv(Node3D root)
    {
        var env = new Godot.Environment();
        env.BackgroundMode = Godot.Environment.BGMode.Color;
        env.BackgroundColor = new Color(0.55f, 0.63f, 0.72f);
        env.AmbientLightSource = Godot.Environment.AmbientSource.Color;
        env.AmbientLightColor = new Color(0.62f, 0.66f, 0.72f);
        env.AmbientLightEnergy = 0.45f;
        var we = new WorldEnvironment { Environment = env };
        root.AddChild(we);
    }

    static StandardMaterial3D[] Mats()
    {
        if (_mats != null) return _mats;
        var tex = Checker();
        Color[] cols =
        {
            new(0.42f, 0.44f, 0.4f),
            new(0.55f, 0.52f, 0.46f),
            new(0.62f, 0.48f, 0.28f),
            new(0.85f, 0.45f, 0.18f),
            new(0.22f, 0.42f, 0.78f),
            new(0.32f, 0.33f, 0.34f)
        };
        _mats = new StandardMaterial3D[cols.Length];
        for (int i = 0; i < cols.Length; i++)
        {
            _mats[i] = new StandardMaterial3D
            {
                AlbedoColor = cols[i],
                AlbedoTexture = tex,
                Uv1Scale = new Vector3(2, 2, 2),
                Roughness = 0.85f
            };
        }
        return _mats;
    }

    static ImageTexture Checker()
    {
        var img = Image.CreateEmpty(8, 8, false, Image.Format.Rgb8);
        for (int y = 0; y < 8; y++)
            for (int x = 0; x < 8; x++)
                img.SetPixel(x, y, ((x + y) & 1) == 0 ? new Color(0.92f, 0.92f, 0.9f) : new Color(0.72f, 0.72f, 0.7f));
        return ImageTexture.CreateFromImage(img);
    }
}
