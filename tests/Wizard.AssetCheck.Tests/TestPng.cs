using System;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace Wizard.AssetCheck.Tests
{
    // 시험용 PNG 쓰개(RGBA 8비트, 또는 팔레트 8비트). 필터는 줄마다 돌려 써서 읽개의 다섯 필터를 모두 지나게 한다
    internal static class TestPng
    {
        public static byte[] Rgba(int width, int height, Func<int, int, uint> pixel, bool cycleFilters = true)
        {
            var raw = new MemoryStream();
            var prev = new byte[width * 4];
            for (int y = 0; y < height; y++)
            {
                var line = new byte[width * 4];
                for (int x = 0; x < width; x++)
                {
                    uint p = pixel(x, y);
                    line[x * 4] = (byte)(p >> 24);
                    line[x * 4 + 1] = (byte)(p >> 16);
                    line[x * 4 + 2] = (byte)(p >> 8);
                    line[x * 4 + 3] = (byte)p;
                }
                int filter = cycleFilters ? y % 5 : 0;
                raw.WriteByte((byte)filter);
                byte[] f = Filter(filter, line, prev, 4);
                raw.Write(f, 0, f.Length);
                prev = line;
            }
            return Build(width, height, 6, null, null, raw.ToArray());
        }

        // 팔레트 PNG(색 형식 3, 8비트). alpha 는 tRNS
        public static byte[] Palette(int width, int height, uint[] paletteRgba, Func<int, int, byte> index)
        {
            var raw = new MemoryStream();
            for (int y = 0; y < height; y++)
            {
                raw.WriteByte(0);
                for (int x = 0; x < width; x++)
                {
                    raw.WriteByte(index(x, y));
                }
            }
            var plte = new byte[paletteRgba.Length * 3];
            var trns = new byte[paletteRgba.Length];
            for (int i = 0; i < paletteRgba.Length; i++)
            {
                plte[i * 3] = (byte)(paletteRgba[i] >> 24);
                plte[i * 3 + 1] = (byte)(paletteRgba[i] >> 16);
                plte[i * 3 + 2] = (byte)(paletteRgba[i] >> 8);
                trns[i] = (byte)paletteRgba[i];
            }
            return Build(width, height, 3, plte, trns, raw.ToArray());
        }

        public static void Write(string path, byte[] png)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, png);
        }

        public static uint Opaque(int rgb) => ((uint)rgb << 8) | 0xFF;

        private static byte[] Filter(int filter, byte[] line, byte[] prev, int bpp)
        {
            var o = new byte[line.Length];
            for (int i = 0; i < line.Length; i++)
            {
                int a = i >= bpp ? line[i - bpp] : 0;
                int b = prev[i];
                int c = i >= bpp ? prev[i - bpp] : 0;
                int pred;
                switch (filter)
                {
                    case 1: pred = a; break;
                    case 2: pred = b; break;
                    case 3: pred = (a + b) >> 1; break;
                    case 4:
                    {
                        int p = a + b - c;
                        int pa = Math.Abs(p - a), pb = Math.Abs(p - b), pc = Math.Abs(p - c);
                        pred = pa <= pb && pa <= pc ? a : (pb <= pc ? b : c);
                        break;
                    }
                    default: pred = 0; break;
                }
                o[i] = (byte)(line[i] - pred);
            }
            return o;
        }

        private static byte[] Build(int width, int height, byte colorType, byte[]? plte, byte[]? trns, byte[] raw)
        {
            var ms = new MemoryStream();
            ms.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, 0, 8);
            var ihdr = new byte[13];
            WriteU32(ihdr, 0, (uint)width);
            WriteU32(ihdr, 4, (uint)height);
            ihdr[8] = 8;
            ihdr[9] = colorType;
            Chunk(ms, "IHDR", ihdr);
            if (plte != null)
            {
                Chunk(ms, "PLTE", plte);
            }
            if (trns != null)
            {
                Chunk(ms, "tRNS", trns);
            }
            var z = new MemoryStream();
            using (var zs = new ZLibStream(z, CompressionLevel.Optimal, true))
            {
                zs.Write(raw, 0, raw.Length);
            }
            Chunk(ms, "IDAT", z.ToArray());
            Chunk(ms, "IEND", new byte[0]);
            return ms.ToArray();
        }

        private static void Chunk(Stream s, string type, byte[] data)
        {
            var len = new byte[4];
            WriteU32(len, 0, (uint)data.Length);
            s.Write(len, 0, 4);
            byte[] t = Encoding.ASCII.GetBytes(type);
            s.Write(t, 0, 4);
            s.Write(data, 0, data.Length);
            var crc = new byte[4];
            WriteU32(crc, 0, Crc(t, data));
            s.Write(crc, 0, 4);
        }

        private static uint Crc(byte[] type, byte[] data)
        {
            uint c = 0xFFFFFFFF;
            foreach (byte b in type) c = Step(c, b);
            foreach (byte b in data) c = Step(c, b);
            return c ^ 0xFFFFFFFF;
        }

        private static uint Step(uint c, byte b)
        {
            c ^= b;
            for (int k = 0; k < 8; k++)
            {
                c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
            }
            return c;
        }

        private static void WriteU32(byte[] b, int i, uint v)
        {
            b[i] = (byte)(v >> 24);
            b[i + 1] = (byte)(v >> 16);
            b[i + 2] = (byte)(v >> 8);
            b[i + 3] = (byte)v;
        }
    }
}
