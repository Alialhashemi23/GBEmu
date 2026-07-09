namespace GBEmu.Core.Audio;

/// <summary>
/// The audio processing unit. The frame sequencer is clocked by the falling
/// edge of DIV bit 4 (bit 12 of the internal divider) — 512 Hz — stepping
/// length (0,2,4,6), sweep (2,6) and envelope (7). A fractional-step
/// resampler mixes the four DACs into an interleaved-stereo ring buffer at
/// <see cref="SampleRate"/>.
/// </summary>
public sealed class Apu
{
    public const int SampleRate = 48000;
    private const double CyclesPerSample = 4194304.0 / SampleRate;
    private const int RingCapacity = 32768; // frames

    public readonly SquareChannel Square1 = new(hasSweep: true);
    public readonly SquareChannel Square2 = new(hasSweep: false);
    public readonly WaveChannel Wave = new();
    public readonly NoiseChannel Noise = new();

    private bool _powered = true;
    private byte _nr50 = 0x77, _nr51 = 0xF3; // post-boot values
    private int _fsNextStep;
    private bool _lastDivBit;

    private readonly short[] _ring = new short[RingCapacity * 2];
    private int _ringRead, _ringWrite, _ringCount;
    private double _sampleAccumulator;

    /// <summary>True when the frame sequencer's next step clocks length counters (steps 0,2,4,6).</summary>
    private bool FsNextStepClocksLength => (_fsNextStep & 1) == 0;

    public void Tick(int tCycles, ushort divCounter)
    {
        if (_powered)
        {
            Square1.Tick(tCycles);
            Square2.Tick(tCycles);
            Wave.Tick(tCycles);
            Noise.Tick(tCycles);

            bool divBit = (divCounter & 0x1000) != 0;
            if (_lastDivBit && !divBit)
                StepFrameSequencer();
            _lastDivBit = divBit;
        }

        _sampleAccumulator += tCycles;
        while (_sampleAccumulator >= CyclesPerSample)
        {
            _sampleAccumulator -= CyclesPerSample;
            EmitSample();
        }
    }

    private void StepFrameSequencer()
    {
        int step = _fsNextStep;
        _fsNextStep = (step + 1) & 7;

        if ((step & 1) == 0)
        {
            Square1.ClockLength();
            Square2.ClockLength();
            Wave.ClockLength();
            Noise.ClockLength();
        }
        if (step is 2 or 6)
            Square1.ClockSweep();
        if (step == 7)
        {
            Square1.ClockEnvelope();
            Square2.ClockEnvelope();
            Noise.ClockEnvelope();
        }
    }

    // --- Mixing ---

    private void EmitSample()
    {
        int left = 0, right = 0;
        MixChannel(Square1.Output, 0, ref left, ref right);
        MixChannel(Square2.Output, 1, ref left, ref right);
        MixChannel(Wave.Output, 2, ref left, ref right);
        MixChannel(Noise.Output, 3, ref left, ref right);

        // Max amplitude: 4 channels × 15 × volume 8 × 64 = 30720 of ±32767.
        int leftVol = ((_nr50 >> 4) & 0x07) + 1;
        int rightVol = (_nr50 & 0x07) + 1;
        short l = (short)(left * leftVol * 64);
        short r = (short)(right * rightVol * 64);

        if (_ringCount < RingCapacity)
        {
            _ring[_ringWrite * 2] = l;
            _ring[_ringWrite * 2 + 1] = r;
            _ringWrite = (_ringWrite + 1) % RingCapacity;
            _ringCount++;
        }
    }

    private void MixChannel(int output, int channelBit, ref int left, ref int right)
    {
        if ((_nr51 & (0x10 << channelBit)) != 0)
            left += output;
        if ((_nr51 & (0x01 << channelBit)) != 0)
            right += output;
    }

    /// <summary>Frames (stereo pairs) available to drain.</summary>
    public int AvailableSamples => _ringCount;

    /// <summary>Drains up to buffer.Length/2 stereo frames; returns shorts written.</summary>
    public int ReadSamples(short[] buffer)
    {
        int frames = Math.Min(_ringCount, buffer.Length / 2);
        for (int i = 0; i < frames; i++)
        {
            buffer[i * 2] = _ring[_ringRead * 2];
            buffer[i * 2 + 1] = _ring[_ringRead * 2 + 1];
            _ringRead = (_ringRead + 1) % RingCapacity;
        }
        _ringCount -= frames;
        return frames * 2;
    }

    // --- Register access (FF10-FF26, FF30-FF3F) ---

    public byte ReadRegister(ushort address)
    {
        if (address is >= 0xFF30 and <= 0xFF3F)
            return Wave.ReadWaveRam(address - 0xFF30);

        return address switch
        {
            >= 0xFF10 and <= 0xFF14 => Square1.ReadRegister(address - 0xFF10),
            0xFF15 => 0xFF,
            >= 0xFF16 and <= 0xFF19 => Square2.ReadRegister(address - 0xFF15),
            >= 0xFF1A and <= 0xFF1E => Wave.ReadRegister(address - 0xFF1A),
            0xFF1F => 0xFF,
            >= 0xFF20 and <= 0xFF23 => Noise.ReadRegister(address - 0xFF1F),
            0xFF24 => _nr50,
            0xFF25 => _nr51,
            0xFF26 => (byte)(0x70
                             | (_powered ? 0x80 : 0)
                             | (Square1.Enabled ? 0x01 : 0)
                             | (Square2.Enabled ? 0x02 : 0)
                             | (Wave.Enabled ? 0x04 : 0)
                             | (Noise.Enabled ? 0x08 : 0)),
            _ => 0xFF,
        };
    }

    public void WriteRegister(ushort address, byte value)
    {
        if (address is >= 0xFF30 and <= 0xFF3F)
        {
            Wave.WriteWaveRam(address - 0xFF30, value);
            return;
        }

        if (address == 0xFF26)
        {
            bool wasPowered = _powered;
            _powered = (value & 0x80) != 0;
            if (wasPowered && !_powered)
            {
                Square1.PowerOff();
                Square2.PowerOff();
                Wave.PowerOff();
                Noise.PowerOff();
                _nr50 = 0;
                _nr51 = 0;
            }
            else if (!wasPowered && _powered)
            {
                _fsNextStep = 0; // frame sequencer restarts at step 0
            }
            return;
        }

        bool clocks = FsNextStepClocksLength;
        switch (address)
        {
            case >= 0xFF10 and <= 0xFF14:
                Square1.WriteRegister(address - 0xFF10, value, _powered, clocks);
                break;
            case >= 0xFF16 and <= 0xFF19:
                Square2.WriteRegister(address - 0xFF15, value, _powered, clocks);
                break;
            case >= 0xFF1A and <= 0xFF1E:
                Wave.WriteRegister(address - 0xFF1A, value, _powered, clocks);
                break;
            case >= 0xFF20 and <= 0xFF23:
                Noise.WriteRegister(address - 0xFF1F, value, _powered, clocks);
                break;
            case 0xFF24:
                if (_powered)
                    _nr50 = value;
                break;
            case 0xFF25:
                if (_powered)
                    _nr51 = value;
                break;
        }
    }
}
