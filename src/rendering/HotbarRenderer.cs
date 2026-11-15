using VoxelChaos.Gameplay;
using VoxelChaos.Scene;

namespace VoxelChaos.Rendering;

/// <summary>Рисует панель инвентаря прямо в low-res screenImage (pixel-art стиль).</summary>
public sealed class HotbarRenderer
{
    private const int SlotSize = 24;
    private const int Spacing  = 2;
    private const int Padding  = 6;
    private const int Bottom   = 8;

    private static readonly (byte R, byte G, byte B)[] BlockColors =
    {
        (0,   0,   0),    // Air
        (139, 110, 78),   // Dirt
        (91,  139, 58),   // Grass
        (128, 128, 128),  // Stone
        (107, 74,  43),   // Wood
        (58,  122, 42),   // Leaves
        (224, 216, 160),  // Sand
        (160, 208, 240),  // Ice
        (240, 240, 240),  // Snow
        (51,  51,  51),   // Basalt
        (255, 102, 0),    // Lava
        (200, 168, 120),  // TempleStone
        (64,  96,  192),  // Water
    };

    private readonly Inventory _inventory;
    public HotbarRenderer(Inventory inventory) => _inventory = inventory;

    public void Draw(byte[] img, int imgW, int imgH)
    {
        int panelW = Inventory.Slots * SlotSize + (Inventory.Slots - 1) * Spacing + 2 * Padding;
        int panelH = SlotSize + 2 * Padding;
        int px0 = (imgW - panelW) / 2;
        int py0 = imgH - panelH - Bottom;

        // Панель
        FillRect(img, imgW, imgH, px0, py0, panelW, panelH, 40, 40, 40, 220);
        FillRect(img, imgW, imgH, px0, py0, panelW, 1, 20, 20, 20, 255);
        FillRect(img, imgW, imgH, px0, py0 + panelH - 1, panelW, 1, 20, 20, 20, 255);

        for (int i = 0; i < Inventory.Slots; i++)
        {
            int sx = px0 + Padding + i * (SlotSize + Spacing);
            int sy = py0 + Padding;

            FillRect(img, imgW, imgH, sx, sy, SlotSize, SlotSize, 80, 80, 80, 255);

            var blockId = _inventory.GetSlot(i);
            var c = BlockColors[blockId];
            FillRect(img, imgW, imgH, sx + 3, sy + 3, SlotSize - 6, SlotSize - 6, c.R, c.G, c.B, 255);

            if (i == _inventory.SelectedIndex)
            {
                FillRect(img, imgW, imgH, sx - 1, sy - 1, SlotSize + 2, 1, 255, 255, 255, 255);
                FillRect(img, imgW, imgH, sx - 1, sy + SlotSize, SlotSize + 2, 1, 255, 255, 255, 255);
                FillRect(img, imgW, imgH, sx - 1, sy, 1, SlotSize, 255, 255, 255, 255);
                FillRect(img, imgW, imgH, sx + SlotSize, sy, 1, SlotSize, 255, 255, 255, 255);
            }
        }
    }

    private static void FillRect(byte[] img, int imgW, int imgH,
        int x0, int y0, int w, int h, byte r, byte g, byte b, byte a)
    {
        for (int py = y0; py < y0 + h; py++)
        {
            if ((uint)py >= (uint)imgH) continue;
            for (int px = x0; px < x0 + w; px++)
            {
                if ((uint)px >= (uint)imgW) continue;
                int idx = (py * imgW + px) * 4;
                img[idx]     = r;
                img[idx + 1] = g;
                img[idx + 2] = b;
                img[idx + 3] = a;
            }
        }
    }
}