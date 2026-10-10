using System;
using System.Globalization;

namespace Rubezh.Core;

public enum MatchMode { Competitive, Sandbox, Community }

public readonly struct ConsoleResult
{
    public readonly bool Ok;
    public readonly string Code;
    public ConsoleResult(bool ok, string code) { Ok = ok; Code = code; }
}

/// <summary>Серверный парсер: 15 модулей, квота 3/аккаунт/матч, nonce, Competitive-запреты. Без Godot.</summary>
public sealed class ConsoleState
{
    public const int ModCount = 15;
    public MatchMode Mode = MatchMode.Sandbox;
    public int RoundIndex;
    public bool WallhackOn;
    public int LastHealthBonus;
    public float LastDamageMul = 1f;

    readonly uint[] _acc = new uint[16];
    int _nAcc;
    readonly int[] _used = new int[256];
    readonly int[] _until = new int[256];
    readonly byte[] _on = new byte[256];
    readonly float[] _bhopMax = new float[16];
    readonly byte[] _auto = new byte[16];
    readonly float[] _dmg = new float[16];
    readonly uint[] _nonce = new uint[32];
    int _nNonce;

    public static int ModId(string n) => n switch
    {
        "wallhack" => 0,
        "aimbot" or "legit" or "легитный_аим" => 1,
        "triggerbot" => 2,
        "bhop" => 3,
        "skinchanger" => 4,
        "rage" => 5,
        "noclip" => 6,
        "impacts" or "sv_showimpacts" => 7,
        "rethrow" or "sv_rethrow_last_grenade" => 8,
        "maxmoney" or "mp_maxmoney" => 9,
        "startmoney" or "mp_startmoney" => 10,
        "weapon" => 11,
        "grenade" => 12,
        "health" => 13,
        "damage" => 14,
        _ => -1
    };

    static bool Banned(MatchMode m, int id) =>
        m == MatchMode.Competitive && (id is 0 or 5 or 6 or 8 or 9 or 10);

    int AccIndex(uint id)
    {
        for (int i = 0; i < _nAcc; i++) if (_acc[i] == id) return i;
        if (_nAcc >= _acc.Length) return 0;
        int n = _nAcc++;
        _acc[n] = id;
        _bhopMax[n] = 15f;
        _auto[n] = 1;
        _dmg[n] = 1f;
        return n;
    }

    public bool IsOn(uint account, int mod)
    {
        int a = AccIndex(account);
        int k = a * 16 + mod;
        return _on[k] != 0 && (_until[k] < 0 || RoundIndex < _until[k]);
    }

    public float BhopLimit(uint account)
    {
        if (!IsOn(account, 3)) return 25f;
        if (Mode == MatchMode.Competitive) return 10f;
        if (Mode == MatchMode.Sandbox) return 15f;
        float v = _bhopMax[AccIndex(account)];
        return Math.Clamp(v, 15f, 25f);
    }

    public float AirLimit(uint account) => BhopLimit(account) * 2f;
    public bool AutoJump(uint account) => IsOn(account, 3) && _auto[AccIndex(account)] != 0;
    public float DamageMul(uint account) => IsOn(account, 14) ? _dmg[AccIndex(account)] : 1f;

    public void ApplyMove(MovementSettings cfg, uint account)
    {
        cfg.BhopSpeedLimit = BhopLimit(account);
        cfg.AirStrafeLimit = AirLimit(account);
    }

    public ConsoleResult Execute(string line, uint accountId, uint nonce)
    {
        LastHealthBonus = 0;
        LastDamageMul = 1f;
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
            if (Banned(Mode, id)) return new(false, "mode");
            if (!ParseParams(id, t, 4, accountId, out var err)) return new(false, err);
            int a = AccIndex(accountId);
            int k = a * 16 + id;
            if (_used[k] >= 3) return new(false, "quota");
            _used[k]++;
            _until[k] = RoundIndex + 2;
            _on[k] = 1;
            if (id == 0) WallhackOn = true;
            if (id == 14) LastDamageMul = _dmg[a];
            return new(true, "ok");
        }
        if (t[1] == "revoke")
        {
            int id = ModId(t[3]);
            if (id < 0) return new(false, "unknown");
            int a = AccIndex(accountId);
            int k = a * 16 + id;
            _until[k] = -1;
            _on[k] = 0;
            if (id == 0) WallhackOn = false;
            return new(true, "revoked");
        }
        if (t[1] == "set") return new(true, "set");
        return new(false, "syntax");
    }

    public void OnRoundEnd()
    {
        RoundIndex++;
        WallhackOn = false;
        for (int a = 0; a < _nAcc; a++)
        {
            for (int m = 0; m < ModCount; m++)
            {
                int i = a * 16 + m;
                if (_until[i] >= 0 && RoundIndex >= _until[i]) _on[i] = 0;
            }
            if (IsOn(_acc[a], 0)) WallhackOn = true;
        }
    }

    bool ParseParams(int id, string[] t, int from, uint accountId, out string err)
    {
        err = "param";
        int a = AccIndex(accountId);
        if (id == 3) { _bhopMax[a] = 15f; _auto[a] = 1; }
        if (id == 14) _dmg[a] = 1.25f;
        if (id == 13) LastHealthBonus = 25;
        for (int i = from; i < t.Length; i++)
        {
            int eq = t[i].IndexOf('=');
            if (eq <= 0) { err = "syntax"; return false; }
            if (!ApplyParam(id, a, t[i].Substring(0, eq), t[i].Substring(eq + 1), out err)) return false;
        }
        return true;
    }

    bool ApplyParam(int id, int a, string key, string val, out string err)
    {
        err = "param";
        if (id == 3)
        {
            if (key == "maxspeed")
            {
                if (!TryFloat(val, 15f, 25f, out float v)) { err = "range"; return false; }
                _bhopMax[a] = v; return true;
            }
            if (key == "autojump")
            {
                if (!TryBool(val, out bool b)) { err = "range"; return false; }
                _auto[a] = b ? (byte)1 : (byte)0; return true;
            }
            return false;
        }
        if (id == 14 && key == "multiplier")
        {
            if (!TryFloat(val, 0.5f, 2f, out float v)) { err = "range"; return false; }
            _dmg[a] = v; return true;
        }
        if (id == 13 && key == "bonus")
        {
            if (!int.TryParse(val, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) || n < 1 || n > 100) { err = "range"; return false; }
            LastHealthBonus = n; return true;
        }
        if (!Known(id, key)) return false;
        return true;
    }

    static bool Known(int id, string key) => id switch
    {
        0 => key is "color" or "range" or "enemiesonly",
        1 => key is "fov" or "smooth" or "bone" or "visibleonly",
        2 => key is "delayms" or "weapon" or "visibleonly",
        4 => key is "weapon" or "skin" or "wear",
        5 => key is "pitchoffset" or "yawoffset",
        6 => key is "speed",
        7 => key is "mode",
        8 => key is "source",
        9 or 10 => key is "amount",
        11 => key is "weapon" or "ammo",
        12 => key is "grenade" or "count",
        _ => false
    };

    static bool TryFloat(string val, float min, float max, out float v)
    {
        v = 0;
        if (!float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out v) || float.IsNaN(v) || float.IsInfinity(v)) return false;
        return v >= min && v <= max;
    }

    static bool TryBool(string val, out bool b)
    {
        b = val is "true" or "1";
        return val is "true" or "false" or "1" or "0";
    }

    static string[] Split(string s)
    {
        var a = new string[12]; int n = 0, i = 0;
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
