using System;
using System.Text;
using System.Threading.Tasks;
using Wizard.Core;
using Xunit;

namespace Wizard.Core.Tests
{
    // CONTRACTS 1.4, T-10. FNV-1a 64 와 리틀 엔디언 정규 직렬화
    public sealed class StateAndBytesTests
    {
        [Theory]
        [InlineData("", 0xcbf29ce484222325UL)]
        [InlineData("a", 0xaf63dc4c8601ec8cUL)]
        [InlineData("foobar", 0x85944171f73967e8UL)]
        public void StateWriter_HashIsFnv1a64(string text, ulong expected)
        {
            StateWriter w = StateWriter.ForHash();
            foreach (byte b in Encoding.ASCII.GetBytes(text))
            {
                w.U8(b);
            }
            Assert.Equal(expected, w.Hash);
        }

        [Fact]
        public void StateWriter_BytesModeWritesLittleEndianAndSameHash()
        {
            var target = new ByteWriter();
            StateWriter bytes = StateWriter.ForBytes(target);
            StateWriter hash = StateWriter.ForHash();
            Write(ref bytes);
            Write(ref hash);
            Assert.Equal(hash.Hash, bytes.Hash);
            byte[] expected =
            {
                0x01,                                     // Bool(true)
                0x78, 0x56, 0x34, 0x12,                   // I32(0x12345678)
                0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,   // I64(-1)
                0x00, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00,   // Fix(One)
                0x28, 0x23, 0x00, 0x00,                   // Ang(9000)
                0x02, 0x00, 0x00, 0x00,                   // Count(2)
            };
            Assert.Equal(expected, target.Written.ToArray());

            var r = new StateReader(target.Written);
            Assert.True(r.Bool());
            Assert.Equal(0x12345678, r.I32());
            Assert.Equal(-1L, r.I64());
            Assert.Equal(Fix64.One, r.Fix());
            Assert.Equal(9000, r.Ang().Centi);
            Assert.Equal(2, r.Count());
            Assert.Equal(0, r.Remaining);
            Assert.Throws<SimInvariantException>(() => new StateReader(ReadOnlySpan<byte>.Empty).U8());
        }

        private static void Write(ref StateWriter w)
        {
            w.Bool(true);
            w.I32(0x12345678);
            w.I64(-1);
            w.Fix(Fix64.One);
            w.Ang(Angle.FromCenti(9000));
            w.Count(2);
        }

        [Fact]
        public void StateHash_SameSequenceSameHashAcrossThreads()
        {
            ulong reference = HashSequence(1234);
            var results = new ulong[16];
            Parallel.For(0, results.Length, i => results[i] = HashSequence(1234));
            Assert.All(results, h => Assert.Equal(reference, h));
            Assert.NotEqual(reference, HashSequence(1235));
        }

        private static ulong HashSequence(int seed)
        {
            StateWriter w = StateWriter.ForHash();
            var rng = new Pcg32((ulong)seed, 1);
            for (int i = 0; i < 1000; i++)
            {
                w.U32(rng.NextU32());
                w.Vec(new FixVec2(Fix64.FromRaw(i), Fix64.FromRaw(-i)));
            }
            return w.Hash;
        }

        [Fact]
        public void ByteWriter_LittleEndianAndGrowth()
        {
            var w = new ByteWriter(1);
            w.U16(0x0102);
            w.U32(0x03040506);
            w.U64(0x0708090A0B0C0D0EUL);
            w.I16(-2);
            w.Bytes(new byte[] { 0xAA, 0xBB });
            Assert.Equal(new byte[] { 0x02, 0x01, 0x06, 0x05, 0x04, 0x03, 0x0E, 0x0D, 0x0C, 0x0B, 0x0A, 0x09, 0x08, 0x07, 0xFE, 0xFF, 0xAA, 0xBB },
                w.Written.ToArray());
            w.Reset();
            Assert.Equal(0, w.Length);
        }

        [Theory]
        [InlineData(0UL, new byte[] { 0x00 })]
        [InlineData(127UL, new byte[] { 0x7F })]
        [InlineData(300UL, new byte[] { 0xAC, 0x02 })]
        [InlineData(ulong.MaxValue, new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x01 })]
        public void VarUInt_RoundTrips(ulong value, byte[] encoded)
        {
            var w = new ByteWriter();
            w.VarUInt(value);
            Assert.Equal(encoded, w.Written.ToArray());
            var r = new ByteReader(encoded);
            Assert.True(r.TryVarUInt(out ulong back));
            Assert.Equal(value, back);
            Assert.Equal(0, r.Remaining);
        }

        [Fact]
        public void ByteReader_RejectsTruncatedAndOverlongInput()
        {
            var r = new ByteReader(new byte[] { 0x01, 0x02, 0x03 });
            Assert.False(r.TryU32(out _));
            Assert.Equal(3, r.Remaining);                     // 실패해도 위치는 그대로
            Assert.True(r.TryU16(out ushort u16));
            Assert.Equal(0x0201, u16);
            Assert.False(new ByteReader(new byte[] { 0x80, 0x80 }).TryVarUInt(out _));   // 끝나지 않은 varint
            byte[] overflow = { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x02 };
            Assert.False(new ByteReader(overflow).TryVarUInt(out _));                    // 64비트를 넘는다
            Assert.False(new ByteReader(new byte[2]).TryBytes(3, out _));
            Assert.False(new ByteReader(new byte[2]).TryBytes(-1, out _));
        }

        [Fact]
        public void ByteReader_SignedValues()
        {
            var w = new ByteWriter();
            w.I32(-5);
            w.I64(long.MinValue);
            var r = new ByteReader(w.Written);
            Assert.True(r.TryI32(out int i32));
            Assert.True(r.TryI64(out long i64));
            Assert.Equal(-5, i32);
            Assert.Equal(long.MinValue, i64);
        }
    }
}
