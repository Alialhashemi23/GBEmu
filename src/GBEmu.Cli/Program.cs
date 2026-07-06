using GBEmu.Core;
using GBEmu.Core.Cartridge;

if (args.Length < 1)
{
    Console.Error.WriteLine("Usage: gbemu <rom.gb>                 print the cartridge header");
    Console.Error.WriteLine("       gbemu <rom.gb> --run [--max-cycles N]");
    Console.Error.WriteLine("                                      run headless, stream serial output");
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

if (args.Contains("--run"))
    return RunHeadless(rom, args);

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

// Runs the machine with serial output streamed to stdout. Exits 0 when the
// ROM prints "Passed" (Blargg convention), 1 on "Failed" or budget exhaustion.
static int RunHeadless(byte[] rom, string[] args)
{
    long maxCycles = 2_000_000_000;
    int maxIndex = Array.IndexOf(args, "--max-cycles");
    if (maxIndex >= 0 && maxIndex + 1 < args.Length)
        maxCycles = long.Parse(args[maxIndex + 1]);

    var gb = new GameBoy(rom);
    var received = new System.Text.StringBuilder();
    int verdict = -1;
    gb.Bus.SerialByteTransferred += b =>
    {
        Console.Write((char)b);
        received.Append((char)b);
        if (b == 'd') // last letter of both "Passed" and "Failed"
        {
            string text = received.ToString();
            if (text.EndsWith("Passed")) verdict = 0;
            else if (text.EndsWith("Failed")) verdict = 1;
        }
    };

    long total = 0;
    while (verdict < 0 && total < maxCycles)
        total += gb.Step();

    if (verdict >= 0)
    {
        Console.WriteLine();
        return verdict;
    }
    Console.Error.WriteLine($"\n[no verdict after {total} T-cycles]");
    return 1;
}
