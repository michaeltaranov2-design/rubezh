using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;

namespace Rubezh.Core;

public readonly struct AccountSession
{
    public readonly uint AccountId;
    public readonly string Token;
    public readonly bool Admin;
    public AccountSession(uint id, string token, bool admin)
    { AccountId = id; Token = token; Admin = admin; }
}

/// <summary>Лобби-аккаунты. Join-токен не даёт игрового admin.</summary>
public sealed class AccountServer : IDisposable
{
    readonly Dictionary<string, string> _pass = new();
    readonly Dictionary<string, AccountSession> _sess = new();
    readonly Dictionary<string, int> _slot = new();
    uint _next = 1000;
    HttpListener? _http;
    volatile bool _run;
    public int Port { get; private set; }

    public AccountServer()
    {
        _pass["player"] = "rubezh";
        _pass["admin"] = "rubezh-admin";
    }

    public AccountSession? Login(string user, string pass)
    {
        if (user == null || pass == null) return null;
        if (!_pass.TryGetValue(user, out var p) || p != pass) return null;
        bool admin = user == "admin";
        var s = new AccountSession(++_next, Guid.NewGuid().ToString("N"), admin);
        _sess[s.Token] = s;
        return s;
    }

    public AccountSession? Validate(string token)
    {
        if (token == null) return null;
        return _sess.TryGetValue(token, out var s) ? s : null;
    }

    public int? Join(string token)
    {
        var s = Validate(token);
        if (s == null) return null;
        if (_slot.TryGetValue(token, out int old)) return old;
        if (_slot.Count >= 10) return null;
        int slot = _slot.Count;
        _slot[token] = slot;
        return slot;
    }

    public bool IsAdmin(string token)
    {
        var s = Validate(token);
        return s != null && s.Value.Admin;
    }

    public bool StartHttp(int port = 0)
    {
        StopHttp();
        int p = port > 0 ? port : 18080;
        for (int i = 0; i < 8; i++)
        {
            var http = new HttpListener();
            try
            {
                http.Prefixes.Add("http://127.0.0.1:" + p + "/");
                http.Start();
                _http = http; Port = p; _run = true;
                var th = new Thread(Loop) { IsBackground = true };
                th.Start();
                return true;
            }
            catch { try { http.Close(); } catch { } p++; }
        }
        return false;
    }

    void Loop()
    {
        while (_run && _http != null)
        {
            HttpListenerContext ctx;
            try { ctx = _http.GetContext(); }
            catch { break; }
            try { Handle(ctx); } catch { try { ctx.Response.Abort(); } catch { } }
        }
    }

    void Handle(HttpListenerContext ctx)
    {
        string path = ctx.Request.Url?.AbsolutePath ?? "/";
        string body;
        using (var sr = new StreamReader(ctx.Request.InputStream, Encoding.UTF8)) body = sr.ReadToEnd();
        int code = 404; string json = "{\"error\":\"not_found\"}";
        if (path == "/v1/login" && ctx.Request.HttpMethod == "POST")
        {
            var s = Login(Field(body, "user"), Field(body, "pass"));
            if (s == null) { code = 401; json = "{\"error\":\"bad_login\"}"; }
            else { code = 200; json = "{\"accountId\":" + s.Value.AccountId + ",\"token\":\"" + s.Value.Token + "\",\"admin\":" + (s.Value.Admin ? "true" : "false") + "}"; }
        }
        else if (path == "/v1/join" && ctx.Request.HttpMethod == "POST")
        {
            var slot = Join(Field(body, "token"));
            if (slot == null) { code = 403; json = "{\"error\":\"no_slot\"}"; }
            else { code = 200; json = "{\"slot\":" + slot.Value + ",\"admin\":false}"; }
        }
        else if (path == "/v1/catalog" && ctx.Request.HttpMethod == "GET")
        { code = 200; json = "{\"maps\":[\"klin_greybox\",\"uzel_greybox\",\"horda_greybox\"]}"; }
        var bytes = Encoding.UTF8.GetBytes(json);
        ctx.Response.StatusCode = code;
        ctx.Response.ContentType = "application/json";
        ctx.Response.ContentLength64 = bytes.Length;
        ctx.Response.OutputStream.Write(bytes, 0, bytes.Length);
        ctx.Response.Close();
    }

    static string Field(string json, string key)
    {
        string pat = "\"" + key + "\":\"";
        int i = json.IndexOf(pat, StringComparison.Ordinal);
        if (i < 0) return "";
        i += pat.Length;
        int j = json.IndexOf('"', i);
        return j < 0 ? "" : json.Substring(i, j - i);
    }

    public void StopHttp()
    {
        _run = false;
        try { _http?.Stop(); } catch { }
        _http = null;
    }

    public void Dispose() => StopHttp();
}
