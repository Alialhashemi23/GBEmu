namespace GBEmu.Core.Audio;

/// <summary>
/// The wave channel (CH3): plays 32 4-bit samples from wave RAM. While the
/// channel runs, CPU wave-RAM access only works in the brief window right
/// after the channel fetches a sample (DMG behaviour), and it reaches the
/// byte the channel is playing rather than the addressed one.
/// </summary>
public sealed class WaveChannel
{
    public readonly byte[] WaveRam = new byte[16];

    public bool Enabled { get; private set; }
    private bool DacOn => (_nr30 & 0x80) != 0;

    private byte _nr30, _nr31, _nr32, _nr33, _nr34;

    private int _lengthCounter;
    private int _frequencyTimer;
    private int _position;        // 0-31 sample index
    private byte _sampleBuffer;   // byte fetched at the last step
    private int _fetchAge = 1000; // T-cycles since the last fetch

    private int Frequency => ((_nr34 & 0x07) << 8) | _nr33;

    public int Output
    {
        get
        {
            if (!Enabled)
                return 0;
            int sample = (_position & 1) == 0 ? _sampleBuffer >> 4 : _sampleBuffer & 0x0F;
            return ((_nr32 >> 5) & 0x03) switch
            {
                0 => 0,
                1 => sample,
                2 => sample >> 1,
                _ => sample >> 2,
            };
        }
    }

    public void Tick(int tCycles)
    {
        for (int i = 0; i < tCycles; i++)
        {
            _fetchAge++;
            if (Enabled && --_frequencyTimer <= 0)
            {
                _frequencyTimer = (2048 - Frequency) * 2;
                _position = (_position + 1) & 31;
                _sampleBuffer = WaveRam[_position >> 1];
                _fetchAge = 0;
            }
        }
    }

    public void ClockLength()
    {
        if ((_nr34 & 0x40) != 0 && _lengthCounter > 0 && --_lengthCounter == 0)
            Enabled = false;
    }

    public byte ReadWaveRam(int offset)
    {
        if (!Enabled)
            return WaveRam[offset];
        // While playing, access lands on the byte the channel just fetched —
        // and only during the fetch window.
        return _fetchAge < 2 ? WaveRam[_position >> 1] : (byte)0xFF;
    }

    public void WriteWaveRam(int offset, byte value)
    {
        if (!Enabled)
            WaveRam[offset] = value;
        else if (_fetchAge < 2)
            WaveRam[_position >> 1] = value;
    }

    public byte ReadRegister(int index) => index switch
    {
        0 => (byte)(_nr30 | 0x7F),
        1 => 0xFF,
        2 => (byte)(_nr32 | 0x9F),
        3 => 0xFF,
        _ => (byte)(_nr34 | 0xBF),
    };

    public void WriteRegister(int index, byte value, bool powered, bool fsNextStepClocksLength)
    {
        if (!powered)
        {
            if (index == 1)
                _lengthCounter = 256 - value;
            return;
        }

        switch (index)
        {
            case 0:
                _nr30 = value;
                if (!DacOn)
                    Enabled = false;
                break;
            case 1:
                _nr31 = value;
                _lengthCounter = 256 - value;
                break;
            case 2:
                _nr32 = value;
                break;
            case 3:
                _nr33 = value;
                break;
            case 4:
                bool wasEnabled = (_nr34 & 0x40) != 0;
                _nr34 = value;
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
        // DMG wave RAM corruption: retriggering while the channel is reading
        // wave RAM clobbers its first byte(s) with data near the read point.
        if (Enabled && _frequencyTimer <= 2)
        {
            int byteIndex = ((_position + 1) & 31) >> 1;
            if (byteIndex < 4)
                WaveRam[0] = WaveRam[byteIndex];
            else
                Array.Copy(WaveRam, byteIndex & ~3, WaveRam, 0, 4);
        }

        Enabled = DacOn;

        if (_lengthCounter == 0)
        {
            _lengthCounter = 256;
            if ((_nr34 & 0x40) != 0 && !fsNextStepClocksLength)
                _lengthCounter = 255;
        }

        _position = 0;
        _frequencyTimer = (2048 - Frequency) * 2 + 6; // short trigger delay
    }

    public void PowerOff()
    {
        _nr30 = 0;
        _nr31 = 0;
        _nr32 = 0;
        _nr33 = 0;
        _nr34 = 0;
        Enabled = false;
        _position = 0;
        // Wave RAM survives power-off.
    }
}
