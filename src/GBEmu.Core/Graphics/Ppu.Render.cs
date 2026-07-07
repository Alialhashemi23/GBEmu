namespace GBEmu.Core.Graphics;

/// <summary>
/// Scanline rendering: background, window, then sprites. All of dmg-acid2's
/// behaviours are honoured: the window's internal line counter, the 10-sprite
/// limit, lower-X sprite priority, 8x16 tile index masking, and OBJ-to-BG
/// priority decided by the highest-priority sprite pixel.
/// </summary>
public sealed partial class Ppu
{
    // Per-line scratch: BG color indices (pre-palette) for sprite priority.
    private readonly byte[] _bgIndexLine = new byte[ScreenWidth];
    private readonly (byte Color, byte Flags)[] _spriteLine = new (byte, byte)[ScreenWidth];
    private readonly int[] _scanSprites = new int[10];

    private void RenderScanline()
    {
        int lineStart = Ly * ScreenWidth;

        RenderBackgroundAndWindow(lineStart);
        if ((_lcdc & 0x02) != 0)
            RenderSprites(lineStart);
    }

    private void RenderBackgroundAndWindow(int lineStart)
    {
        bool windowOnLine = false;

        if ((_lcdc & 0x01) == 0)
        {
            // BG/window disabled: the line is white and counts as color 0
            // for sprite priority.
            for (int x = 0; x < ScreenWidth; x++)
            {
                _bgIndexLine[x] = 0;
                _frame[lineStart + x] = 0;
            }
            return;
        }

        bool windowActive = (_lcdc & 0x20) != 0 && _wyTriggered && Wx <= 166;

        for (int x = 0; x < ScreenWidth; x++)
        {
            byte colorIndex;
            if (windowActive && x >= Wx - 7)
            {
                int wx = x - (Wx - 7);
                colorIndex = FetchTilePixel(
                    mapBase: (_lcdc & 0x40) != 0 ? 0x1C00 : 0x1800,
                    tx: wx >> 3, ty: _windowLine >> 3,
                    px: wx & 7, py: _windowLine & 7);
                windowOnLine = true;
            }
            else
            {
                int bx = (Scx + x) & 0xFF;
                int by = (Scy + Ly) & 0xFF;
                colorIndex = FetchTilePixel(
                    mapBase: (_lcdc & 0x08) != 0 ? 0x1C00 : 0x1800,
                    tx: bx >> 3, ty: by >> 3,
                    px: bx & 7, py: by & 7);
            }

            _bgIndexLine[x] = colorIndex;
            _frame[lineStart + x] = Shade(Bgp, colorIndex);
        }

        if (windowOnLine)
            _windowLine++; // internal counter only advances on lines the window rendered
    }

    private byte FetchTilePixel(int mapBase, int tx, int ty, int px, int py)
    {
        byte tileIndex = Vram[mapBase + ty * 32 + tx];
        int tileAddr = (_lcdc & 0x10) != 0
            ? tileIndex * 16
            : 0x1000 + (sbyte)tileIndex * 16;
        byte lo = Vram[tileAddr + py * 2];
        byte hi = Vram[tileAddr + py * 2 + 1];
        int bit = 7 - px;
        return (byte)(((lo >> bit) & 1) | (((hi >> bit) & 1) << 1));
    }

    private void RenderSprites(int lineStart)
    {
        int height = (_lcdc & 0x04) != 0 ? 16 : 8;

        // OAM scan: the first 10 sprites in OAM order that cover this line.
        int count = 0;
        for (int i = 0; i < 40 && count < 10; i++)
        {
            int sy = Oam[i * 4] - 16;
            if (Ly >= sy && Ly < sy + height)
                _scanSprites[count++] = i;
        }

        // Draw priority: lowest X wins; ties broken by OAM order. Iterate in
        // priority order and let the first opaque pixel claim each column.
        Array.Clear(_spriteLine);
        Span<int> order = _scanSprites.AsSpan(0, count);
        order.Sort((a, b) => Oam[a * 4 + 1] != Oam[b * 4 + 1]
            ? Oam[a * 4 + 1] - Oam[b * 4 + 1]
            : a - b);

        foreach (int i in order)
        {
            int sy = Oam[i * 4] - 16;
            int sx = Oam[i * 4 + 1] - 8;
            byte tile = Oam[i * 4 + 2];
            byte flags = Oam[i * 4 + 3];

            int row = Ly - sy;
            if ((flags & 0x40) != 0) // Y flip
                row = height - 1 - row;
            if (height == 16)
                tile &= 0xFE; // bit 0 of the tile index is ignored in 8x16 mode

            int tileAddr = (tile + (row >> 3)) * 16 + (row & 7) * 2;
            byte lo = Vram[tileAddr];
            byte hi = Vram[tileAddr + 1];

            for (int px = 0; px < 8; px++)
            {
                int x = sx + px;
                if (x is < 0 or >= ScreenWidth || _spriteLine[x].Color != 0)
                    continue;
                int bit = (flags & 0x20) != 0 ? px : 7 - px; // X flip
                byte color = (byte)(((lo >> bit) & 1) | (((hi >> bit) & 1) << 1));
                if (color != 0)
                    _spriteLine[x] = (color, flags);
            }
        }

        for (int x = 0; x < ScreenWidth; x++)
        {
            (byte color, byte flags) = _spriteLine[x];
            if (color == 0)
                continue;
            // OBJ-to-BG priority: a behind-background sprite only shows over
            // BG color 0 — decided by the winning sprite alone.
            if ((flags & 0x80) != 0 && _bgIndexLine[x] != 0)
                continue;
            byte palette = (flags & 0x10) != 0 ? Obp1 : Obp0;
            _frame[lineStart + x] = Shade(palette, color);
        }
    }

    private static byte Shade(byte palette, int colorIndex) =>
        (byte)((palette >> (colorIndex * 2)) & 3);
}
