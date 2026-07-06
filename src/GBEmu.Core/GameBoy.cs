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
}
