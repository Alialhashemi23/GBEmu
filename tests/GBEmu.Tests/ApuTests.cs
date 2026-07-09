using GBEmu.Core;
using GBEmu.Core.Cartridge;

namespace GBEmu.Tests;

public class ApuTests
{
    [Fact]
    public void Produces_samples_at_the_configured_rate()
    {
        var gb = new GameBoy(TestRom.Build(type: CartridgeType.RomOnly));

        // 60 frames ≈ 1.004s of emulated time at 70224 T-cycles per frame.
        int total = 0;
        var buffer = new short[16384];
        for (int i = 0; i < 60; i++)
        {
            gb.RunFrame();
            total += gb.Bus.Apu.ReadSamples(buffer) / 2;
        }

        int expected = (int)(60 * 70224 / 4194304.0 * GBEmu.Core.Audio.Apu.SampleRate);
        Assert.InRange(total, expected - 100, expected + 100);
    }

    [Fact]
    public void Square_channel_emits_audible_samples_when_played()
    {
        var gb = new GameBoy(TestRom.Build(type: CartridgeType.RomOnly));
        var bus = gb.Bus;

        // Program CH2: full volume, 50% duty, mid frequency, trigger, route everywhere.
        bus.Write(0xFF26, 0x80); // power
        bus.Write(0xFF25, 0xFF); // route all channels L+R
        bus.Write(0xFF24, 0x77); // max master volume
        bus.Write(0xFF16, 0x80); // duty 50%
        bus.Write(0xFF17, 0xF0); // volume 15, no envelope
        bus.Write(0xFF18, 0x00);
        bus.Write(0xFF19, 0x87); // trigger, freq high bits

        gb.RunFrame();
        var buffer = new short[16384];
        int shorts = bus.Apu.ReadSamples(buffer);

        Assert.True(shorts > 1000, "expected a frame's worth of samples");
        Assert.Contains(buffer.Take(shorts), s => s != 0);
    }
}
