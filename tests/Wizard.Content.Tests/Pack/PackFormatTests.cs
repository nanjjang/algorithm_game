using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using Wizard.Content;
using Wizard.Core;
using Wizard.DataBuild.Core;
using Xunit;

namespace Wizard.Content.Tests.Pack
{
    // CONTRACTS 11.2: DataBuild 가 쓴 머리말(magic, schema, data_version, core_compat, 절 목차, SHA-256)을 ContentPack 이 읽는다.
    // 해시·형식이 틀리면 ContentException.
    public sealed class PackFormatTests
    {
        private static readonly byte[] Compat = PackWriter.CoreCompatFromHex("00112233445566778899aabbccddeeff");

        private static byte[] Pack(params (ushort Id, byte[] Body)[] sections)
        {
            var list = new List<PackSection>();
            foreach (var (id, body) in sections)
            {
                list.Add(new PackSection(id, body));
            }
            return PackWriter.Write(PackWriter.SchemaVersion, 7, Compat, list);
        }

        [Fact]
        public void Header_ByteLayoutIsContracts11_2()
        {
            byte[] b = Pack((1, new byte[] { 0xAA, 0xBB }), (20, new byte[] { 0xCC }));
            // magic "WZPK"
            Assert.Equal(new byte[] { (byte)'W', (byte)'Z', (byte)'P', (byte)'K' }, b.AsSpan(0, 4).ToArray());
            var r = new ByteReader(b);
            Assert.True(r.TryBytes(4, out _));
            Assert.True(r.TryU16(out ushort schema));
            Assert.Equal(PackWriter.SchemaVersion, schema);
            Assert.True(r.TryU32(out uint dataVersion));
            Assert.Equal(7u, dataVersion);
            Assert.True(r.TryBytes(16, out ReadOnlySpan<byte> compat));
            Assert.Equal(Compat, compat.ToArray());
            Assert.True(r.TryU16(out ushort count));
            Assert.Equal(2, count);
            int headerEnd = 4 + 2 + 4 + 16 + 2 + 2 * 10;
            Assert.True(r.TryU16(out ushort id0));
            Assert.True(r.TryU32(out uint off0));
            Assert.True(r.TryU32(out uint len0));
            Assert.True(r.TryU16(out ushort id1));
            Assert.True(r.TryU32(out uint off1));
            Assert.True(r.TryU32(out uint len1));
            Assert.Equal((1, (uint)headerEnd, 2u), (id0, off0, len0));
            Assert.Equal((20, (uint)headerEnd + 2, 1u), (id1, off1, len1));
            Assert.Equal(new byte[] { 0xAA, 0xBB, 0xCC }, b.AsSpan(headerEnd, 3).ToArray());
            // sha256 는 앞의 모든 바이트
            Assert.Equal(headerEnd + 3 + 32, b.Length);
            Assert.Equal(SHA256.HashData(b.AsSpan(0, b.Length - 32)), b.AsSpan(b.Length - 32).ToArray());
        }

        [Fact]
        public void RoundTrip_ContentPackReadsWhatPackWriterWrote()
        {
            byte[] body1 = { 1, 2, 3 };
            byte[] body40 = { 9, 8 };
            ContentPack pack = ContentPack.Load(Pack((1, body1), (34, Array.Empty<byte>()), (40, body40)));
            Assert.Equal(PackWriter.SchemaVersion, pack.SchemaVersion);
            Assert.Equal(7, pack.DataVersion);
            Assert.Equal("00112233445566778899aabbccddeeff", pack.CoreCompat);
            Assert.Equal(3, pack.SectionCount);
            Assert.True(pack.TryGetSection(1, out ReadOnlyMemory<byte> s1));
            Assert.Equal(body1, s1.ToArray());
            Assert.True(pack.TryGetSection(34, out ReadOnlyMemory<byte> s34));
            Assert.Equal(0, s34.Length);
            Assert.True(pack.TryGetSection(40, out ReadOnlyMemory<byte> s40));
            Assert.Equal(body40, s40.ToArray());
            Assert.False(pack.TryGetSection(2, out _));
            byte[] all = Pack((1, body1), (34, Array.Empty<byte>()), (40, body40));
            Assert.Equal(all.AsSpan(all.Length - 32).ToArray(), pack.Sha256.ToArray());
        }

        // ContentDb.Build 는 아는 절의 본문을 절 훅으로 읽는다(T-47). 그래서 임의 바이트를 아는 절 id 에 실은 팩으로는 만들지 않는다.
        // 모르는 절은 건너뛴다(CONTRACTS 11.2): 목차의 맨 끝 id 에 임의 바이트를 실어도 Build 는 그대로 된다.
        [Fact]
        public void ContentDb_BuildSkipsUnknownSections()
        {
            ContentPack pack = ContentPack.Load(Pack((65534, new byte[] { 1, 2, 3 }), (65535, new byte[] { 0xFF })));
            Assert.Null(ContentSections.Name((ContentSection)65534));
            Assert.Null(ContentSections.Name((ContentSection)65535));
            ContentDb db = ContentDb.Build(pack);
            Assert.Equal(7, db.DataVersion);
            Assert.Same(pack, db.Pack);
        }

        [Fact]
        public void EmptyPack_RoundTrips()
        {
            ContentPack pack = ContentPack.Load(Pack());
            Assert.Equal(0, pack.SectionCount);
        }

        [Fact]
        public void SameInput_SameBytes()
        {
            Assert.Equal(Pack((1, new byte[] { 5 }), (2, new byte[] { 6 })), Pack((1, new byte[] { 5 }), (2, new byte[] { 6 })));
        }

        [Fact]
        public void Corruption_EveryByteFlipIsContentException()
        {
            byte[] good = Pack((1, new byte[] { 1, 2, 3 }), (20, new byte[] { 4 }));
            for (int i = 0; i < good.Length; i++)
            {
                byte[] bad = (byte[])good.Clone();
                bad[i] ^= 0x01;
                Assert.Throws<ContentException>(() => ContentPack.Load(bad));
            }
        }

        [Fact]
        public void Truncation_IsContentException()
        {
            byte[] good = Pack((1, new byte[] { 1, 2, 3 }));
            for (int n = 0; n < good.Length; n++)
            {
                byte[] cut = good.AsSpan(0, n).ToArray();
                Assert.Throws<ContentException>(() => ContentPack.Load(cut));
            }
        }

        // 해시를 다시 맞춘 채 형식만 틀린 팩
        private static byte[] Resign(byte[] b)
        {
            byte[] sha = SHA256.HashData(b.AsSpan(0, b.Length - 32));
            sha.CopyTo(b, b.Length - 32);
            return b;
        }

        [Fact]
        public void FormatErrors_WithValidHash_AreContentException()
        {
            byte[] magic = Pack((1, new byte[] { 1 }));
            magic[3] = (byte)'X';
            Assert.Throws<ContentException>(() => ContentPack.Load(Resign(magic)));

            // 절 목차의 길이가 본문을 넘는다
            byte[] range = Pack((1, new byte[] { 1 }));
            range[28 + 2 + 4] = 0xFF;
            Assert.Throws<ContentException>(() => ContentPack.Load(Resign(range)));

            // 절 수가 목차보다 크다
            byte[] count = Pack((1, new byte[] { 1 }));
            count[26] = 9;
            Assert.Throws<ContentException>(() => ContentPack.Load(Resign(count)));

            // 절 id 가 오름차순이 아니다(writer 를 거치지 않고 목차의 둘째 id 를 첫째보다 작게)
            byte[] order = Pack((3, new byte[] { 1 }), (5, new byte[] { 2 }));
            order[28 + 10] = 2;
            Assert.Throws<ContentException>(() => ContentPack.Load(Resign(order)));
        }

        [Fact]
        public void Writer_RejectsBadInput()
        {
            var two = new List<PackSection> { new PackSection(5, new byte[0]), new PackSection(5, new byte[0]) };
            Assert.Throws<ArgumentException>(() => PackWriter.Write(1, 1, Compat, two));
            var desc = new List<PackSection> { new PackSection(5, new byte[0]), new PackSection(2, new byte[0]) };
            Assert.Throws<ArgumentException>(() => PackWriter.Write(1, 1, Compat, desc));
            Assert.Throws<ArgumentException>(() => PackWriter.Write(1, 1, new byte[15], new List<PackSection>()));
            Assert.Throws<ArgumentException>(() => PackWriter.CoreCompatFromHex("00112233"));
            Assert.Throws<ArgumentException>(() => PackWriter.CoreCompatFromHex("00112233445566778899AABBCCDDEEFF"));
        }

        [Fact]
        public void CoreCompat_IsTheLinkedCoreVersion()
        {
            PackOptions o = PackOptions.Default(3);
            Assert.Equal(Wizard.Sim.SimVersion.CoreVersion, o.CoreVersion);
            byte[] b = PackBuilder.WritePack(new List<PackSection>(), o);
            Assert.Equal(Wizard.Sim.SimVersion.CoreVersion, ContentPack.Load(b).CoreCompat);
            Assert.Equal(3, ContentPack.Load(b).DataVersion);
        }
    }
}
