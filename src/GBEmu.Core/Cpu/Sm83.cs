using GBEmu.Core.Memory;

namespace GBEmu.Core.Cpu;

/// <summary>
/// The Sharp SM83 CPU core (the Game Boy's processor).
///
/// Cycle accounting: every memory access costs 4 T-cycles and is counted in
/// <see cref="ReadByte"/> / <see cref="WriteByte"/>; instructions with internal
/// (non-memory) machine cycles call <see cref="InternalCycle"/> at the point
/// the hardware spends them. <see cref="Step"/> returns the T-cycles consumed.
/// This keeps totals correct now and allows per-access component ticking later.
/// </summary>
public sealed partial class Sm83
{
    private const ushort InterruptFlagAddress = 0xFF0F;
    private const ushort InterruptEnableAddress = 0xFFFF;

    private readonly IBus _bus;

    public byte A, B, C, D, E, H, L;
    private byte _f;
    public ushort SP, PC;

    /// <summary>Interrupt master enable.</summary>
    public bool Ime;
    private bool _imeEnableNext; // EI takes effect after the following instruction

    /// <summary>True in the one-instruction window after EI before IME is set.</summary>
    public bool ImePending => _imeEnableNext;

    public bool Halted { get; set; }

    /// <summary>Set after executing an illegal opcode; the real CPU hangs permanently.</summary>
    public bool Locked { get; private set; }

    private bool _haltBug;
    private int _cycles;

    /// <summary>
    /// Ticks peripherals by the given T-cycles. Called twice per machine
    /// cycle, splitting the 4 T-cycles around the bus access so peripherals
    /// advance mid-instruction — required by the stricter timing tests
    /// (mem_timing, Mooneye). The split is defined by <see cref="AccessTCycle"/>.
    /// </summary>
    public Action<int>? Tick;

    /// <summary>How many T-cycles of a machine cycle elapse before the bus access lands.</summary>
    public const int AccessTCycle = 4;

    public Sm83(IBus bus)
    {
        _bus = bus;
        Reset();
    }

    /// <summary>Flags register; the low nibble always reads as zero.</summary>
    public byte F
    {
        get => _f;
        set => _f = (byte)(value & 0xF0);
    }

    public bool FlagZ { get => (_f & 0x80) != 0; set => SetFlag(0x80, value); }
    public bool FlagN { get => (_f & 0x40) != 0; set => SetFlag(0x40, value); }
    public bool FlagH { get => (_f & 0x20) != 0; set => SetFlag(0x20, value); }
    public bool FlagC { get => (_f & 0x10) != 0; set => SetFlag(0x10, value); }

    private void SetFlag(byte mask, bool on)
    {
        if (on) _f |= mask;
        else _f &= (byte)~mask;
    }

    public ushort AF
    {
        get => (ushort)((A << 8) | _f);
        set { A = (byte)(value >> 8); F = (byte)value; }
    }

    public ushort BC
    {
        get => (ushort)((B << 8) | C);
        set { B = (byte)(value >> 8); C = (byte)value; }
    }

    public ushort DE
    {
        get => (ushort)((D << 8) | E);
        set { D = (byte)(value >> 8); E = (byte)value; }
    }

    public ushort HL
    {
        get => (ushort)((H << 8) | L);
        set { H = (byte)(value >> 8); L = (byte)value; }
    }

    /// <summary>Post-boot-ROM register state of a DMG (Pan Docs "Power Up Sequence").</summary>
    public void Reset()
    {
        AF = 0x01B0;
        BC = 0x0013;
        DE = 0x00D8;
        HL = 0x014D;
        SP = 0xFFFE;
        PC = 0x0100;
        Ime = false;
        _imeEnableNext = false;
        Halted = false;
        Locked = false;
        _haltBug = false;
    }

    /// <summary>
    /// Services pending interrupts or executes one instruction.
    /// Returns the number of T-cycles consumed (always a multiple of 4).
    /// </summary>
    public int Step()
    {
        _cycles = 0;

        if (Locked)
        {
            InternalCycle();
            return _cycles;
        }

        // Interrupt lines are sampled without spending bus cycles.
        byte pending = (byte)(_bus.Read(InterruptFlagAddress)
                              & _bus.Read(InterruptEnableAddress)
                              & 0x1F);

        if (Halted)
        {
            if (pending == 0)
            {
                InternalCycle();
                return _cycles;
            }
            Halted = false; // wake regardless of IME; IF is only cleared on dispatch
        }

        if (Ime && pending != 0)
        {
            DispatchInterrupt(pending);
            return _cycles;
        }

        if (_imeEnableNext)
        {
            Ime = true;
            _imeEnableNext = false;
        }

        byte opcode = Fetch();
        if (_haltBug)
        {
            // HALT bug: the fetch after a failed HALT does not advance PC,
            // so the byte at PC is executed twice.
            PC--;
            _haltBug = false;
        }

        Execute(opcode);
        return _cycles;
    }

    /// <summary>5 machine cycles: two internal, two stack pushes, one internal while PC is set.</summary>
    private void DispatchInterrupt(byte pending)
    {
        Ime = false;
        Halted = false;

        InternalCycle();
        InternalCycle();

        int bit = System.Numerics.BitOperations.TrailingZeroCount(pending);
        byte flags = _bus.Read(InterruptFlagAddress);
        _bus.Write(InterruptFlagAddress, (byte)(flags & ~(1 << bit)));

        Push16(PC);
        InternalCycle();
        PC = (ushort)(0x0040 + bit * 8);
    }

    private byte Fetch() => ReadByte(PC++);

    private ushort Fetch16()
    {
        byte lo = Fetch();
        byte hi = Fetch();
        return (ushort)(lo | (hi << 8));
    }

    private byte ReadByte(ushort address)
    {
        _cycles += 4;
        Tick?.Invoke(AccessTCycle);
        byte value = _bus.Read(address);
        if (AccessTCycle < 4)
            Tick?.Invoke(4 - AccessTCycle);
        return value;
    }

    private void WriteByte(ushort address, byte value)
    {
        _cycles += 4;
        Tick?.Invoke(AccessTCycle);
        _bus.Write(address, value);
        if (AccessTCycle < 4)
            Tick?.Invoke(4 - AccessTCycle);
    }

    private void InternalCycle()
    {
        _cycles += 4;
        Tick?.Invoke(4);
    }

    private void Push16(ushort value)
    {
        WriteByte(--SP, (byte)(value >> 8));
        WriteByte(--SP, (byte)value);
    }

    private ushort Pop16()
    {
        byte lo = ReadByte(SP++);
        byte hi = ReadByte(SP++);
        return (ushort)(lo | (hi << 8));
    }
}
