using GBEmu.Core.Cpu;

namespace GBEmu.Tests.Cpu;

/// <summary>
/// Runs Blargg's cpu_instrs sub-ROMs headlessly. Each ROM prints its result
/// over the serial port: "Passed" on success, "Failed" plus details otherwise.
/// Skipped when tests/data/cpu_instrs is absent (run scripts/fetch-test-data.sh).
/// </summary>
public class BlarggCpuInstrsTests
{
    private const long CycleBudget = 400_000_000; // ~95 emulated seconds

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
    public void Rom_reports_passed(string romName)
    {
        string path = Path.Combine(TestData.CpuInstrsDir, romName);

        string output = RunUntilVerdict(File.ReadAllBytes(path));

        Assert.Contains("Passed", output);
    }

    private static string RunUntilVerdict(byte[] rom)
    {
        var bus = new BlarggBus(rom);
        var cpu = new Sm83(bus);

        long total = 0;
        long nextCheck = 1_000_000;
        while (total < CycleBudget)
        {
            int cycles = cpu.Step();
            total += cycles;
            bus.StepPeripherals(cycles);

            if (total >= nextCheck)
            {
                nextCheck = total + 1_000_000;
                string text = bus.SerialText;
                if (text.Contains("Passed") || text.Contains("Failed"))
                    return text;
            }
        }
        return bus.SerialText + "\n[TIMED OUT after " + total + " T-cycles]";
    }
}
