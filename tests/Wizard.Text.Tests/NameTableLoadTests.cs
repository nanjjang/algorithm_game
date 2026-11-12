using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Wizard.Core;
using Wizard.DataBuild.Core;
using Wizard.DataBuild.Names;
using Wizard.TestKit;
using Xunit;

namespace Wizard.Text.Tests
{
    // CONTRACTS 5절: NameTable.Load 는 DataBuild 가 만든 이진 절(names.ko.wznm)을 ByteReader 로 읽는다.
    public sealed class NameTableLoadTests
    {
        [Fact]
        public void Load_ReadsWhatDataBuildWrites()
        {
            NameTable t = NameFixtures.InitialTable();
            Assert.Equal("ko", t.Locale);
            Assert.Equal(NameTableWriter.FormatVersion, t.Version);
            Assert.Equal(NameTable.FormatVersion, NameTableWriter.FormatVersion);
            Assert.Equal(NameFixtures.Initial.Length, t.Count);
            foreach (var kv in NameFixtures.Initial)
            {
                Assert.True(t.TryGet(kv.Key, out string v), kv.Key);
                Assert.Equal(kv.Value, v);   // 값은 원문 그대로(틀이면 {…} 가 남는다)
            }
        }

        [Fact]
        public void Load_RepositoryNamesFileRoundTrips()
        {
            // 저장소의 data/names/names.ko.json → DataBuild build 와 같은 길 → Load. 모든 항목이 같은 값으로 읽힌다
            var diagnostics = new Diagnostics();
            NamesFile? names = NamesFile.LoadFromRepo(TestRepo.Root, diagnostics);
            Assert.False(diagnostics.HasErrors);
            Assert.NotNull(names);
            NameTable t = NameTable.Load(NameTableWriter.Write(names!));
            Assert.Equal(names!.Entries.Count, t.Count);
            foreach (NameEntry e in names.Entries)
            {
                Assert.True(t.TryGet(e.Key, out string v), e.Key);
                Assert.Equal(e.Value, v);
            }
            Assert.False(t.TryGet(NamesFile.EndKey, out _));   // 표지는 이름이 아니다
        }

        [Fact]
        public void TryGet_UnknownKeyIsFalse()
        {
            NameTable t = NameFixtures.InitialTable();
            Assert.False(t.TryGet("boss.unknown", out string v));
            Assert.Equal(string.Empty, v);
        }

        [Fact]
        public void TryGet_AliasOnlyForTheFourWcZeroNames()
        {
            NameTable t = NameFixtures.InitialTable();
            Assert.True(t.TryGet("master", out _));
            Assert.True(t.TryGet("field", out _));
            Assert.True(t.TryGet("plaza", out _));
            Assert.True(t.TryGet("arena", out _));
            Assert.False(t.TryGet("tower", out _));     // place.tower 는 별칭이 없다(spec 14 2.3 표)
            Assert.False(t.TryGet("peddler", out _));
        }

        [Fact]
        public void TryGet_NullKeyThrows()
        {
            NameTable t = NameFixtures.InitialTable();
            Assert.Throws<ArgumentNullException>(() => t.TryGet(null!, out _));
        }

        [Fact]
        public void Load_EmptyTable()
        {
            NameTable t = NameTable.Load(NameFixtures.Bytes(Array.Empty<System.Collections.Generic.KeyValuePair<string, string>>()));
            Assert.Equal(0, t.Count);
            Assert.Equal("ko", t.Locale);
        }

        [Fact]
        public void Load_RejectsFlippedByte()
        {
            byte[] b = NameFixtures.Bytes(NameFixtures.Initial);
            for (int i = 0; i < b.Length; i++)
            {
                byte[] copy = (byte[])b.Clone();
                copy[i] ^= 0x01;
                Assert.Throws<InvalidDataException>(() => NameTable.Load(copy));
            }
        }

        [Fact]
        public void Load_RejectsTruncated()
        {
            byte[] b = NameFixtures.Bytes(NameFixtures.Initial);
            for (int len = 0; len < b.Length; len++)
            {
                Assert.Throws<InvalidDataException>(() => NameTable.Load(b.AsSpan(0, len)));
            }
        }

        [Fact]
        public void Load_RejectsWrongMagicEvenWithValidHash()
        {
            byte[] body = Body(w => { w.U16(NameTable.FormatVersion); Str(w, "ko"); w.U32(0); }, "WZNX");
            Assert.Throws<InvalidDataException>(() => NameTable.Load(body));
        }

        [Fact]
        public void Load_RejectsUnknownVersion()
        {
            byte[] b = Body(w => { w.U16(NameTable.FormatVersion + 1); Str(w, "ko"); w.U32(0); });
            Assert.Throws<InvalidDataException>(() => NameTable.Load(b));
        }

        [Fact]
        public void Load_RejectsUnsortedOrDuplicateKeys()
        {
            byte[] unsorted = Body(w => { w.U16(NameTable.FormatVersion); Str(w, "ko"); w.U32(2); Str(w, "char.2"); Str(w, "b"); Str(w, "char.1"); Str(w, "a"); });
            Assert.Throws<InvalidDataException>(() => NameTable.Load(unsorted));
            byte[] duplicate = Body(w => { w.U16(NameTable.FormatVersion); Str(w, "ko"); w.U32(2); Str(w, "char.1"); Str(w, "b"); Str(w, "char.1"); Str(w, "a"); });
            Assert.Throws<InvalidDataException>(() => NameTable.Load(duplicate));
        }

        [Fact]
        public void Load_RejectsTrailingBytesAndHugeCount()
        {
            byte[] trailing = Body(w => { w.U16(NameTable.FormatVersion); Str(w, "ko"); w.U32(0); w.U8(0); });
            Assert.Throws<InvalidDataException>(() => NameTable.Load(trailing));
            byte[] huge = Body(w => { w.U16(NameTable.FormatVersion); Str(w, "ko"); w.U32(uint.MaxValue); });
            Assert.Throws<InvalidDataException>(() => NameTable.Load(huge));
        }

        [Fact]
        public void Load_RejectsInvalidUtf8AndEmptyKey()
        {
            byte[] badUtf8 = Body(w =>
            {
                w.U16(NameTable.FormatVersion);
                Str(w, "ko");
                w.U32(1);
                Str(w, "char.1");
                w.VarUInt(1);
                w.U8(0xFF);
            });
            Assert.Throws<InvalidDataException>(() => NameTable.Load(badUtf8));
            byte[] emptyKey = Body(w => { w.U16(NameTable.FormatVersion); Str(w, "ko"); w.U32(1); Str(w, ""); Str(w, "a"); });
            Assert.Throws<InvalidDataException>(() => NameTable.Load(emptyKey));
            byte[] emptyLocale = Body(w => { w.U16(NameTable.FormatVersion); Str(w, ""); w.U32(0); });
            Assert.Throws<InvalidDataException>(() => NameTable.Load(emptyLocale));
        }

        [Fact]
        public void Load_SameBytesSameTable()
        {
            byte[] b = NameFixtures.Bytes(NameFixtures.Initial);
            Assert.Equal(b, NameFixtures.Bytes(NameFixtures.Initial));
            NameTable x = NameTable.Load(b);
            NameTable y = NameTable.Load(b);
            foreach (var kv in NameFixtures.Initial)
            {
                Assert.True(x.TryGet(kv.Key, out string a));
                Assert.True(y.TryGet(kv.Key, out string c));
                Assert.Equal(a, c);
            }
        }

        // magic + 본문 + 올바른 sha256
        private static byte[] Body(Action<ByteWriter> write, string magic = "WZNM")
        {
            var w = new ByteWriter(256);
            foreach (char ch in magic)
            {
                w.U8((byte)ch);
            }
            write(w);
            w.Bytes(SHA256.HashData(w.Written));
            return w.ToArray();
        }

        private static void Str(ByteWriter w, string s)
        {
            byte[] b = Encoding.UTF8.GetBytes(s);
            w.VarUInt((ulong)b.Length);
            w.Bytes(b);
        }
    }
}
