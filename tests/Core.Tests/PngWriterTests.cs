using System;
using System.IO;
using System.IO.Compression;
using SpellBrigadeMinimap.Core;

namespace SpellBrigadeMinimap.Tests
{
    public static class PngWriterTests
    {
        static int ReadInt(byte[] b, int i) => (b[i] << 24) | (b[i + 1] << 16) | (b[i + 2] << 8) | b[i + 3];

        public static void WritesAValidHeaderAndEnding()
        {
            var png = PngWriter.Encode(new PixelBuffer(3, 2));
            byte[] signature = { 137, 80, 78, 71, 13, 10, 26, 10 };
            for (int i = 0; i < 8; i++) Check.Equal(signature[i], png[i], $"signature byte {i}");
            Check.Equal(13, ReadInt(png, 8), "IHDR length");
            Check.Equal("IHDR", System.Text.Encoding.ASCII.GetString(png, 12, 4), "IHDR type");
            Check.Equal(3, ReadInt(png, 16), "width");
            Check.Equal(2, ReadInt(png, 20), "height");
            Check.Equal((byte)8, png[24], "bit depth");
            Check.Equal((byte)6, png[25], "RGBA colour type");
            byte[] iend = { 0, 0, 0, 0, (byte)'I', (byte)'E', (byte)'N', (byte)'D', 0xAE, 0x42, 0x60, 0x82 };
            for (int i = 0; i < 12; i++) Check.Equal(iend[i], png[png.Length - 12 + i], $"IEND byte {i}");
        }

        public static void StoresTheTopRowFirst()
        {
            var img = new PixelBuffer(1, 2);
            img.SetPixel(0, 1, new Rgba(9, 8, 7, 6)); // row 1 is the top row
            var png = PngWriter.Encode(img);

            int at = 8;
            byte[] data = null;
            while (at < png.Length)
            {
                int len = ReadInt(png, at);
                string type = System.Text.Encoding.ASCII.GetString(png, at + 4, 4);
                if (type == "IDAT") { data = new byte[len]; Array.Copy(png, at + 8, data, 0, len); break; }
                at += 12 + len;
            }
            Check.True(data != null, "IDAT found");

            using var z = new ZLibStream(new MemoryStream(data), CompressionMode.Decompress);
            var raw = new MemoryStream();
            z.CopyTo(raw);
            byte[] rows = raw.ToArray();
            byte[] want = { 0, 9, 8, 7, 6, 0, 0, 0, 0, 0 };
            Check.Equal(want.Length, rows.Length, "two filtered rows");
            for (int i = 0; i < want.Length; i++) Check.Equal(want[i], rows[i], $"row byte {i}");
        }
    }
}
