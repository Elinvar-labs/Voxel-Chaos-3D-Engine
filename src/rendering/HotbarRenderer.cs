using VoxelChaos.Gameplay;
using VoxelChaos.Scene;

namespace VoxelChaos.Rendering;

/// <summary>
/// Панель быстрого доступа (9 слотов) внизу экрана.
/// Рисуется прямо в low-res screenImage — пиксельный стиль, апскейл бесплатный.
/// </summary>
public sealed class HotbarRenderer
{
    // ---------- Геометрия ----------
    private const int SlotSize  = 24;   // внутренний размер слота (без рамки)
    private const int Spacing   = 2;    // зазор между слотами
    private const int Padding   = 6;    // отступ панели по краям
    private const int Bottom    = 8;    // отступ панели от низа экрана
    private const int InnerInset = 3;   // «рамка» внутри слота вокруг иконки блока

    // ---------- Цвета панели ----------
    private static readonly (byte R, byte G, byte B, byte A) PanelBg     = (40,  40,  40,  220);
    private static readonly (byte R, byte G, byte B, byte A) PanelBorder = (20,  20,  20,  255);
    private static readonly (byte R, byte G, byte B, byte A) SlotBg      = (80,  80,  80,  255);
    private static readonly (byte R, byte G, byte B, byte A) SlotBgSel   = (110, 110, 110, 255);
    private static readonly (byte R, byte G, byte B, byte A) Highlight   = (255, 255, 255, 255);
    private static readonly (byte R, byte G, byte B, byte A) NumberText  = (220, 220, 220, 255);

    // ---------- Цвета блоков (индекс = BlockId) ----------
    // 0 — Air (не используется, но храним для компактности switch → индексация)
    private static readonly (byte R, byte G, byte B)[] BlockColors =
    {
        (0,   0,   0),    // 0  Air
        (139, 110, 78),   // 1  Dirt
        (91,  139, 58),   // 2  Grass
        (128, 128, 128),  // 3  Stone
        (107, 74,  43),   // 4  Wood
        (58,  122, 42),   // 5  Leaves
        (224, 216, 160),  // 6  Sand
        (160, 208, 240),  // 7  Ice
        (240, 240, 240),  // 8  Snow
        (51,  51,  51),   // 9  Basalt
        (255, 102, 0),    // 10 Lava
        (200, 168, 120),  // 11 TempleStone
        (64,  96,  192),  // 12 Water
    };

    private readonly Inventory _inventory;

    public HotbarRenderer(Inventory inventory) => _inventory = inventory;

    public void Draw(byte[] img, int imgW, int imgH)
    {
        if (img == null || imgW <= 0 || imgH <= 0) return;

        int panelW = Inventory.Slots * SlotSize + (Inventory.Slots - 1) * Spacing + 2 * Padding;
        int panelH = SlotSize + 2 * Padding;
        int px0    = (imgW - panelW) / 2;
        int py0    = imgH - panelH - Bottom;

        DrawPanel(img, imgW, imgH, px0, py0, panelW, panelH);

        for (int i = 0; i < Inventory.Slots; i++)
            DrawSlot(img, imgW, imgH, px0, py0, i);
    }

    // ------------------------------------------------------------
    //  Панель (фон + рамка)
    // ------------------------------------------------------------
    private static void DrawPanel(byte[] img, int imgW, int imgH,
        int x0, int y0, int w, int h)
    {
        UiPainter.FillRect(img, imgW, imgH, x0, y0, w, h,
            PanelBg.R, PanelBg.G, PanelBg.B, PanelBg.A);

        UiPainter.FillRect(img, imgW, imgH, x0, y0,         w, 1,
            PanelBorder.R, PanelBorder.G, PanelBorder.B, PanelBorder.A);
        UiPainter.FillRect(img, imgW, imgH, x0, y0 + h - 1, w, 1,
            PanelBorder.R, PanelBorder.G, PanelBorder.B, PanelBorder.A);
        UiPainter.FillRect(img, imgW, imgH, x0,         y0, 1, h,
            PanelBorder.R, PanelBorder.G, PanelBorder.B, PanelBorder.A);
        UiPainter.FillRect(img, imgW, imgH, x0 + w - 1, y0, 1, h,
            PanelBorder.R, PanelBorder.G, PanelBorder.B, PanelBorder.A);
    }

    // ------------------------------------------------------------
    //  Один слот: фон → иконка блока → номер → выделение
    // ------------------------------------------------------------
    private void DrawSlot(byte[] img, int imgW, int imgH,
        int panelX, int panelY, int index)
    {
        int sx = panelX + Padding + index * (SlotSize + Spacing);
        int sy = panelY + Padding;
        bool selected = index == _inventory.SelectedIndex;

        // 1) Фон слота
        var bg = selected ? SlotBgSel : SlotBg;
        UiPainter.FillRect(img, imgW, imgH, sx, sy, SlotSize, SlotSize, bg.R, bg.G, bg.B, bg.A);

        // 2) Иконка блока
        int inner = SlotSize - 2 * InnerInset;
        var c = BlockColorOf(_inventory.GetSlot(index));
        UiPainter.FillRect(img, imgW, imgH,
            sx + InnerInset, sy + InnerInset, inner, inner, c.R, c.G, c.B, 255);

        // 3) Номер слота (1..9) — верхний левый угол, если влезает
        if (index + 1 <= 9)
        {
            var digit = ((char)('0' + index + 1)).ToString();
            HudRenderer.DrawTextShadow(img, imgW, imgH, sx + 2, sy + 1, digit);
        }

        // 4) Выделение — белая рамка вокруг выбранного слота
        if (selected)
        {
            UiPainter.FillRect(img, imgW, imgH, sx - 1, sy - 1, SlotSize + 2, 1,
                Highlight.R, Highlight.G, Highlight.B, Highlight.A);
            UiPainter.FillRect(img, imgW, imgH, sx - 1, sy + SlotSize, SlotSize + 2, 1,
                Highlight.R, Highlight.G, Highlight.B, Highlight.A);
            UiPainter.FillRect(img, imgW, imgH, sx - 1, sy, 1, SlotSize,
                Highlight.R, Highlight.G, Highlight.B, Highlight.A);
            UiPainter.FillRect(img, imgW, imgH, sx + SlotSize, sy, 1, SlotSize,
                Highlight.R, Highlight.G, Highlight.B, Highlight.A);
        }
    }

    // ------------------------------------------------------------
    private static (byte R, byte G, byte B) BlockColorOf(byte blockId)
    {
        if (blockId < BlockColors.Length) return BlockColors[blockId];
        return (255, 0, 255);   // magenta = неизвестный блок (визуальный маркер)
    }
}