using VoxelChaos.Core;

namespace VoxelChaos.Rendering;

public static class MenuRenderer
{
    public static bool DrawButton(byte[] img, int w, int h, int x, int y, int bw, int bh, string text, bool isHovered)
    {
        byte bgR = isHovered ? (byte)80 : (byte)60;
        byte bgG = isHovered ? (byte)80 : (byte)60;
        byte bgB = isHovered ? (byte)100 : (byte)70;

        UiPainter.FillRect(img, w, h, x, y, bw, bh, bgR, bgG, bgB, 255);
        
        // Рамка
        UiPainter.FillRect(img, w, h, x, y, bw, 1, 200, 200, 200, 255);
        UiPainter.FillRect(img, w, h, x, y + bh - 1, bw, 1, 200, 200, 200, 255);

        // Текст по центру
        int textW = text.Length * (BitmapFont5x7.CharWidth + BitmapFont5x7.CharSpacing);
        int tx = x + (bw - textW) / 2;
        int ty = y + (bh - BitmapFont5x7.CharHeight) / 2;
        HudRenderer.DrawTextShadow(img, w, h, tx, ty, text);
        
        return isHovered && Input.Jclicked(Silk.NET.Input.MouseButton.Left);
    }
}