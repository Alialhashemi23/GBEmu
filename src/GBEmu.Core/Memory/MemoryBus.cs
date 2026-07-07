using GBEmu.Core.Graphics;

namespace GBEmu.Core.Memory;

/// <summary>
/// The DMG memory map. Owns system RAM, the I/O registers, and OAM DMA, and
/// routes cartridge/PPU windows to their owners. APU registers are raw byte
/// stores until Phase 5.
/// </summary>
public sealed class MemoryBus : IBus
{
    private readonly Cartridge.Cartridge _cartridge;
    private readonly byte[] _wram = new byte[0x2000];
    private readonly byte[] _hram = new byte[0x7F];
    private readonly byte[] _ioStore = new byte[0x80]; // unimplemented I/O (APU for now)

    public GbTimer Timer { get; } = new();
    public Ppu Ppu { get; } = new();
    public Joypad Joypad { get; } = new();

    public byte InterruptFlags = 0xE1; // post-boot value
    public byte InterruptEnable;

    private byte _serialData;

    // OAM DMA state. While active the CPU sees OAM as 0xFF.
    private byte _dmaRegister = 0xFF;
    private ushort _dmaSource;
    private int _dmaIndex = -1;  // -1 = idle; 0..159 = next byte to copy
    private bool _dmaPending;    // one M-cycle startup delay
    public bool DmaActive => _dmaIndex >= 0;

    /// <summary>Raised when a serial transfer completes (test ROMs print through this).</summary>
    public event Action<byte>? SerialByteTransferred;

    public MemoryBus(Cartridge.Cartridge cartridge)
    {
        _cartridge = cartridge;
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

        Ppu.Tick(tCycles);
        InterruptFlags |= Ppu.PendingInterrupts;
        Ppu.PendingInterrupts = 0;

        if (Joypad.InterruptRequested)
        {
            Joypad.InterruptRequested = false;
            InterruptFlags |= 0x10;
        }

        TickDma(tCycles / 4);
    }

    private void TickDma(int mCycles)
    {
        for (int i = 0; i < mCycles; i++)
        {
            if (_dmaPending)
            {
                _dmaPending = false;
                _dmaIndex = 0;
            }
            else if (_dmaIndex is >= 0 and < 160)
            {
                // Sources above 0xDFFF wrap back into work RAM (echo behaviour).
                ushort src = (ushort)(_dmaSource + _dmaIndex);
                if (src >= 0xE000)
                    src -= 0x2000;
                Ppu.Oam[_dmaIndex] = ReadInternal(src);
                if (++_dmaIndex == 160)
                    _dmaIndex = -1;
            }
        }
    }

    /// <summary>Reads that bypass CPU-visible blocking (used by DMA).</summary>
    private byte ReadInternal(ushort address) => address switch
    {
        < 0x8000 => _cartridge.Read(address),
        < 0xA000 => Ppu.Vram[address - 0x8000],
        < 0xC000 => _cartridge.Read(address),
        < 0xE000 => _wram[address - 0xC000],
        _ => 0xFF,
    };

    public byte Read(ushort address) => address switch
    {
        < 0x8000 => _cartridge.Read(address),
        < 0xA000 => Ppu.ReadVram(address),
        < 0xC000 => _cartridge.Read(address),
        < 0xE000 => _wram[address - 0xC000],
        < 0xFE00 => _wram[address - 0xE000], // echo RAM
        < 0xFEA0 => Ppu.ReadOam(address, DmaActive),
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
            case < 0xA000: Ppu.WriteVram(address, value); break;
            case < 0xC000: _cartridge.Write(address, value); break;
            case < 0xE000: _wram[address - 0xC000] = value; break;
            case < 0xFE00: _wram[address - 0xE000] = value; break;
            case < 0xFEA0: Ppu.WriteOam(address, value, DmaActive); break;
            case < 0xFF00: break;
            case < 0xFF80: WriteIo(address, value); break;
            case < 0xFFFF: _hram[address - 0xFF80] = value; break;
            default: InterruptEnable = value; break;
        }
    }

    private byte ReadIo(ushort address) => address switch
    {
        0xFF00 => Joypad.Read(),
        0xFF01 => _serialData,
        0xFF02 => 0x7E,
        >= 0xFF04 and <= 0xFF07 => Timer.ReadRegister(address),
        0xFF0F => (byte)(InterruptFlags | 0xE0),
        0xFF46 => _dmaRegister,
        >= 0xFF40 and <= 0xFF4B => Ppu.ReadRegister(address),
        _ => _ioStore[address - 0xFF00],
    };

    private void WriteIo(ushort address, byte value)
    {
        switch (address)
        {
            case 0xFF00:
                Joypad.Write(value);
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
            case 0xFF46:
                _dmaRegister = value;
                _dmaSource = (ushort)(value << 8);
                _dmaPending = true;
                break;
            case >= 0xFF40 and <= 0xFF4B:
                Ppu.WriteRegister(address, value);
                break;
            default:
                _ioStore[address - 0xFF00] = value;
                break;
        }
    }
}
