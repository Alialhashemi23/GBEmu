namespace GBEmu.Tests.Roms;

/// <summary>
/// Mooneye test suite ROMs (built from source by scripts/fetch-test-data.sh).
/// Only tests our current subsystems can satisfy are included; the rest of
/// the acceptance suite joins in later phases.
/// </summary>
public class MooneyeTests
{
    // Known failure, revisit in the Phase 6 accuracy pass:
    //   acceptance/timer/rapid_toggle.gb — interrupt lands one loop iteration
    //   late (BC=FFD8 vs FFD9). Root cause: we don't model the SM83
    //   fetch/execute overlap, which shifts I/O writes by one M-cycle
    //   relative to interrupt sampling.
    [MooneyeRomTheory]
    [InlineData("acceptance/timer/div_write.gb")]
    [InlineData("acceptance/timer/tim00.gb")]
    [InlineData("acceptance/timer/tim00_div_trigger.gb")]
    [InlineData("acceptance/timer/tim01.gb")]
    [InlineData("acceptance/timer/tim01_div_trigger.gb")]
    [InlineData("acceptance/timer/tim10.gb")]
    [InlineData("acceptance/timer/tim10_div_trigger.gb")]
    [InlineData("acceptance/timer/tim11.gb")]
    [InlineData("acceptance/timer/tim11_div_trigger.gb")]
    [InlineData("acceptance/timer/tima_reload.gb")]
    [InlineData("acceptance/timer/tima_write_reloading.gb")]
    [InlineData("acceptance/timer/tma_write_reloading.gb")]
    public void Timer_rom_passes(string rom) => AssertPasses(rom);

    [MooneyeRomTheory]
    [InlineData("acceptance/instr/daa.gb")]
    public void Instr_rom_passes(string rom) => AssertPasses(rom);

    [MooneyeRomTheory]
    [InlineData("emulator-only/mbc1/bits_bank1.gb")]
    [InlineData("emulator-only/mbc1/bits_bank2.gb")]
    [InlineData("emulator-only/mbc1/bits_mode.gb")]
    [InlineData("emulator-only/mbc1/bits_ramg.gb")]
    [InlineData("emulator-only/mbc1/ram_64kb.gb")]
    [InlineData("emulator-only/mbc1/ram_256kb.gb")]
    [InlineData("emulator-only/mbc1/rom_512kb.gb")]
    [InlineData("emulator-only/mbc1/rom_1Mb.gb")]
    [InlineData("emulator-only/mbc1/rom_2Mb.gb")]
    [InlineData("emulator-only/mbc1/rom_4Mb.gb")]
    [InlineData("emulator-only/mbc1/rom_8Mb.gb")]
    [InlineData("emulator-only/mbc1/rom_16Mb.gb")]
    // multicart_rom_8Mb is excluded: MBC1M wiring is deliberately unsupported.
    public void Mbc1_rom_passes(string rom) => AssertPasses(rom);

    [MooneyeRomTheory]
    [InlineData("emulator-only/mbc2/bits_ramg.gb")]
    [InlineData("emulator-only/mbc2/bits_romb.gb")]
    [InlineData("emulator-only/mbc2/bits_unused.gb")]
    [InlineData("emulator-only/mbc2/ram.gb")]
    [InlineData("emulator-only/mbc2/rom_512kb.gb")]
    [InlineData("emulator-only/mbc2/rom_1Mb.gb")]
    [InlineData("emulator-only/mbc2/rom_2Mb.gb")]
    public void Mbc2_rom_passes(string rom) => AssertPasses(rom);

    [MooneyeRomTheory]
    [InlineData("emulator-only/mbc5/rom_512kb.gb")]
    [InlineData("emulator-only/mbc5/rom_1Mb.gb")]
    [InlineData("emulator-only/mbc5/rom_2Mb.gb")]
    [InlineData("emulator-only/mbc5/rom_4Mb.gb")]
    [InlineData("emulator-only/mbc5/rom_8Mb.gb")]
    [InlineData("emulator-only/mbc5/rom_16Mb.gb")]
    [InlineData("emulator-only/mbc5/rom_32Mb.gb")]
    [InlineData("emulator-only/mbc5/rom_64Mb.gb")]
    public void Mbc5_rom_passes(string rom) => AssertPasses(rom);

    private static void AssertPasses(string relativePath)
    {
        string path = Path.Combine(TestData.MooneyeDir, relativePath);
        Assert.Equal("Passed", RomTestRunner.RunMooneye(path));
    }
}
