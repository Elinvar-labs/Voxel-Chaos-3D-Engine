using Silk.NET.Input;
using VoxelChaos.Core;
using VoxelChaos.Scene;
using VoxelChaos.World;
using Timer = VoxelChaos.Utils.Timer;

namespace VoxelChaos.Gameplay;

public sealed class BlockInteraction
{
    private const float Reach          = 6f;
    private const float BreakInterval  = 0.25f;   // Survival: раз в 0.25 с
    private const float MaxDelta       = 0.1f;    // защита от больших лагов

    private readonly World.World            _world;
    private readonly Camera           _camera;
    private readonly PlayerController _player;
    private readonly Inventory        _inventory;
    private readonly Timer            _timer = new();

    private float _breakAccum;
    private (int X, int Y, int Z) _lastTarget;
    private bool _hasLastTarget;

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
        if (dt <= 0f || dt > MaxDelta) dt = 1f / 60f;

        if (_player.Mode == GameMode.Spectator) { _breakAccum = 0f; _hasLastTarget = false; return; }

        UpdateBreak(dt);
        UpdatePlace();
    }

    private void UpdateBreak(float dt)
    {
        bool held     = Input.Clicked(MouseButton.Left);
        bool justDown = Input.Jclicked(MouseButton.Left);

        if (!held) { _breakAccum = 0f; _hasLastTarget = false; return; }

        if (!VoxelRaycaster.Raycast(_world, _camera.GetPosition(), _camera.GetFront(),
                Reach, out var hit, out _))
        {
            _breakAccum = 0f;
            _hasLastTarget = false;
            return;
        }

        var target = (hit.X, hit.Y, hit.Z);
        bool targetChanged = !_hasLastTarget || _lastTarget != target;
        _lastTarget = target;
        _hasLastTarget = true;

        // Первый клик ИЛИ цель сменилась (в т.ч. только что сломали блок) — ломаем сразу
        if (justDown || targetChanged)
        {
            _world.SetBlock(target.X, target.Y, target.Z, Blocks.Air);
            _breakAccum = 0f;
            return;
        }

        // Survival: удерживание = повтор каждые 0.25 с
        if (_player.Mode == GameMode.Survival)
        {
            _breakAccum += dt;
            if (_breakAccum >= BreakInterval)
            {
                _world.SetBlock(target.X, target.Y, target.Z, Blocks.Air);
                _breakAccum -= BreakInterval;
            }
        }
        // Creative: один клик — один блок (уже обработан в justDown выше)
    }

    private void UpdatePlace()
    {
        if (!Input.Jclicked(MouseButton.Right)) return;

        if (!VoxelRaycaster.Raycast(_world, _camera.GetPosition(), _camera.GetFront(),
                Reach, out var hit, out var normal))
            return;

        int px = hit.X + normal.X;
        int py = hit.Y + normal.Y;
        int pz = hit.Z + normal.Z;

        if (_player.Mode == GameMode.Survival && _player.IntersectsVoxel(px, py, pz))
            return;

        _world.SetBlock(px, py, pz, _inventory.GetSelectedBlock());
    }
}