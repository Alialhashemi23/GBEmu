using GBEmu.Core.Cartridge;
using GBEmu.Desktop;

if (args.Length != 1)
{
    Console.Error.WriteLine("Usage: GBEmu.Desktop <rom.gb>");
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

using var game = new EmulatorGame(rom, header.Title);
game.Run();
return 0;
