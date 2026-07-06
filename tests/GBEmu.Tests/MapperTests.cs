using GBEmu.Core.Cartridge;

namespace GBEmu.Tests;

/// <summary>
/// Mapper unit tests. MBC1/2/5 are covered end-to-end by the Mooneye ROMs in
/// MooneyeTests; these focus on MBC3 (no Mooneye coverage) and basics.
/// </summary>
public class MapperTests
{
    /// <summary>ROM where the first byte of every 16 KiB bank is the bank number.</summary>
    private static byte[] BankedRom(int banks)
    {
        var rom = new byte[banks * 0x4000];
        for (int b = 0; b < banks; b++)
            rom[b * 0x4000] = (byte)b;
        return rom;
    }

    [Fact]
    public void Mbc3_switches_rom_banks_and_maps_bank0_to_bank1()
    {
        var m = new Mbc3(BankedRom(128), romBankCount: 128, ramSize: 0x8000);

        Assert.Equal(0, m.Read(0x0000));  // fixed bank
        Assert.Equal(1, m.Read(0x4000));  // default bank

        m.Write(0x2000, 0x00);            // bank 0 selects bank 1
        Assert.Equal(1, m.Read(0x4000));

        m.Write(0x2000, 0x50);
        Assert.Equal(0x50, m.Read(0x4000));

        m.Write(0x2000, 0x7F);
        Assert.Equal(0x7F, m.Read(0x4000));
    }

    [Fact]
    public void Mbc3_ram_banking_isolates_banks_and_respects_enable()
    {
        var m = new Mbc3(BankedRom(8), romBankCount: 8, ramSize: 0x8000);

        m.Write(0xA000, 0x11); // RAM disabled: write ignored
        Assert.Equal(0xFF, m.Read(0xA000));

        m.Write(0x0000, 0x0A); // enable
        m.Write(0x4000, 0x00);
        m.Write(0xA000, 0x11);
        m.Write(0x4000, 0x03);
        m.Write(0xA000, 0x33);

        m.Write(0x4000, 0x00);
        Assert.Equal(0x11, m.Read(0xA000));
        m.Write(0x4000, 0x03);
        Assert.Equal(0x33, m.Read(0xA000));

        m.Write(0x0000, 0x00); // disable again
        Assert.Equal(0xFF, m.Read(0xA000));
    }

    [Fact]
    public void Mbc3_rtc_registers_are_readable_after_selection()
    {
        var m = new Mbc3(BankedRom(8), romBankCount: 8, ramSize: 0x2000);

        m.Write(0x0000, 0x0A);
        m.Write(0x4000, 0x08); // RTC seconds register
        m.Write(0xA000, 42);

        Assert.Equal(42, m.Read(0xA000));

        m.Write(0x4000, 0x00); // back to RAM bank 0 — separate storage, still empty
        Assert.Equal(0, m.Read(0xA000));
        m.Write(0x4000, 0x08);
        Assert.Equal(42, m.Read(0xA000));
    }

    [Fact]
    public void Cartridge_load_exposes_external_ram_for_battery_types()
    {
        byte[] rom = TestRom.Build(type: CartridgeType.Mbc1RamBattery, ramSizeCode: 0x03);
        var cart = GBEmu.Core.Cartridge.Cartridge.Load(rom);

        Assert.NotNull(cart.ExternalRam);
        Assert.Equal(32 * 1024, cart.ExternalRam!.Length);
    }

    [Fact]
    public void Cartridge_load_rejects_unsupported_mapper()
    {
        byte[] rom = TestRom.Build(type: CartridgeType.HuC3);

        Assert.Throws<NotSupportedException>(() => GBEmu.Core.Cartridge.Cartridge.Load(rom));
    }
}
