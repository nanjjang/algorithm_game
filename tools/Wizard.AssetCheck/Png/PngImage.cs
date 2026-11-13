using System;
using System.IO;
using System.IO.Compression;

namespace Wizard.AssetCheck.Png
{
    // 반입 검사용 PNG 읽개(외부 패키지 없이 System.IO.Compression 만 쓴다). 픽셀은 RGBA 8비트로 펼친다.
    // 지원: 색 형식 0(회색), 2(RGB), 3(팔레트), 4(회색+알파), 6(RGBA), 비트 깊이 1·2·4·8·16(16비트는 위 바이트), 비월 없음.
    // 비월(Adam7)은 그림 규격 밖이라 읽지 않고 PngFormatException 으로 알린다(반입 검사에서 실패로 적힌다).
    public sealed class PngImage
    {
        private static readonly byte[] Signature = { 137, 80, 78, 71, 13, 10, 26, 10 };

        private const int ChannelsRgba = 4;
        private const byte Opaque = 255;

        private PngImage(int width, int height, byte[] rgba)
        {
            Width = width;
            Height = height;
            Rgba = rgba;
        }

        public int Width { get; }

        public int Height { get; }

        // 행 우선, 픽셀마다 R, G, B, A
        public byte[] Rgba { get; }

        public uint PixelAt(int x, int y)
        {
            int i = (y * Width + x) * ChannelsRgba;
            return ((uint)Rgba[i] << 24) | ((uint)Rgba[i + 1] << 16) | ((uint)Rgba[i + 2] << 8) | Rgba[i + 3];
        }

        public byte AlphaAt(int x, int y) => Rgba[(y * Width + x) * ChannelsRgba + 3];

        // 0xRRGGBB
        public int RgbAt(int x, int y)
        {
            int i = (y * Width + x) * ChannelsRgba;
            return (Rgba[i] << 16) | (Rgba[i + 1] << 8) | Rgba[i + 2];
        }

        public static PngImage Load(string path) => Decode(File.ReadAllBytes(path));

        // 머리말(IHDR)만 읽어 크기를 낸다
        public static bool TryReadSize(string path, out int width, out int height)
        {
            width = 0;
            height = 0;
            byte[] head = new byte[24];
            using (FileStream fs = File.OpenRead(path))
            {
                int n = 0;
                while (n < head.Length)
                {
                    int r = fs.Read(head, n, head.Length - n);
                    if (r <= 0)
                    {
                        return false;
                    }
                    n += r;
                }
            }
            for (int i = 0; i < Signature.Length; i++)
            {
                if (head[i] != Signature[i])
                {
                    return false;
                }
            }
            if (head[12] != 'I' || head[13] != 'H' || head[14] != 'D' || head[15] != 'R')
            {
                return false;
            }
            width = (int)ReadU32(head, 16);
            height = (int)ReadU32(head, 20);
            return width > 0 && height > 0;
        }

        public static PngImage Decode(byte[] data)
        {
            if (data.Length < Signature.Length)
            {
                throw new PngFormatException("too short");
            }
            for (int i = 0; i < Signature.Length; i++)
            {
                if (data[i] != Signature[i])
                {
                    throw new PngFormatException("not a PNG signature");
                }
            }
            int pos = Signature.Length;
            int width = 0, height = 0, bitDepth = 0, colorType = -1, interlace = 0;
            byte[]? palette = null;
            byte[]? trns = null;
            var idat = new MemoryStream();
            bool sawHeader = false, sawEnd = false;
            while (pos + 8 <= data.Length)
            {
                int len = (int)ReadU32(data, pos);
                if (len < 0 || pos + 12 + len > data.Length)
                {
                    throw new PngFormatException("chunk runs past the end of the file");
                }
                string type = new string(new[] { (char)data[pos + 4], (char)data[pos + 5], (char)data[pos + 6], (char)data[pos + 7] });
                int body = pos + 8;
                switch (type)
                {
                    case "IHDR":
                        width = (int)ReadU32(data, body);
                        height = (int)ReadU32(data, body + 4);
                        bitDepth = data[body + 8];
                        colorType = data[body + 9];
                        interlace = data[body + 12];
                        sawHeader = true;
                        break;
                    case "PLTE":
                        palette = Slice(data, body, len);
                        break;
                    case "tRNS":
                        trns = Slice(data, body, len);
                        break;
                    case "IDAT":
                        idat.Write(data, body, len);
                        break;
                    case "IEND":
                        sawEnd = true;
                        break;
                }
                pos += 12 + len;
                if (sawEnd)
                {
                    break;
                }
            }
            if (!sawHeader || width <= 0 || height <= 0)
            {
                throw new PngFormatException("missing or bad IHDR");
            }
            if (interlace != 0)
            {
                throw new PngFormatException("interlaced PNG is not supported");
            }
            int channels = ChannelsOf(colorType);
            if (channels == 0 || !ValidDepth(colorType, bitDepth))
            {
                throw new PngFormatException("unsupported color type " + colorType + " / bit depth " + bitDepth);
            }
            if (colorType == 3 && palette == null)
            {
                throw new PngFormatException("palette PNG without PLTE");
            }

            int bitsPerPixel = channels * bitDepth;
            int stride = (width * bitsPerPixel + 7) / 8;
            int bpp = Math.Max(1, bitsPerPixel / 8);
            byte[] raw = Inflate(idat.ToArray());
            if (raw.Length < (stride + 1) * height)
            {
                throw new PngFormatException("image data is shorter than the image");
            }

            var rgba = new byte[width * height * ChannelsRgba];
            var prev = new byte[stride];
            var line = new byte[stride];
            int src = 0;
            for (int y = 0; y < height; y++)
            {
                int filter = raw[src++];
                Array.Copy(raw, src, line, 0, stride);
                src += stride;
                Unfilter(filter, line, prev, bpp);
                WriteRow(line, y, width, colorType, bitDepth, palette, trns, rgba);
                byte[] t = prev;
                prev = line;
                line = t;
            }
            return new PngImage(width, height, rgba);
        }

        private static void Unfilter(int filter, byte[] line, byte[] prev, int bpp)
        {
            int n = line.Length;
            switch (filter)
            {
                case 0:
                    return;
                case 1:
                    for (int i = bpp; i < n; i++)
                    {
                        line[i] = (byte)(line[i] + line[i - bpp]);
                    }
                    return;
                case 2:
                    for (int i = 0; i < n; i++)
                    {
                        line[i] = (byte)(line[i] + prev[i]);
                    }
                    return;
                case 3:
                    for (int i = 0; i < n; i++)
                    {
                        int a = i >= bpp ? line[i - bpp] : 0;
                        line[i] = (byte)(line[i] + ((a + prev[i]) >> 1));
                    }
                    return;
                case 4:
                    for (int i = 0; i < n; i++)
                    {
                        int a = i >= bpp ? line[i - bpp] : 0;
                        int b = prev[i];
                        int c = i >= bpp ? prev[i - bpp] : 0;
                        int p = a + b - c;
                        int pa = Math.Abs(p - a), pb = Math.Abs(p - b), pc = Math.Abs(p - c);
                        int pr = pa <= pb && pa <= pc ? a : (pb <= pc ? b : c);
                        line[i] = (byte)(line[i] + pr);
                    }
                    return;
                default:
                    throw new PngFormatException("bad filter type " + filter);
            }
        }

        private static void WriteRow(byte[] line, int y, int width, int colorType, int depth, byte[]? palette, byte[]? trns, byte[] rgba)
        {
            int o = y * width * ChannelsRgba;
            for (int x = 0; x < width; x++, o += ChannelsRgba)
            {
                byte r, g, b, a = Opaque;
                switch (colorType)
                {
                    case 0:
                    {
                        int v = Sample(line, x, depth);
                        byte gray = ToByte(v, depth);
                        r = g = b = gray;
                        if (trns != null && trns.Length >= 2 && v == ((trns[0] << 8) | trns[1]))
                        {
                            a = 0;
                        }
                        break;
                    }
                    case 2:
                    {
                        int step = depth / 8;
                        int i = x * 3 * step;
                        r = line[i];
                        g = line[i + step];
                        b = line[i + 2 * step];
                        if (trns != null && trns.Length >= 6)
                        {
                            int rv = Sample16Or8(line, i, step), gv = Sample16Or8(line, i + step, step), bv = Sample16Or8(line, i + 2 * step, step);
                            if (rv == ((trns[0] << 8) | trns[1]) && gv == ((trns[2] << 8) | trns[3]) && bv == ((trns[4] << 8) | trns[5]))
                            {
                                a = 0;
                            }
                        }
                        break;
                    }
                    case 3:
                    {
                        int k = Sample(line, x, depth);
                        if (palette == null || k * 3 + 2 >= palette.Length)
                        {
                            throw new PngFormatException("palette index " + k + " out of range");
                        }
                        r = palette[k * 3];
                        g = palette[k * 3 + 1];
                        b = palette[k * 3 + 2];
                        if (trns != null && k < trns.Length)
                        {
                            a = trns[k];
                        }
                        break;
                    }
                    case 4:
                    {
                        int step = depth / 8;
                        int i = x * 2 * step;
                        r = g = b = line[i];
                        a = line[i + step];
                        break;
                    }
                    default:
                    {
                        int step = depth / 8;
                        int i = x * 4 * step;
                        r = line[i];
                        g = line[i + step];
                        b = line[i + 2 * step];
                        a = line[i + 3 * step];
                        break;
                    }
                }
                rgba[o] = r;
                rgba[o + 1] = g;
                rgba[o + 2] = b;
                rgba[o + 3] = a;
            }
        }

        private static int Sample16Or8(byte[] line, int i, int step) => step == 2 ? (line[i] << 8) | line[i + 1] : line[i];

        // 한 채널 표본(회색·팔레트). 1·2·4비트는 한 바이트에 여럿이 왼쪽부터 들어 있다
        private static int Sample(byte[] line, int x, int depth)
        {
            switch (depth)
            {
                case 8:
                    return line[x];
                case 16:
                    return (line[x * 2] << 8) | line[x * 2 + 1];
                default:
                {
                    int perByte = 8 / depth;
                    int shift = (perByte - 1 - (x % perByte)) * depth;
                    return (line[x / perByte] >> shift) & ((1 << depth) - 1);
                }
            }
        }

        private static byte ToByte(int v, int depth)
        {
            switch (depth)
            {
                case 16: return (byte)(v >> 8);
                case 8: return (byte)v;
                default: return (byte)(v * 255 / ((1 << depth) - 1));
            }
        }

        private static int ChannelsOf(int colorType)
        {
            switch (colorType)
            {
                case 0: return 1;
                case 2: return 3;
                case 3: return 1;
                case 4: return 2;
                case 6: return 4;
                default: return 0;
            }
        }

        private static bool ValidDepth(int colorType, int depth)
        {
            switch (colorType)
            {
                case 0: return depth == 1 || depth == 2 || depth == 4 || depth == 8 || depth == 16;
                case 3: return depth == 1 || depth == 2 || depth == 4 || depth == 8;
                default: return depth == 8 || depth == 16;
            }
        }

        private static byte[] Inflate(byte[] zlib)
        {
            try
            {
                using (var input = new MemoryStream(zlib))
                using (var z = new ZLibStream(input, CompressionMode.Decompress))
                using (var output = new MemoryStream())
                {
                    z.CopyTo(output);
                    return output.ToArray();
                }
            }
            catch (InvalidDataException e)
            {
                throw new PngFormatException("bad zlib data: " + e.Message);
            }
        }

        private static byte[] Slice(byte[] data, int start, int len)
        {
            var b = new byte[len];
            Array.Copy(data, start, b, 0, len);
            return b;
        }

        private static uint ReadU32(byte[] d, int i) => ((uint)d[i] << 24) | ((uint)d[i + 1] << 16) | ((uint)d[i + 2] << 8) | d[i + 3];
    }
}
