using VoxelChaos.Scene;

namespace VoxelChaos.World;

/// <summary>Собирает плоский массив вокселей для 3D-текстуры из загруженных чанков.</summary>
public sealed class VolumeBuilder
{
    public const int SizeX = WorldConfig.VolSizeX;
    public const int SizeY = WorldConfig.VolSizeY;
    public const int SizeZ = WorldConfig.VolSizeZ;

    public byte[] Data { get; } = new byte[SizeX * SizeY * SizeZ];

    public int OriginChunkX { get; private set; }
    public int OriginChunkY { get; private set; }
    public int OriginChunkZ { get; private set; }

    public bool NeedsRebuild { get; private set; } = true;

    public void MarkDirty() => NeedsRebuild = true;

    public void SetOrigin(int cx, int cy, int cz)
    {
        if (cx == OriginChunkX && cy == OriginChunkY && cz == OriginChunkZ) return;
        OriginChunkX = cx;
        OriginChunkY = cy;
        OriginChunkZ = cz;
        NeedsRebuild = true;
    }

    public void Rebuild(ChunkStorage storage)
    {
        Array.Clear(Data);
        int total = WorldConfig.VolChunksX * WorldConfig.VolChunksY * WorldConfig.VolChunksZ;

        Parallel.For(0, total, i =>
        {
            int cz = i / (WorldConfig.VolChunksX * WorldConfig.VolChunksY);
            int rem = i % (WorldConfig.VolChunksX * WorldConfig.VolChunksY);
            int cy = rem / WorldConfig.VolChunksX;
            int cx = rem % WorldConfig.VolChunksX;

            var pos = new ChunkPos(OriginChunkX + cx, OriginChunkY + cy, OriginChunkZ + cz);
            if (!storage.TryGet(pos, out var chunk)) return;

            int vbx = cx * Chunk.SIZE;
            int vby = cy * Chunk.SIZE;
            int vbz = cz * Chunk.SIZE;

            var src = chunk.data;
            var dst = Data;
            for (int y = 0; y < Chunk.SIZE; y++)
            for (int z = 0; z < Chunk.SIZE; z++)
            {
                int srcOff = (y * Chunk.SIZE) + (z * Chunk.SIZE * Chunk.SIZE);
                int dstOff = vbx + (vby + y) * SizeX + (vbz + z) * SizeX * SizeY;
                Buffer.BlockCopy(src, srcOff, dst, dstOff, Chunk.SIZE);
            }
        });

        NeedsRebuild = false;
    }
}