namespace VoxelChaos.Rendering;

internal static class UiPainter
{
    /// <summary>
    /// Рисует прямоугольник, но с инверсией по X.
    /// Это компенсирует глобальное зеркало экрана (см. PixelRenderer / шейдер).
    /// </summary>
    public static void FillRect(byte[] img, int imgW, int imgH,
        int x0, int y0, int w, int h, byte r, byte g, byte b, byte a = 255)
    {
        int y1 = Math.Min(y0 + h, imgH);
        y0 = Math.Max(y0, 0);

        for (int py = y0; py < y1; py++)
        {
            int row = py * imgW * 4;
            for (int dx = 0; dx < w; dx++)
            {
                int px = x0 + dx;
                int flippedPx = imgW - 1 - px;   // ← ИНВЕРСИЯ X
                if (flippedPx < 0 || flippedPx >= imgW) continue;

                int i = row + flippedPx * 4;
                img[i]     = r;
                img[i + 1] = g;
                img[i + 2] = b;
                img[i + 3] = a;
            }
        }
    }
}