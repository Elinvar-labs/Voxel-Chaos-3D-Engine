namespace VoxelChaos.Scene;

public readonly struct VoxelMod
{
    public readonly int X, Y, Z;
    public readonly byte Block;

    public VoxelMod(int x, int y, int z, byte b) { X = x; Y = y; Z = z; Block = b; }
}