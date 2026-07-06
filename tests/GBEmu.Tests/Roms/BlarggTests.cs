namespace GBEmu.Tests.Roms;

/// <summary>
/// Blargg's CPU test ROMs, run on the full machine (real bus + mappers).
/// Skipped when tests/data is absent (run scripts/fetch-test-data.sh).
/// </summary>
public class BlarggTests
{
    [BlarggRomTheory]
    [InlineData("01-special.gb")]
    [InlineData("02-interrupts.gb")]
    [InlineData("03-op sp,hl.gb")]
    [InlineData("04-op r,imm.gb")]
    [InlineData("05-op rp.gb")]
    [InlineData("06-ld r,r.gb")]
    [InlineData("07-jr,jp,call,ret,rst.gb")]
    [InlineData("08-misc instrs.gb")]
    [InlineData("09-op r,r.gb")]
    [InlineData("10-bit ops.gb")]
    [InlineData("11-op a,(hl).gb")]
    public void Cpu_instrs_rom_passes(string romName)
    {
        string output = RomTestRunner.RunBlargg(Path.Combine(TestData.CpuInstrsDir, romName));
        Assert.Contains("Passed", output);
    }

    [BlarggRomTheory]
    [InlineData("cpu_instrs")] // the combined 64 KiB MBC1 ROM — exercises bank switching
    public void Combined_cpu_instrs_rom_passes(string name)
    {
        string path = Path.Combine(TestData.Root, name, name + ".gb");
        string output = RomTestRunner.RunBlargg(path, cycleBudget: 2_000_000_000);
        Assert.Contains("Passed", output);
        Assert.DoesNotContain("Failed", output);
    }

    [BlarggRomTheory]
    [InlineData("instr_timing")]
    public void Instr_timing_rom_passes(string name)
    {
        string path = Path.Combine(TestData.Root, name, name + ".gb");
        string output = RomTestRunner.RunBlargg(path);
        Assert.Contains("Passed", output);
    }

    [BlarggRomTheory]
    [InlineData("01-read_timing.gb")]
    [InlineData("02-write_timing.gb")]
    [InlineData("03-modify_timing.gb")]
    public void Mem_timing_rom_passes(string romName)
    {
        string path = Path.Combine(TestData.Root, "mem_timing", "individual", romName);
        string output = RomTestRunner.RunBlargg(path);
        Assert.Contains("Passed", output);
    }
}
