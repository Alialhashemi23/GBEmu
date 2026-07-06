using GBEmu.Core;

namespace GBEmu.Tests;

/// <summary>
/// Unit tests for the TIMA overflow/reload state machine. TAC=0x05 selects
/// the 262144 Hz input (counter bit 3), so with DIV reset TIMA increments on
/// every 16th T-cycle.
/// </summary>
public class TimerTests
{
    private static GbTimer MakeTimer(byte tima = 0)
    {
        var t = new GbTimer();
        t.WriteRegister(0xFF07, 0x05); // enable, 262144 Hz
        t.WriteRegister(0xFF04, 0);    // reset DIV so edges land predictably
        t.WriteRegister(0xFF05, tima);
        return t;
    }

    [Fact]
    public void Tima_increments_at_selected_rate()
    {
        var t = MakeTimer();

        t.Tick(16);
        Assert.Equal(1, t.Tima);
        t.Tick(16 * 5);
        Assert.Equal(6, t.Tima);
    }

    [Fact]
    public void Overflow_reads_zero_then_reloads_tma_and_requests_interrupt()
    {
        var t = MakeTimer(tima: 0xFF);
        t.WriteRegister(0xFF06, 0x77); // TMA

        t.Tick(16); // overflow
        Assert.Equal(0x00, t.Tima); // holds 0 during the reload delay
        Assert.False(t.InterruptRequested);

        t.Tick(3);
        Assert.Equal(0x00, t.Tima);
        Assert.False(t.InterruptRequested);

        t.Tick(1); // 4th T-cycle after overflow
        Assert.Equal(0x77, t.Tima);
        Assert.True(t.InterruptRequested);
    }

    [Fact]
    public void Tima_write_during_reload_delay_cancels_reload()
    {
        var t = MakeTimer(tima: 0xFF);
        t.WriteRegister(0xFF06, 0x77);

        t.Tick(16); // overflow, delay running
        t.WriteRegister(0xFF05, 0x42);
        t.Tick(8);

        Assert.Equal(0x42, t.Tima);
        Assert.False(t.InterruptRequested);
    }

    [Fact]
    public void Tima_write_right_after_reload_is_ignored()
    {
        var t = MakeTimer(tima: 0xFF);
        t.WriteRegister(0xFF06, 0x77);

        t.Tick(20); // overflow + full delay: reload just happened
        t.WriteRegister(0xFF05, 0x42);

        Assert.Equal(0x77, t.Tima); // write swallowed inside the reload window
    }

    [Fact]
    public void Tma_write_during_reload_window_propagates_to_tima()
    {
        var t = MakeTimer(tima: 0xFF);
        t.WriteRegister(0xFF06, 0x77);

        t.Tick(20); // reload just happened
        t.WriteRegister(0xFF06, 0x55);

        Assert.Equal(0x55, t.Tima);
    }

    [Fact]
    public void Div_write_with_selected_bit_high_increments_tima()
    {
        var t = MakeTimer();

        t.Tick(8); // counter = 8: selected bit (bit 3) is high
        t.WriteRegister(0xFF04, 0); // reset drives the mux input low → falling edge

        Assert.Equal(1, t.Tima);
    }

    [Fact]
    public void Disabling_timer_with_selected_bit_high_increments_tima()
    {
        var t = MakeTimer();

        t.Tick(8);
        t.WriteRegister(0xFF07, 0x01); // clear the enable bit

        Assert.Equal(1, t.Tima);
    }
}
