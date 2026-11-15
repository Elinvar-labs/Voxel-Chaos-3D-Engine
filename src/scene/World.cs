using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using raymarching;
using Silk.NET.OpenGL;
using VoxelChaos.Core;
using VoxelChaos.graphics;
using VoxelChaos.Graphics;
using VoxelChaos.Voxeling;
using Shader = VoxelChaos.Graphics.Shader;

namespace VoxelChaos.scene;

public enum Biome : byte
{
    Plains = 0,
    Forest = 1,
    Desert = 2,
    Ice = 3,
    Volcano = 4
}

public static class Blocks
{
    public const byte Air = 0,
        Dirt = 1,
        Grass = 2,
        Stone = 3,
        Wood = 4,
        Leaves = 5,
        Sand = 6,
        Ice = 7,
        Snow = 8,
        Basalt = 9,
        Lava = 10,
        TempleStone = 11,
        Water = 12;
}

public readonly struct ChunkPos : IEquatable<ChunkPos>
{
    public readonly int X, Y, Z;

    public ChunkPos(int x, int y, int z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public bool Equals(ChunkPos o) => X == o.X && Y == o.Y && Z == o.Z;
    public override bool Equals(object? o) => o is ChunkPos p && Equals(p);
    public override int GetHashCode() => HashCode.Combine(X, Y, Z);
    public static bool operator ==(ChunkPos a, ChunkPos b) => a.Equals(b);
    public static bool operator !=(ChunkPos a, ChunkPos b) => !a.Equals(b);
}

public struct VoxelMod
{
    public int X, Y, Z;
    public byte Block;

    public VoxelMod(int x, int y, int z, byte b)
    {
        X = x;
        Y = y;
        Z = z;
        Block = b;
    }
}

public class Chunk
{
    public const int SIZE = 32;
    public const int SIZE_CUBED = SIZE * SIZE * SIZE;
    public const int SHIFT = 5;
    public const int MASK = SIZE - 1;

    // Layout: [x][y][z] => x + y*32 + z*32*32 (x-fastest — совпадает с OpenGL 3D-текстурой)
    public readonly byte[] data = new byte[SIZE_CUBED];
    public readonly int[] heights = new int[SIZE * SIZE]; // [x + z*SIZE] — поверхность (heightmap, 1.7.10-style)
    public ChunkPos pos;
    public Biome biome;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Index(int x, int y, int z) => (x & MASK) | ((y & MASK) << SHIFT) | ((z & MASK) << (SHIFT * 2));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Set(int x, int y, int z, byte b) => data[Index(x, y, z)] = b;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte Get(int x, int y, int z) => data[Index(x, y, z)];
}

// ---------- RNG по координатам чанка (детерминированный) ----------
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

// ---------- Быстрый Perlin ----------
public static class FastNoise
{
    private const int K1 = unchecked((int)0x27d4eb2d);
    private const int K2 = unchecked((int)0x85ebca6b);
    private const int K3 = unchecked((int)0xc2b2ae35);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int H2(int x, int y, int s)
    {
        unchecked
        {
            int h = s ^ x * K1;
            h ^= y * K2;
            h ^= h >> 15;
            h *= K3;
            return h ^ (h >> 13);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int H3(int x, int y, int z, int s)
    {
        unchecked
        {
            int h = s ^ x * K1;
            h ^= y * K2;
            h ^= z * K3;
            h ^= h >> 15;
            h *= K1;
            return h ^ (h >> 13);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float Fade(float t) => t * t * t * (t * (t * 6f - 15f) + 10f);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float Lerp(float a, float b, float t) => a + (b - a) * t;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float G2(int h, float x, float y)
    {
        switch (h & 7)
        {
            case 0: return x + y;
            case 1: return x - y;
            case 2: return -x + y;
            case 3: return -x - y;
            case 4: return x;
            case 5: return -x;
            case 6: return y;
            default: return -y;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float G3(int h, float x, float y, float z)
    {
        switch (h & 15)
        {
            case 0: return x + y;
            case 1: return -x + y;
            case 2: return x - y;
            case 3: return -x - y;
            case 4: return x + z;
            case 5: return -x + z;
            case 6: return x - z;
            case 7: return -x - z;
            case 8: return y + z;
            case 9: return -y + z;
            case 10: return y - z;
            case 11: return -y - z;
            case 12: return x + y;
            case 13: return -x + y;
            case 14: return y - z;
            default: return -y - z;
        }
    }

    public static float Perlin2(float x, float y, int seed)
    {
        int xi = (int)MathF.Floor(x), yi = (int)MathF.Floor(y);
        float xf = x - xi, yf = y - yi;
        float u = Fade(xf), v = Fade(yf);
        int aa = H2(xi, yi, seed), ab = H2(xi, yi + 1, seed);
        int ba = H2(xi + 1, yi, seed), bb = H2(xi + 1, yi + 1, seed);
        float x1 = Lerp(G2(aa, xf, yf), G2(ba, xf - 1, yf), u);
        float x2 = Lerp(G2(ab, xf, yf - 1), G2(bb, xf - 1, yf - 1), u);
        return Lerp(x1, x2, v) * 0.75f;
    }

    public static float Perlin3(float x, float y, float z, int seed)
    {
        int xi = (int)MathF.Floor(x), yi = (int)MathF.Floor(y), zi = (int)MathF.Floor(z);
        float xf = x - xi, yf = y - yi, zf = z - zi;
        float u = Fade(xf), v = Fade(yf), w = Fade(zf);
        int aaa = H3(xi, yi, zi, seed), aba = H3(xi, yi + 1, zi, seed);
        int aab = H3(xi, yi, zi + 1, seed), abb = H3(xi, yi + 1, zi + 1, seed);
        int baa = H3(xi + 1, yi, zi, seed), bba = H3(xi + 1, yi + 1, zi, seed);
        int bab = H3(xi + 1, yi, zi + 1, seed), bbb = H3(xi + 1, yi + 1, zi + 1, seed);
        float x1 = Lerp(G3(aaa, xf, yf, zf), G3(baa, xf - 1, yf, zf), u);
        float x2 = Lerp(G3(aba, xf, yf - 1, zf), G3(bba, xf - 1, yf - 1, zf), u);
        float x3 = Lerp(G3(aab, xf, yf, zf - 1), G3(bab, xf - 1, yf, zf - 1), u);
        float x4 = Lerp(G3(abb, xf, yf - 1, zf - 1), G3(bbb, xf - 1, yf - 1, zf - 1), u);
        return Lerp(Lerp(x1, x2, v), Lerp(x3, x4, v), w) * 0.95f;
    }

    public static float Fbm2(float x, float y, int seed, int oct, float pers = 0.5f, float lac = 2f)
    {
        float s = 0, a = 1, f = 1, n = 0;
        for (int i = 0; i < oct; i++)
        {
            s += Perlin2(x * f, y * f, seed + i * 1013) * a;
            n += a;
            a *= pers;
            f *= lac;
        }

        return s / n;
    }
}

// ============================================================
//   ГЕНЕРАТОР TERRAIN — Minecraft 1.7.10 style (pure heightmap)
// ============================================================
public sealed class TerrainGenerator
{
    public const int WATER_LEVEL = 62;
    public int Seed;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float SmoothStep(float e0, float e1, float x)
    {
        float t = Math.Clamp((x - e0) / (e1 - e0), 0f, 1f);
        return t * t * (3f - 2f * t);
    }

    // -------- Биом (3 климатических шкалы) --------
    public Biome GetBiome(int wx, int wz)
    {
        float temp = FastNoise.Fbm2(wx * 0.0016f, wz * 0.0016f, Seed + 1000, 3);
        float humid = FastNoise.Fbm2(wx * 0.0018f, wz * 0.0018f, Seed + 2000, 3);
        float rare = FastNoise.Fbm2(wx * 0.0007f, wz * 0.0007f, Seed + 3000, 2);

        if (rare > 0.4f)
        {
            if (temp < -0.2f) return Biome.Ice;
            if (temp > 0.25f) return Biome.Volcano;
        }

        if (temp > 0.2f) return Biome.Desert;
        if (humid > 0.05f) return Biome.Forest;
        return Biome.Plains;
    }

    // ============================================================
    //   1.7.10-like: чистая 2D heightmap (без 3D-плотности/нависаний)
    // ============================================================
    public int GetHeight(int wx, int wz, Biome biome)
    {
        // === Domain-warp для "изрезанных" берегов (как в старых MC-версиях) ===
        float warpX = FastNoise.Fbm2(wx * 0.0030f, wz * 0.0030f, Seed + 5000, 2) * 35f;
        float warpZ = FastNoise.Fbm2(wx * 0.0030f, wz * 0.0030f, Seed + 5001, 2) * 35f;

        // === Continentalness: океан vs суша ===
        // Высокая частота => меньше размер «континентов», меньше огромных океанов.
        // Положительный bias => суши больше, океанов меньше.
        float cont = FastNoise.Fbm2(
            (wx + warpX) * 0.0028f,
            (wz + warpZ) * 0.0028f,
            Seed + 100, 4, 0.5f, 2f);
        cont = cont * 1.8f + 0.30f;
        cont = Math.Clamp(cont, -1.5f, 1.3f);

        // === Erosion: плоские равнины vs гористые районы ===
        float erosion = FastNoise.Fbm2(wx * 0.0032f, wz * 0.0032f, Seed + 200, 3, 0.5f, 2f);
        float erosionMask = SmoothStep(0.35f, -0.35f, erosion); // 0 = плоско, 1 = рвано

        // === Ridged-noise: острые хребты ===
        float r1 = 1f - MathF.Abs(FastNoise.Perlin2(wx * 0.0045f, wz * 0.0045f, Seed + 300));
        float r2 = 1f - MathF.Abs(FastNoise.Perlin2(wx * 0.0110f, wz * 0.0110f, Seed + 301));
        float r3 = 1f - MathF.Abs(FastNoise.Perlin2(wx * 0.0260f, wz * 0.0260f, Seed + 302));
        // r*r — обострение пиков (важно для "старых" MC-гор)
        float ridge = r1 * r1 * 0.55f + r2 * r2 * 0.30f + r3 * r3 * 0.15f;

        // === Локальные холмы ===
        float hills = FastNoise.Fbm2(wx * 0.0065f, wz * 0.0065f, Seed + 400, 4, 0.5f, 2f);
        hills = Math.Clamp(hills * 2.5f, -1f, 1f);

        // === Детализация поверхности ===
        float dFine = FastNoise.Perlin2(wx * 0.090f, wz * 0.090f, Seed + 500);
        float dMed = FastNoise.Fbm2(wx * 0.028f, wz * 0.028f, Seed + 501, 2);

        // === Биом-специфичная крутизна ===
        if (biome == Biome.Plains || biome == Biome.Desert)
            erosionMask *= 0.4f;                     // равнины/пустыни — в основном плоские
        else if (biome == Biome.Volcano)
            erosionMask = Math.Max(erosionMask, 0.6f); // «extreme hills» — почти всегда рвано

        // === Собираем высоту ===
        const float SEA = WATER_LEVEL;
        float h;

        if (cont < 0f)
        {
            // Океан/побережье
            float depth = -cont;                     // 0 у берега → 1.5 в глубине
            h = SEA - depth * 32f;                   // до ~Y=14
            h += dMed * 1.5f + dFine * 0.6f;
        }
        else
        {
            // Суша
            float baseH = SEA + cont * 16f;          // 62 → ~78 в глубине
            float landMask = SmoothStep(0f, 0.2f, cont);
            float hillsContribution = hills * 12f * landMask;

            // Маска гор: чем глубже на суше и чем сильнее erosion — тем выше
            float mtnCont = SmoothStep(0.10f, 0.70f, cont);
            float mtnMask = mtnCont * erosionMask;

            // Высокие острые горы — до +65 блоков над базой
            float mountainH = ridge * 65f * mtnMask;

            // Биомные смещения
            float biomeBoost = 0f;
            if (biome == Biome.Volcano) biomeBoost = 6f;
            else if (biome == Biome.Plains || biome == Biome.Desert) biomeBoost = -2f;

            h = baseH + hillsContribution + mountainH + biomeBoost;
            h += dMed * 2.0f + dFine * 1.2f;
        }

        // === Дюны для пустыни ===
        if (biome == Biome.Desert)
        {
            float dune = FastNoise.Perlin2(wx * 0.06f, wz * 0.06f, Seed + 4100);
            dune = dune * 0.5f + 0.5f; // 0..1
            h += dune * dune * 3f;
        }

        return Math.Clamp((int)h, 4, 145);
    }

    // -------- Пещеры (spaghetti + cheese), как в старых MC --------
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsCave(int wx, int wy, int wz, int surfaceY)
    {
        if (wy < 5 || wy > 52) return false;
        if (wy > surfaceY - 8) return false;

        // «Червь» из двух шумов
        float n1 = FastNoise.Perlin3(wx * 0.018f, wy * 0.038f, wz * 0.018f, Seed + 5000);
        if (n1 * n1 > 0.022f) return false;

        float n2 = FastNoise.Perlin3(wx * 0.018f, wy * 0.038f, wz * 0.018f, Seed + 6000);
        if (n1 * n1 + n2 * n2 < 0.005f) return true;

        // Cheese-каверны — только глубоко
        if (surfaceY - wy < 22) return false;
        float cheese = FastNoise.Perlin3(wx * 0.012f, wy * 0.024f, wz * 0.012f, Seed + 7000);
        return cheese > 0.60f;
    }

    // ============================================================
    //          Заполнение чанка (pure-heightmap 1.7.10-style)
    // ============================================================
    public Chunk GenerateChunk(ChunkPos pos)
    {
        var chunk = new Chunk { pos = pos };
        int wX0 = pos.X * Chunk.SIZE, wY0 = pos.Y * Chunk.SIZE, wZ0 = pos.Z * Chunk.SIZE;

        // === 1) Биомы и высоты (2D heightmap) ===
        Span<byte> bcol = stackalloc byte[Chunk.SIZE * Chunk.SIZE];
        Span<int> hcol = stackalloc int[Chunk.SIZE * Chunk.SIZE];
        for (int z = 0; z < Chunk.SIZE; z++)
        for (int x = 0; x < Chunk.SIZE; x++)
        {
            int i = x + z * Chunk.SIZE;
            var b = GetBiome(wX0 + x, wZ0 + z);
            bcol[i] = (byte)b;
            hcol[i] = GetHeight(wX0 + x, wZ0 + z, b);
            chunk.heights[i] = hcol[i]; // surface = heightmap (для структур)
        }

        // === 2) Доминирующий биом чанка ===
        Span<int> counts = stackalloc int[5];
        for (int i = 0; i < bcol.Length; i++) counts[bcol[i]]++;
        int maxI = 0;
        for (int k = 1; k < 5; k++) if (counts[k] > counts[maxI]) maxI = k;
        chunk.biome = (Biome)maxI;

        // === 3) Заполнение вокселей ===
        var data = chunk.data;
        bool mayHaveCaves = wY0 <= 52;

        for (int y = 0; y < Chunk.SIZE; y++)
        {
            int wy = wY0 + y;
            if (wy < 0 || wy > 127) continue;

            bool belowCaveZone = wy < 5;
            bool waterLayer = wy <= WATER_LEVEL;

            for (int z = 0; z < Chunk.SIZE; z++)
            for (int x = 0; x < Chunk.SIZE; x++)
            {
                int i = x + z * Chunk.SIZE;
                int h = hcol[i];
                int dh = h - wy;   // dh > 0 — ниже поверхности (твёрдо), dh < 0 — выше (воздух)
                int idx = Chunk.Index(x, y, z);
                byte b;

                if (dh >= 0)
                {
                    // --- Твёрдый блок ---
                    if (mayHaveCaves && dh > 3 && !belowCaveZone
                        && wy < h - 8
                        && IsCave(wX0 + x, wy, wZ0 + z, h))
                    {
                        b = Blocks.Air;
                    }
                    else
                    {
                        b = SolidBlockFor((Biome)bcol[i], wy, dh, h);
                    }
                }
                else
                {
                    // --- Над поверхностью ---
                    b = waterLayer ? Blocks.Water : Blocks.Air;
                }

                data[idx] = b;
            }
        }

        return chunk;
    }

    // -------- Поверхностные блоки (по глубине от поверхности) --------
    // depth = h - wy (0 = поверхность, 1..N = глубже)
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte SolidBlockFor(Biome b, int y, int depth, int h)
    {
        // Под водой / на пляже — песок
        if (y <= WATER_LEVEL + 1)
        {
            if (b == Biome.Volcano && depth < 3) return Blocks.Basalt;
            if (depth < 3) return Blocks.Sand;
            return Blocks.Stone;
        }

        // Верхний слой (поверхность суши)
        if (depth < 1)
        {
            switch (b)
            {
                case Biome.Plains:
                case Biome.Forest:  return Blocks.Grass;
                case Biome.Desert:  return Blocks.Sand;
                case Biome.Ice:     return Blocks.Snow;
                case Biome.Volcano: return Blocks.Basalt;
            }
        }

        // Подповерхностный слой (1..3 блока глубиной)
        if (depth < 4)
        {
            switch (b)
            {
                case Biome.Desert:  return Blocks.Sand;
                case Biome.Ice:     return Blocks.Ice;
                case Biome.Volcano: return Blocks.Basalt;
                default:            return Blocks.Dirt;
            }
        }

        // Глубина — камень
        return Blocks.Stone;
    }
}

// ============================================================
public class World
{
    // ----- Размеры -----
    private const int CHUNK_SIZE = Chunk.SIZE;
    private const int WORLD_HEIGHT_CH = 4;
    private const int VOL_CHUNKS_X = 8;
    private const int VOL_CHUNKS_Y = 4;
    private const int VOL_CHUNKS_Z = 8;
    private const int VOL_SIZE_X = VOL_CHUNKS_X * CHUNK_SIZE;
    private const int VOL_SIZE_Y = VOL_CHUNKS_Y * CHUNK_SIZE;
    private const int VOL_SIZE_Z = VOL_CHUNKS_Z * CHUNK_SIZE;

    private uint renderWidth, renderHeight, realWidth, realHeight;
    private PixelRenderer renderer;
    private GL _gl;
    private byte[] screenImage;

    private string computeShaderPath = "assets/shaders/DDA_Voxeling.glsl";
    private Shader DDAShader;
    private ByteShaderStorageBuffer _ssbo;
    private VoxelTexture3D voxelTexture3D;
    private TextureAtlas _atlas;
    private BlockPalette _palette;

    public Camera camera;

    private int worldSeed = 1337;
    private readonly TerrainGenerator generator;
    private readonly Dictionary<ChunkPos, Chunk> loadedChunks = new();
    private readonly Dictionary<ChunkPos, List<VoxelMod>> spillover = new();

    private int volOriginX, volOriginY, volOriginZ;
    private bool volumeDirty = true;

    private readonly byte[] volumeData = new byte[VOL_SIZE_X * VOL_SIZE_Y * VOL_SIZE_Z];

    public World(GL gl, float camX, float camY, float camZ, uint realWidth, uint realHeight,
        uint renderWidth, uint renderHeight)
    {
        this._gl = gl;
        this.renderWidth = renderWidth;
        this.renderHeight = renderHeight;
        this.realWidth = realWidth;
        this.realHeight = realHeight;

        camera = new Camera(new Vector3(camX, camY, camZ), 60, renderWidth, renderHeight);

        renderer = new PixelRenderer();
        renderer.Initialize(gl, renderWidth, renderHeight, realWidth, realHeight);
        screenImage = new byte[renderWidth * renderHeight * 4];

        _ssbo = new ByteShaderStorageBuffer(gl);
        _ssbo.Initialize(screenImage);

        DDAShader = new Shader(gl, computeShaderPath);
        voxelTexture3D = new VoxelTexture3D(gl);

        _atlas = new TextureAtlas(gl, "assets/atlas.png");
        _palette = new BlockPalette(gl);
        ConfigurePalette();

        generator = new TerrainGenerator { Seed = worldSeed };

        UpdateStreaming();
    }

    private void ConfigurePalette()
    {
        _palette.SetBlock(Blocks.Dirt, top: 55, bottom: 55, side: 55);
        _palette.SetBlock(Blocks.Grass, top: 83, bottom: 55, side: 83);
        _palette.SetBlock(Blocks.Stone, top: 128, bottom: 128, side: 128);
        _palette.SetBlock(Blocks.Wood, top: 132, bottom: 132, side: 131);
        _palette.SetBlock(Blocks.Leaves, top: 96, bottom: 96, side: 96);
        _palette.SetBlock(Blocks.Sand, top: 116, bottom: 116, side: 116);
        _palette.SetBlock(Blocks.Ice, top: 87, bottom: 87, side: 87);
        _palette.SetBlock(Blocks.Snow, top: 125, bottom: 125, side: 125);
        _palette.SetBlock(Blocks.Basalt, top: 102, bottom: 102, side: 102);
        _palette.SetBlock(Blocks.Lava, top: 94, bottom: 94, side: 94);
        _palette.SetBlock(Blocks.TempleStone, top: 118, bottom: 118, side: 118);
        _palette.SetBlock(Blocks.Water, top: 133, bottom: 133, side: 133);
        _palette.Upload();
    }

    // ============================================================
    public void Update()
    {
        camera.Update();

        var p = camera.GetPosition();
        if (p.Y < 3f)
            camera.SetPosition(new Vector3(p.X, 3f, p.Z));
        if (p.Y > 140f)
            camera.SetPosition(new Vector3(p.X, 140f, p.Z));

        UpdateStreaming();
    }

    private void UpdateStreaming()
    {
        var camPos = camera.GetPosition();
        int camCX = (int)MathF.Floor(camPos.X / CHUNK_SIZE);
        int camCY = (int)MathF.Floor(camPos.Y / CHUNK_SIZE);
        int camCZ = (int)MathF.Floor(camPos.Z / CHUNK_SIZE);

        int newOX = camCX - VOL_CHUNKS_X / 2;
        int newOY = Math.Clamp(camCY - VOL_CHUNKS_Y / 2, 0, WORLD_HEIGHT_CH - VOL_CHUNKS_Y);
        int newOZ = camCZ - VOL_CHUNKS_Z / 2;

        if (!volumeDirty && newOX == volOriginX && newOY == volOriginY && newOZ == volOriginZ)
            return;

        bool originChanged = newOX != volOriginX || newOY != volOriginY || newOZ != volOriginZ;
        volOriginX = newOX;
        volOriginY = newOY;
        volOriginZ = newOZ;

        var toGen = new List<ChunkPos>(32);
        for (int cx = 0; cx < VOL_CHUNKS_X; cx++)
        for (int cy = 0; cy < VOL_CHUNKS_Y; cy++)
        for (int cz = 0; cz < VOL_CHUNKS_Z; cz++)
        {
            var pos = new ChunkPos(volOriginX + cx, volOriginY + cy, volOriginZ + cz);
            if (!loadedChunks.ContainsKey(pos)) toGen.Add(pos);
        }

        if (toGen.Count > 0)
        {
            var fresh = new Chunk[toGen.Count];
            Parallel.For(0, toGen.Count, i => fresh[i] = generator.GenerateChunk(toGen[i]));
            for (int i = 0; i < fresh.Length; i++) loadedChunks[toGen[i]] = fresh[i];

            foreach (var ch in fresh) PlaceStructures(ch);

            foreach (var kv in loadedChunks)
            {
                if (!spillover.TryGetValue(kv.Key, out var mods)) continue;
                var ch = kv.Value;
                for (int i = 0; i < mods.Count; i++)
                {
                    var m = mods[i];
                    int lx = m.X & Chunk.MASK, ly = m.Y & Chunk.MASK, lz = m.Z & Chunk.MASK;
                    ch.Set(lx, ly, lz, m.Block);
                }
            }

            volumeDirty = true;
        }

        if (originChanged)
        {
            var toDel = new List<ChunkPos>();
            foreach (var pos in loadedChunks.Keys)
            {
                if (pos.X < volOriginX || pos.X >= volOriginX + VOL_CHUNKS_X ||
                    pos.Y < volOriginY || pos.Y >= volOriginY + VOL_CHUNKS_Y ||
                    pos.Z < volOriginZ || pos.Z >= volOriginZ + VOL_CHUNKS_Z)
                    toDel.Add(pos);
            }

            foreach (var d in toDel) loadedChunks.Remove(d);
        }

        if (volumeDirty) RebuildVolume();
    }

    private void SetWorld(Chunk current, int wx, int wy, int wz, byte block)
    {
        if (wy < 0 || wy > 127) return;
        int cx = wx >> 5, cy = wy >> 5, cz = wz >> 5;
        if (cx == current.pos.X && cy == current.pos.Y && cz == current.pos.Z)
            current.Set(wx & 31, wy & 31, wz & 31, block);
        else
        {
            var dp = new ChunkPos(cx, cy, cz);
            if (!spillover.TryGetValue(dp, out var list))
                spillover[dp] = list = new List<VoxelMod>(8);
            list.Add(new VoxelMod(wx, wy, wz, block));
        }
    }

    private void PlaceStructures(Chunk chunk)
    {
        var rng = new ChunkRng(worldSeed, chunk.pos);
        int bx = chunk.pos.X * CHUNK_SIZE, bz = chunk.pos.Z * CHUNK_SIZE;

        switch (chunk.biome)
        {
            case Biome.Forest:
            {
                int n = rng.NextInt(2, 5);
                for (int t = 0; t < n; t++)
                {
                    int lx = rng.NextInt(2, 30), lz = rng.NextInt(2, 30);
                    int surfY = chunk.heights[lx + lz * CHUNK_SIZE];
                    if (surfY < TerrainGenerator.WATER_LEVEL + 1) continue;
                    PlaceTree(chunk, bx + lx, bz + lz, surfY, ref rng);
                }
                break;
            }
            case Biome.Plains:
                if (chunk.heights[16 + 16 * CHUNK_SIZE] >= TerrainGenerator.WATER_LEVEL + 1
                    && rng.NextFloat() < 0.10f)
                    PlaceRuins(chunk, bx, bz, ref rng);
                break;
            case Biome.Desert:
                if (chunk.heights[16 + 16 * CHUNK_SIZE] >= TerrainGenerator.WATER_LEVEL + 1
                    && rng.NextFloat() < 0.15f)
                    PlaceDesertTemple(chunk, bx + 16, bz + 16, ref rng);
                break;
            case Biome.Ice:
                if (chunk.heights[16 + 16 * CHUNK_SIZE] >= TerrainGenerator.WATER_LEVEL + 1
                    && rng.NextFloat() < 0.40f)
                    PlaceIceSpikes(chunk, bx, bz, ref rng);
                break;
            case Biome.Volcano:
                if (chunk.heights[16 + 16 * CHUNK_SIZE] >= TerrainGenerator.WATER_LEVEL + 1
                    && rng.NextFloat() < 0.20f)
                    PlaceVolcano(chunk, bx + 16, bz + 16, ref rng);
                break;
        }
    }

    private void PlaceTree(Chunk chunk, int wx, int wz, int surfY, ref ChunkRng rng)
    {
        int h = rng.NextInt(4, 7);
        for (int y = 1; y <= h; y++) SetWorld(chunk, wx, surfY + y, wz, Blocks.Wood);
        int top = surfY + h;
        for (int dy = -2; dy <= 2; dy++)
        for (int dx = -2; dx <= 2; dx++)
        for (int dz = -2; dz <= 2; dz++)
        {
            int md = Math.Abs(dx) + Math.Abs(dy) + Math.Abs(dz);
            if (md <= 3 && !(dx == 0 && dz == 0 && dy <= 0))
                SetWorld(chunk, wx + dx, top + dy, wz + dz, Blocks.Leaves);
        }
    }

    private void PlaceRuins(Chunk chunk, int bx, int bz, ref ChunkRng rng)
    {
        int lx = 8 + rng.NextInt(0, 16), lz = 8 + rng.NextInt(0, 16);
        int surf = chunk.heights[lx + lz * CHUNK_SIZE];
        int cx = bx + lx, cz = bz + lz;
        int size = rng.NextInt(4, 7);

        for (int i = -size; i <= size; i++)
        {
            if (rng.NextFloat() > 0.35f) SetWorld(chunk, cx + i, surf + 1, cz - size, Blocks.Stone);
            if (rng.NextFloat() > 0.35f) SetWorld(chunk, cx + i, surf + 1, cz + size, Blocks.Stone);
            if (rng.NextFloat() > 0.35f) SetWorld(chunk, cx - size, surf + 1, cz + i, Blocks.Stone);
            if (rng.NextFloat() > 0.35f) SetWorld(chunk, cx + size, surf + 1, cz + i, Blocks.Stone);
        }

        for (int p = 0; p < 4; p++)
        {
            int px = cx + rng.NextInt(-size, size + 1);
            int pz = cz + rng.NextInt(-size, size + 1);
            int ph = rng.NextInt(2, 4);
            for (int y = 1; y <= ph; y++) SetWorld(chunk, px, surf + y, pz, Blocks.Stone);
        }
    }

    private void PlaceDesertTemple(Chunk chunk, int cx, int cz, ref ChunkRng rng)
    {
        int surf = chunk.heights[16 + 16 * CHUNK_SIZE];
        for (int layer = 0; layer < 4; layer++)
        {
            int half = 4 - layer, ly = surf + 1 + layer;
            for (int dx = -half; dx <= half; dx++)
            for (int dz = -half; dz <= half; dz++)
            {
                bool shell = Math.Abs(dx) == half || Math.Abs(dz) == half || layer == 3;
                if (shell) SetWorld(chunk, cx + dx, ly, cz + dz, Blocks.TempleStone);
            }
        }

        for (int y = 1; y <= 2; y++)
        for (int dx = -1; dx <= 1; dx++)
        for (int dz = -1; dz <= 1; dz++)
            SetWorld(chunk, cx + dx, surf + y, cz + dz, Blocks.Air);
    }

    private void PlaceIceSpikes(Chunk chunk, int bx, int bz, ref ChunkRng rng)
    {
        int n = rng.NextInt(2, 5);
        for (int s = 0; s < n; s++)
        {
            int lx = 4 + rng.NextInt(0, 24), lz = 4 + rng.NextInt(0, 24);
            int surf = chunk.heights[lx + lz * CHUNK_SIZE];
            int h = rng.NextInt(5, 15);
            for (int y = 0; y < h; y++)
            {
                int r = Math.Max(0, (h - y) / 4);
                for (int dx = -r; dx <= r; dx++)
                for (int dz = -r; dz <= r; dz++)
                    if (dx * dx + dz * dz <= r * r + 1)
                        SetWorld(chunk, bx + lx + dx, surf + 1 + y, bz + lz + dz, Blocks.Ice);
            }
        }
    }

    private void PlaceVolcano(Chunk chunk, int cx, int cz, ref ChunkRng rng)
    {
        int surf = chunk.heights[16 + 16 * CHUNK_SIZE];
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
                if (!crater) SetWorld(chunk, cx + dx, surf + y, cz + dz, Blocks.Basalt);
            }
        }

        int cy = surf + height - 1;
        for (int dx = -2; dx <= 2; dx++)
        for (int dz = -2; dz <= 2; dz++)
            if (dx * dx + dz * dz <= 4)
                SetWorld(chunk, cx + dx, cy, cz + dz, Blocks.Lava);
    }

    // ============================================================
    private void RebuildVolume()
    {
        Array.Clear(volumeData);
        int total = VOL_CHUNKS_X * VOL_CHUNKS_Y * VOL_CHUNKS_Z;

        Parallel.For(0, total, i =>
        {
            int cz = i / (VOL_CHUNKS_X * VOL_CHUNKS_Y);
            int rem = i % (VOL_CHUNKS_X * VOL_CHUNKS_Y);
            int cy = rem / VOL_CHUNKS_X;
            int cx = rem % VOL_CHUNKS_X;

            var pos = new ChunkPos(volOriginX + cx, volOriginY + cy, volOriginZ + cz);
            if (!loadedChunks.TryGetValue(pos, out var chunk)) return;

            int vbx = cx * CHUNK_SIZE;
            int vby = cy * CHUNK_SIZE;
            int vbz = cz * CHUNK_SIZE;

            var src = chunk.data;
            var dst = volumeData;
            for (int y = 0; y < CHUNK_SIZE; y++)
            for (int z = 0; z < CHUNK_SIZE; z++)
            {
                int srcOff = (y * CHUNK_SIZE) + (z * CHUNK_SIZE * CHUNK_SIZE);
                int dstOff = vbx + (vby + y) * VOL_SIZE_X + (vbz + z) * VOL_SIZE_X * VOL_SIZE_Y;
                System.Buffer.BlockCopy(src, srcOff, dst, dstOff, CHUNK_SIZE);
            }
        });
    }

    // ============================================================
    public void Render()
    {
        Array.Fill(screenImage, (byte)255);

        DDAShader.Bind();

        if (volumeDirty)
        {
            voxelTexture3D.Update(volumeData, VOL_SIZE_X, VOL_SIZE_Y, VOL_SIZE_Z);
            volumeDirty = false;
        }

        voxelTexture3D.Bind(DDAShader);

        DDAShader.SetUniformFloat("volOriginX", volOriginX * CHUNK_SIZE);
        DDAShader.SetUniformFloat("volOriginY", volOriginY * CHUNK_SIZE);
        DDAShader.SetUniformFloat("volOriginZ", volOriginZ * CHUNK_SIZE);
        DDAShader.SetUniformInt("volSizeX", VOL_SIZE_X);
        DDAShader.SetUniformInt("volSizeY", VOL_SIZE_Y);
        DDAShader.SetUniformInt("volSizeZ", VOL_SIZE_Z);

        _atlas.Bind(DDAShader, 1);
        _palette.Bind();

        camera.Render(DDAShader);
        GetScreenImageFromShader();
        renderer.UploadLowResImage(screenImage, renderWidth, renderHeight);
        renderer.Render(realWidth, realHeight);
    }

    private void GetScreenImageFromShader()
    {
        int totalPixels = screenImage.Length / 4;
        uint groupsX = (uint)Math.Ceiling((double)totalPixels / 256);

        DDAShader.Bind();
        _ssbo.Bind();
        _gl.BindBufferBase(BufferTargetARB.ShaderStorageBuffer, 0, _ssbo.GetID());
        DDAShader.SetUniformInt("screenWidth", (int)renderWidth);
        DDAShader.SetUniformInt("screenHeight", (int)renderHeight);
        _gl.DispatchCompute(groupsX, 1, 1);
        _ssbo.DownloadData(screenImage);
    }
}