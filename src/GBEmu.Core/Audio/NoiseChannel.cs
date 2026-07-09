namespace GBEmu.Core.Audio;

/// <summary>The noise channel (CH4): a 15-bit LFSR with optional 7-bit mode.</summary>
public sealed class NoiseChannel
{
    public bool Enabled { get; private set; }
    private bool DacOn => (_nr42 & 0xF8) != 0;

    private byte _nr41, _nr42, _nr43, _nr44;

    private int _lengthCounter;
    private int _frequencyTimer;
    private ushort _lfsr = 0x7FFF;

    private int _envelopeVolume;
    private int _envelopeTimer;

    public int Output => Enabled && (_lfsr & 1) == 0 ? _envelopeVolume : 0;

    private int Period
    {
        get
        {
            int divisor = (_nr43 & 0x07) == 0 ? 8 : (_nr43 & 0x07) * 16;
            return divisor << (_nr43 >> 4);
        }
    }

    public void Tick(int tCycles)
    {
        if ((_nr43 >> 4) >= 14)
            return; // invalid clock shifts stop the LFSR
        _frequencyTimer -= tCycles;
        while (_frequencyTimer <= 0)
        {
            _frequencyTimer += Period;
            int feedback = (_lfsr ^ (_lfsr >> 1)) & 1;
            _lfsr >>= 1;
            _lfsr |= (ushort)(feedback << 14);
            if ((_nr43 & 0x08) != 0)
                _lfsr = (ushort)((_lfsr & ~0x40) | (feedback << 6)); // 7-bit mode
        }
    }

    public void ClockLength()
    {
        if ((_nr44 & 0x40) != 0 && _lengthCounter > 0 && --_lengthCounter == 0)
            Enabled = false;
    }

    public void ClockEnvelope()
    {
        int period = _nr42 & 0x07;
        if (period == 0)
            return;
        if (--_envelopeTimer <= 0)
        {
            _envelopeTimer = period;
            int direction = (_nr42 & 0x08) != 0 ? 1 : -1;
            int next = _envelopeVolume + direction;
            if (next is >= 0 and <= 15)
                _envelopeVolume = next;
        }
    }

    public byte ReadRegister(int index) => index switch
    {
        1 => 0xFF,
        2 => _nr42,
        3 => _nr43,
        4 => (byte)(_nr44 | 0xBF),
        _ => 0xFF,
    };

    public void WriteRegister(int index, byte value, bool powered, bool fsNextStepClocksLength)
    {
        if (!powered)
        {
            if (index == 1)
                _lengthCounter = 64 - (value & 0x3F);
            return;
        }

        switch (index)
        {
            case 1:
                _nr41 = value;
                _lengthCounter = 64 - (value & 0x3F);
                break;
            case 2:
                _nr42 = value;
                if (!DacOn)
                    Enabled = false;
                break;
            case 3:
                _nr43 = value;
                break;
            case 4:
                bool wasEnabled = (_nr44 & 0x40) != 0;
                _nr44 = value;
                bool nowEnabled = (value & 0x40) != 0;

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
            if ((_nr44 & 0x40) != 0 && !fsNextStepClocksLength)
                _lengthCounter = 63;
        }

        _frequencyTimer = Period;
        _lfsr = 0x7FFF;
        _envelopeVolume = _nr42 >> 4;
        _envelopeTimer = _nr42 & 0x07;
    }

    public void PowerOff()
    {
        _nr41 = 0;
        _nr42 = 0;
        _nr43 = 0;
        _nr44 = 0;
        Enabled = false;
        _envelopeVolume = 0;
    }
}
