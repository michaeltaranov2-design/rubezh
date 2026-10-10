using Godot;

namespace Rubezh.Game;

public static class GameSettings
{
    public static float Fov = 90f;
    public static float Sensitivity = 0.022f;
    public static int MaxFps = 60;
    public static string Quality = "low";
    public static float CrosshairSize = 8f;
    public static float CrosshairGap = 4f;
    public static Color CrosshairColor = new(0.2f, 1f, 0.35f, 0.9f);
    const string Path = "user://settings.cfg";

    public static void Load()
    {
        var cfg = new ConfigFile();
        if (cfg.Load(Path) != Error.Ok) return;
        Fov = (float)cfg.GetValue("video", "fov", Fov);
        MaxFps = (int)cfg.GetValue("video", "max_fps", MaxFps);
        Quality = (string)cfg.GetValue("video", "quality", Quality);
        Sensitivity = (float)cfg.GetValue("input", "sensitivity", Sensitivity);
    }

    public static void Save()
    {
        var cfg = new ConfigFile();
        cfg.SetValue("video", "fov", Fov);
        cfg.SetValue("video", "max_fps", MaxFps);
        cfg.SetValue("video", "quality", Quality);
        cfg.SetValue("input", "sensitivity", Sensitivity);
        cfg.Save(Path);
    }

    public static void ApplyGraphics(Viewport vp, DirectionalLight3D? sun)
    {
        bool high = Quality == "high";
        Engine.MaxFps = MaxFps;
        if (sun != null) sun.ShadowEnabled = high;
        if (vp is SubViewport sv) sv.Msaa3D = high ? Viewport.Msaa.Msaa2X : Viewport.Msaa.Disabled;
    }
}
