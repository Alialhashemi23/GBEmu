namespace GBEmu.Core.Graphics;

/// <summary>
/// The DMG picture processing unit.
///
/// Timing model: 456 dots (T-cycles) per scanline, 154 lines per frame.
/// Visible lines run OAM scan (mode 2, 80 dots) → drawing (mode 3, fixed 172
/// dots) → HBlank (mode 0); lines 144-153 are VBlank (mode 1). Whole
/// scanlines are rendered at the mode 3 → 0 transition — a pixel-FIFO fetcher
/// is a later refinement if a target test ever demands it.
/// </summary>
public sealed partial class Ppu
{
    public const int ScreenWidth = 160;
    public const int ScreenHeight = 144;
    private const int Mode3End = 80 + 172;

    public readonly byte[] Vram = new byte[0x2000];
    public readonly byte[] Oam = new byte[0xA0];

    // Shade indices 0 (white) - 3 (black), after palette application.
    private readonly byte[] _frame = new byte[ScreenWidth * ScreenHeight];
    public ReadOnlySpan<byte> FrameShades => _frame;

    // Registers (post-boot values).
    private byte _lcdc = 0x91;
    private byte _statEnables = 0x00; // STAT bits 3-6 as written
    public byte Scy, Scx;
    public byte Ly { get; private set; }
    public byte Lyc;
    public byte Bgp = 0xFC, Obp0, Obp1;
    public byte Wy, Wx;

    private int _mode = 1;
    private int _dot;
    private int _windowLine;
    private bool _wyTriggered;
    private bool _statLine;

    /// <summary>IF bits raised by the PPU (bit 0 VBlank, bit 1 STAT); the bus collects these.</summary>
    public byte PendingInterrupts;

    /// <summary>Set when a frame finishes (VBlank entry); consumers clear it.</summary>
    public bool FrameReady;

    private bool LcdEnabled => (_lcdc & 0x80) != 0;

    public void Tick(int tCycles)
    {
        if (!LcdEnabled)
            return;
        for (int i = 0; i < tCycles; i++)
            TickDot();
    }

    private void TickDot()
    {
        _dot++;

        if (Ly < ScreenHeight)
        {
            if (_dot == 80)
                _mode = 3;
            else if (_dot == Mode3End)
            {
                _mode = 0;
                RenderScanline();
            }
        }

        if (_dot == 456)
        {
            _dot = 0;
            Ly++;
            switch (Ly)
            {
                case ScreenHeight:
                    _mode = 1;
                    PendingInterrupts |= 0x01; // VBlank
                    FrameReady = true;
                    break;
                case 154:
                    Ly = 0;
                    _windowLine = 0;
                    _wyTriggered = false;
                    _mode = 2;
                    break;
                default:
                    if (Ly < ScreenHeight)
                        _mode = 2;
                    break;
            }
            if (Ly == Wy)
                _wyTriggered = true; // window activation latches for the frame
        }

        UpdateStatLine();
    }

    /// <summary>
    /// The STAT interrupt fires on the rising edge of the OR of all enabled
    /// sources ("STAT blocking": while any source holds the line high, other
    /// sources cannot retrigger it).
    /// </summary>
    private void UpdateStatLine()
    {
        bool line =
            (_mode == 0 && (_statEnables & 0x08) != 0) ||
            (_mode == 1 && (_statEnables & 0x10) != 0) ||
            (_mode == 2 && (_statEnables & 0x20) != 0) ||
            (Ly == Lyc && (_statEnables & 0x40) != 0);

        if (line && !_statLine)
            PendingInterrupts |= 0x02;
        _statLine = line;
    }

    // --- Register access (via the bus) ---

    public byte ReadRegister(ushort address) => address switch
    {
        0xFF40 => _lcdc,
        0xFF41 => (byte)(0x80 | _statEnables
                              | (Ly == Lyc && LcdEnabled ? 0x04 : 0)
                              | (LcdEnabled ? _mode : 0)),
        0xFF42 => Scy,
        0xFF43 => Scx,
        0xFF44 => Ly,
        0xFF45 => Lyc,
        0xFF47 => Bgp,
        0xFF48 => Obp0,
        0xFF49 => Obp1,
        0xFF4A => Wy,
        0xFF4B => Wx,
        _ => 0xFF,
    };

    public void WriteRegister(ushort address, byte value)
    {
        switch (address)
        {
            case 0xFF40:
                bool wasEnabled = LcdEnabled;
                _lcdc = value;
                if (wasEnabled && !LcdEnabled)
                {
                    // Turning the LCD off resets the frame position.
                    Ly = 0;
                    _dot = 0;
                    _mode = 0;
                    _statLine = false;
                }
                else if (!wasEnabled && LcdEnabled)
                {
                    _mode = 2;
                    if (Ly == Wy)
                        _wyTriggered = true;
                }
                break;
            case 0xFF41:
                _statEnables = (byte)(value & 0x78);
                UpdateStatLine();
                break;
            case 0xFF42: Scy = value; break;
            case 0xFF43: Scx = value; break;
            case 0xFF44: break; // LY is read-only
            case 0xFF45:
                Lyc = value;
                UpdateStatLine();
                break;
            case 0xFF47: Bgp = value; break;
            case 0xFF48: Obp0 = value; break;
            case 0xFF49: Obp1 = value; break;
            case 0xFF4A: Wy = value; break;
            case 0xFF4B: Wx = value; break;
        }
    }

    // --- VRAM/OAM access with mode-based blocking (LCD on only) ---

    public byte ReadVram(ushort address) =>
        LcdEnabled && _mode == 3 ? (byte)0xFF : Vram[address - 0x8000];

    public void WriteVram(ushort address, byte value)
    {
        if (!(LcdEnabled && _mode == 3))
            Vram[address - 0x8000] = value;
    }

    public byte ReadOam(ushort address, bool dmaActive) =>
        dmaActive || (LcdEnabled && _mode >= 2) ? (byte)0xFF : Oam[address - 0xFE00];

    public void WriteOam(ushort address, byte value, bool dmaActive)
    {
        if (!dmaActive && !(LcdEnabled && _mode >= 2))
            Oam[address - 0xFE00] = value;
    }

    /// <summary>Copies the frame into an ARGB buffer using a 4-entry palette (shade 0..3).</summary>
    public void CopyFrame(uint[] destination, ReadOnlySpan<uint> palette)
    {
        for (int i = 0; i < _frame.Length; i++)
            destination[i] = palette[_frame[i]];
    }
}
