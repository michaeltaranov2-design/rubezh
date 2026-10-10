using Godot;

namespace Rubezh.Game;

public static class InputSetup
{
    public static void Register()
    {
        BindKey("move_forward", Key.W);
        BindKey("move_back", Key.S);
        BindKey("move_left", Key.A);
        BindKey("move_right", Key.D);
        BindKey("jump", Key.Space);
        BindKey("crouch", Key.Ctrl);
        BindKey("walk", Key.Shift);
        BindKey("reload", Key.R);
        BindKey("slot1", Key.Key1);
        BindKey("slot2", Key.Key2);
        BindMouse("fire", MouseButton.Left);
    }

    static void BindKey(string action, Key key)
    {
        if (!InputMap.HasAction(action)) InputMap.AddAction(action);
        foreach (var e in InputMap.ActionGetEvents(action))
            if (e is InputEventKey k && k.Keycode == key) return;
        var ev = new InputEventKey { Keycode = key };
        InputMap.ActionAddEvent(action, ev);
    }

    static void BindMouse(string action, MouseButton button)
    {
        if (!InputMap.HasAction(action)) InputMap.AddAction(action);
        foreach (var e in InputMap.ActionGetEvents(action))
            if (e is InputEventMouseButton m && m.ButtonIndex == button) return;
        var ev = new InputEventMouseButton { ButtonIndex = button };
        InputMap.ActionAddEvent(action, ev);
    }
}
