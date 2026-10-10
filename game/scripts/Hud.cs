using Godot;

namespace Rubezh.Game;

public partial class Hud : CanvasLayer
{
    Label _status = null!;
    Label _help = null!;
    Label _center = null!;
    int _hp = -1, _ammo = -1, _mag = -1, _kills = -1;
    bool _reloading, _dead;
    string _weapon = "";

    public override void _Ready()
    {
        Layer = 10;
        _status = MkLabel(new Vector2(16, 8), 18);
        _help = MkLabel(new Vector2(16, 36), 14);
        _help.Modulate = new Color(0.85f, 0.9f, 1f, 0.7f);
        _help.Text = "WASD ход  |  мышь взгляд  |  ЛКМ огонь  |  R перезарядка  |  1/2 оружие  |  Ctrl присед  |  Esc курсор";
        _center = MkLabel(new Vector2(0, 0), 22);
        _center.HorizontalAlignment = HorizontalAlignment.Center;
        _center.AnchorLeft = 0.5f; _center.AnchorRight = 0.5f;
        _center.AnchorTop = 0.45f; _center.AnchorBottom = 0.45f;
        _center.OffsetLeft = -240; _center.OffsetRight = 240;
        AddCrosshair();
    }

    Label MkLabel(Vector2 pos, int size)
    {
        var l = new Label { Position = pos };
        l.AddThemeFontSizeOverride("font_size", size);
        AddChild(l);
        return l;
    }

    void AddCrosshair()
    {
        var root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(root);
        float s = GameSettings.CrosshairSize, g = GameSettings.CrosshairGap;
        Color c = GameSettings.CrosshairColor;
        Bar(root, -s - g, -1, s, 2, c);
        Bar(root, g, -1, s, 2, c);
        Bar(root, -1, -s - g, 2, s, c);
        Bar(root, -1, g, 2, s, c);
    }

    static void Bar(Control parent, float x, float y, float w, float h, Color c)
    {
        var r = new ColorRect { Color = c, MouseFilter = Control.MouseFilterEnum.Ignore };
        r.SetAnchorsPreset(Control.LayoutPreset.Center);
        r.OffsetLeft = x; r.OffsetTop = y; r.OffsetRight = x + w; r.OffsetBottom = y + h;
        parent.AddChild(r);
    }

    public void SetStatus(string weapon, int ammo, int mag, bool reloading, int hp, int kills)
    {
        if (weapon == _weapon && ammo == _ammo && mag == _mag && reloading == _reloading && hp == _hp && kills == _kills) return;
        _weapon = weapon; _ammo = ammo; _mag = mag; _reloading = reloading; _hp = hp; _kills = kills;
        _status.Text = weapon + "   " + (reloading ? "перезарядка" : ammo + " / " + mag) +
                       "      HP " + hp + "      убийств " + kills;
    }

    public void SetDead(bool dead)
    {
        if (dead == _dead) return;
        _dead = dead;
        _center.Text = dead ? "Вы погибли — возрождение через 2 с" : "";
    }
}
