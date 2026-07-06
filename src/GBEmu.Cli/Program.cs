using GBEmu.Core.Cartridge;

if (args.Length != 1)
{
    Console.Error.WriteLine("Usage: gbemu <rom.gb>");
    Console.Error.WriteLine("Prints the cartridge header of a Game Boy ROM.");
    return 2;
}

byte[] rom;
try
{
    rom = File.ReadAllBytes(args[0]);
}
catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
{
    Console.Error.WriteLine($"Could not read '{args[0]}': {ex.Message}");
    return 1;
}

CartridgeHeader header;
try
{
    header = CartridgeHeader.Parse(rom);
}
catch (InvalidRomException ex)
{
    Console.Error.WriteLine($"Not a valid Game Boy ROM: {ex.Message}");
    return 1;
}

Console.WriteLine($"File:            {Path.GetFileName(args[0])} ({rom.Length:N0} bytes)");
Console.WriteLine($"Title:           {header.Title}");
Console.WriteLine($"Cartridge type:  {header.CartridgeType} (mapper: {header.CartridgeType.Mapper()})");
Console.WriteLine($"ROM size:        {header.RomSizeBytes / 1024} KiB ({header.RomBankCount} banks)");
Console.WriteLine($"RAM size:        {header.RamSizeBytes / 1024} KiB");
Console.WriteLine($"Battery save:    {(header.HasBattery ? "yes" : "no")}");
Console.WriteLine($"CGB flag:        0x{header.CgbFlag:X2}{(header.RequiresCgb ? " (CGB only)" : "")}");
Console.WriteLine($"SGB support:     {(header.SupportsSgb ? "yes" : "no")}");
Console.WriteLine($"Destination:     {(header.DestinationCode == 0 ? "Japan" : "Overseas")}");
Console.WriteLine($"Version:         {header.MaskRomVersion}");
Console.WriteLine($"Header checksum: 0x{header.HeaderChecksum:X2} ({(header.HeaderChecksumValid ? "OK" : "BAD")})");
Console.WriteLine($"Global checksum: 0x{header.GlobalChecksum:X4} ({(header.GlobalChecksumValid ? "OK" : "BAD")})");

if (!header.HeaderChecksumValid)
{
    Console.Error.WriteLine("Warning: header checksum is invalid — a real DMG would refuse to boot this ROM.");
    return 1;
}

return 0;
