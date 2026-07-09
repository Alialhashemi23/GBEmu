namespace GBEmu.Tests.Roms;

/// <summary>Blargg's dmg_sound suite (memory-reporting protocol). All 12 pass.</summary>
public class DmgSoundTests
{
    [DmgSoundRomTheory]
    [InlineData("01-registers.gb")]
    [InlineData("02-len ctr.gb")]
    [InlineData("03-trigger.gb")]
    [InlineData("04-sweep.gb")]
    [InlineData("05-sweep details.gb")]
    [InlineData("06-overflow on trigger.gb")]
    [InlineData("07-len sweep period sync.gb")]
    [InlineData("08-len ctr during power.gb")]
    [InlineData("09-wave read while on.gb")]
    [InlineData("10-wave trigger while on.gb")]
    [InlineData("11-regs after power.gb")]
    [InlineData("12-wave write while on.gb")]
    public void Rom_passes(string romName)
    {
        string path = Path.Combine(TestData.Root, "dmg_sound", "rom_singles", romName);
        Assert.Equal("Passed", RomTestRunner.RunBlarggMemory(path));
    }
}

public sealed class DmgSoundRomTheoryAttribute : TheoryAttribute
{
    public DmgSoundRomTheoryAttribute()
    {
        if (!Directory.Exists(Path.Combine(TestData.Root, "dmg_sound")))
            Skip = "dmg_sound ROMs not found — run scripts/fetch-test-data.sh";
    }
}
