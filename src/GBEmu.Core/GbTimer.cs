namespace GBEmu.Core;

/// <summary>
/// The DIV/TIMA/TMA/TAC timer unit.
///
/// DIV is the high byte of a free-running 16-bit counter. TIMA increments on
/// the falling edge of one counter bit (selected by TAC) ANDed with the TAC
/// enable — modelling it this way makes the classic quirks (DIV-write causing
/// a TIMA tick, TAC changes glitching) fall out naturally.
///
/// TIMA overflow behaviour: the counter reads 0x00 for 4 T-cycles, then TMA
/// is loaded and the interrupt is requested. Writing TIMA during those 4
/// cycles cancels the reload; during the 4 cycles after the reload, TIMA
/// writes are ignored and TMA writes propagate into TIMA.
/// </summary>
public sealed class GbTimer
{
    private ushort _counter = 0xABCC; // post-boot value (DIV reads 0xAB)
    private bool _lastSignal;

    private int _reloadCountdown = -1; // T-cycles until reload; -1 = idle
    private int _reloadedWindow;       // T-cycles left in the just-reloaded window

    public byte Tima;
    public byte Tma;
    private byte _tac = 0xF8;

    /// <summary>Set when the overflow reload fires; the bus transfers this into IF bit 2.</summary>
    public bool InterruptRequested;

    public byte Div => (byte)(_counter >> 8);

    public byte ReadRegister(ushort address) => address switch
    {
        0xFF04 => Div,
        0xFF05 => Tima,
        0xFF06 => Tma,
        0xFF07 => (byte)(_tac | 0xF8),
        _ => 0xFF,
    };

    public void WriteRegister(ushort address, byte value)
    {
        switch (address)
        {
            case 0xFF04:
                _counter = 0; // any write resets DIV (may itself tick TIMA via the edge)
                UpdateEdge();
                break;
            case 0xFF05:
                if (_reloadedWindow > 0)
                    break; // write ignored in the cycle window right after reload
                Tima = value;
                _reloadCountdown = -1; // a write during the delay cancels the reload
                break;
            case 0xFF06:
                Tma = value;
                if (_reloadedWindow > 0)
                    Tima = value; // reload window forwards TMA writes into TIMA
                break;
            case 0xFF07:
                _tac = (byte)(value | 0xF8);
                UpdateEdge();
                break;
        }
    }

    public void Tick(int tCycles)
    {
        for (int i = 0; i < tCycles; i++)
        {
            if (_reloadedWindow > 0)
                _reloadedWindow--;
            if (_reloadCountdown >= 0 && --_reloadCountdown < 0)
            {
                Tima = Tma;
                InterruptRequested = true;
                _reloadedWindow = 4;
            }
            _counter++;
            UpdateEdge();
        }
    }

    private void UpdateEdge()
    {
        bool signal = (_tac & 0x04) != 0 && (_counter & SelectedBitMask()) != 0;
        if (_lastSignal && !signal && ++Tima == 0)
            _reloadCountdown = 3; // fires on the 4th T-cycle after the overflow
        _lastSignal = signal;
    }

    private int SelectedBitMask() => (_tac & 0x03) switch
    {
        0 => 1 << 9, // 4096 Hz
        1 => 1 << 3, // 262144 Hz
        2 => 1 << 5, // 65536 Hz
        _ => 1 << 7, // 16384 Hz
    };
}
