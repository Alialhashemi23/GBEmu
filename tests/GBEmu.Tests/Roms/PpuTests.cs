using GBEmu.Core;

namespace GBEmu.Tests.Roms;

public class PpuTests
{
    /// <summary>
    /// dmg-acid2 (mattcurrie): renders a face whose every feature depends on
    /// a specific PPU behaviour. Our output was verified pixel-identical to
    /// img/reference-dmg.png in the dmg-acid2 repository; this hash pins that
    /// exact framebuffer.
    /// </summary>
    [Acid2RomFact]
    public void Dmg_acid2_renders_reference_image()
    {
        var gb = new GameBoy(File.ReadAllBytes(Path.Combine(TestData.Root, "acid2", "dmg-acid2.gb")));
        for (int i = 0; i < 120; i++)
            gb.RunFrame();

        Assert.Equal(0xF272A8FFE3DB4C16UL, Fnv1a(gb.Ppu.FrameShades));
    }

    private static ulong Fnv1a(ReadOnlySpan<byte> data)
    {
        ulong hash = 14695981039346656037;
        foreach (byte b in data)
        {
            hash ^= b;
            hash *= 1099511628211;
        }
        return hash;
    }
}

public sealed class Acid2RomFactAttribute : FactAttribute
{
    public Acid2RomFactAttribute()
    {
        if (!File.Exists(Path.Combine(TestData.Root, "acid2", "dmg-acid2.gb")))
            Skip = "dmg-acid2.gb not found — run scripts/fetch-test-data.sh";
    }
}
