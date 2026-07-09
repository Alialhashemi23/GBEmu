using System.Text;
using GBEmu.Core;

namespace GBEmu.Tests.Roms;

/// <summary>Runs test ROMs on the full GameBoy machine and detects their verdicts.</summary>
public static class RomTestRunner
{
    /// <summary>
    /// Blargg protocol: the ROM prints text over the serial port; "Passed" or
    /// "Failed" ends the run. Returns the captured serial text.
    /// </summary>
    public static string RunBlargg(string romPath, long cycleBudget = 800_000_000)
    {
        var gb = new GameBoy(File.ReadAllBytes(romPath));
        var serial = new StringBuilder();
        gb.Bus.SerialByteTransferred += b => serial.Append((char)b);

        long total = 0;
        long nextCheck = 1_000_000;
        while (total < cycleBudget)
        {
            total += gb.Step();
            if (total >= nextCheck)
            {
                nextCheck = total + 1_000_000;
                string text = serial.ToString();
                if (text.Contains("Passed") || text.Contains("Failed"))
                    return text;
            }
        }
        return serial + "\n[TIMED OUT after " + total + " T-cycles]";
    }

    /// <summary>
    /// Mooneye protocol: on completion the ROM sends the Fibonacci bytes
    /// 3,5,8,13,21,34 (pass) or six 0x42 bytes (fail) over serial, then hits
    /// a LD B,B breakpoint. Returns a human-readable verdict.
    /// </summary>
    public static string RunMooneye(string romPath, long cycleBudget = 200_000_000)
    {
        var gb = new GameBoy(File.ReadAllBytes(romPath));
        var received = new List<byte>();
        bool done = false;
        gb.Bus.SerialByteTransferred += b =>
        {
            received.Add(b);
            if (received.Count >= 6 && (EndsWith(received, FibonacciMarker) || EndsWith(received, FailMarker)))
                done = true;
        };

        long total = 0;
        while (!done && total < cycleBudget)
            total += gb.Step();

        if (!done)
            return $"TIMED OUT after {total} T-cycles (serial bytes: {Hex(received)})";

        if (EndsWith(received, FibonacciMarker))
            return "Passed";
        // Failure text (if any) precedes the 0x42 markers on the serial line.
        string text = new(received.Where(b => b is >= 0x20 and < 0x7F).Select(b => (char)b).ToArray());
        return $"Failed (serial: {Hex(received)}; text: \"{text}\")";
    }

    /// <summary>
    /// Blargg memory protocol (dmg_sound, oam_bug): the ROM writes 0x80 to
    /// 0xA000 while running, the signature DE B0 61 at 0xA001, and replaces
    /// 0x80 with the result code (0 = pass) when done. Result text is a
    /// NUL-terminated string at 0xA004.
    /// </summary>
    public static string RunBlarggMemory(string romPath, long cycleBudget = 800_000_000)
    {
        var gb = new GameBoy(File.ReadAllBytes(romPath));

        long total = 0;
        long nextCheck = 1_000_000;
        while (total < cycleBudget)
        {
            total += gb.Step();
            if (total < nextCheck)
                continue;
            nextCheck = total + 1_000_000;

            if (gb.Bus.Read(0xA001) != 0xDE || gb.Bus.Read(0xA002) != 0xB0 ||
                gb.Bus.Read(0xA003) != 0x61)
                continue;
            byte status = gb.Bus.Read(0xA000);
            if (status == 0x80)
                continue; // still running

            var text = new StringBuilder();
            for (ushort addr = 0xA004; addr < 0xA200; addr++)
            {
                byte b = gb.Bus.Read(addr);
                if (b == 0)
                    break;
                text.Append((char)b);
            }
            return status == 0 ? "Passed" : $"Failed (status {status}): {text.ToString().Trim()}";
        }
        return $"TIMED OUT after {total} T-cycles";
    }

    private static readonly byte[] FibonacciMarker = { 3, 5, 8, 13, 21, 34 };
    private static readonly byte[] FailMarker = { 0x42, 0x42, 0x42, 0x42, 0x42, 0x42 };

    private static bool EndsWith(List<byte> list, byte[] suffix)
    {
        if (list.Count < suffix.Length)
            return false;
        for (int i = 0; i < suffix.Length; i++)
        {
            if (list[list.Count - suffix.Length + i] != suffix[i])
                return false;
        }
        return true;
    }

    private static string Hex(IEnumerable<byte> bytes) =>
        string.Join(" ", bytes.Select(b => b.ToString("X2")));
}
