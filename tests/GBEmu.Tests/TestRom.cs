using GBEmu.Core.Cartridge;

namespace GBEmu.Tests;

/// <summary>Builds minimal valid ROM images for tests.</summary>
public static class TestRom
{
    /// <summary>
    /// Creates a 32 KiB ROM with the given header fields and valid checksums.
    /// </summary>
    public static byte[] Build(
        string title = "TEST",
        CartridgeType type = CartridgeType.RomOnly,
        byte romSizeCode = 0x00,
        byte ramSizeCode = 0x00,
        byte cgbFlag = 0x00)
    {
        var rom = new byte[32 * 1024];

        for (int i = 0; i < title.Length; i++)
            rom[0x134 + i] = (byte)title[i];
        rom[0x143] = cgbFlag;
        rom[0x147] = (byte)type;
        rom[0x148] = romSizeCode;
        rom[0x149] = ramSizeCode;

        rom[0x14D] = CartridgeHeader.ComputeHeaderChecksum(rom);
        ushort global = CartridgeHeader.ComputeGlobalChecksum(rom);
        rom[0x14E] = (byte)(global >> 8);
        rom[0x14F] = (byte)global;
        return rom;
    }
}
