namespace GBEmu.Core.Cpu;

public sealed partial class Sm83
{
    /// <summary>alu[y]: ADD ADC SUB SBC AND XOR OR CP, operating on A.</summary>
    private void AluOp(int index, byte value)
    {
        switch (index)
        {
            case 0: Add(value, withCarry: false); break;
            case 1: Add(value, withCarry: true); break;
            case 2: A = Sub(value, withCarry: false); break;
            case 3: A = Sub(value, withCarry: true); break;
            case 4:
                A &= value;
                FlagZ = A == 0; FlagN = false; FlagH = true; FlagC = false;
                break;
            case 5:
                A ^= value;
                FlagZ = A == 0; FlagN = false; FlagH = false; FlagC = false;
                break;
            case 6:
                A |= value;
                FlagZ = A == 0; FlagN = false; FlagH = false; FlagC = false;
                break;
            default: // CP: SUB that discards the result
                Sub(value, withCarry: false);
                break;
        }
    }

    private void Add(byte value, bool withCarry)
    {
        int carry = withCarry && FlagC ? 1 : 0;
        int result = A + value + carry;
        FlagH = (A & 0x0F) + (value & 0x0F) + carry > 0x0F;
        FlagC = result > 0xFF;
        A = (byte)result;
        FlagZ = A == 0;
        FlagN = false;
    }

    private byte Sub(byte value, bool withCarry)
    {
        int carry = withCarry && FlagC ? 1 : 0;
        int result = A - value - carry;
        FlagH = (A & 0x0F) - (value & 0x0F) - carry < 0;
        FlagC = result < 0;
        byte r = (byte)result;
        FlagZ = r == 0;
        FlagN = true;
        return r;
    }

    private byte Inc8(byte value)
    {
        byte r = (byte)(value + 1);
        FlagZ = r == 0;
        FlagN = false;
        FlagH = (value & 0x0F) == 0x0F;
        return r;
    }

    private byte Dec8(byte value)
    {
        byte r = (byte)(value - 1);
        FlagZ = r == 0;
        FlagN = true;
        FlagH = (value & 0x0F) == 0;
        return r;
    }

    /// <summary>
    /// Decimal-adjust A after BCD arithmetic. The adjustment depends on the
    /// N/H/C flags left by the previous ADD or SUB.
    /// </summary>
    private void Daa()
    {
        int a = A;
        if (!FlagN)
        {
            if (FlagC || a > 0x99)
            {
                a += 0x60;
                FlagC = true;
            }
            if (FlagH || (a & 0x0F) > 0x09)
                a += 0x06;
        }
        else
        {
            if (FlagC) a -= 0x60;
            if (FlagH) a -= 0x06;
        }
        A = (byte)a;
        FlagZ = A == 0;
        FlagH = false;
    }

    /// <summary>Shared by ADD SP,e and LD HL,SP+e: flags come from unsigned low-byte addition.</summary>
    private ushort SpPlusImmediate()
    {
        byte e = Fetch();
        FlagZ = false;
        FlagN = false;
        FlagH = (SP & 0x0F) + (e & 0x0F) > 0x0F;
        FlagC = (SP & 0xFF) + e > 0xFF;
        return (ushort)(SP + (sbyte)e);
    }

    // --- Rotates and shifts. The A-register forms (RLCA...) always clear Z;
    //     the CB-prefixed forms set Z from the result. ---

    private byte Rlc(byte v, bool zFromResult)
    {
        byte r = (byte)((v << 1) | (v >> 7));
        SetShiftFlags(r, carry: (v & 0x80) != 0, zFromResult);
        return r;
    }

    private byte Rrc(byte v, bool zFromResult)
    {
        byte r = (byte)((v >> 1) | (v << 7));
        SetShiftFlags(r, carry: (v & 0x01) != 0, zFromResult);
        return r;
    }

    private byte Rl(byte v, bool zFromResult)
    {
        byte r = (byte)((v << 1) | (FlagC ? 1 : 0));
        SetShiftFlags(r, carry: (v & 0x80) != 0, zFromResult);
        return r;
    }

    private byte Rr(byte v, bool zFromResult)
    {
        byte r = (byte)((v >> 1) | (FlagC ? 0x80 : 0));
        SetShiftFlags(r, carry: (v & 0x01) != 0, zFromResult);
        return r;
    }

    private byte Sla(byte v)
    {
        byte r = (byte)(v << 1);
        SetShiftFlags(r, carry: (v & 0x80) != 0, zFromResult: true);
        return r;
    }

    private byte Sra(byte v)
    {
        byte r = (byte)((v >> 1) | (v & 0x80));
        SetShiftFlags(r, carry: (v & 0x01) != 0, zFromResult: true);
        return r;
    }

    private byte Swap(byte v)
    {
        byte r = (byte)((v >> 4) | (v << 4));
        SetShiftFlags(r, carry: false, zFromResult: true);
        return r;
    }

    private byte Srl(byte v)
    {
        byte r = (byte)(v >> 1);
        SetShiftFlags(r, carry: (v & 0x01) != 0, zFromResult: true);
        return r;
    }

    private void SetShiftFlags(byte result, bool carry, bool zFromResult)
    {
        FlagZ = zFromResult && result == 0;
        FlagN = false;
        FlagH = false;
        FlagC = carry;
    }

    /// <summary>CB-prefixed opcodes: rotates/shifts, BIT, RES, SET on r[z].</summary>
    private void ExecuteCb()
    {
        byte op = Fetch();
        int y = (op >> 3) & 7;
        int z = op & 7;

        switch (op >> 6)
        {
            case 0:
                byte rotated = y switch
                {
                    0 => Rlc(ReadReg8(z), zFromResult: true),
                    1 => Rrc(ReadReg8(z), zFromResult: true),
                    2 => Rl(ReadReg8(z), zFromResult: true),
                    3 => Rr(ReadReg8(z), zFromResult: true),
                    4 => Sla(ReadReg8(z)),
                    5 => Sra(ReadReg8(z)),
                    6 => Swap(ReadReg8(z)),
                    _ => Srl(ReadReg8(z)),
                };
                WriteReg8(z, rotated);
                break;

            case 1: // BIT y,r[z] — read-only, so (HL) costs no write cycle
                FlagZ = (ReadReg8(z) & (1 << y)) == 0;
                FlagN = false;
                FlagH = true;
                break;

            case 2: // RES y,r[z]
                WriteReg8(z, (byte)(ReadReg8(z) & ~(1 << y)));
                break;

            default: // SET y,r[z]
                WriteReg8(z, (byte)(ReadReg8(z) | (1 << y)));
                break;
        }
    }
}
