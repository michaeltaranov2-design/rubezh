using System;
using Godot;
using Rubezh.Core;

namespace Rubezh.Game;

/// <summary>Выбор карты из каталога, аргументов и безопасная загрузка пользовательского glTF/GLB.</summary>
public static class MapRuntime
{
    public static MapDef Current = MapDef.FromUzel();
    public static Node3D? Root;

    public static void ResolveFromArgs()
    {
        string id = GameSettings.MapId;
        string? user = null;
        foreach (string a in OS.GetCmdlineUserArgs())
        {
            if (a.StartsWith("--map=", StringComparison.Ordinal)) id = a.Substring(6);
            if (a.StartsWith("--user-map=", StringComparison.Ordinal)) user = a.Substring(11);
        }
        if (!string.IsNullOrEmpty(user))
        {
            var err = TryLoadUserGltf(user);
            if (err != null) GD.PrintErr("[map] пользовательская карта отклонена: " + err);
        }
        try { Current = MapCatalog.Get(id); }
        catch { Current = MapDef.FromUzel(); }
        GameSettings.MapId = Current.Id;
    }

    public static Node3D BuildInto(Node parent)
    {
        Root = MapBuilder.Build(parent, Current);
        return Root;
    }

    public static string? TryLoadUserGltf(string path)
    {
        var err = MapLoader.InspectFile(path, out _);
        if (err != null) return err;
        var doc = new GltfDocument();
        var state = new GltfState();
        var e = doc.AppendFromFile(path, state);
        if (e != Error.Ok) return "gltf: " + e;
        return doc.GenerateScene(state) == null ? "gltf: пустая сцена" : null;
    }
}
