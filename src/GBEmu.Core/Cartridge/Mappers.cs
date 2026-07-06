namespace GBEmu.Core.Cartridge;

/// <summary>ROM-only cartridges (Tetris), optionally with a fixed 8 KiB RAM.</summary>
public sealed class NoMbc : IMapper
{
    private readonly byte[] _rom;
    public byte[]? Ram { get; }

    public NoMbc(byte[] rom, int ramSize)
    {
        _rom = rom;
        Ram = ramSize > 0 ? new byte[ramSize] : null;
    }

    public byte Read(ushort address)
    {
        if (address < 0x8000)
            return address < _rom.Length ? _rom[address] : (byte)0xFF;
        int offset = address - 0xA000;
        return Ram is not null && offset < Ram.Length ? Ram[offset] : (byte)0xFF;
    }

    public void Write(ushort address, byte value)
    {
        if (address < 0x8000)
            return;
        int offset = address - 0xA000;
        if (Ram is not null && offset < Ram.Length)
            Ram[offset] = value;
    }
}

/// <summary>
/// MBC1: 5-bit BANK1 + 2-bit BANK2 registers and a mode flag deciding whether
/// BANK2 applies to the 0x0000 ROM window / RAM banking. MBC1 multicarts
/// (MBC1M wiring) are not supported.
/// </summary>
public sealed class Mbc1 : IMapper
{
    private readonly byte[] _rom;
    private readonly int _romBankMask;
    private readonly int _ramBankMask;
    public byte[]? Ram { get; }

    private bool _ramEnabled;
    private int _bank1 = 1; // 5 bits, never 0
    private int _bank2;     // 2 bits
    private bool _mode;     // false: BANK2 affects 0x4000 window only

    public Mbc1(byte[] rom, int romBankCount, int ramSize)
    {
        _rom = rom;
        _romBankMask = romBankCount - 1;
        Ram = ramSize > 0 ? new byte[ramSize] : null;
        _ramBankMask = ramSize > 0x2000 ? 3 : 0;
    }

    public byte Read(ushort address)
    {
        switch (address)
        {
            case < 0x4000:
            {
                int bank = _mode ? (_bank2 << 5) & _romBankMask : 0;
                return RomByte(bank, address);
            }
            case < 0x8000:
            {
                int bank = ((_bank2 << 5) | _bank1) & _romBankMask;
                return RomByte(bank, address - 0x4000);
            }
            default:
            {
                if (!_ramEnabled || Ram is null)
                    return 0xFF;
                return Ram[RamOffset(address)];
            }
        }
    }

    public void Write(ushort address, byte value)
    {
        switch (address)
        {
            case < 0x2000:
                _ramEnabled = (value & 0x0F) == 0x0A;
                break;
            case < 0x4000:
                _bank1 = value & 0x1F;
                if (_bank1 == 0)
                    _bank1 = 1;
                break;
            case < 0x6000:
                _bank2 = value & 0x03;
                break;
            case < 0x8000:
                _mode = (value & 0x01) != 0;
                break;
            default:
                if (_ramEnabled && Ram is not null)
                    Ram[RamOffset(address)] = value;
                break;
        }
    }

    private byte RomByte(int bank, int offset) => _rom[(bank * 0x4000 + offset) % _rom.Length];

    private int RamOffset(ushort address)
    {
        int bank = _mode ? _bank2 & _ramBankMask : 0;
        return (bank * 0x2000 + (address - 0xA000)) % Ram!.Length;
    }
}

/// <summary>
/// MBC2: 4-bit ROM bank register and 512 half-bytes of built-in RAM.
/// A single write range 0x0000-0x3FFF controls both — address bit 8 selects
/// between RAM enable (clear) and ROM bank (set).
/// </summary>
public sealed class Mbc2 : IMapper
{
    private readonly byte[] _rom;
    private readonly int _romBankMask;
    public byte[]? Ram { get; } = new byte[512];

    private bool _ramEnabled;
    private int _romBank = 1;

    public Mbc2(byte[] rom, int romBankCount)
    {
        _rom = rom;
        _romBankMask = romBankCount - 1;
    }

    public byte Read(ushort address)
    {
        switch (address)
        {
            case < 0x4000:
                return _rom[address % _rom.Length];
            case < 0x8000:
                return _rom[((_romBank & _romBankMask) * 0x4000 + address - 0x4000) % _rom.Length];
            default:
                if (!_ramEnabled)
                    return 0xFF;
                // Only 512 nibbles exist, echoed through the whole 0xA000-0xBFFF range.
                return (byte)(Ram![(address - 0xA000) & 0x1FF] | 0xF0);
        }
    }

    public void Write(ushort address, byte value)
    {
        switch (address)
        {
            case < 0x4000:
                if ((address & 0x0100) == 0)
                {
                    _ramEnabled = (value & 0x0F) == 0x0A;
                }
                else
                {
                    _romBank = value & 0x0F;
                    if (_romBank == 0)
                        _romBank = 1;
                }
                break;
            case < 0x8000:
                break;
            default:
                if (_ramEnabled)
                    Ram![(address - 0xA000) & 0x1FF] = (byte)(value & 0x0F);
                break;
        }
    }
}

/// <summary>
/// MBC3: 7-bit ROM bank, RAM banking, and an RTC. The RTC is a stub for now —
/// registers hold whatever was written but do not tick with wall-clock time.
/// </summary>
public sealed class Mbc3 : IMapper
{
    private readonly byte[] _rom;
    private readonly int _romBankMask;
    private readonly int _ramBankMask;
    public byte[]? Ram { get; }

    private bool _ramEnabled;
    private int _romBank = 1;
    private int _ramBank; // 0-3 = RAM bank, 0x08-0x0C = RTC register
    private readonly byte[] _rtc = new byte[5]; // S, M, H, DL, DH

    public Mbc3(byte[] rom, int romBankCount, int ramSize)
    {
        _rom = rom;
        _romBankMask = romBankCount - 1;
        Ram = ramSize > 0 ? new byte[ramSize] : null;
        _ramBankMask = Math.Max(1, ramSize / 0x2000) - 1;
    }

    public byte Read(ushort address)
    {
        switch (address)
        {
            case < 0x4000:
                return _rom[address % _rom.Length];
            case < 0x8000:
                return _rom[((_romBank & _romBankMask) * 0x4000 + address - 0x4000) % _rom.Length];
            default:
                if (!_ramEnabled)
                    return 0xFF;
                if (_ramBank >= 0x08)
                    return _ramBank <= 0x0C ? _rtc[_ramBank - 0x08] : (byte)0xFF;
                if (Ram is null)
                    return 0xFF;
                return Ram[((_ramBank & _ramBankMask) * 0x2000 + address - 0xA000) % Ram.Length];
        }
    }

    public void Write(ushort address, byte value)
    {
        switch (address)
        {
            case < 0x2000:
                _ramEnabled = (value & 0x0F) == 0x0A;
                break;
            case < 0x4000:
                _romBank = value & 0x7F;
                if (_romBank == 0)
                    _romBank = 1;
                break;
            case < 0x6000:
                _ramBank = value & 0x0F;
                break;
            case < 0x8000:
                break; // RTC latch — no-op while the RTC is a static stub
            default:
                if (!_ramEnabled)
                    break;
                if (_ramBank >= 0x08)
                {
                    if (_ramBank <= 0x0C)
                        _rtc[_ramBank - 0x08] = value;
                }
                else if (Ram is not null)
                {
                    Ram[((_ramBank & _ramBankMask) * 0x2000 + address - 0xA000) % Ram.Length] = value;
                }
                break;
        }
    }
}

/// <summary>MBC5: 9-bit ROM bank (bank 0 selectable), 4-bit RAM bank.</summary>
public sealed class Mbc5 : IMapper
{
    private readonly byte[] _rom;
    private readonly int _romBankMask;
    private readonly int _ramBankMask;
    public byte[]? Ram { get; }

    private bool _ramEnabled;
    private int _romBank = 1;
    private int _ramBank;

    public Mbc5(byte[] rom, int romBankCount, int ramSize)
    {
        _rom = rom;
        _romBankMask = romBankCount - 1;
        Ram = ramSize > 0 ? new byte[ramSize] : null;
        _ramBankMask = Math.Max(1, ramSize / 0x2000) - 1;
    }

    public byte Read(ushort address)
    {
        switch (address)
        {
            case < 0x4000:
                return _rom[address % _rom.Length];
            case < 0x8000:
                return _rom[((_romBank & _romBankMask) * 0x4000 + address - 0x4000) % _rom.Length];
            default:
                if (!_ramEnabled || Ram is null)
                    return 0xFF;
                return Ram[((_ramBank & _ramBankMask) * 0x2000 + address - 0xA000) % Ram.Length];
        }
    }

    public void Write(ushort address, byte value)
    {
        switch (address)
        {
            case < 0x2000:
                _ramEnabled = (value & 0x0F) == 0x0A;
                break;
            case < 0x3000:
                _romBank = (_romBank & 0x100) | value;
                break;
            case < 0x4000:
                _romBank = (_romBank & 0xFF) | ((value & 1) << 8);
                break;
            case < 0x6000:
                _ramBank = value & 0x0F; // bit 3 doubles as rumble on rumble carts
                break;
            case < 0x8000:
                break;
            default:
                if (_ramEnabled && Ram is not null)
                    Ram[((_ramBank & _ramBankMask) * 0x2000 + address - 0xA000) % Ram.Length] = value;
                break;
        }
    }
}
