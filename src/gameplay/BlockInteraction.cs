using Silk.NET.Input;
using VoxelChaos.Core;
using VoxelChaos.Scene;
using VoxelChaos.World;

namespace VoxelChaos.Gameplay;

public sealed class BlockInteraction
{
    private const float Reach = 6f;

    private readonly World.World            _world;
    private readonly Camera           _camera;
    private readonly PlayerController _player;
    private readonly Inventory        _inventory;

    public BlockInteraction(World.World world, Camera camera, PlayerController player, Inventory inventory)
    {
        _world = world;
        _camera = camera;
        _player = player;
        _inventory = inventory;
    }

    public void Update()
    {
        if (_player.Mode == GameMode.Spectator) return;

        if (Input.Jclicked(MouseButton.Left))
        {
            if (VoxelRaycaster.Raycast(_world, _camera.GetPosition(), _camera.GetFront(),
                    Reach, out var hit, out _))
                _world.SetBlock(hit.X, hit.Y, hit.Z, Blocks.Air);
        }

        if (Input.Jclicked(MouseButton.Right))
        {
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
}