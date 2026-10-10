using System;
using System.IO;
using System.Text;
using System.Text.Json;

namespace Rubezh.Core;

/// <summary>Проверка пользовательского glTF/GLB: размер, магия, внешние URI, extras карт.</summary>
public static class MapLoader
{
    public const int MaxBytes = 8 * 1024 * 1024;
    public const int MaxJson = 2 * 1024 * 1024;

    public static string? Inspect(byte[] data, out string? mapId)
    {
        mapId = null;
        if (data == null || data.Length < 20) return "файл слишком короткий";
        if (data.Length > MaxBytes) return "файл больше 8 МБ";
        if (data[0] == (byte)'{' || data[0] == (byte)' ' || data[0] == (byte)'\t')
            return InspectJson(Encoding.UTF8.GetString(data), out mapId);
        if (data[0] == (byte)'g' && data[1] == (byte)'l' && data[2] == (byte)'T' && data[3] == (byte)'F')
            return InspectJson(Encoding.UTF8.GetString(data), out mapId);
        if (BitConverter.ToUInt32(data, 0) != 0x46546C67) return "не GLB/glTF";
        uint ver = BitConverter.ToUInt32(data, 4);
        uint len = BitConverter.ToUInt32(data, 8);
        if (ver != 2 || len != (uint)data.Length) return "битый заголовок GLB";
        int off = 12;
        if (off + 8 > data.Length) return "обрезанный chunk";
        uint clen = BitConverter.ToUInt32(data, off);
        uint ctype = BitConverter.ToUInt32(data, off + 4);
        if (ctype != 0x4E4F534A) return "первый chunk не JSON";
        if (clen > MaxJson || off + 8 + clen > data.Length) return "JSON chunk слишком большой";
        string json = Encoding.UTF8.GetString(data, off + 8, (int)clen);
        return InspectJson(json, out mapId);
    }

    static string? InspectJson(string json, out string? mapId)
    {
        mapId = null;
        if (json.IndexOf("data:", StringComparison.OrdinalIgnoreCase) >= 0) return "запрещён data: URI";
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (root.TryGetProperty("buffers", out var bufs))
            foreach (var b in bufs.EnumerateArray())
                if (b.TryGetProperty("uri", out var u) && u.GetString() is string s && s.Length > 0 && !s.StartsWith("data:", StringComparison.Ordinal))
                    return "внешний buffer URI запрещён";
        if (root.TryGetProperty("images", out var imgs))
            foreach (var im in imgs.EnumerateArray())
                if (im.TryGetProperty("uri", out var u) && u.GetString() is string s && s.Length > 0 && !s.StartsWith("data:", StringComparison.Ordinal))
                    return "внешний image URI запрещён";
        if (root.TryGetProperty("extras", out var extras) && extras.TryGetProperty("rubezhMapId", out var idEl))
            mapId = idEl.GetString();
        return null;
    }

    public static string? InspectFile(string path, out string? mapId)
    {
        mapId = null;
        if (!File.Exists(path)) return "файл не найден";
        var fi = new FileInfo(path);
        if (fi.Length > MaxBytes) return "файл больше 8 МБ";
        return Inspect(File.ReadAllBytes(path), out mapId);
    }
}
