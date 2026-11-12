using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Wizard.TestKit;

namespace Wizard.Protocol.Tests.Duel
{
    // tests/fixtures/packets/ 의 두 파일을 읽는다. 형식은 파일 머리의 설명 그대로다.
    internal static class PacketFixtures
    {
        public const string BodiesPath = "tests/fixtures/packets/duel-bodies.txt";
        public const string FramesPath = "tests/fixtures/packets/duel-frames.txt";

        private static readonly Lazy<Dictionary<string, Body>> BodiesLazy = new Lazy<Dictionary<string, Body>>(LoadBodies);
        private static readonly Lazy<Dictionary<string, Frame>> FramesLazy = new Lazy<Dictionary<string, Frame>>(LoadFrames);

        internal sealed class Body
        {
            public Body(string name, int kind, byte[] bytes)
            {
                Name = name;
                Kind = kind;
                Bytes = bytes;
            }

            public string Name { get; }
            public int Kind { get; }
            public byte[] Bytes { get; }
        }

        internal sealed class Frame
        {
            public Frame(string name, byte[] key, int direction, byte[] nonce, string body, byte[] packet)
            {
                Name = name;
                Key = key;
                Direction = direction;
                Nonce = nonce;
                BodyName = body;
                Packet = packet;
            }

            public string Name { get; }
            public byte[] Key { get; }
            public int Direction { get; }
            public byte[] Nonce { get; }
            public string BodyName { get; }
            public byte[] Packet { get; }
        }

        public static IReadOnlyDictionary<string, Body> Bodies => BodiesLazy.Value;

        public static IReadOnlyDictionary<string, Frame> Frames => FramesLazy.Value;

        public static byte[] BodyBytes(string name) => (byte[])Bodies[name].Bytes.Clone();

        public static IEnumerable<object[]> BodyNames()
        {
            foreach (string n in Bodies.Keys)
            {
                yield return new object[] { n };
            }
        }

        public static IEnumerable<object[]> FrameNames()
        {
            foreach (string n in Frames.Keys)
            {
                yield return new object[] { n };
            }
        }

        public static byte[] Hex(string text)
        {
            string s = text.Replace(" ", "", StringComparison.Ordinal);
            if (s.Length % 2 != 0)
            {
                throw new FormatException("odd hex length: " + text);
            }
            var b = new byte[s.Length / 2];
            for (int i = 0; i < b.Length; i++)
            {
                b[i] = byte.Parse(s.AsSpan(2 * i, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            }
            return b;
        }

        private static IEnumerable<(string Section, string Key, string Value)> Lines(string path)
        {
            string section = "";
            foreach (string raw in File.ReadAllLines(TestRepo.Path(path)))
            {
                string line = raw;
                int hash = line.IndexOf('#', StringComparison.Ordinal);
                if (hash >= 0)
                {
                    line = line.Substring(0, hash);
                }
                line = line.Trim();
                if (line.Length == 0)
                {
                    continue;
                }
                if (line.StartsWith("[", StringComparison.Ordinal))
                {
                    section = line.Substring(1, line.Length - 2);
                    yield return (section, "", "");
                    continue;
                }
                int sp = line.IndexOf(' ', StringComparison.Ordinal);
                yield return (section, line.Substring(0, sp), line.Substring(sp + 1).Trim());
            }
        }

        private static Dictionary<string, Body> LoadBodies()
        {
            var kinds = new Dictionary<string, int>(StringComparer.Ordinal);
            var hex = new Dictionary<string, List<byte>>(StringComparer.Ordinal);
            var order = new List<string>();
            foreach (var (section, key, value) in Lines(BodiesPath))
            {
                if (key.Length == 0)
                {
                    order.Add(section);
                    hex[section] = new List<byte>();
                }
                else if (key == "kind")
                {
                    kinds[section] = int.Parse(value, CultureInfo.InvariantCulture);
                }
                else if (key == "hex")
                {
                    hex[section].AddRange(Hex(value));
                }
                else
                {
                    throw new FormatException("unknown key " + key);
                }
            }
            var result = new Dictionary<string, Body>(StringComparer.Ordinal);
            foreach (string n in order)
            {
                result[n] = new Body(n, kinds[n], hex[n].ToArray());
            }
            return result;
        }

        private static Dictionary<string, Frame> LoadFrames()
        {
            var fields = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
            var packets = new Dictionary<string, List<byte>>(StringComparer.Ordinal);
            var order = new List<string>();
            foreach (var (section, key, value) in Lines(FramesPath))
            {
                if (key.Length == 0)
                {
                    order.Add(section);
                    fields[section] = new Dictionary<string, string>(StringComparer.Ordinal);
                    packets[section] = new List<byte>();
                }
                else if (key == "packet")
                {
                    packets[section].AddRange(Hex(value));
                }
                else
                {
                    fields[section][key] = value;
                }
            }
            var result = new Dictionary<string, Frame>(StringComparer.Ordinal);
            foreach (string n in order)
            {
                Dictionary<string, string> f = fields[n];
                result[n] = new Frame(n, Hex(f["key"]), int.Parse(f["direction"], CultureInfo.InvariantCulture), Hex(f["nonce"]), f["body"],
                    packets[n].ToArray());
            }
            return result;
        }
    }
}
