using System;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace SpellBrigadeMinimap.Core
{
    /// <summary>Minimal RGBA PNG encoder (for saving debug pictures; no engine or image library needed).</summary>
    public static class PngWriter
    {
        static readonly uint[] CrcTable = BuildCrcTable();

        /// <summary>Encodes the buffer as a PNG. The buffer's bottom row (row 0) ends up at the bottom of the image.</summary>
        public static byte[] Encode(PixelBuffer img)
        {
            using var png = new MemoryStream();
            png.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, 0, 8);

            var header = new byte[13];
            WriteInt(header, 0, img.Width);
            WriteInt(header, 4, img.Height);
            header[8] = 8;  // bits per channel
            header[9] = 6;  // RGBA
            WriteChunk(png, "IHDR", header);

            using (var raw = new MemoryStream())
            {
                using (var z = new ZLibStream(raw, CompressionLevel.Fastest, leaveOpen: true))
                {
                    int stride = img.Width * 4;
                    for (int y = img.Height - 1; y >= 0; y--) // PNG rows run top to bottom
                    {
                        z.WriteByte(0); // no filter
                        z.Write(img.Data, y * stride, stride);
                    }
                }
                WriteChunk(png, "IDAT", raw.ToArray());
            }
            WriteChunk(png, "IEND", Array.Empty<byte>());
            return png.ToArray();
        }

        static void WriteChunk(Stream s, string type, byte[] data)
        {
            var len = new byte[4];
            WriteInt(len, 0, data.Length);
            s.Write(len, 0, 4);
            byte[] typeBytes = Encoding.ASCII.GetBytes(type);
            s.Write(typeBytes, 0, 4);
            s.Write(data, 0, data.Length);
            uint crc = 0xFFFFFFFF;
            foreach (byte b in typeBytes) crc = CrcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
            foreach (byte b in data) crc = CrcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
            var crcBytes = new byte[4];
            WriteInt(crcBytes, 0, (int)~crc);
            s.Write(crcBytes, 0, 4);
        }

        static void WriteInt(byte[] b, int i, int v)
        {
            b[i] = (byte)(v >> 24);
            b[i + 1] = (byte)(v >> 16);
            b[i + 2] = (byte)(v >> 8);
            b[i + 3] = (byte)v;
        }

        static uint[] BuildCrcTable()
        {
            var table = new uint[256];
            for (uint n = 0; n < 256; n++)
            {
                uint c = n;
                for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
                table[n] = c;
            }
            return table;
        }
    }
}
