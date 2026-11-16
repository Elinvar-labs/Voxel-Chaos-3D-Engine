using Silk.NET.Input;
using VoxelChaos.Core;
using VoxelChaos.Scene;

namespace VoxelChaos.Gameplay;

public sealed class Inventory
{
    public const int Slots = 9;

    private readonly byte[] _slots = new byte[Slots];

    public int SelectedIndex { get; private set; }

    public Inventory()
    {
        _slots[0] = Blocks.Grass;
        _slots[1] = Blocks.Dirt;
        _slots[2] = Blocks.Stone;
        _slots[3] = Blocks.Wood;
        _slots[4] = Blocks.Leaves;
        _slots[5] = Blocks.Sand;
        _slots[6] = Blocks.Snow;
        _slots[7] = Blocks.Basalt;
        _slots[8] = Blocks.TempleStone;
    }

    public byte GetSlot(int i)       => _slots[i];
    public byte GetSelectedBlock()   => _slots[SelectedIndex];
    
    public void Update()
    {
        int scroll = Input.ConsumeScroll();
        
        if (scroll != 0)
        {
            SelectedIndex = (SelectedIndex - scroll) % Slots;
            if (SelectedIndex < 0) SelectedIndex += Slots;
        }
    
        for (int i = 0; i < Slots; i++)
            if (Input.Jpressed(Key.Number1 + i))
                SelectedIndex = i;
    }
}