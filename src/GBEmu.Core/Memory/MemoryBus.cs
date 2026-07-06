using GBEmu.Core.Cartridge;

namespace GBEmu.Core.Memory;

/// <summary>
/// The DMG memory map. Owns system RAM and the I/O registers, and routes
/// cartridge windows to the mapper. PPU registers are raw byte stores (plus
/// an LY stub) until the PPU lands in Phase 3.
/// </summary>
public sealed class MemoryBus : IBus
{
    private readonly Cartridge.Cartridge _cartridge;
    private readonly byte[] _vram = new byte[0x2000];
    private readonly byte[] _wram = new byte[0x2000];
    private readonly byte[] _oam = new byte[0xA0];
    private readonly byte[] _hram = new byte[0x7F];
    private readonly byte[] _ioStore = new byte[0x80]; // unimplemented I/O (PPU regs for now)

    public GbTimer Timer { get; } = new();

    public byte InterruptFlags = 0xE1; // post-boot value
    public byte InterruptEnable;

    private byte _serialData;
    private byte _joypadSelect = 0x30;

    /// <summary>Raised when a serial transfer completes (test ROMs print through this).</summary>
    public event Action<byte>? SerialByteTransferred;

    public MemoryBus(Cartridge.Cartridge cartridge)
    {
        _cartridge = cartridge;

        // Post-boot I/O state for the stored PPU registers (Pan Docs).
        _ioStore[0x40] = 0x91; // LCDC
        _ioStore[0x41] = 0x85; // STAT
        _ioStore[0x46] = 0xFF; // DMA
        _ioStore[0x47] = 0xFC; // BGP
    }

    /// <summary>Advances all bus-owned peripherals; called once per CPU machine cycle.</summary>
    public void TickComponents(int tCycles)
    {
        Timer.Tick(tCycles);
        if (Timer.InterruptRequested)
        {
            Timer.InterruptRequested = false;
            InterruptFlags |= 0x04;
        }
    }

    public byte Read(ushort address) => address switch
    {
        < 0x8000 => _cartridge.Read(address),
        < 0xA000 => _vram[address - 0x8000],
        < 0xC000 => _cartridge.Read(address),
        < 0xE000 => _wram[address - 0xC000],
        < 0xFE00 => _wram[address - 0xE000], // echo RAM
        < 0xFEA0 => _oam[address - 0xFE00],
        < 0xFF00 => 0x00,                    // prohibited region
        < 0xFF80 => ReadIo(address),
        < 0xFFFF => _hram[address - 0xFF80],
        _ => InterruptEnable,
    };

    public void Write(ushort address, byte value)
    {
        switch (address)
        {
            case < 0x8000: _cartridge.Write(address, value); break;
            case < 0xA000: _vram[address - 0x8000] = value; break;
            case < 0xC000: _cartridge.Write(address, value); break;
            case < 0xE000: _wram[address - 0xC000] = value; break;
            case < 0xFE00: _wram[address - 0xE000] = value; break;
            case < 0xFEA0: _oam[address - 0xFE00] = value; break;
            case < 0xFF00: break;
            case < 0xFF80: WriteIo(address, value); break;
            case < 0xFFFF: _hram[address - 0xFF80] = value; break;
            default: InterruptEnable = value; break;
        }
    }

    private byte ReadIo(ushort address) => address switch
    {
        0xFF00 => (byte)(0xC0 | _joypadSelect | 0x0F), // joypad stub: nothing pressed
        0xFF01 => _serialData,
        0xFF02 => 0x7E,
        >= 0xFF04 and <= 0xFF07 => Timer.ReadRegister(address),
        0xFF0F => (byte)(InterruptFlags | 0xE0),
        0xFF44 => 0x90, // LY stub: pretend VBlank so busy-waits terminate
        _ => _ioStore[address - 0xFF00],
    };

    private void WriteIo(ushort address, byte value)
    {
        switch (address)
        {
            case 0xFF00:
                _joypadSelect = (byte)(value & 0x30);
                break;
            case 0xFF01:
                _serialData = value;
                break;
            case 0xFF02:
                // Transfers complete instantly: the peer shifts in 0xFF.
                if ((value & 0x80) != 0)
                {
                    SerialByteTransferred?.Invoke(_serialData);
                    _serialData = 0xFF;
                }
                break;
            case >= 0xFF04 and <= 0xFF07:
                Timer.WriteRegister(address, value);
                break;
            case 0xFF0F:
                InterruptFlags = (byte)(value & 0x1F);
                break;
            default:
                _ioStore[address - 0xFF00] = value;
                break;
        }
    }
}
