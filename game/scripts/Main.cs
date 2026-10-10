using System;
using System.Globalization;
using Godot;
using Rubezh.Core;

namespace Rubezh.Game;

public partial class Main : Node3D
{
    Player _player = null!;
    Bot[] _bots = Array.Empty<Bot>();
    bool _selfTest, _profile, _done;
    string _profileOut = "user://profile-stage1.json";
    double _profStart, _sampleFor;
    int _samples; double _fpsSum, _fpsMin = 1e9, _fpsMax;
    long _drawMax, _primMax, _memMax;
    int _fail;

    public override void _Ready()
    {
        foreach (string a in OS.GetCmdlineUserArgs())
        {
            if (a == "--self-test") _selfTest = true;
            if (a == "--profile") _profile = true;
            if (a.StartsWith("--profile-out=", StringComparison.Ordinal)) _profileOut = a.Substring("--profile-out=".Length);
        }
        GameSettings.Load();
        InputSetup.Register();
        if (_profile) { Engine.MaxFps = 0; DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Disabled); }
        else Engine.MaxFps = GameSettings.MaxFps;
        MapBuilder.Build(this);
        GameSettings.ApplyGraphics(GetViewport(), GetNodeOrNull<DirectionalLight3D>("MapUzel/Sun"));
        Combat.Pool = new ImpactPool();
        AddChild(Combat.Pool);
        var hud = new Hud(); AddChild(hud);
        _player = new Player();
        AddChild(_player);
        _player.Hud = hud;
        _player.Place(Conv.G(MapUzel.TSpawns[0]), 0f);
        _bots = new Bot[4];
        for (int i = 0; i < 4; i++)
        {
            var b = new Bot();
            AddChild(b);
            b.Init(1, Conv.G(MapUzel.CtSpawns[i]), Mathf.Pi, _player, (uint)(1000 + i));
            _bots[i] = b;
        }
        if (_selfTest) CallDeferred(nameof(RunSelfTest));
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

    void RunSelfTest()
    {
        try
        {
            if (_player == null) Fail("игрок не создан");
            if (_bots.Length != 4) Fail("ожидалось 4 бота");
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
                float moved = new Godot.Vector2(_player.GlobalPosition.X - Conv.G(MapUzel.TSpawns[0]).X, _player.GlobalPosition.Z - Conv.G(MapUzel.TSpawns[0]).Z).Length();
                if (moved < 3f || moved > 8f) Fail("самопроверка: ход игрока " + moved.ToString("0.00", CultureInfo.InvariantCulture) + " м");
                FinishSelfTest();
            };
        }
        catch (Exception e) { Fail("исключение: " + e.Message); FinishSelfTest(); }
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
