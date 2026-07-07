using GBEmu.Core.Cpu;
using GBEmu.Core.Memory;
using Cart = GBEmu.Core.Cartridge.Cartridge;

namespace GBEmu.Core;

/// <summary>
/// The assembled machine: cartridge + bus + CPU, with peripherals ticked once
/// per CPU machine cycle. (PPU and APU join in later phases.)
/// </summary>
public sealed class GameBoy
{
    public Cart Cartridge { get; }
    public MemoryBus Bus { get; }
    public Sm83 Cpu { get; }

    public GameBoy(byte[] rom)
    {
        Cartridge = Cart.Load(rom);
        Bus = new MemoryBus(Cartridge);
        Cpu = new Sm83(Bus);
        Cpu.Tick = Bus.TickComponents;
    }

    /// <summary>Executes one instruction (or interrupt dispatch); returns T-cycles consumed.</summary>
    public int Step() => Cpu.Step();

    public Graphics.Ppu Ppu => Bus.Ppu;
    public Joypad Joypad => Bus.Joypad;

    /// <summary>
    /// Runs until the PPU completes a frame (~70224 T-cycles). With the LCD
    /// off, runs one frame's worth of cycles so callers still make progress.
    /// </summary>
    public void RunFrame()
    {
        const int frameCycles = 70224;
        Bus.Ppu.FrameReady = false;

        long budget = frameCycles * 2; // safety margin; LCD-off never sets FrameReady
        long spent = 0;
        while (!Bus.Ppu.FrameReady && spent < budget)
        {
            spent += Step();
            if (spent >= frameCycles && !LcdOn())
                return;
        }
    }

    private bool LcdOn() => (Bus.Ppu.ReadRegister(0xFF40) & 0x80) != 0;
}
