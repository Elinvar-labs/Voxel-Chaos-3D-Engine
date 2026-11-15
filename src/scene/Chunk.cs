using System.Runtime.CompilerServices;

namespace VoxelChaos.Scene;

public sealed class Chunk
{
    public const int SIZE = 32;
    public const int SIZE_CUBED = SIZE * SIZE * SIZE;
    public const int SHIFT = 5;
    public const int MASK = SIZE - 1;
    public const int AREA = SIZE * SIZE;

    /// <summary>Layout: [x][y][z] => x + y*32 + z*32*32 (x-fastest, совпадает с OpenGL 3D-текстурой).</summary>
    public readonly byte[] data = new byte[SIZE_CUBED];

    /// <summary>[x + z*SIZE] — поверхность (1.7.10-style heightmap).</summary>
    public readonly int[] heights = new int[AREA];

    public ChunkPos pos;
    public Biome biome;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Index(int x, int y, int z)
        => (x & MASK) | ((y & MASK) << SHIFT) | ((z & MASK) << (SHIFT * 2));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Set(int x, int y, int z, byte b) => data[Index(x, y, z)] = b;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte Get(int x, int y, int z) => data[Index(x, y, z)];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetSurface(int lx, int lz) => heights[lx + lz * SIZE];
}