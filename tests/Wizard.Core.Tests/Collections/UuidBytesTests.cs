using System;
using System.Collections.Generic;
using System.IO;
using Wizard.Core;
using Wizard.TestKit;
using Xunit;

namespace Wizard.Core.Tests.Collections
{
    // CONTRACTS 0.3("uuid 의 바이트")·1.5. M1-CORE-05 수락 조건 5.
    // 기대 바이트는 tests/fixtures/uuid/known-uuids.txt(문서에서 손으로 옮긴 값)에서 온다.
    public sealed class UuidBytesTests
    {
        private const string FixturePath = "tests/fixtures/uuid/known-uuids.txt";

        internal sealed class Known
        {
            public Known(string text, byte[] network, byte[] mixed)
            {
                Text = text;
                Network = network;
                Mixed = mixed;
            }

            public string Text { get; }
            public byte[] Network { get; }
            public byte[] Mixed { get; }
        }

        [Fact]
        public void Fixture_HasAsymmetricEntries()
        {
            List<Known> all = Load();
            Assert.True(all.Count >= 5);
            int asymmetric = 0;
            foreach (Known k in all)
            {
                if (!Same(k.Network, k.Mixed))
                {
                    asymmetric++;
                }
            }
            Assert.True(asymmetric >= 4, "fixture must contain uuids whose network and mixed orders differ");
        }

        [Fact]
        public void Write_IsNetworkOrderAndDiffersFromToByteArray()
        {
            foreach (Known k in Load())
            {
                Guid g = Guid.Parse(k.Text);
                var bytes = new byte[16];
                UuidBytes.Write(g, bytes);
                Assert.Equal(k.Network, bytes);                           // 픽스처의 네트워크 순서와 같다
                Assert.Equal(k.Mixed, g.ToByteArray());                   // 픽스처의 혼합 순서 = .NET 의 ToByteArray(픽스처 자체의 검산)
                if (!Same(k.Network, k.Mixed))
                {
                    Assert.NotEqual(g.ToByteArray(), bytes);              // ToByteArray 와 다르다
                }
                Assert.Equal(k.Text.Replace("-", string.Empty), Hex(bytes));   // 문자열 hex 순서 그대로
            }
        }

        [Fact]
        public void Read_IsInverseOfWriteAndParsesNetworkOrder()
        {
            foreach (Known k in Load())
            {
                Guid g = Guid.Parse(k.Text);
                Assert.Equal(g, UuidBytes.Read(k.Network));
                var round = new byte[16];
                UuidBytes.Write(UuidBytes.Read(k.Network), round);
                Assert.Equal(k.Network, round);
                if (!Same(k.Network, k.Mixed))
                {
                    Assert.NotEqual(g, new Guid(k.Network));              // 네트워크 바이트를 Guid(byte[]) 로 읽으면 다른 값이 된다
                }
            }
        }

        // 더 큰 버퍼의 앞 16바이트에 쓰고 읽는다. 뒤는 건드리지 않는다
        [Fact]
        public void WriteAndRead_UseFirst16BytesOfLargerSpan()
        {
            Known k = Load()[0];
            Guid g = Guid.Parse(k.Text);
            var buf = new byte[20];
            for (int i = 0; i < buf.Length; i++)
            {
                buf[i] = 0xEE;
            }
            UuidBytes.Write(g, new Span<byte>(buf, 2, 18));
            Assert.Equal(k.Network, new ReadOnlySpan<byte>(buf, 2, 16).ToArray());
            Assert.Equal(0xEE, buf[0]);
            Assert.Equal(0xEE, buf[1]);
            Assert.Equal(0xEE, buf[18]);
            Assert.Equal(0xEE, buf[19]);
            Assert.Equal(g, UuidBytes.Read(new ReadOnlySpan<byte>(buf, 2, 18)));
        }

        [Fact]
        public void RejectsShortSpans()
        {
            Assert.Throws<ArgumentException>(() => UuidBytes.Write(Guid.Empty, new byte[15]));
            Assert.Throws<ArgumentException>(() => UuidBytes.Read(new byte[15]));
        }

        private static List<Known> Load()
        {
            var list = new List<Known>();
            foreach (string raw in File.ReadAllLines(TestRepo.Path(FixturePath)))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line[0] == '#')
                {
                    continue;
                }
                string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                Assert.Equal(3, parts.Length);
                list.Add(new Known(parts[0], FromHex(parts[1]), FromHex(parts[2])));
            }
            return list;
        }

        private static byte[] FromHex(string hex)
        {
            Assert.Equal(32, hex.Length);
            return Convert.FromHexString(hex);
        }

        private static string Hex(byte[] bytes) => Convert.ToHexString(bytes).ToLowerInvariant();

        private static bool Same(byte[] a, byte[] b) => a.AsSpan().SequenceEqual(b);
    }
}
