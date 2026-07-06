namespace GBEmu.Core.Memory;

/// <summary>
/// The address bus as seen by the CPU. Every component (cartridge, RAM, I/O
/// registers, IE) is reached through this single interface.
/// </summary>
public interface IBus
{
    byte Read(ushort address);
    void Write(ushort address, byte value);
}
