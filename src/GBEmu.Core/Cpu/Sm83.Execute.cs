namespace GBEmu.Core.Cpu;

/// <summary>
/// Instruction execution. Opcodes are decoded by their octal structure
/// (x = bits 7-6, y = bits 5-3, z = bits 2-0, p = bits 5-4, q = bit 3),
/// the scheme documented in the gbdev community's "DECODING Gameboy Z80
/// OPCODES" reference. Register/operand tables:
///   r[y]:   B C D E H L (HL) A
///   rp[p]:  BC DE HL SP        (16-bit loads, INC/DEC, ADD HL)
///   rp2[p]: BC DE HL AF        (PUSH/POP)
///   cc[y]:  NZ Z NC C
///   alu[y]: ADD ADC SUB SBC AND XOR OR CP
/// </summary>
public sealed partial class Sm83
{
    private void Execute(byte op)
    {
        // x=1: LD r[y],r[z] — except 0x76, which is HALT.
        if (op is >= 0x40 and <= 0x7F)
        {
            if (op == 0x76) { ExecuteHalt(); return; }
            WriteReg8((op >> 3) & 7, ReadReg8(op & 7));
            return;
        }

        // x=2: alu[y] A,r[z]
        if (op is >= 0x80 and <= 0xBF)
        {
            AluOp((op >> 3) & 7, ReadReg8(op & 7));
            return;
        }

        if (op < 0x40) ExecuteBlock0(op);
        else ExecuteBlock3(op);
    }

    /// <summary>Opcodes 0x00-0x3F.</summary>
    private void ExecuteBlock0(byte op)
    {
        int y = (op >> 3) & 7;
        int p = (op >> 4) & 3;
        bool q = (op & 0x08) != 0;

        switch (op & 7)
        {
            case 0:
                switch (y)
                {
                    case 0: // NOP
                        break;
                    case 1: // LD (nn),SP
                        ushort addr = Fetch16();
                        WriteByte(addr, (byte)SP);
                        WriteByte((ushort)(addr + 1), (byte)(SP >> 8));
                        break;
                    case 2: // STOP — low-power mode; rare on DMG (games use HALT).
                        // Whether the pad byte is skipped depends on joypad/DIV
                        // state; we model the simple 1-byte form for now.
                        break;
                    case 3: // JR e
                        sbyte offset = (sbyte)Fetch();
                        InternalCycle();
                        PC = (ushort)(PC + offset);
                        break;
                    default: // JR cc,e
                        sbyte e = (sbyte)Fetch();
                        if (Condition(y & 3))
                        {
                            InternalCycle();
                            PC = (ushort)(PC + e);
                        }
                        break;
                }
                break;

            case 1:
                if (!q) // LD rp[p],nn
                {
                    SetRp(p, Fetch16());
                }
                else // ADD HL,rp[p]
                {
                    ushort v = GetRp(p);
                    int result = HL + v;
                    FlagN = false;
                    FlagH = (HL & 0x0FFF) + (v & 0x0FFF) > 0x0FFF;
                    FlagC = result > 0xFFFF;
                    HL = (ushort)result;
                    InternalCycle();
                }
                break;

            case 2: // LD (rr),A / LD A,(rr) with BC, DE, HL+, HL-
                ushort target = p switch
                {
                    0 => BC,
                    1 => DE,
                    2 => HL,
                    _ => HL,
                };
                if (!q) WriteByte(target, A);
                else A = ReadByte(target);
                if (p == 2) HL++;
                else if (p == 3) HL--;
                break;

            case 3: // INC rp / DEC rp
                SetRp(p, (ushort)(GetRp(p) + (q ? -1 : 1)));
                InternalCycle();
                break;

            case 4: // INC r[y]
                WriteReg8(y, Inc8(ReadReg8(y)));
                break;

            case 5: // DEC r[y]
                WriteReg8(y, Dec8(ReadReg8(y)));
                break;

            case 6: // LD r[y],n
                WriteReg8(y, Fetch());
                break;

            case 7:
                switch (y)
                {
                    case 0: A = Rlc(A, zFromResult: false); break; // RLCA
                    case 1: A = Rrc(A, zFromResult: false); break; // RRCA
                    case 2: A = Rl(A, zFromResult: false); break;  // RLA
                    case 3: A = Rr(A, zFromResult: false); break;  // RRA
                    case 4: Daa(); break;
                    case 5: A = (byte)~A; FlagN = true; FlagH = true; break; // CPL
                    case 6: FlagC = true; FlagN = false; FlagH = false; break; // SCF
                    case 7: FlagC = !FlagC; FlagN = false; FlagH = false; break; // CCF
                }
                break;
        }
    }

    /// <summary>Opcodes 0xC0-0xFF.</summary>
    private void ExecuteBlock3(byte op)
    {
        int y = (op >> 3) & 7;
        int p = (op >> 4) & 3;

        switch (op & 7)
        {
            case 0:
                switch (y)
                {
                    case < 4: // RET cc
                        InternalCycle();
                        if (Condition(y))
                        {
                            PC = Pop16();
                            InternalCycle();
                        }
                        break;
                    case 4: // LDH (n),A
                        WriteByte((ushort)(0xFF00 + Fetch()), A);
                        break;
                    case 5: // ADD SP,e
                        SP = SpPlusImmediate();
                        InternalCycle();
                        InternalCycle();
                        break;
                    case 6: // LDH A,(n)
                        A = ReadByte((ushort)(0xFF00 + Fetch()));
                        break;
                    case 7: // LD HL,SP+e
                        HL = SpPlusImmediate();
                        InternalCycle();
                        break;
                }
                break;

            case 1:
                if ((op & 0x08) == 0) // POP rp2[p]
                {
                    SetRp2(p, Pop16());
                }
                else
                {
                    switch (p)
                    {
                        case 0: // RET
                            PC = Pop16();
                            InternalCycle();
                            break;
                        case 1: // RETI — enables IME immediately, no EI delay
                            PC = Pop16();
                            InternalCycle();
                            Ime = true;
                            break;
                        case 2: // JP HL
                            PC = HL;
                            break;
                        case 3: // LD SP,HL
                            SP = HL;
                            InternalCycle();
                            break;
                    }
                }
                break;

            case 2:
                switch (y)
                {
                    case < 4: // JP cc,nn
                        ushort dest = Fetch16();
                        if (Condition(y))
                        {
                            InternalCycle();
                            PC = dest;
                        }
                        break;
                    case 4: WriteByte((ushort)(0xFF00 + C), A); break; // LDH (C),A
                    case 5: WriteByte(Fetch16(), A); break;            // LD (nn),A
                    case 6: A = ReadByte((ushort)(0xFF00 + C)); break; // LDH A,(C)
                    case 7: A = ReadByte(Fetch16()); break;            // LD A,(nn)
                }
                break;

            case 3:
                switch (op)
                {
                    case 0xC3: // JP nn
                        ushort target = Fetch16();
                        InternalCycle();
                        PC = target;
                        break;
                    case 0xCB:
                        ExecuteCb();
                        break;
                    case 0xF3: // DI
                        Ime = false;
                        _imeEnableNext = false;
                        break;
                    case 0xFB: // EI
                        _imeEnableNext = true;
                        break;
                    default: // D3 DB E3 E4 EB EC ED — illegal; the CPU hangs
                        Locked = true;
                        break;
                }
                break;

            case 4:
                if (y < 4) // CALL cc,nn
                {
                    ushort dest = Fetch16();
                    if (Condition(y))
                    {
                        InternalCycle();
                        Push16(PC);
                        PC = dest;
                    }
                }
                else // DC E4 EC F4 FC — illegal
                {
                    Locked = true;
                }
                break;

            case 5:
                if ((op & 0x08) == 0) // PUSH rp2[p]
                {
                    InternalCycle();
                    Push16(GetRp2(p));
                }
                else if (op == 0xCD) // CALL nn
                {
                    ushort dest = Fetch16();
                    InternalCycle();
                    Push16(PC);
                    PC = dest;
                }
                else // DD ED FD — illegal
                {
                    Locked = true;
                }
                break;

            case 6: // alu[y] A,n
                AluOp(y, Fetch());
                break;

            case 7: // RST y*8
                InternalCycle();
                Push16(PC);
                PC = (ushort)(op & 0x38);
                break;
        }
    }

    private void ExecuteHalt()
    {
        byte pending = (byte)(_bus.Read(InterruptFlagAddress)
                              & _bus.Read(InterruptEnableAddress)
                              & 0x1F);
        if (!Ime && pending != 0)
            _haltBug = true; // HALT is skipped and the next opcode byte is read twice
        else
            Halted = true;
    }

    private bool Condition(int index) => index switch
    {
        0 => !FlagZ,
        1 => FlagZ,
        2 => !FlagC,
        _ => FlagC,
    };

    // --- 8-bit register table access (index 6 = memory at HL) ---

    private byte ReadReg8(int index) => index switch
    {
        0 => B,
        1 => C,
        2 => D,
        3 => E,
        4 => H,
        5 => L,
        6 => ReadByte(HL),
        _ => A,
    };

    private void WriteReg8(int index, byte value)
    {
        switch (index)
        {
            case 0: B = value; break;
            case 1: C = value; break;
            case 2: D = value; break;
            case 3: E = value; break;
            case 4: H = value; break;
            case 5: L = value; break;
            case 6: WriteByte(HL, value); break;
            default: A = value; break;
        }
    }

    private ushort GetRp(int p) => p switch { 0 => BC, 1 => DE, 2 => HL, _ => SP };

    private void SetRp(int p, ushort value)
    {
        switch (p)
        {
            case 0: BC = value; break;
            case 1: DE = value; break;
            case 2: HL = value; break;
            default: SP = value; break;
        }
    }

    private ushort GetRp2(int p) => p switch { 0 => BC, 1 => DE, 2 => HL, _ => AF };

    private void SetRp2(int p, ushort value)
    {
        switch (p)
        {
            case 0: BC = value; break;
            case 1: DE = value; break;
            case 2: HL = value; break;
            default: AF = value; break;
        }
    }
}
