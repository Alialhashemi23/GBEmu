namespace GBEmu.Core.Cartridge;

/// <summary>
/// A cartridge mapper (MBC chip). Handles reads in the two cartridge windows
/// (0x0000-0x7FFF ROM, 0xA000-0xBFFF external RAM) and the banking control
/// writes that games issue into the ROM address range.
/// </summary>
public interface IMapper
{
    byte Read(ushort address);
    void Write(ushort address, byte value);

    /// <summary>External RAM contents, or null if the cartridge has none. Used for battery saves.</summary>
    byte[]? Ram { get; }
}
