namespace GBEmu.Core.Audio;

/// <summary>
/// Square/pulse channel (CH1 and CH2). CH1 additionally has the frequency
/// sweep unit. Length, envelope, and sweep behaviours follow the gbdev wiki's
/// "obscure behaviour" notes, which Blargg's dmg_sound suite tests.
/// </summary>
public sealed class SquareChannel
{
    private static readonly byte[] DutyTable =
    {
        0b00000001, // 12.5%
        0b10000001, // 25%
        0b10000111, // 50%
        0b01111110, // 75%
    };

    private readonly bool _hasSweep;

    public bool Enabled { get; private set; }
    private bool DacOn => (_nrX2 & 0xF8) != 0;

    // Registers as written.
    private byte _nrX0, _nrX1, _nrX2, _nrX3, _nrX4;

    private int _lengthCounter;
    private int _frequencyTimer;
    private int _dutyStep;

    private int _envelopeVolume;
    private int _envelopeTimer;

    private int _sweepShadow;
    private int _sweepTimer;
    private bool _sweepEnabled;
    private bool _sweepNegateUsed;

    public SquareChannel(bool hasSweep) => _hasSweep = hasSweep;

    private int Frequency => ((_nrX4 & 0x07) << 8) | _nrX3;

    /// <summary>Digital output 0-15 (before the DAC).</summary>
    public int Output => Enabled && ((DutyTable[_nrX1 >> 6] >> _dutyStep) & 1) != 0
        ? _envelopeVolume
        : 0;

    public void Tick(int tCycles)
    {
        _frequencyTimer -= tCycles;
        while (_frequencyTimer <= 0)
        {
            _frequencyTimer += (2048 - Frequency) * 4;
            _dutyStep = (_dutyStep + 1) & 7;
        }
    }

    public void ClockLength()
    {
        if ((_nrX4 & 0x40) != 0 && _lengthCounter > 0 && --_lengthCounter == 0)
            Enabled = false;
    }

    public void ClockEnvelope()
    {
        int period = _nrX2 & 0x07;
        if (period == 0)
            return;
        if (--_envelopeTimer <= 0)
        {
            _envelopeTimer = period;
            int direction = (_nrX2 & 0x08) != 0 ? 1 : -1;
            int next = _envelopeVolume + direction;
            if (next is >= 0 and <= 15)
                _envelopeVolume = next;
        }
    }

    public void ClockSweep()
    {
        if (!_hasSweep || --_sweepTimer > 0)
            return;

        int period = (_nrX0 >> 4) & 0x07;
        _sweepTimer = period != 0 ? period : 8;
        if (!_sweepEnabled || period == 0)
            return;

        int next = SweepCalculation();
        if (next <= 2047 && (_nrX0 & 0x07) != 0)
        {
            _nrX3 = (byte)next;
            _nrX4 = (byte)((_nrX4 & 0xF8) | (next >> 8));
            _sweepShadow = next;
            SweepCalculation(); // second calculation, overflow check only
        }
    }

    private int SweepCalculation()
    {
        int delta = _sweepShadow >> (_nrX0 & 0x07);
        int next;
        if ((_nrX0 & 0x08) != 0)
        {
            next = _sweepShadow - delta;
            _sweepNegateUsed = true;
        }
        else
        {
            next = _sweepShadow + delta;
        }
        if (next > 2047)
            Enabled = false;
        return next;
    }

    public byte ReadRegister(int index) => index switch
    {
        0 => (byte)(_nrX0 | 0x80),
        1 => (byte)(_nrX1 | 0x3F),
        2 => _nrX2,
        3 => 0xFF,
        _ => (byte)(_nrX4 | 0xBF),
    };

    /// <summary>fsNextStepClocksLength: whether the frame sequencer's next step clocks length.</summary>
    public void WriteRegister(int index, byte value, bool powered, bool fsNextStepClocksLength)
    {
        if (!powered)
        {
            // DMG: length counters remain writable while the APU is off.
            if (index == 1)
                _lengthCounter = 64 - (value & 0x3F);
            return;
        }

        switch (index)
        {
            case 0:
                bool oldNegate = (_nrX0 & 0x08) != 0;
                _nrX0 = value;
                // Clearing negate after a negate-mode calculation kills the channel.
                if (_hasSweep && oldNegate && (value & 0x08) == 0 && _sweepNegateUsed)
                    Enabled = false;
                break;
            case 1:
                _nrX1 = value;
                _lengthCounter = 64 - (value & 0x3F);
                break;
            case 2:
                _nrX2 = value;
                if (!DacOn)
                    Enabled = false;
                break;
            case 3:
                _nrX3 = value;
                break;
            case 4:
                bool wasEnabled = (_nrX4 & 0x40) != 0;
                _nrX4 = value;
                bool nowEnabled = (value & 0x40) != 0;

                // Enabling length when the next FS step won't clock it causes
                // an immediate extra clock.
                if (!wasEnabled && nowEnabled && !fsNextStepClocksLength
                    && _lengthCounter > 0 && --_lengthCounter == 0 && (value & 0x80) == 0)
                {
                    Enabled = false;
                }

                if ((value & 0x80) != 0)
                    Trigger(fsNextStepClocksLength);
                break;
        }
    }

    private void Trigger(bool fsNextStepClocksLength)
    {
        Enabled = DacOn;

        if (_lengthCounter == 0)
        {
            _lengthCounter = 64;
            if ((_nrX4 & 0x40) != 0 && !fsNextStepClocksLength)
                _lengthCounter = 63;
        }

        _frequencyTimer = (2048 - Frequency) * 4;
        _envelopeVolume = _nrX2 >> 4;
        _envelopeTimer = _nrX2 & 0x07;

        if (_hasSweep)
        {
            _sweepShadow = Frequency;
            int period = (_nrX0 >> 4) & 0x07;
            _sweepTimer = period != 0 ? period : 8;
            _sweepEnabled = period != 0 || (_nrX0 & 0x07) != 0;
            _sweepNegateUsed = false;
            if ((_nrX0 & 0x07) != 0)
                SweepCalculation(); // immediate overflow check
        }
    }

    public void PowerOff()
    {
        // Length counters survive power-off on DMG; everything else clears.
        _nrX0 = 0;
        _nrX1 = 0;
        _nrX2 = 0;
        _nrX3 = 0;
        _nrX4 = 0;
        Enabled = false;
        _envelopeVolume = 0;
        _dutyStep = 0;
        _sweepEnabled = false;
        _sweepNegateUsed = false;
    }
}
