using VoxelChaos.Scene;

namespace VoxelChaos.World;

public static class WorldConfig
{
    public const int ChunkSize = Chunk.SIZE;

    /// <summary>Мир 4×32 = 128 блоков в высоту (Y = 0..127).</summary>
    public const int WorldHeightChunks = 4;
    public const int WorldHeight = WorldHeightChunks * ChunkSize;
    public const int MaxY = WorldHeight - 1;

    public const int VolChunksX = 8;
    public const int VolChunksY = 4;
    public const int VolChunksZ = 8;

    public const int VolSizeX = VolChunksX * ChunkSize;
    public const int VolSizeY = VolChunksY * ChunkSize;
    public const int VolSizeZ = VolChunksZ * ChunkSize;

    public const int WaterLevel = 62;
}