using System.Runtime.CompilerServices;
using VoxelChaos.Scene;

namespace VoxelChaos.Noise;

public struct ChunkRng
{
    private ulong _s;

    public ChunkRng(int seed, ChunkPos p)
    {
        ulong s = (ulong)(uint)seed;
        s = s * 6364136223846793005UL + (ulong)(uint)p.X * 1442695040888963407UL;
        s = s * 6364136223846793005UL + (ulong)(uint)p.Y * 1442695040888963407UL;
        s = s * 6364136223846793005UL + (ulong)(uint)p.Z * 1442695040888963407UL;
        _s = s;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint Next()
    {
        _s = _s * 6364136223846793005UL + 1442695040888963407UL;
        return (uint)(_s >> 32);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float NextFloat() => (Next() >> 8) * (1.0f / 16777216f);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int NextInt(int min, int max) => min + (int)(Next() % (uint)(max - min));
}