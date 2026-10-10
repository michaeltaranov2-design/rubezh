using Godot;
using Rubezh.Core;

namespace Rubezh.Game;

/// <summary>Сборка greybox-карты: меши, коллизии, окклюдеры, LOD, навмеш, LightmapGI.</summary>
public static class MapBuilder
{
    static StandardMaterial3D[]? _mats;
    public static int LastOccluders;
    public static int LastMeshes;

    public static Node3D Build(Node parent) => Build(parent, MapRuntime.Current);

    public static Node3D Build(Node parent, MapDef map)
    {
        string name = map.Id == MapUzel.Id ? "MapUzel" : map.Id == MapKlin.Id ? "MapKlin" : map.Id == MapHorda.Id ? "MapHorda" : "Map";
        var root = new Node3D { Name = name };
        parent.AddChild(root);
        var mats = Mats();
        var nav = new NavigationRegion3D { Name = "Nav" };
        nav.NavigationMesh = new NavigationMesh
        {
            AgentRadius = map.BotRadius,
            AgentHeight = 1.8f,
            AgentMaxClimb = 0.4f,
            CellSize = 0.2f,
            CellHeight = 0.2f,
            GeometryParsedGeometryType = NavigationMesh.ParsedGeometryType.StaticColliders,
            GeometrySourceGeometryMode = NavigationMesh.SourceGeometryMode.RootNodeChildren
        };
        root.AddChild(nav);
        LastOccluders = 0;
        LastMeshes = 0;
        foreach (var box in map.Boxes)
        {
            var primitive = new BoxMesh { Size = Conv.G(box.Size) };
            var mesh = new ArrayMesh();
            mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, primitive.GetMeshArrays());
            mesh.LightmapUnwrap(Transform3D.Identity, 0.1f);
            var mi = new MeshInstance3D { Mesh = mesh, MaterialOverride = mats[box.Material] };
            mi.Position = Conv.G(box.Center);
            mi.GIMode = GeometryInstance3D.GIModeEnum.Static;
            mi.CastShadow = GameSettings.Quality == "low"
                ? GeometryInstance3D.ShadowCastingSetting.Off
                : GeometryInstance3D.ShadowCastingSetting.On;
            if (box.Material == MapMaterial.Crate)
            {
                mi.VisibilityRangeEnd = 40f;
                mi.VisibilityRangeEndMargin = 8f;
            }
            else if (box.Material == MapMaterial.Wall)
            {
                mi.VisibilityRangeEnd = 90f;
                mi.VisibilityRangeEndMargin = 10f;
            }
            nav.AddChild(mi);
            LastMeshes++;
            if (!box.Solid) continue;
            var body = new StaticBody3D();
            body.CollisionLayer = Layers.World;
            body.CollisionMask = 0;
            body.Position = Conv.G(box.Center);
            body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = Conv.G(box.Size) } });
            nav.AddChild(body);
            if (box.Material == MapMaterial.Wall || box.Material == MapMaterial.Outer || box.Material == MapMaterial.Crate)
            {
                var oc = new OccluderInstance3D();
                oc.Occluder = new BoxOccluder3D { Size = Conv.G(box.Size) };
                oc.Position = Conv.G(box.Center);
                root.AddChild(oc);
                LastOccluders++;
            }
        }
        nav.BakeNavigationMesh();
        var a = (map.SiteAMin + map.SiteAMax) * 0.5f;
        var b = (map.SiteBMin + map.SiteBMax) * 0.5f;
        AddSiteLabel(root, new Vector3(a.X, 2.4f, a.Z), "A");
        AddSiteLabel(root, new Vector3(b.X, 2.4f, b.Z), "B");
        AddLight(root);
        AddWorldEnv(root);
        AddGi(root);
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
        root.AddChild(new WorldEnvironment { Environment = env });
    }

    static void AddGi(Node3D root)
    {
        var gi = new LightmapGI { Name = "LightmapGI" };
        gi.Quality = LightmapGI.BakeQuality.Low;
        gi.MaxTextureSize = 2048;
        gi.Directional = false;
        gi.UseDenoiser = false;
        root.AddChild(gi);
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
                Uv1Scale = new Vector3(0.25f, 0.25f, 0.25f),
                TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest
            };
        }
        return _mats;
    }

    static ImageTexture Checker()
    {
        var img = Image.CreateEmpty(8, 8, false, Image.Format.Rgb8);
        for (int y = 0; y < 8; y++)
            for (int x = 0; x < 8; x++)
                img.SetPixel(x, y, ((x + y) & 1) == 0 ? Colors.White : new Color(0.7f, 0.7f, 0.7f));
        return ImageTexture.CreateFromImage(img);
    }
}
