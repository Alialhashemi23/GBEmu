namespace GBEmu.Core.Cartridge;

/// <summary>A loaded cartridge: parsed header + the mapper chip serving reads/writes.</summary>
public sealed class Cartridge
{
    public CartridgeHeader Header { get; }
    private readonly IMapper _mapper;

    private Cartridge(CartridgeHeader header, IMapper mapper)
    {
        Header = header;
        _mapper = mapper;
    }

    public static Cartridge Load(byte[] rom)
    {
        CartridgeHeader header = CartridgeHeader.Parse(rom);
        IMapper mapper = header.CartridgeType.Mapper() switch
        {
            MapperKind.None => new NoMbc(rom, header.RamSizeBytes),
            MapperKind.Mbc1 => new Mbc1(rom, header.RomBankCount, header.RamSizeBytes),
            MapperKind.Mbc2 => new Mbc2(rom, header.RomBankCount),
            MapperKind.Mbc3 => new Mbc3(rom, header.RomBankCount, header.RamSizeBytes),
            MapperKind.Mbc5 => new Mbc5(rom, header.RomBankCount, header.RamSizeBytes),
            _ => throw new NotSupportedException(
                $"Cartridge type {header.CartridgeType} is not supported."),
        };
        return new Cartridge(header, mapper);
    }

    /// <summary>Reads from the ROM window (0x0000-0x7FFF) or external RAM (0xA000-0xBFFF).</summary>
    public byte Read(ushort address) => _mapper.Read(address);

    /// <summary>Banking control writes (ROM range) or external RAM writes.</summary>
    public void Write(ushort address, byte value) => _mapper.Write(address, value);

    /// <summary>External RAM for battery saves; null when the cartridge has none.</summary>
    public byte[]? ExternalRam => _mapper.Ram;
}
