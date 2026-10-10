using System;
using System.Numerics;

namespace Rubezh.Core;

/// <summary>Параметры движения в метрах и секундах. Значения подобраны под тик 64 Гц.</summary>
public sealed class MovementSettings
{
    public float MaxSpeed = 6.35f;
    public float CrouchSpeedFactor = 0.34f;
    public float WalkSpeedFactor = 0.52f;
    public float GroundAccelerate = 5.5f;
    public float AirAccelerate = 12f;
    public float AirWishSpeedCap = 0.76f;
    public float Friction = 5.2f;
    public float StopSpeed = 2.0f;
    public float Gravity = 20.3f;
    public float JumpSpeed = 7.3f;
    /// <summary>0 — без ограничения. Значение задаёт модуль bhop (этап 4).</summary>
    public float BhopSpeedLimit;
}

public struct MoveInput
{
    public float Forward, Right, Yaw;
    public bool JumpPressed, Crouch, Walk;
}

public struct MoveState
{
    public Vector3 Velocity;
    public bool OnGround;
    public bool Crouched;
}

/// <summary>
/// Детерминированное движение без зависимостей от движка. Используется игроком, ботами,
/// а на этапе 3 — сервером и клиентским предсказанием. Без выделений памяти.
/// </summary>
public static class MovementCore
{
    public static void Step(ref MoveState s, in MoveInput input, MovementSettings cfg, float dt)
    {
        if (!(dt > 0f) || dt > 0.25f) throw new ArgumentOutOfRangeException(nameof(dt));
        float sin = MathF.Sin(input.Yaw), cos = MathF.Cos(input.Yaw);
        float f = Math.Clamp(input.Forward, -1f, 1f), r = Math.Clamp(input.Right, -1f, 1f);
        float wx = -sin * f + cos * r, wz = -cos * f - sin * r;
        float wishLen = MathF.Sqrt(wx * wx + wz * wz);
        float wishSpeed = 0f;
        if (wishLen > 1e-5f)
        {
            wx /= wishLen;
            wz /= wishLen;
            float limit = cfg.MaxSpeed * (input.Crouch ? cfg.CrouchSpeedFactor : input.Walk ? cfg.WalkSpeedFactor : 1f);
            wishSpeed = limit * MathF.Min(1f, wishLen);
        }
        s.Crouched = input.Crouch;

        // Прыжок до трения: прыжок в тик приземления сохраняет скорость (bunnyhop).
        if (s.OnGround && input.JumpPressed)
        {
            s.OnGround = false;
            s.Velocity.Y = cfg.JumpSpeed;
            if (cfg.BhopSpeedLimit > 0f) ClampHorizontal(ref s.Velocity, cfg.BhopSpeedLimit);
        }

        if (s.OnGround)
        {
            ApplyFriction(ref s.Velocity, cfg, dt);
            AddSpeed(ref s.Velocity, wx, wz, wishSpeed, cfg.GroundAccelerate * wishSpeed * dt);
            s.Velocity.Y = 0f;
        }
        else
        {
            AddSpeed(ref s.Velocity, wx, wz, MathF.Min(wishSpeed, cfg.AirWishSpeedCap), cfg.AirAccelerate * wishSpeed * dt);
            s.Velocity.Y -= cfg.Gravity * dt;
            if (cfg.BhopSpeedLimit > 0f) ClampHorizontal(ref s.Velocity, cfg.BhopSpeedLimit);
        }
    }

    public static float HorizontalSpeed(in Vector3 v) => MathF.Sqrt(v.X * v.X + v.Z * v.Z);

    static void ApplyFriction(ref Vector3 v, MovementSettings cfg, float dt)
    {
        float speed = HorizontalSpeed(v);
        if (speed < 1e-4f) { v.X = 0f; v.Z = 0f; return; }
        float drop = MathF.Max(speed, cfg.StopSpeed) * cfg.Friction * dt;
        float scale = MathF.Max(speed - drop, 0f) / speed;
        v.X *= scale;
        v.Z *= scale;
    }

    static void AddSpeed(ref Vector3 v, float wx, float wz, float targetSpeed, float maxGain)
    {
        if (targetSpeed <= 0f || maxGain <= 0f) return;
        float add = targetSpeed - (v.X * wx + v.Z * wz);
        if (add <= 0f) return;
        float gain = MathF.Min(maxGain, add);
        v.X += gain * wx;
        v.Z += gain * wz;
    }

    static void ClampHorizontal(ref Vector3 v, float limit)
    {
        float speed = HorizontalSpeed(v);
        if (speed <= limit || speed < 1e-5f) return;
        float k = limit / speed;
        v.X *= k;
        v.Z *= k;
    }
}
