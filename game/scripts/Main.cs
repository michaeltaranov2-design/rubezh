using System;
using System.Globalization;
using Godot;
using Rubezh.Core;

namespace Rubezh.Game;

public partial class Main : Node3D
{
    Player _player = null!;
    Bot[] _bots = Array.Empty<Bot>();
    bool _selfTest, _profile, _mapsTest, _dedicated, _netTest, _done;
    string _connect = "";
    string _profileOut = "user://profile-stage1.json";
    double _profStart, _sampleFor;
    int _samples; double _fpsSum, _fpsMin = 1e9, _fpsMax;
    long _drawMax, _primMax, _memMax;
    int _fail;
    NetHost? _net;

    public override void _Ready()
    {
        foreach (string a in OS.GetCmdlineUserArgs())
        {
            if (a == "--self-test") _selfTest = true;
            if (a == "--profile") _profile = true;
            if (a == "--maps-test") _mapsTest = true;
            if (a == "--dedicated") _dedicated = true;
            if (a == "--net-test") _netTest = true;
            if (a.StartsWith("--connect=", StringComparison.Ordinal)) _connect = a.Substring(10);
            if (a.StartsWith("--profile-out=", StringComparison.Ordinal)) _profileOut = a.Substring("--profile-out=".Length);
        }
        GameSettings.Load();
        InputSetup.Register();
        MapRuntime.ResolveFromArgs();
        if (_profile) { Engine.MaxFps = 0; DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Disabled); }
        else Engine.MaxFps = GameSettings.MaxFps;
        MapRuntime.BuildInto(this);
        GameSettings.ApplyGraphics(GetViewport(), MapRuntime.Root?.GetNodeOrNull<DirectionalLight3D>("Sun"));
        Combat.Pool = new ImpactPool();
        AddChild(Combat.Pool);
        if (_dedicated)
        {
            _net = new NetHost();
            AddChild(_net);
            _net.Loop = new DedicatedLoop(MapRuntime.Current);
            _net.Loop.Accounts.StartHttp();
            var e = _net.Listen();
            if (e != Error.Ok) GD.PrintErr("[net] listen: " + e);
            GD.Print("DEDICATED_LISTEN " + DedicatedLoop.GamePort);
            if (_netTest) CallDeferred(nameof(RunNetTest));
            return;
        }
        var hud = new Hud(); AddChild(hud);
        _player = new Player();
        AddChild(_player);
        _player.Hud = hud;
        var spawn = MapRuntime.Current.TSpawns.Length > 0 ? MapRuntime.Current.TSpawns[0] : MapUzel.TSpawns[0];
        _player.Place(Conv.G(spawn), 0f);
        if (_connect.Length > 0)
        {
            _net = new NetHost();
            AddChild(_net);
            var host = _connect; int port = DedicatedLoop.GamePort;
            int c = host.LastIndexOf(':');
            if (c > 0 && int.TryParse(host.AsSpan(c + 1), out int p)) { port = p; host = host.Substring(0, c); }
            _net.Connect(host, port);
            GD.Print("NET_CONNECT " + host + ":" + port);
            if (_selfTest) CallDeferred(nameof(RunSelfTest));
            return;
        }
        int nCt = Math.Min(4, MapRuntime.Current.CtSpawns.Length);
        _bots = new Bot[nCt];
        for (int i = 0; i < nCt; i++)
        {
            var b = new Bot();
            AddChild(b);
            b.Init(1, Conv.G(MapRuntime.Current.CtSpawns[i]), Mathf.Pi, _player, (uint)(1000 + i));
            _bots[i] = b;
        }
        if (_mapsTest) CallDeferred(nameof(RunMapsTest));
        else if (_selfTest) CallDeferred(nameof(RunSelfTest));
        if (_profile) { _profStart = Time.GetTicksMsec() / 1000.0; _sampleFor = 8; }
    }

    public override void _Process(double delta)
    {
        if (!_profile || _done) return;
        double now = Time.GetTicksMsec() / 1000.0;
        if (now - _profStart < 1.5) return;
        float fps = (float)Engine.GetFramesPerSecond();
        _samples++;
        _fpsSum += fps; if (fps < _fpsMin) _fpsMin = fps; if (fps > _fpsMax) _fpsMax = fps;
        long dc = (long)Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame);
        long pr = (long)Performance.GetMonitor(Performance.Monitor.RenderTotalPrimitivesInFrame);
        long mem = (long)Performance.GetMonitor(Performance.Monitor.MemoryStatic);
        if (dc > _drawMax) _drawMax = dc; if (pr > _primMax) _primMax = pr; if (mem > _memMax) _memMax = mem;
        if (now - _profStart > 1.5 + _sampleFor) WriteProfile();
    }

    void RunNetTest()
    {
        try
        {
            if (_net == null) Fail("нет NetHost");
            else
            {
                var s = _net.Loop.Accounts.Login("player", "rubezh");
                if (s == null) Fail("login");
                else
                {
                    var slot = _net.Loop.Authorize(s.Value.Token);
                    if (slot == null) Fail("join");
                    else
                    {
                        var cmd = new InputCommand { Seq = 1, Slot = (byte)slot.Value, Flags = 1 };
                        if (!_net.Loop.PushInput(slot.Value, cmd)) Fail("push");
                        _net.Loop.TickAccum(0.05f);
                        var snap = _net.Loop.Snapshot((byte)slot.Value);
                        if (snap.Viewer != slot.Value) Fail("viewer");
                    }
                }
            }
        }
        catch (Exception e) { Fail("исключение: " + e.Message); }
        if (_fail == 0) GD.Print("STAGE3_NET_OK");
        else GD.Print("STAGE3_NET_FAIL " + _fail);
        GetTree().Quit(_fail == 0 ? 0 : 1);
    }

    void RunSelfTest()
    {
        try
        {
            if (_player == null) Fail("игрок не создан");
            if (_bots.Length != 4 && _connect.Length == 0) Fail("ожидалось 4 бота");
            var dummy = new Bot();
            AddChild(dummy);
            dummy.AiEnabled = false;
            dummy.Init(1, _player.GlobalPosition + new Godot.Vector3(2.2f, 0, -6f), 0, _player, 7);
            Godot.Vector3 eye = _player.Cam.GlobalPosition;
            Godot.Vector3 aim = dummy.GlobalPosition + new Godot.Vector3(0, 1.25f, 0);
            var info = Combat.Fire(GetWorld3D(), _player, 0, WeaponCatalog.Rifle, eye, (aim - eye).Normalized(), _player.GetRid());
            if (!info.Hit) Fail("самопроверка: луч не попал в манекен");
            dummy.QueueFree();
            Input.ActionPress("move_forward");
            GetTree().CreateTimer(1.0).Timeout += () =>
            {
                Input.ActionRelease("move_forward");
                var s = Conv.G(MapRuntime.Current.TSpawns[0]);
                float moved = new Godot.Vector2(_player.GlobalPosition.X - s.X, _player.GlobalPosition.Z - s.Z).Length();
                if (moved < 3f || moved > 8f) Fail("самопроверка: ход игрока " + moved.ToString("0.00", CultureInfo.InvariantCulture) + " м");
                FinishSelfTest();
            };
        }
        catch (Exception e) { Fail("исключение: " + e.Message); FinishSelfTest(); }
    }

    void RunMapsTest()
    {
        try
        {
            foreach (var map in MapCatalog.All())
            {
                if (MapRuntime.Root != null) { MapRuntime.Root.Free(); MapRuntime.Root = null; }
                MapRuntime.Current = map;
                var root = MapRuntime.BuildInto(this);
                if (root.GetNodeOrNull("Nav") == null) Fail(map.Id + ": нет Nav");
                if (root.GetNodeOrNull("LightmapGI") == null) Fail(map.Id + ": нет LightmapGI");
                if (root.GetNodeOrNull("Sun") == null) Fail(map.Id + ": нет Sun");
                if (MapBuilder.LastOccluders < 3) Fail(map.Id + ": мало окклюдеров " + MapBuilder.LastOccluders);
                if (MapBuilder.LastMeshes < 8) Fail(map.Id + ": мало мешей " + MapBuilder.LastMeshes);
                var r = BotNavSim.Run(map, 20, 7);
                if (!r.Ok) Fail(map.Id + " 20 раундов stuck=" + r.Stuck + " fall=" + r.Falls);
            }
            var bad = MapRuntime.TryLoadUserGltf("res://missing.glb");
            if (bad == null) Fail("отсутствующий glb должен отклоняться");
        }
        catch (Exception e) { Fail("исключение: " + e.Message); }
        if (_fail == 0) GD.Print("MAPS_SELFTEST_OK");
        else GD.Print("MAPS_SELFTEST_FAIL " + _fail);
        GetTree().Quit(_fail == 0 ? 0 : 1);
    }

    void Fail(string m) { _fail++; GD.PrintErr("[self-test] " + m); }

    void FinishSelfTest()
    {
        if (_fail == 0) GD.Print("STAGE1_SELFTEST_OK");
        else GD.Print("STAGE1_SELFTEST_FAIL " + _fail);
        GetTree().Quit(_fail == 0 ? 0 : 1);
    }

    void WriteProfile()
    {
        if (_done) return; _done = true;
        var inv = CultureInfo.InvariantCulture;
        string json = "{" +
            "\"renderer\":\"gl_compatibility\"," +
            "\"samples\":" + _samples + "," +
            "\"fpsAvg\":" + (_samples > 0 ? _fpsSum / _samples : 0).ToString("0.00", inv) + "," +
            "\"fpsMin\":" + _fpsMin.ToString("0.00", inv) + "," +
            "\"fpsMax\":" + _fpsMax.ToString("0.00", inv) + "," +
            "\"drawCallsMax\":" + _drawMax + "," +
            "\"primitivesMax\":" + _primMax + "," +
            "\"memoryStaticMax\":" + _memMax +
            "}";
        using var file = FileAccess.Open(_profileOut, FileAccess.ModeFlags.Write);
        if (file == null) { GD.PrintErr("не удалось записать профиль: " + _profileOut); GetTree().Quit(1); return; }
        file.StoreString(json);
        GD.Print("STAGE1_PROFILE_OK " + _profileOut);
        GetTree().Quit(0);
    }
}
