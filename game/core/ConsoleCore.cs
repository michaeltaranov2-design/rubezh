using System;

namespace Rubezh.Core;

public enum MatchMode { Competitive, Sandbox, Community }

public readonly struct ConsoleResult
{
    public readonly bool Ok;
    public readonly string Code;
    public ConsoleResult(bool ok, string code) { Ok = ok; Code = code; }
}

/// <summary>Серверный парсер консоли: квоты, nonce, Competitive-запреты. Без Godot.</summary>
public sealed class ConsoleState
{
    public MatchMode Mode = MatchMode.Sandbox;
    public int RoundIndex;
    readonly int[] _used = new int[8];
    readonly int[] _until = new int[8];
    readonly uint[] _nonce = new uint[32];
    int _nNonce;
    public bool WallhackOn;

    static int ModId(string n) => n == "wallhack" ? 0 : n == "aimbot" ? 1 : n == "triggerbot" ? 2 : -1;

    public ConsoleResult Execute(string line, uint accountId, uint nonce)
    {
        if (string.IsNullOrWhiteSpace(line) || line.Length > 256) return new(false, "too_long");
        for (int i = 0; i < _nNonce; i++) if (_nonce[i] == nonce) return new(true, "dup");
        if (_nNonce < _nonce.Length) _nonce[_nNonce++] = nonce;
        var t = Split(line);
        if (t.Length < 2 || t[0] != "game") return new(false, "syntax");
        if (t[1] == "list") return new(true, "list");
        if (t[1] == "status") return new(true, "status");
        if (t.Length < 4) return new(false, "syntax");
        if (t[1] == "give")
        {
            if (t[2] != "self" && t[2] != "all") return new(false, "target");
            int id = ModId(t[3]);
            if (id < 0) return new(false, "unknown");
            if (Mode == MatchMode.Competitive && id == 0) return new(false, "mode");
            if (_used[id] >= 3) return new(false, "quota");
            _used[id]++;
            _until[id] = RoundIndex + 2;
            if (id == 0) WallhackOn = true;
            return new(true, "ok");
        }
        if (t[1] == "revoke")
        {
            int id = ModId(t[3]);
            if (id < 0) return new(false, "unknown");
            _until[id] = -1;
            if (id == 0) WallhackOn = false;
            return new(true, "revoked");
        }
        return new(false, "syntax");
    }

    public void OnRoundEnd()
    {
        RoundIndex++;
        if (_until[0] >= 0 && RoundIndex >= _until[0]) WallhackOn = false;
    }

    static string[] Split(string s)
    {
        var a = new string[8]; int n = 0, i = 0;
        while (i < s.Length && n < a.Length)
        {
            while (i < s.Length && s[i] == ' ') i++;
            int j = i; while (j < s.Length && s[j] != ' ') j++;
            if (j > i) a[n++] = s.Substring(i, j - i).ToLowerInvariant();
            i = j;
        }
        var r = new string[n];
        for (int k = 0; k < n; k++) r[k] = a[k];
        return r;
    }
}
