using System.Text;
using GBEmu.Core;
using GBEmu.Core.Memory;

namespace GBEmu.Tests.Cpu;

/// <summary>
/// Minimal machine for running Blargg's CPU test ROMs headlessly: flat ROM
/// (the individual cpu_instrs ROMs are 32 KiB, no banking), WRAM, HRAM, timer,
/// interrupt registers, and a serial port that captures the test's output.
/// No PPU — LY is stubbed so busy-wait loops on it terminate.
/// </summary>
public sealed class BlarggBus : IBus
{
    private readonly byte[] _rom;
    private readonly byte[] _vram = new byte[0x2000];
    private readonly byte[] _extRam = new byte[0x2000];
    private readonly byte[] _wram = new byte[0x2000];
    private readonly byte[] _oam = new byte[0xA0];
    private readonly byte[] _hram = new byte[0x7F];

    public readonly GbTimer Timer = new();
    private byte _if = 0xE1; // post-boot value
    private byte _ie;
    private byte _serialData;

    private readonly StringBuilder _serial = new();
    public string SerialText => _serial.ToString();

    public BlarggBus(byte[] rom) => _rom = rom;

    public byte Read(ushort address) => address switch
    {
        < 0x8000 => _rom[address % _rom.Length],
        < 0xA000 => _vram[address - 0x8000],
        < 0xC000 => _extRam[address - 0xA000],
        < 0xE000 => _wram[address - 0xC000],
        < 0xFE00 => _wram[address - 0xE000], // echo RAM
        < 0xFEA0 => _oam[address - 0xFE00],
        < 0xFF00 => 0xFF,
        0xFF01 => _serialData,
        0xFF02 => 0x7E,
        >= 0xFF04 and <= 0xFF07 => Timer.ReadRegister(address),
        0xFF0F => (byte)(_if | 0xE0),
        0xFF44 => 0x90, // LY stub: pretend we're in VBlank
        >= 0xFF80 and <= 0xFFFE => _hram[address - 0xFF80],
        0xFFFF => _ie,
        _ => 0xFF,
    };

    public void Write(ushort address, byte value)
    {
        switch (address)
        {
            case < 0x8000: break; // MBC writes — no banking needed for 32 KiB ROMs
            case < 0xA000: _vram[address - 0x8000] = value; break;
            case < 0xC000: _extRam[address - 0xA000] = value; break;
            case < 0xE000: _wram[address - 0xC000] = value; break;
            case < 0xFE00: _wram[address - 0xE000] = value; break;
            case < 0xFEA0: _oam[address - 0xFE00] = value; break;
            case 0xFF01: _serialData = value; break;
            case 0xFF02:
                if ((value & 0x80) != 0)
                    _serial.Append((char)_serialData);
                break;
            case >= 0xFF04 and <= 0xFF07: Timer.WriteRegister(address, value); break;
            case 0xFF0F: _if = (byte)(value & 0x1F); break;
            case >= 0xFF80 and <= 0xFFFE: _hram[address - 0xFF80] = value; break;
            case 0xFFFF: _ie = value; break;
        }
    }

    /// <summary>Advances everything except the CPU by the given T-cycles.</summary>
    public void StepPeripherals(int tCycles)
    {
        Timer.Tick(tCycles);
        if (Timer.InterruptRequested)
        {
            Timer.InterruptRequested = false;
            _if |= 0x04;
        }
    }
}
