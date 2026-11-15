using VoxelChaos.Noise;
using VoxelChaos.Scene;

namespace VoxelChaos.World;

/// <summary>Детерминированное размещение структур по координатам чанка.</summary>
public sealed class StructurePlacer
{
    private readonly int _seed;
    public StructurePlacer(int seed) => _seed = seed;

    public void PlaceAll(Chunk chunk, ChunkStorage storage)
    {
        var rng = new ChunkRng(_seed, chunk.pos);
        int bx = chunk.pos.X * Chunk.SIZE;
        int bz = chunk.pos.Z * Chunk.SIZE;

        int centerY = chunk.GetSurface(Chunk.SIZE / 2, Chunk.SIZE / 2);
        bool landCenter = centerY >= WorldConfig.WaterLevel + 1;

        switch (chunk.biome)
        {
            case Biome.Forest:
            {
                int n = rng.NextInt(2, 5);
                for (int t = 0; t < n; t++)
                {
                    int lx = rng.NextInt(2, 30), lz = rng.NextInt(2, 30);
                    int surfY = chunk.GetSurface(lx, lz);
                    if (surfY < WorldConfig.WaterLevel + 1) continue;
                    PlaceTree(storage, chunk, bx + lx, bz + lz, surfY, ref rng);
                }
                break;
            }
            case Biome.Plains:
                if (landCenter && rng.NextFloat() < 0.10f)
                    PlaceRuins(storage, chunk, bx, bz, ref rng);
                break;
            case Biome.Desert:
                if (landCenter && rng.NextFloat() < 0.15f)
                    PlaceDesertTemple(storage, chunk, bx + 16, bz + 16, ref rng);
                break;
            case Biome.Ice:
                if (landCenter && rng.NextFloat() < 0.40f)
                    PlaceIceSpikes(storage, chunk, bx, bz, ref rng);
                break;
            case Biome.Volcano:
                if (landCenter && rng.NextFloat() < 0.20f)
                    PlaceVolcano(storage, chunk, bx + 16, bz + 16, ref rng);
                break;
        }
    }

    private static void PlaceTree(ChunkStorage s, Chunk ch, int wx, int wz, int surfY, ref ChunkRng rng)
    {
        int h = rng.NextInt(4, 7);
        for (int y = 1; y <= h; y++) s.SetWorld(ch, wx, surfY + y, wz, Blocks.Wood);

        int top = surfY + h;
        for (int dy = -2; dy <= 2; dy++)
        for (int dx = -2; dx <= 2; dx++)
        for (int dz = -2; dz <= 2; dz++)
        {
            int md = Math.Abs(dx) + Math.Abs(dy) + Math.Abs(dz);
            if (md <= 3 && !(dx == 0 && dz == 0 && dy <= 0))
                s.SetWorld(ch, wx + dx, top + dy, wz + dz, Blocks.Leaves);
        }
    }

    private static void PlaceRuins(ChunkStorage s, Chunk ch, int bx, int bz, ref ChunkRng rng)
    {
        int lx = 8 + rng.NextInt(0, 16), lz = 8 + rng.NextInt(0, 16);
        int surf = ch.GetSurface(lx, lz);
        int cx = bx + lx, cz = bz + lz;
        int size = rng.NextInt(4, 7);

        for (int i = -size; i <= size; i++)
        {
            if (rng.NextFloat() > 0.35f) s.SetWorld(ch, cx + i, surf + 1, cz - size, Blocks.Stone);
            if (rng.NextFloat() > 0.35f) s.SetWorld(ch, cx + i, surf + 1, cz + size, Blocks.Stone);
            if (rng.NextFloat() > 0.35f) s.SetWorld(ch, cx - size, surf + 1, cz + i, Blocks.Stone);
            if (rng.NextFloat() > 0.35f) s.SetWorld(ch, cx + size, surf + 1, cz + i, Blocks.Stone);
        }

        for (int p = 0; p < 4; p++)
        {
            int px = cx + rng.NextInt(-size, size + 1);
            int pz = cz + rng.NextInt(-size, size + 1);
            int ph = rng.NextInt(2, 4);
            for (int y = 1; y <= ph; y++) s.SetWorld(ch, px, surf + y, pz, Blocks.Stone);
        }
    }

    private static void PlaceDesertTemple(ChunkStorage s, Chunk ch, int cx, int cz, ref ChunkRng rng)
    {
        int surf = ch.GetSurface(Chunk.SIZE / 2, Chunk.SIZE / 2);
        for (int layer = 0; layer < 4; layer++)
        {
            int half = 4 - layer, ly = surf + 1 + layer;
            for (int dx = -half; dx <= half; dx++)
            for (int dz = -half; dz <= half; dz++)
            {
                bool shell = Math.Abs(dx) == half || Math.Abs(dz) == half || layer == 3;
                if (shell) s.SetWorld(ch, cx + dx, ly, cz + dz, Blocks.TempleStone);
            }
        }

        for (int y = 1; y <= 2; y++)
        for (int dx = -1; dx <= 1; dx++)
        for (int dz = -1; dz <= 1; dz++)
            s.SetWorld(ch, cx + dx, surf + y, cz + dz, Blocks.Air);
    }

    private static void PlaceIceSpikes(ChunkStorage s, Chunk ch, int bx, int bz, ref ChunkRng rng)
    {
        int n = rng.NextInt(2, 5);
        for (int sp = 0; sp < n; sp++)
        {
            int lx = 4 + rng.NextInt(0, 24), lz = 4 + rng.NextInt(0, 24);
            int surf = ch.GetSurface(lx, lz);
            int h = rng.NextInt(5, 15);
            for (int y = 0; y < h; y++)
            {
                int r = Math.Max(0, (h - y) / 4);
                for (int dx = -r; dx <= r; dx++)
                for (int dz = -r; dz <= r; dz++)
                    if (dx * dx + dz * dz <= r * r + 1)
                        s.SetWorld(ch, bx + lx + dx, surf + 1 + y, bz + lz + dz, Blocks.Ice);
            }
        }
    }

    private static void PlaceVolcano(ChunkStorage s, Chunk ch, int cx, int cz, ref ChunkRng rng)
    {
        int surf = ch.GetSurface(Chunk.SIZE / 2, Chunk.SIZE / 2);
        int radius = rng.NextInt(9, 13);
        int height = rng.NextInt(16, 25);

        for (int y = 0; y <= height; y++)
        {
            float r = radius * (1f - (float)y / height);
            int ri = (int)MathF.Ceiling(r);
            for (int dx = -ri; dx <= ri; dx++)
            for (int dz = -ri; dz <= ri; dz++)
            {
                float d2 = dx * dx + dz * dz;
                if (d2 > r * r) continue;
                bool crater = y > height - 3 && d2 < (r * r) * 0.4f;
                if (!crater) s.SetWorld(ch, cx + dx, surf + y, cz + dz, Blocks.Basalt);
            }
        }

        int cy = surf + height - 1;
        for (int dx = -2; dx <= 2; dx++)
        for (int dz = -2; dz <= 2; dz++)
            if (dx * dx + dz * dz <= 4)
                s.SetWorld(ch, cx + dx, cy, cz + dz, Blocks.Lava);
    }
}