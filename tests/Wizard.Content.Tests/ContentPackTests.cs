using System;
using System.Security.Cryptography;
using Wizard.Content;
using Wizard.Core;
using Xunit;

namespace Wizard.Content.Tests
{
    // CONTRACTS 11.2. 팩 머리말·목차·해시
    public sealed class ContentPackTests
    {
        private static byte[] BuildPack(params (ushort Id, byte[] Body)[] sections)
        {
            var w = new ByteWriter();
            w.U8((byte)'W'); w.U8((byte)'Z'); w.U8((byte)'P'); w.U8((byte)'K');
            w.U16(1);                     // schema
            w.U32(42);                    // data_version
            for (int i = 0; i < 16; i++)
            {
                w.U8((byte)(0xA0 + i));   // core_compat
            }
            w.U16((ushort)sections.Length);
            int offset = 4 + 2 + 4 + 16 + 2 + sections.Length * 10;
            foreach (var (id, body) in sections)
            {
                w.U16(id);
                w.U32((uint)offset);
                w.U32((uint)body.Length);
                offset += body.Length;
            }
            foreach (var (_, body) in sections)
            {
                w.Bytes(body);
            }
            byte[] head = w.ToArray();
            byte[] sha = SHA256.HashData(head);
            var all = new byte[head.Length + sha.Length];
            head.CopyTo(all, 0);
            sha.CopyTo(all, head.Length);
            return all;
        }

        [Fact]
        public void Load_ReadsHeaderAndSections()
        {
            byte[] bytes = BuildPack((1, new byte[] { 1, 2, 3 }), (20, new byte[] { 9 }));
            ContentPack pack = ContentPack.Load(bytes);
            Assert.Equal(1, pack.SchemaVersion);
            Assert.Equal(42, pack.DataVersion);
            Assert.Equal("a0a1a2a3a4a5a6a7a8a9aaabacadaeaf", pack.CoreCompat);
            Assert.Equal(2, pack.SectionCount);
            Assert.True(pack.TryGetSection(1, out ReadOnlyMemory<byte> s1));
            Assert.Equal(new byte[] { 1, 2, 3 }, s1.ToArray());
            Assert.True(pack.TryGetSection(20, out ReadOnlyMemory<byte> s20));
            Assert.Equal(new byte[] { 9 }, s20.ToArray());
            Assert.False(pack.TryGetSection(7, out _));
            // ContentDb.Build 는 아는 절의 본문을 절 훅으로 읽는다(T-47). 위의 임의 바이트로는 만들지 않고 절이 없는 팩으로 본다
            Assert.Equal(42, ContentDb.Build(ContentPack.Load(BuildPack())).DataVersion);
        }

        [Fact]
        public void Load_RejectsCorruption()
        {
            byte[] bytes = BuildPack((1, new byte[] { 1, 2, 3 }));
            bytes[bytes.Length - 40] ^= 0xFF;   // 본문 한 바이트
            Assert.Throws<ContentException>(() => ContentPack.Load(bytes));
            byte[] magic = BuildPack();
            magic[0] = (byte)'X';
            Assert.Throws<ContentException>(() => ContentPack.Load(magic));
            Assert.Throws<ContentException>(() => ContentPack.Load(new byte[10]));
        }

        [Fact]
        public void Load_RejectsUnorderedSections()
        {
            Assert.Throws<ContentException>(() => ContentPack.Load(BuildPack((5, new byte[1]), (2, new byte[1]))));
        }

        [Fact]
        public void ContentDb_MissingSectionIsContentException()
        {
            ContentDb db = ContentDb.Build(ContentPack.Load(BuildPack()));
            Assert.Throws<ContentException>(() => db.Block(BlockId.Fireball));
            Assert.Throws<ContentException>(() => db.Net);
        }
    }
}
