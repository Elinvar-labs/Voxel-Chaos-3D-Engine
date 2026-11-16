using System.Runtime.CompilerServices;
using VoxelChaos.Noise;
using VoxelChaos.Scene;

namespace VoxelChaos.World;

/// <summary>Minecraft 1.7.10-style pure-heightmap генератор (без нависаний).</summary>
public sealed class TerrainGenerator
{
    public int Seed { get; init; }
    public int WaterLevel { get; init; } = WorldConfig.WaterLevel;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float SmoothStep(float e0, float e1, float x)
    {
        float t = Math.Clamp((x - e0) / (e1 - e0), 0f, 1f);
        return t * t * (3f - 2f * t);
    }

    // ------------------------------------------------------------
    //  Биом (3 климатических шкалы)
    // ------------------------------------------------------------
    public Biome GetBiome(int wx, int wz)
    {
        float temp = FastNoise.Fbm2(wx * 0.0016f, wz * 0.0016f, Seed + 1000, 3);
        float humid = FastNoise.Fbm2(wx * 0.0018f, wz * 0.0018f, Seed + 2000, 3);
        float rare = FastNoise.Fbm2(wx * 0.0007f, wz * 0.0007f, Seed + 3000, 2);

        // Снижаем порог "редкости" с 0.4 до 0.15, чтобы биомы действительно появлялись
        if (rare > 0.15f)
        {
            // Снижаем требования к температуре, чтобы они появлялись чаще
            if (temp < -0.05f) return Biome.Ice; // Было -0.2f
            if (temp > 0.10f) return Biome.Volcano; // Было 0.25f
        }

        if (temp > 0.2f) return Biome.Desert;
        if (humid > 0.05f) return Biome.Forest;
        return Biome.Plains;
    }

    // ------------------------------------------------------------
    //  Высота поверхности (чистая 2D heightmap)
    // ------------------------------------------------------------
    public int GetHeight(int wx, int wz, Biome biome)
    {
        float warpX = FastNoise.Fbm2(wx * 0.0030f, wz * 0.0030f, Seed + 5000, 2) * 35f;
        float warpZ = FastNoise.Fbm2(wx * 0.0030f, wz * 0.0030f, Seed + 5001, 2) * 35f;

        float cont = FastNoise.Fbm2(
            (wx + warpX) * 0.0028f,
            (wz + warpZ) * 0.0028f,
            Seed + 100, 4, 0.5f, 2f);
        cont = Math.Clamp(cont * 1.8f + 0.30f, -1.5f, 1.3f);

        float erosion = FastNoise.Fbm2(wx * 0.0032f, wz * 0.0032f, Seed + 200, 3, 0.5f, 2f);
        float erosionMask = SmoothStep(0.35f, -0.35f, erosion);

        float r1 = 1f - MathF.Abs(FastNoise.Perlin2(wx * 0.0045f, wz * 0.0045f, Seed + 300));
        float r2 = 1f - MathF.Abs(FastNoise.Perlin2(wx * 0.0110f, wz * 0.0110f, Seed + 301));
        float r3 = 1f - MathF.Abs(FastNoise.Perlin2(wx * 0.0260f, wz * 0.0260f, Seed + 302));
        float ridge = r1 * r1 * 0.55f + r2 * r2 * 0.30f + r3 * r3 * 0.15f;

        float hills = FastNoise.Fbm2(wx * 0.0065f, wz * 0.0065f, Seed + 400, 4, 0.5f, 2f);
        hills = Math.Clamp(hills * 2.5f, -1f, 1f);

        float dFine = FastNoise.Perlin2(wx * 0.090f, wz * 0.090f, Seed + 500);
        float dMed = FastNoise.Fbm2(wx * 0.028f, wz * 0.028f, Seed + 501, 2);

        if (biome is Biome.Plains or Biome.Desert)
            erosionMask *= 0.4f;
        else if (biome == Biome.Volcano)
            erosionMask = Math.Max(erosionMask, 0.6f);

        float sea = WaterLevel;
        float h;

        if (cont < 0f)
        {
            float depth = -cont;
            h = sea - depth * 32f;
            h += dMed * 1.5f + dFine * 0.6f;
        }
        else
        {
            float baseH = sea + cont * 16f;
            float landMask = SmoothStep(0f, 0.2f, cont);
            float hillsContribution = hills * 12f * landMask;

            float mtnCont = SmoothStep(0.10f, 0.70f, cont);
            float mtnMask = mtnCont * erosionMask;
            float mountainH = ridge * 65f * mtnMask;

            float biomeBoost = biome switch
            {
                Biome.Volcano => 6f,
                Biome.Plains or Biome.Desert => -2f,
                _ => 0f
            };

            h = baseH + hillsContribution + mountainH + biomeBoost;
            h += dMed * 2.0f + dFine * 1.2f;
        }

        if (biome == Biome.Desert)
        {
            float dune = FastNoise.Perlin2(wx * 0.06f, wz * 0.06f, Seed + 4100);
            dune = dune * 0.5f + 0.5f;
            h += dune * dune * 3f;
        }

        return Math.Clamp((int)h, 4, 145);
    }

    // ------------------------------------------------------------
    //  Пещеры (spaghetti + cheese)
    // ------------------------------------------------------------
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsCave(int wx, int wy, int wz, int surfaceY)
    {
        if (wy < 5 || wy > 120) return false;

        // 1) РАСШИРЯЕМ ТУННЕЛИ (Spaghetti caves)
        // Было 0.035f, стало 0.060f — туннели станут шире, и в них легче будет попасть.
        float n1 = FastNoise.Perlin3(wx * 0.018f, wy * 0.038f, wz * 0.018f, Seed + 5000);
        if (n1 * n1 > 0.060f) return false;

        float n2 = FastNoise.Perlin3(wx * 0.018f, wy * 0.038f, wz * 0.018f, Seed + 6000);
        // Было 0.008f, стало 0.025f — пещеры станут объемнее.
        bool isSpaghetti = n1 * n1 + n2 * n2 < 0.025f;

        if (isSpaghetti)
        {
            // 2) ОСЛАБЛЯЕМ МАСКУ ПОВЕРХНОСТИ
            if (wy > surfaceY - 8)
            {
                // Было 0.1f, стало -0.25f. 
                // Теперь пещера сможет пробить землю почти везде (кроме самых глубоких низин).
                float surfaceMask = FastNoise.Fbm2(wx * 0.01f, wz * 0.01f, Seed + 8000, 2);
                if (surfaceMask < -0.25f) return false;
            }

            return true;
        }

        // Cheese-каверны (крупные залы) — оставляем только глубоко под землей
        if (surfaceY - wy < 22) return false;
        float cheese = FastNoise.Perlin3(wx * 0.012f, wy * 0.024f, wz * 0.012f, Seed + 7000);
        return cheese > 0.58f; // Чуть-чуть увеличили частоту появления залов
    }

    // ------------------------------------------------------------
    //  Генерация чанка
    // ------------------------------------------------------------
    public Chunk GenerateChunk(ChunkPos pos)
    {
        var chunk = new Chunk { pos = pos };
        int wX0 = pos.X * Chunk.SIZE, wY0 = pos.Y * Chunk.SIZE, wZ0 = pos.Z * Chunk.SIZE;

        // 1) 2D heightmap + биомы
        Span<byte> bcol = stackalloc byte[Chunk.AREA];
        Span<int> hcol = stackalloc int[Chunk.AREA];
        for (int z = 0; z < Chunk.SIZE; z++)
        for (int x = 0; x < Chunk.SIZE; x++)
        {
            int i = x + z * Chunk.SIZE;
            var b = GetBiome(wX0 + x, wZ0 + z);
            bcol[i] = (byte)b;
            hcol[i] = GetHeight(wX0 + x, wZ0 + z, b);
            chunk.heights[i] = hcol[i];
        }

        // 2) Доминирующий биом
        Span<int> counts = stackalloc int[5];
        foreach (var b in bcol) counts[b]++;
        int maxI = 0;
        for (int k = 1; k < 5; k++)
            if (counts[k] > counts[maxI])
                maxI = k;
        chunk.biome = (Biome)maxI;

        // 3) Заполнение вокселей
        var data = chunk.data;
        bool mayHaveCaves = wY0 <= 52;

        for (int y = 0; y < Chunk.SIZE; y++)
        {
            int wy = wY0 + y;
            if (wy < 0 || wy >= WorldConfig.WorldHeight) continue;

            bool belowCaveZone = wy < 5;
            bool waterLayer = wy <= WaterLevel;

            for (int z = 0; z < Chunk.SIZE; z++)
            for (int x = 0; x < Chunk.SIZE; x++)
            {
                int i = x + z * Chunk.SIZE;
                int h = hcol[i];
                int dh = h - wy;
                int idx = Chunk.Index(x, y, z);
                byte b;

                if (dh >= 0)
                {
                    if (mayHaveCaves && !belowCaveZone && IsCave(wX0 + x, wy, wZ0 + z, h))
                    {
                        b = Blocks.Air;
                    }
                    else
                    {
                        b = SolidBlockFor((Biome)bcol[i], wy, dh);
                    }
                }
                else
                {
                    b = waterLayer ? Blocks.Water : Blocks.Air;
                }

                data[idx] = b;
            }
        }

        return chunk;
    }

    private byte SolidBlockFor(Biome b, int y, int depth)
    {
        if (y <= WaterLevel + 1)
        {
            if (b == Biome.Volcano && depth < 3) return Blocks.Basalt;
            return depth < 3 ? Blocks.Sand : Blocks.Stone;
        }

        if (depth < 1)
            return b switch
            {
                Biome.Plains or Biome.Forest => Blocks.Grass,
                Biome.Desert => Blocks.Sand,
                Biome.Ice => Blocks.Snow,
                Biome.Volcano => Blocks.Basalt,
                _ => Blocks.Stone
            };

        if (depth < 4)
            return b switch
            {
                Biome.Desert => Blocks.Sand,
                Biome.Ice => Blocks.Ice,
                Biome.Volcano => Blocks.Basalt,
                _ => Blocks.Dirt
            };

        return Blocks.Stone;
    }
}