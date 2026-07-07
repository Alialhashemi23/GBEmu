namespace GBEmu.Cli;

/// <summary>
/// Minimal 8-bit grayscale PNG writer (uncompressed deflate blocks) — enough
/// to dump emulator framebuffers without pulling in an imaging dependency.
/// </summary>
public static class PngWriter
{
    public static void WriteGrayscale(string path, int width, int height, ReadOnlySpan<byte> pixels)
    {
        using var stream = File.Create(path);
        Span<byte> signature = stackalloc byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        stream.Write(signature);

        Span<byte> ihdr = stackalloc byte[13];
        WriteBigEndian(ihdr, (uint)width);
        WriteBigEndian(ihdr[4..], (uint)height);
        ihdr[8] = 8; // bit depth
        ihdr[9] = 0; // grayscale
        WriteChunk(stream, "IHDR", ihdr);

        // Raw scanlines, each prefixed with filter type 0.
        var raw = new byte[height * (width + 1)];
        for (int y = 0; y < height; y++)
            pixels.Slice(y * width, width).CopyTo(raw.AsSpan(y * (width + 1) + 1));

        WriteChunk(stream, "IDAT", Deflate(raw));
        WriteChunk(stream, "IEND", ReadOnlySpan<byte>.Empty);
    }

    /// <summary>zlib stream using stored (uncompressed) deflate blocks.</summary>
    private static byte[] Deflate(byte[] raw)
    {
        using var ms = new MemoryStream();
        ms.WriteByte(0x78);
        ms.WriteByte(0x01);
        for (int offset = 0; offset < raw.Length; offset += 65535)
        {
            int len = Math.Min(65535, raw.Length - offset);
            bool final = offset + len >= raw.Length;
            ms.WriteByte((byte)(final ? 1 : 0));
            ms.WriteByte((byte)len);
            ms.WriteByte((byte)(len >> 8));
            ms.WriteByte((byte)~len);
            ms.WriteByte((byte)(~len >> 8));
            ms.Write(raw, offset, len);
        }
        uint adler = Adler32(raw);
        Span<byte> tail = stackalloc byte[4];
        WriteBigEndian(tail, adler);
        ms.Write(tail);
        return ms.ToArray();
    }

    private static void WriteChunk(Stream stream, string type, ReadOnlySpan<byte> data)
    {
        Span<byte> header = stackalloc byte[8];
        WriteBigEndian(header, (uint)data.Length);
        for (int i = 0; i < 4; i++)
            header[4 + i] = (byte)type[i];
        stream.Write(header);
        stream.Write(data);

        uint crc = Crc32(header[4..], seed: 0xFFFFFFFF);
        crc = Crc32(data, seed: crc);
        Span<byte> tail = stackalloc byte[4];
        WriteBigEndian(tail, crc ^ 0xFFFFFFFF);
        stream.Write(tail);
    }

    private static void WriteBigEndian(Span<byte> dest, uint value)
    {
        dest[0] = (byte)(value >> 24);
        dest[1] = (byte)(value >> 16);
        dest[2] = (byte)(value >> 8);
        dest[3] = (byte)value;
    }

    private static uint Adler32(ReadOnlySpan<byte> data)
    {
        uint a = 1, b = 0;
        foreach (byte value in data)
        {
            a = (a + value) % 65521;
            b = (b + a) % 65521;
        }
        return (b << 16) | a;
    }

    private static uint Crc32(ReadOnlySpan<byte> data, uint seed)
    {
        uint crc = seed;
        foreach (byte value in data)
        {
            crc ^= value;
            for (int i = 0; i < 8; i++)
                crc = (crc >> 1) ^ (0xEDB88320 & (uint)-(crc & 1));
        }
        return crc;
    }
}
