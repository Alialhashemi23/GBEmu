namespace GBEmu.Core;

/// <summary>
/// The DIV/TIMA/TMA/TAC timer unit.
///
/// DIV is the high byte of a free-running 16-bit counter. TIMA increments on
/// the falling edge of one counter bit (selected by TAC) ANDed with the TAC
/// enable — modelling it this way makes the classic quirks (DIV-write causing
/// a TIMA tick, TAC frequency changes glitching) fall out naturally.
/// The 4-cycle TIMA reload delay after overflow is not yet modelled.
/// </summary>
public sealed class GbTimer
{
    private ushort _counter = 0xABCC; // post-boot DIV value (DIV reads 0xAB)
    private bool _lastSignal;

    public byte Tima;
    public byte Tma;
    private byte _tac = 0xF8;

    /// <summary>Set when TIMA overflows; the bus owner transfers this into IF bit 2.</summary>
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
                _counter = 0; // any write resets DIV
                UpdateEdge();
                break;
            case 0xFF05: Tima = value; break;
            case 0xFF06: Tma = value; break;
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
            _counter++;
            UpdateEdge();
        }
    }

    private void UpdateEdge()
    {
        bool signal = (_tac & 0x04) != 0 && (_counter & SelectedBitMask()) != 0;
        if (_lastSignal && !signal)
        {
            if (++Tima == 0)
            {
                Tima = Tma;
                InterruptRequested = true;
            }
        }
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
