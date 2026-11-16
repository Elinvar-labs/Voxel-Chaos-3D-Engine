using Silk.NET.Input;
using VoxelChaos.Core;
using VoxelChaos.Scene;
using VoxelChaos.World;
using Timer = VoxelChaos.Utils.Timer;

namespace VoxelChaos.Gameplay;

public sealed class BlockInteraction
{
    private const float Reach = 6f;

    private readonly World.World _world;
    private readonly Camera _camera;
    private readonly PlayerController _player;
    private readonly Inventory _inventory;
    private readonly Timer _timer = new();

    public BlockInteraction(World.World world, Camera camera, PlayerController player, Inventory inventory)
    {
        _world = world;
        _camera = camera;
        _player = player;
        _inventory = inventory;
    }

    public void Update()
    {
        _timer.Update();
        float dt = (float)_timer.GetDelta();
        if (dt <= 0f || dt > 0.1f) dt = 1f / 60f;

        if (_player.Mode == GameMode.Spectator) { ResetBreak(); return; }

        UpdateBreak(dt);
        UpdatePlace();
    }

    private void UpdateBreak(float dt)
    {
        // В креативе ломаем мгновенно по клику
        if (_player.Mode == GameMode.Creative)
        {
            ResetBreak();
            if (Input.Jclicked(MouseButton.Left))
            {
                if (VoxelRaycaster.Raycast(_world, _camera.GetPosition(), _camera.GetFront(), Reach, out var hit, out _))
                    _world.SetBlock(hit.X, hit.Y, hit.Z, Blocks.Air);
            }
            return;
        }

        // В выживании ломаем постепенно
        bool held = Input.Clicked(MouseButton.Left);
        if (!held) { ResetBreak(); return; }

        if (!VoxelRaycaster.Raycast(_world, _camera.GetPosition(), _camera.GetFront(), Reach, out var target, out _))
        {
            ResetBreak();
            return;
        }

        byte block = _world.GetBlock(target.X, target.Y, target.Z);
        if (block == Blocks.Air || block == Blocks.Water || block == Blocks.Lava)
        {
            ResetBreak();
            return;
        }

        // Если прицел сдвинулся на другой блок — сбрасываем прогресс
        if (_world.BreakX != target.X || _world.BreakY != target.Y || _world.BreakZ != target.Z)
        {
            _world.BreakX = target.X;
            _world.BreakY = target.Y;
            _world.BreakZ = target.Z;
            _world.BreakProgress = 0f;
        }

        // Накапливаем прогресс в зависимости от твердости
        float hardness = GetHardness(block);
        _world.BreakProgress += dt / hardness;

        // Блок сломан
        if (_world.BreakProgress >= 1.0f)
        {
            _world.SetBlock(target.X, target.Y, target.Z, Blocks.Air);
            ResetBreak();
        }
    }

    private void UpdatePlace()
    {
        if (_player.Mode == GameMode.Spectator) return;
        if (!Input.Jclicked(MouseButton.Right)) return;

        if (!VoxelRaycaster.Raycast(_world, _camera.GetPosition(), _camera.GetFront(), Reach, out var hit, out var normal))
            return;

        int px = hit.X + normal.X;
        int py = hit.Y + normal.Y;
        int pz = hit.Z + normal.Z;

        if (_player.Mode == GameMode.Survival && _player.IntersectsVoxel(px, py, pz))
            return;

        _world.SetBlock(px, py, pz, _inventory.GetSelectedBlock());
    }

    private void ResetBreak()
    {
        _world.BreakX = -1;
        _world.BreakY = -1;
        _world.BreakZ = -1;
        _world.BreakProgress = 0f;
    }

    private static float GetHardness(byte block)
    {
        return block switch
        {
            Blocks.Leaves      => 0.2f,  // Ломаются за 0.2 сек
            Blocks.Snow        => 0.3f,
            Blocks.Dirt        => 0.5f,
            Blocks.Sand        => 0.5f,
            Blocks.Ice         => 0.5f,
            Blocks.Grass       => 0.6f,
            Blocks.Wood        => 1.2f,
            Blocks.Stone       => 1.5f,
            Blocks.TempleStone => 1.5f,
            Blocks.Basalt      => 1.8f,
            _                  => 1.0f
        };
    }
}