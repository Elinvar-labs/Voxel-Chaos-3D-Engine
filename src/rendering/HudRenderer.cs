using System.Numerics;
using VoxelChaos.Gameplay;

namespace VoxelChaos.Rendering;

public sealed class HudRenderer
{
    // ---------- Ориентация глифов ----------
    // Баг-репорт: «шрифты отзеркалены по оси OY» → горизонтальное зеркало.
    // Если вместо этого глифы перевёрнуты сверху-вниз (OX) — переключи FlipGlyphY.
    private const bool FlipGlyphX = false;
    private const bool FlipGlyphY = false;

    /// <summary>9 пикселей в центре (3×3), 3 строки кода.</summary>
    public void DrawCrosshair(byte[] img, int w, int h)
    {
        int cx = w / 2 - 1, cy = h / 2 - 1;
        UiPainter.FillRect(img, w, h, cx, cy, 3, 1, 255, 255, 255, 220);
        UiPainter.FillRect(img, w, h, cx, cy + 1, 3, 1, 255, 255, 255, 220);
        UiPainter.FillRect(img, w, h, cx, cy + 2, 3, 1, 255, 255, 255, 220);
    }

    /// <summary>Debug HUD: координаты, режим, FPS (левая верхняя четверть).</summary>
    public void DrawDebug(byte[] img, int w, int h, Vector3 pos, GameMode mode, double fps)
    {
        const int x = 6, y0 = 6;
        const int line = BitmapFont5x7.CharHeight + 2;

        DrawTextShadow(img, w, h, x, y0 + 0 * line, $"XYZ: {pos.X:F0} {pos.Y:F0} {pos.Z:F0}");
        DrawTextShadow(img, w, h, x, y0 + 1 * line, $"MODE: {mode}");
        DrawTextShadow(img, w, h, x, y0 + 2 * line, $"FPS: {fps:F0}");
    }

    public static void DrawTextShadow(byte[] img, int imgW, int imgH, int x, int y, string text)
    {
        DrawText(img, imgW, imgH, x + 1, y + 1, text, 0, 0, 0, 200);
        DrawText(img, imgW, imgH, x, y, text, 255, 255, 255, 255);
    }

    public static void DrawText(byte[] img, int imgW, int imgH, int x, int y, string text,
        byte r, byte g, byte b, byte a = 255)
    {
        int cx = x;
        foreach (var ch in text)
        {
            if (BitmapFont5x7.TryGetGlyph(ch, out var rows))
            {
                for (int ry = 0; ry < BitmapFont5x7.CharHeight; ry++)
                {
                    byte row = rows[FlipGlyphY ? (BitmapFont5x7.CharHeight - 1 - ry) : ry];

                    for (int rx = 0; rx < BitmapFont5x7.CharWidth; rx++)
                    {
                        // FIX: было  1 << (CharWidth - 1 - rx)  → глифы зеркалились по OY.
                        // Стало:    1 << rx  (при FlipGlyphX = true) — правильная ориентация.
                        int bitIndex = FlipGlyphX ? rx : (BitmapFont5x7.CharWidth - 1 - rx);

                        if ((row & (1 << bitIndex)) != 0)
                            UiPainter.FillRect(img, imgW, imgH, cx + rx, y + ry, 1, 1, r, g, b, a);
                    }
                }
            }

            cx += BitmapFont5x7.CharWidth + BitmapFont5x7.CharSpacing;
        }
    }
}