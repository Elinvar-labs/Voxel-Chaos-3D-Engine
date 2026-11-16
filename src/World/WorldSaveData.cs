using System.Collections.Generic;
using System.Numerics;

namespace VoxelChaos.World;

public class WorldSaveData
{
    public int Seed { get; set; }
    public float TimeOfDay { get; set; }
    public Vector3 PlayerPos { get; set; }
    public float PlayerYaw { get; set; }
    public float PlayerPitch { get; set; }
    
    // Список всех измененных блоков
    public List<BlockMod> Modifications { get; set; } = new();
}

public class BlockMod
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Z { get; set; }
    public byte Block { get; set; }
}