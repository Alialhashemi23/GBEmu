namespace GBEmu.Core.Cartridge;

/// <summary>
/// Parsed view of the cartridge header located at 0x0100-0x014F.
/// Reference: Pan Docs, "The Cartridge Header".
/// </summary>
public sealed class CartridgeHeader
{
    public const int HeaderEnd = 0x0150;

    public required string Title { get; init; }
    public required CartridgeType CartridgeType { get; init; }
    public required int RomSizeBytes { get; init; }
    public required int RomBankCount { get; init; }
    public required int RamSizeBytes { get; init; }
    public required byte CgbFlag { get; init; }
    public required bool SupportsSgb { get; init; }
    public required byte DestinationCode { get; init; }
    public required byte MaskRomVersion { get; init; }
    public required byte HeaderChecksum { get; init; }
    public required bool HeaderChecksumValid { get; init; }
    public required ushort GlobalChecksum { get; init; }
    public required bool GlobalChecksumValid { get; init; }

    /// <summary>True when the CGB flag requests color-only mode (0xC0).</summary>
    public bool RequiresCgb => CgbFlag == 0xC0;

    /// <summary>True when the cartridge has battery-backed RAM (or RTC) that should persist to disk.</summary>
    public bool HasBattery => CartridgeType.HasBattery();

    public static CartridgeHeader Parse(ReadOnlySpan<byte> rom)
    {
        if (rom.Length < HeaderEnd)
            throw new InvalidRomException(
                $"ROM is {rom.Length} bytes; too small to contain a cartridge header (need at least {HeaderEnd}).");

        byte cgbFlag = rom[0x143];
        string title = ReadTitle(rom, cgbFlag);

        byte romSizeCode = rom[0x148];
        if (romSizeCode > 0x08)
            throw new InvalidRomException($"Unknown ROM size code 0x{romSizeCode:X2}.");
        int romBanks = 2 << romSizeCode;

        int ramSize = rom[0x149] switch
        {
            0x00 => 0,
            0x01 => 0, // unused/undocumented code; no licensed cartridge uses it
            0x02 => 8 * 1024,
            0x03 => 32 * 1024,
            0x04 => 128 * 1024,
            0x05 => 64 * 1024,
            var code => throw new InvalidRomException($"Unknown RAM size code 0x{code:X2}."),
        };

        byte headerChecksum = rom[0x14D];
        ushort globalChecksum = (ushort)((rom[0x14E] << 8) | rom[0x14F]);

        return new CartridgeHeader
        {
            Title = title,
            CartridgeType = (CartridgeType)rom[0x147],
            RomSizeBytes = romBanks * 0x4000,
            RomBankCount = romBanks,
            RamSizeBytes = ramSize,
            CgbFlag = cgbFlag,
            SupportsSgb = rom[0x146] == 0x03,
            DestinationCode = rom[0x14A],
            MaskRomVersion = rom[0x14C],
            HeaderChecksum = headerChecksum,
            HeaderChecksumValid = ComputeHeaderChecksum(rom) == headerChecksum,
            GlobalChecksum = globalChecksum,
            GlobalChecksumValid = ComputeGlobalChecksum(rom) == globalChecksum,
        };
    }

    /// <summary>
    /// Checksum over 0x134-0x14C, verified by the DMG boot ROM: x = 0; for each byte: x = x - byte - 1.
    /// </summary>
    public static byte ComputeHeaderChecksum(ReadOnlySpan<byte> rom)
    {
        byte checksum = 0;
        for (int addr = 0x134; addr <= 0x14C; addr++)
            checksum = (byte)(checksum - rom[addr] - 1);
        return checksum;
    }

    /// <summary>
    /// 16-bit sum of every ROM byte except the two checksum bytes themselves.
    /// Not verified by real hardware, but useful for detecting corrupt dumps.
    /// </summary>
    public static ushort ComputeGlobalChecksum(ReadOnlySpan<byte> rom)
    {
        ushort sum = 0;
        for (int addr = 0; addr < rom.Length; addr++)
        {
            if (addr is 0x14E or 0x14F)
                continue;
            sum += rom[addr];
        }
        return sum;
    }

    private static string ReadTitle(ReadOnlySpan<byte> rom, byte cgbFlag)
    {
        // On CGB-aware cartridges the last title bytes are repurposed
        // (manufacturer code at 0x13F-0x142, CGB flag at 0x143).
        int titleLength = cgbFlag is 0x80 or 0xC0 ? 11 : 16;
        ReadOnlySpan<byte> raw = rom.Slice(0x134, titleLength);

        int end = raw.IndexOf((byte)0);
        if (end < 0)
            end = raw.Length;

        Span<char> chars = stackalloc char[end];
        for (int i = 0; i < end; i++)
        {
            byte b = raw[i];
            // Titles are ASCII; anything else in a dump is garbage — keep it printable.
            chars[i] = b is >= 0x20 and < 0x7F ? (char)b : '?';
        }
        return new string(chars).TrimEnd();
    }
}

public sealed class InvalidRomException : Exception
{
    public InvalidRomException(string message) : base(message) { }
}
