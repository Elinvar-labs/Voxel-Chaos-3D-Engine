using System.Runtime.CompilerServices;

namespace VoxelChaos.Noise;

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
    private static float G2(int h, float x, float y) => (h & 7) switch
    {
        0 => x + y, 1 => x - y, 2 => -x + y, 3 => -x - y,
        4 => x,     5 => -x,    6 => y,      _ => -y
    };

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float G3(int h, float x, float y, float z) => (h & 15) switch
    {
        0  => x + y,  1  => -x + y,  2  => x - y,  3  => -x - y,
        4  => x + z,  5  => -x + z,  6  => x - z,  7  => -x - z,
        8  => y + z,  9  => -y + z,  10 => y - z,  11 => -y - z,
        12 => x + y,  13 => -x + y,  14 => y - z,  _  => -y - z
    };

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
        int aaa = H3(xi, yi, zi, seed),     aba = H3(xi, yi + 1, zi, seed);
        int aab = H3(xi, yi, zi + 1, seed), abb = H3(xi, yi + 1, zi + 1, seed);
        int baa = H3(xi + 1, yi, zi, seed), bba = H3(xi + 1, yi + 1, zi, seed);
        int bab = H3(xi + 1, yi, zi + 1, seed), bbb = H3(xi + 1, yi + 1, zi + 1, seed);
        float x1 = Lerp(G3(aaa, xf, yf, zf),     G3(baa, xf - 1, yf, zf),     u);
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