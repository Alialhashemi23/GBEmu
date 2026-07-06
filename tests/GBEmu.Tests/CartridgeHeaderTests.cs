using GBEmu.Core.Cartridge;

namespace GBEmu.Tests;

public class CartridgeHeaderTests
{
    [Fact]
    public void Parses_basic_header_fields()
    {
        byte[] rom = TestRom.Build(title: "TETRIS", type: CartridgeType.RomOnly);

        var header = CartridgeHeader.Parse(rom);

        Assert.Equal("TETRIS", header.Title);
        Assert.Equal(CartridgeType.RomOnly, header.CartridgeType);
        Assert.Equal(32 * 1024, header.RomSizeBytes);
        Assert.Equal(2, header.RomBankCount);
        Assert.Equal(0, header.RamSizeBytes);
        Assert.False(header.HasBattery);
        Assert.True(header.HeaderChecksumValid);
        Assert.True(header.GlobalChecksumValid);
    }

    [Theory]
    [InlineData(0x00, 2)]      // 32 KiB
    [InlineData(0x01, 4)]      // 64 KiB
    [InlineData(0x05, 64)]     // 1 MiB
    [InlineData(0x08, 512)]    // 8 MiB
    public void Decodes_rom_size_codes(byte code, int expectedBanks)
    {
        var header = CartridgeHeader.Parse(TestRom.Build(romSizeCode: code));

        Assert.Equal(expectedBanks, header.RomBankCount);
        Assert.Equal(expectedBanks * 0x4000, header.RomSizeBytes);
    }

    [Theory]
    [InlineData(0x00, 0)]
    [InlineData(0x02, 8 * 1024)]
    [InlineData(0x03, 32 * 1024)]
    [InlineData(0x04, 128 * 1024)]
    [InlineData(0x05, 64 * 1024)]
    public void Decodes_ram_size_codes(byte code, int expectedBytes)
    {
        var header = CartridgeHeader.Parse(TestRom.Build(ramSizeCode: code));

        Assert.Equal(expectedBytes, header.RamSizeBytes);
    }

    [Theory]
    [InlineData(CartridgeType.Mbc1RamBattery, true)]
    [InlineData(CartridgeType.Mbc3TimerRamBattery, true)]
    [InlineData(CartridgeType.Mbc5RamBattery, true)]
    [InlineData(CartridgeType.Mbc1, false)]
    [InlineData(CartridgeType.RomOnly, false)]
    public void Detects_battery_backed_types(CartridgeType type, bool expected)
    {
        var header = CartridgeHeader.Parse(TestRom.Build(type: type));

        Assert.Equal(expected, header.HasBattery);
    }

    [Theory]
    [InlineData(CartridgeType.RomOnly, MapperKind.None)]
    [InlineData(CartridgeType.Mbc1RamBattery, MapperKind.Mbc1)]
    [InlineData(CartridgeType.Mbc2Battery, MapperKind.Mbc2)]
    [InlineData(CartridgeType.Mbc3RamBattery, MapperKind.Mbc3)]
    [InlineData(CartridgeType.Mbc5Rumble, MapperKind.Mbc5)]
    [InlineData(CartridgeType.HuC3, MapperKind.Unsupported)]
    public void Maps_cartridge_type_to_mapper(CartridgeType type, MapperKind expected)
    {
        Assert.Equal(expected, type.Mapper());
    }

    [Fact]
    public void Cgb_header_uses_shortened_title()
    {
        // 15-char title, but CGB flag 0x80 limits the title field to 11 bytes.
        byte[] rom = TestRom.Build(title: "ABCDEFGHIJKLMNO", cgbFlag: 0x80);

        var header = CartridgeHeader.Parse(rom);

        Assert.Equal("ABCDEFGHIJK", header.Title);
        Assert.False(header.RequiresCgb);
    }

    [Fact]
    public void Cgb_only_flag_is_reported()
    {
        var header = CartridgeHeader.Parse(TestRom.Build(cgbFlag: 0xC0));

        Assert.True(header.RequiresCgb);
    }

    [Fact]
    public void Detects_corrupted_header_checksum()
    {
        byte[] rom = TestRom.Build();
        rom[0x134] ^= 0xFF; // corrupt a title byte without fixing the checksum

        var header = CartridgeHeader.Parse(rom);

        Assert.False(header.HeaderChecksumValid);
    }

    [Fact]
    public void Rejects_rom_smaller_than_header()
    {
        var tiny = new byte[0x100];

        Assert.Throws<InvalidRomException>(() => CartridgeHeader.Parse(tiny));
    }

    [Theory]
    [InlineData(0x148, 0x09)] // ROM size code out of range
    [InlineData(0x149, 0x06)] // RAM size code out of range
    public void Rejects_unknown_size_codes(int offset, byte badValue)
    {
        byte[] rom = TestRom.Build();
        rom[offset] = badValue;

        Assert.Throws<InvalidRomException>(() => CartridgeHeader.Parse(rom));
    }

    [Fact]
    public void Non_ascii_title_bytes_become_placeholders()
    {
        byte[] rom = TestRom.Build(title: "AB");
        rom[0x136] = 0xE5; // non-ASCII byte inside the title field
        rom[0x14D] = CartridgeHeader.ComputeHeaderChecksum(rom);

        var header = CartridgeHeader.Parse(rom);

        Assert.Equal("AB?", header.Title);
    }
}
