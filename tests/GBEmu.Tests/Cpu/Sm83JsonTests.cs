using System.Text.Json;
using GBEmu.Core.Cpu;
using GBEmu.Core.Memory;

namespace GBEmu.Tests.Cpu;

/// <summary>
/// Runs the SingleStepTests sm83 suite: 1000 randomized single-instruction
/// tests per opcode against a flat 64 KiB RAM. Each test sets initial CPU +
/// RAM state, executes exactly one instruction, and checks the final state
/// plus the machine-cycle count.
/// Skipped when tests/data/sm83 is absent (run scripts/fetch-test-data.sh).
/// </summary>
public class Sm83JsonTests
{
    /// <summary>Flat 64 KiB RAM, the memory model the JSON suite assumes.</summary>
    private sealed class FlatBus : IBus
    {
        public readonly byte[] Ram = new byte[0x10000];
        public byte Read(ushort address) => Ram[address];
        public void Write(ushort address, byte value) => Ram[address] = value;
    }

    public static TheoryData<string> OpcodeFiles()
    {
        var data = new TheoryData<string>();
        if (!Directory.Exists(TestData.Sm83Dir))
        {
            data.Add("MISSING"); // theory is skipped by the attribute; placeholder keeps discovery happy
            return data;
        }
        foreach (string file in Directory.EnumerateFiles(TestData.Sm83Dir, "*.json").Order())
            data.Add(Path.GetFileNameWithoutExtension(file));
        return data;
    }

    [Sm83SuiteTheory]
    [MemberData(nameof(OpcodeFiles))]
    public void Opcode_matches_reference(string opcodeFile)
    {
        string path = Path.Combine(TestData.Sm83Dir, opcodeFile + ".json");
        using var doc = JsonDocument.Parse(File.ReadAllBytes(path));

        var bus = new FlatBus();
        var cpu = new Sm83(bus);

        foreach (JsonElement test in doc.RootElement.EnumerateArray())
        {
            LoadState(cpu, bus, test.GetProperty("initial"));
            int expectedTCycles = test.GetProperty("cycles").GetArrayLength() * 4;

            int tCycles = cpu.Step();

            AssertState(cpu, bus, test, expectedTCycles, tCycles);
        }
    }

    private static void LoadState(Sm83 cpu, FlatBus bus, JsonElement s)
    {
        cpu.Reset();
        cpu.PC = s.GetProperty("pc").GetUInt16();
        cpu.SP = s.GetProperty("sp").GetUInt16();
        cpu.A = s.GetProperty("a").GetByte();
        cpu.B = s.GetProperty("b").GetByte();
        cpu.C = s.GetProperty("c").GetByte();
        cpu.D = s.GetProperty("d").GetByte();
        cpu.E = s.GetProperty("e").GetByte();
        cpu.F = s.GetProperty("f").GetByte();
        cpu.H = s.GetProperty("h").GetByte();
        cpu.L = s.GetProperty("l").GetByte();
        cpu.Ime = s.GetProperty("ime").GetByte() != 0;
        cpu.Halted = false;

        // The suite doesn't model interrupt dispatch (tests may place code at
        // 0xFF0F, which our CPU sees as IF), so the "ie" field is deliberately
        // NOT seeded into 0xFFFF — with IE=0 no interrupt can ever be taken.
        Array.Clear(bus.Ram);
        foreach (JsonElement pair in s.GetProperty("ram").EnumerateArray())
            bus.Ram[pair[0].GetUInt16()] = pair[1].GetByte();
    }

    private static void AssertState(
        Sm83 cpu, FlatBus bus, JsonElement test, int expectedTCycles, int actualTCycles)
    {
        string name = test.GetProperty("name").GetString()!;
        JsonElement f = test.GetProperty("final");

        Check(name, "pc", f.GetProperty("pc").GetUInt16(), cpu.PC);
        Check(name, "sp", f.GetProperty("sp").GetUInt16(), cpu.SP);
        Check(name, "a", f.GetProperty("a").GetByte(), cpu.A);
        Check(name, "b", f.GetProperty("b").GetByte(), cpu.B);
        Check(name, "c", f.GetProperty("c").GetByte(), cpu.C);
        Check(name, "d", f.GetProperty("d").GetByte(), cpu.D);
        Check(name, "e", f.GetProperty("e").GetByte(), cpu.E);
        Check(name, "f", f.GetProperty("f").GetByte(), cpu.F);
        Check(name, "h", f.GetProperty("h").GetByte(), cpu.H);
        Check(name, "l", f.GetProperty("l").GetByte(), cpu.L);

        // The suite models the EI delay: after FB the final ime is still 0.
        bool expectedIme = f.GetProperty("ime").GetByte() != 0;
        if (expectedIme != cpu.Ime)
            Assert.Fail($"[{name}] ime: expected {expectedIme}, got {cpu.Ime}");

        foreach (JsonElement pair in f.GetProperty("ram").EnumerateArray())
        {
            ushort addr = pair[0].GetUInt16();
            Check(name, $"ram[0x{addr:X4}]", pair[1].GetByte(), bus.Ram[addr]);
        }

        // HALT (76) and STOP (10) tests record two idle machine cycles after
        // the fetch — an artifact of the reference runner, not instruction
        // cost (halted time depends on when an interrupt arrives). State is
        // still fully checked above.
        byte opcode = (byte)Convert.ToInt32(name.Split(' ')[0], 16);
        if (opcode is not (0x76 or 0x10))
            Check(name, "t-cycles", expectedTCycles, actualTCycles);
    }

    private static void Check<T>(string name, string what, T expected, T actual)
        where T : IEquatable<T>
    {
        if (!expected.Equals(actual))
            Assert.Fail($"[{name}] {what}: expected {expected}, got {actual}");
    }
}
