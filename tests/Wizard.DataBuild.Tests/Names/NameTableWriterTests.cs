using System;
using System.Security.Cryptography;
using System.Text;
using Wizard.Core;
using Wizard.DataBuild.Names;
using Xunit;

namespace Wizard.DataBuild.Tests.Names
{
    // 이름 표의 이진 절 names.ko.wznm(CONTRACTS 5절): magic "WZNM", version u16, locale, count u32, (열쇠, 값)…, sha256.
    // 문자열은 varuint 바이트 수 + UTF-8. 표지 "~end" 는 싣지 않는다. ByteReader 만으로 읽힌다.
    public sealed class NameTableWriterTests
    {
        private static string ReadStr(ref ByteReader r)
        {
            Assert.True(r.TryVarUInt(out ulong n));
            Assert.True(r.TryBytes((int)n, out ReadOnlySpan<byte> b));
            return Encoding.UTF8.GetString(b);
        }

        [Fact]
        public void Layout_ReadableWithByteReader_WithoutEndMarker()
        {
            NamesFile names = NamesFile.FromText("{\n  \"char.1\": \"제자 1\",\n  \"char.5\": \"제자 5\",\n  \"~end\": \"\"\n}\n");
            byte[] b = NameTableWriter.Write(names);
            var r = new ByteReader(b);
            Assert.True(r.TryBytes(4, out ReadOnlySpan<byte> magic));
            Assert.Equal("WZNM", Encoding.ASCII.GetString(magic));
            Assert.True(r.TryU16(out ushort version));
            Assert.Equal(NameTableWriter.FormatVersion, version);
            Assert.Equal("ko", ReadStr(ref r));
            Assert.True(r.TryU32(out uint count));
            Assert.Equal(2u, count);
            Assert.Equal("char.1", ReadStr(ref r));
            Assert.Equal("제자 1", ReadStr(ref r));
            Assert.Equal("char.5", ReadStr(ref r));
            Assert.Equal("제자 5", ReadStr(ref r));
            Assert.Equal(32, r.Remaining);
            Assert.Equal(SHA256.HashData(b.AsSpan(0, b.Length - 32)), b.AsSpan(b.Length - 32).ToArray());
        }

        [Fact]
        public void Empty_HasZeroEntries()
        {
            byte[] b = NameTableWriter.Write(NamesFile.FromText("{\n  \"~end\": \"\"\n}\n"));
            Assert.Equal(4 + 2 + 1 + 2 + 4 + 32, b.Length);
            var r = new ByteReader(b.AsSpan(4 + 2 + 3));
            Assert.True(r.TryU32(out uint count));
            Assert.Equal(0u, count);
        }

        [Fact]
        public void SameNames_SameBytes_InOrdinalKeyOrder()
        {
            byte[] a = NameTableWriter.Write(NamesFile.FromText("{\n  \"b.x\": \"2\",\n  \"a.x\": \"1\",\n  \"~end\": \"\"\n}\n"));
            byte[] b = NameTableWriter.Write(NamesFile.FromText("{\n  \"a.x\": \"1\",\n  \"b.x\": \"2\",\n  \"~end\": \"\"\n}\n"));
            Assert.Equal(a, b);
        }

        [Fact]
        public void LongValue_UsesMultiByteVarUInt()
        {
            string longValue = new string('가', 100);   // UTF-8 300바이트 → varuint 2바이트
            byte[] b = NameTableWriter.Write(NamesFile.FromText("{\n  \"a.x\": \"" + longValue + "\",\n  \"~end\": \"\"\n}\n"));
            var r = new ByteReader(b.AsSpan(4 + 2 + 3 + 4));
            Assert.Equal("a.x", ReadStr(ref r));
            Assert.Equal(longValue, ReadStr(ref r));
        }

        [Fact]
        public void FileName_IsNamesLocaleWznm()
        {
            Assert.Equal("names.ko.wznm", NameTableWriter.FileName("ko"));
        }
    }
}
