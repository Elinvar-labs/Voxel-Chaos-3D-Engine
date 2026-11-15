using System.Numerics;
using Silk.NET.Input;
using VoxelChaos.Core;
using VoxelChaos.World;
using Timer = VoxelChaos.Utils.Timer;

namespace VoxelChaos.Gameplay;

public sealed class PlayerController : Camera
{
    public const float EyeHeight   = 1.62f;
    public const float BodyRadius  = 0.30f;
    public const float BodyHeight  = 1.80f;

    private const float WalkSpeed     = 5f;
    private const float FlySpeed      = 15f;
    private const float RunMultiplier = 5f;
    private const float JumpSpeed     = 8f;
    private const float Gravity       = 25f;

    private readonly Timer  _timer = new();
    private readonly World.World? _world;
    private Vector3 _bodyPos;      // позиция НОГ
    private Vector3 _velocity;
    private bool _onGround;

    public GameMode Mode { get; set; } = GameMode.Creative;
    public World.World? World => _world;

    public PlayerController(Vector3 spawnFeet, float fov, uint width, uint height, World.World? world = null)
        : base(spawnFeet + new Vector3(0, EyeHeight, 0), fov, width, height)
    {
        _bodyPos = spawnFeet;
        _world = world;
    }

    public override void ParseInput()
    {
        _timer.Update();
        float dt = (float)_timer.GetDelta();
        if (dt <= 0f || dt > 0.1f) dt = 1f / 60f;

        // ---------- Look ----------
        yaw   -= Input.DeltaX * 0.1f;
        pitch += Input.DeltaY * 0.1f;
        pitch = Math.Clamp(pitch, -89f, 89f);
        yaw   = (yaw % 360f + 360f) % 360f;

        front.X = (float)(Math.Cos(pitch.ToRadians()) * Math.Cos(yaw.ToRadians()));
        front.Y = (float) Math.Sin(pitch.ToRadians());
        front.Z = (float)(Math.Cos(pitch.ToRadians()) * Math.Sin(yaw.ToRadians()));
        front = Vector3.Normalize(front);
        UpdateVectors();

        // ---------- Смена режима ----------
        if (Input.Jpressed(Key.F1)) Mode = GameMode.Spectator;
        if (Input.Jpressed(Key.F2)) Mode = GameMode.Creative;
        if (Input.Jpressed(Key.F3)) Mode = GameMode.Survival;

        if (_world == null) return;

        // ---------- Движение ----------
        if (Mode == GameMode.Survival) MoveSurvival(dt);
        else                           MoveFly(dt);

        SetPosition(_bodyPos + new Vector3(0, EyeHeight, 0));
    }

    private void MoveFly(float dt)
    {
        var fwd = Mode == GameMode.Spectator
            ? front
            : Vector3.Normalize(new Vector3(front.X, 0, front.Z));

        var move = Vector3.Zero;
        if (Input.Pressed(Key.W)) move += fwd;
        if (Input.Pressed(Key.S)) move -= fwd;
        if (Input.Pressed(Key.A)) move -= right;
        if (Input.Pressed(Key.D)) move += right;
        if (Input.Pressed(Key.Space))     move += worldUp;
        if (Input.Pressed(Key.ShiftLeft)) move -= worldUp;

        if (move.LengthSquared() > 0f) move = Vector3.Normalize(move);
        float speed = FlySpeed;
        if (Input.Pressed(Key.ControlLeft)) speed *= RunMultiplier;

        _bodyPos += move * speed * dt;
        _onGround = false;
    }

    private void MoveSurvival(float dt)
    {
        var fwd = Vector3.Normalize(new Vector3(front.X, 0, front.Z));
        var move = Vector3.Zero;
        if (Input.Pressed(Key.W)) move += fwd;
        if (Input.Pressed(Key.S)) move -= fwd;
        if (Input.Pressed(Key.A)) move -= right;
        if (Input.Pressed(Key.D)) move += right;
        if (move.LengthSquared() > 0f) move = Vector3.Normalize(move);

        float sp = WalkSpeed;
        if (Input.Pressed(Key.ControlLeft)) sp *= 2f;

        _velocity.X = move.X * sp;
        _velocity.Z = move.Z * sp;
        _velocity.Y -= Gravity * dt;

        _onGround = Collides(_bodyPos + new Vector3(0, -0.02f, 0));
        if (_onGround && Input.Pressed(Key.Space))
            _velocity.Y = JumpSpeed;

        MoveAxis(_velocity * dt);
    }

    private void MoveAxis(Vector3 delta)
    {
        // X
        _bodyPos.X += delta.X;
        if (Collides(_bodyPos)) { _bodyPos.X -= delta.X; _velocity.X = 0; }
        // Y
        _bodyPos.Y += delta.Y;
        if (Collides(_bodyPos))
        {
            _bodyPos.Y -= delta.Y;
            if (delta.Y < 0f) _onGround = true;
            _velocity.Y = 0;
        }
        // Z
        _bodyPos.Z += delta.Z;
        if (Collides(_bodyPos)) { _bodyPos.Z -= delta.Z; _velocity.Z = 0; }
    }

    private bool Collides(Vector3 feet)
    {
        if (_world == null) return false;

        float x0f = feet.X - BodyRadius, x1f = feet.X + BodyRadius;
        float y0f = feet.Y,               y1f = feet.Y + BodyHeight;
        float z0f = feet.Z - BodyRadius, z1f = feet.Z + BodyRadius;

        int x0 = (int)MathF.Floor(x0f), x1 = (int)MathF.Floor(x1f - 0.001f);
        int y0 = (int)MathF.Floor(y0f), y1 = (int)MathF.Floor(y1f - 0.001f);
        int z0 = (int)MathF.Floor(z0f), z1 = (int)MathF.Floor(z1f - 0.001f);

        for (int y = y0; y <= y1; y++)
        for (int z = z0; z <= z1; z++)
        for (int x = x0; x <= x1; x++)
            if (_world.IsSolid(x, y, z)) return true;

        return false;
    }

    /// <summary>Проверка «не поставить блок в себя» (для Survival).</summary>
    public bool IntersectsVoxel(int vx, int vy, int vz)
    {
        float x0f = _bodyPos.X - BodyRadius, x1f = _bodyPos.X + BodyRadius;
        float y0f = _bodyPos.Y,               y1f = _bodyPos.Y + BodyHeight;
        float z0f = _bodyPos.Z - BodyRadius, z1f = _bodyPos.Z + BodyRadius;

        return vx + 1 > x0f && vx < x1f
            && vy + 1 > y0f && vy < y1f
            && vz + 1 > z0f && vz < z1f;
    }

    public Vector3 BodyPosition => _bodyPos;
}