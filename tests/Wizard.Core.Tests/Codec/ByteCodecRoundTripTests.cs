using System;
using Wizard.Core;
using Wizard.TestKit;
using Wizard.TestKit.Shared.Fuzz;
using Xunit;
using Xunit.Abstractions;

namespace Wizard.Core.Tests.Codec
{
    // CONTRACTS 1.4. ByteWriter → ByteReader, StateWriter.ForBytes → StateReader 왕복.
    // 버퍼를 넘는 읽기는 예외가 아니라 false 이고 위치는 그대로다(ByteReader). 기대 바이트는 리틀 엔디언과 LEB128 의 정의에서 왔다.
    public sealed class ByteCodecRoundTripTests
    {
        private const ulong FuzzSeed = 0xC0DEC_0004UL;
        private readonly ITestOutputHelper _output;

        public ByteCodecRoundTripTests(ITestOutputHelper output)
        {
            _output = output;
        }

        private static readonly ushort[] U16Edges = { 0, 1, 0x7F, 0x80, 0xFF, 0x100, 0x7FFF, 0x8000, 0xFFFE, ushort.MaxValue };
        private static readonly short[] I16Edges = { 0, 1, -1, 0x7F, -0x80, short.MaxValue, short.MinValue, short.MinValue + 1 };
        private static readonly uint[] U32Edges = { 0, 1, 0xFF, 0x100, 0xFFFF, 0x10000, 0x7FFFFFFF, 0x80000000, 0xFFFFFFFE, uint.MaxValue };
        private static readonly int[] I32Edges = { 0, 1, -1, 0xFF, -0x100, int.MaxValue, int.MinValue, int.MinValue + 1, int.MaxValue - 1 };
        private static readonly ulong[] U64Edges = { 0, 1, 0xFFFFFFFFUL, 0x100000000UL, 0x7FFFFFFFFFFFFFFFUL, 0x8000000000000000UL, ulong.MaxValue - 1, ulong.MaxValue };
        private static readonly long[] I64Edges = { 0, 1, -1, int.MaxValue, int.MinValue, long.MaxValue, long.MinValue, long.MinValue + 1, long.MaxValue - 1 };

        [Fact]
        public void ByteWriterReader_RoundTripsEveryPrimitiveAtEdges()
        {
            var w = new ByteWriter(1);   // 늘어나는 길도 같이 탄다
            for (int i = 0; i < 256; i++)
            {
                w.U8((byte)i);
            }
            foreach (ushort v in U16Edges) { w.U16(v); }
            foreach (short v in I16Edges) { w.I16(v); }
            foreach (uint v in U32Edges) { w.U32(v); }
            foreach (int v in I32Edges) { w.I32(v); }
            foreach (ulong v in U64Edges) { w.U64(v); }
            foreach (long v in I64Edges) { w.I64(v); }
            foreach (ulong v in VarUIntBoundaries()) { w.VarUInt(v); }
            byte[] blob = { 0xDE, 0xAD, 0xBE, 0xEF };
            w.Bytes(blob);
            w.Bytes(ReadOnlySpan<byte>.Empty);

            int expectedLength = 256 + 2 * (U16Edges.Length + I16Edges.Length) + 4 * (U32Edges.Length + I32Edges.Length)
                                 + 8 * (U64Edges.Length + I64Edges.Length) + VarUIntTotalLength() + blob.Length;
            Assert.Equal(expectedLength, w.Length);
            Assert.Equal(expectedLength, w.Written.Length);

            var r = new ByteReader(w.Written);
            for (int i = 0; i < 256; i++)
            {
                Assert.True(r.TryU8(out byte b));
                Assert.Equal((byte)i, b);
            }
            foreach (ushort v in U16Edges) { Assert.True(r.TryU16(out ushort x)); Assert.Equal(v, x); }
            foreach (short v in I16Edges) { Assert.True(r.TryI16(out short x)); Assert.Equal(v, x); }
            foreach (uint v in U32Edges) { Assert.True(r.TryU32(out uint x)); Assert.Equal(v, x); }
            foreach (int v in I32Edges) { Assert.True(r.TryI32(out int x)); Assert.Equal(v, x); }
            foreach (ulong v in U64Edges) { Assert.True(r.TryU64(out ulong x)); Assert.Equal(v, x); }
            foreach (long v in I64Edges) { Assert.True(r.TryI64(out long x)); Assert.Equal(v, x); }
            foreach (ulong v in VarUIntBoundaries()) { Assert.True(r.TryVarUInt(out ulong x)); Assert.Equal(v, x); }
            Assert.True(r.TryBytes(blob.Length, out ReadOnlySpan<byte> got));
            Assert.Equal(blob, got.ToArray());
            Assert.True(r.TryBytes(0, out ReadOnlySpan<byte> none));
            Assert.Equal(0, none.Length);
            Assert.Equal(0, r.Remaining);
            Assert.Equal(expectedLength, r.Position);

            // 끝에서 더 읽으면 모두 false, 기본값, 위치 그대로
            Assert.False(r.TryU8(out byte u8)); Assert.Equal(0, u8);
            Assert.False(r.TryU16(out ushort u16)); Assert.Equal(0, u16);
            Assert.False(r.TryI16(out short i16)); Assert.Equal(0, i16);
            Assert.False(r.TryU32(out uint u32)); Assert.Equal(0U, u32);
            Assert.False(r.TryI32(out int i32)); Assert.Equal(0, i32);
            Assert.False(r.TryU64(out ulong u64)); Assert.Equal(0UL, u64);
            Assert.False(r.TryI64(out long i64)); Assert.Equal(0L, i64);
            Assert.False(r.TryVarUInt(out ulong vu)); Assert.Equal(0UL, vu);
            Assert.False(r.TryBytes(1, out _));
            Assert.Equal(expectedLength, r.Position);
        }

        [Fact]
        public void FixedWidth_IsLittleEndian()
        {
            var w = new ByteWriter();
            w.U16(0xA1B2);
            w.I16(-2);
            w.U32(0xA1B2C3D4U);
            w.I32(int.MinValue);
            w.U64(0x0102030405060708UL);
            w.I64(-2L);
            Assert.Equal(new byte[]
            {
                0xB2, 0xA1,
                0xFE, 0xFF,
                0xD4, 0xC3, 0xB2, 0xA1,
                0x00, 0x00, 0x00, 0x80,
                0x08, 0x07, 0x06, 0x05, 0x04, 0x03, 0x02, 0x01,
                0xFE, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
            }, w.Written.ToArray());
        }

        // 고정 폭 읽기: 모자란 모든 길이에서 false, 위치 그대로, 남은 바이트로 다른 읽기를 이어 갈 수 있다
        [Fact]
        public void FixedWidth_ShortBufferFailsWithoutMoving()
        {
            byte[] full = { 0x11, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77, 0x88 };
            for (int len = 0; len < 8; len++)
            {
                var r = new ByteReader(new ReadOnlySpan<byte>(full, 0, len));
                Assert.False(r.TryU64(out _));
                Assert.False(r.TryI64(out _));
                if (len < 4)
                {
                    Assert.False(r.TryU32(out _));
                    Assert.False(r.TryI32(out _));
                }
                if (len < 2)
                {
                    Assert.False(r.TryU16(out _));
                    Assert.False(r.TryI16(out _));
                }
                if (len < 1)
                {
                    Assert.False(r.TryU8(out _));
                }
                Assert.False(r.TryBytes(len + 1, out _));
                Assert.Equal(len, r.Remaining);
                Assert.Equal(0, r.Position);
                Assert.True(r.TryBytes(len, out ReadOnlySpan<byte> rest));
                Assert.Equal(len, rest.Length);
            }
        }

        // varint 경계: 2^(7k)−1 과 2^(7k) (k = 1..9), 2^63−1, 2^63, 최댓값
        private static ulong[] VarUIntBoundaries()
        {
            var list = new System.Collections.Generic.List<ulong> { 0UL, 1UL };
            for (int k = 1; k <= 9; k++)
            {
                ulong p = 1UL << (7 * k);
                list.Add(p - 1UL);
                list.Add(p);
                list.Add(p + 1UL);
            }
            list.Add(0x7FFFFFFFFFFFFFFFUL);
            list.Add(0x8000000000000000UL);
            list.Add(ulong.MaxValue - 1UL);
            list.Add(ulong.MaxValue);
            return list.ToArray();
        }

        // LEB128 의 길이: 0 은 1바이트, 그 밖에는 ⌈비트 길이 / 7⌉
        private static int ExpectedVarUIntLength(ulong v)
        {
            int bits = 0;
            for (ulong x = v; x != 0UL; x >>= 1)
            {
                bits++;
            }
            return bits == 0 ? 1 : (bits + 6) / 7;
        }

        private static int VarUIntTotalLength()
        {
            int n = 0;
            foreach (ulong v in VarUIntBoundaries())
            {
                n += ExpectedVarUIntLength(v);
            }
            return n;
        }

        // 정의대로 만든 LEB128 (7비트씩 아래부터, 마지막 바이트만 최상위 비트 0)
        private static byte[] ReferenceLeb128(ulong v)
        {
            int n = ExpectedVarUIntLength(v);
            var b = new byte[n];
            for (int i = 0; i < n; i++)
            {
                b[i] = (byte)((v >> (7 * i)) & 0x7FUL);
                if (i < n - 1)
                {
                    b[i] |= 0x80;
                }
            }
            return b;
        }

        [Fact]
        public void VarUInt_BoundariesEncodeAsLeb128AndRoundTrip()
        {
            foreach (ulong v in VarUIntBoundaries())
            {
                var w = new ByteWriter();
                w.VarUInt(v);
                byte[] enc = w.Written.ToArray();
                Assert.Equal(ReferenceLeb128(v), enc);
                Assert.Equal(ExpectedVarUIntLength(v), enc.Length);

                var r = new ByteReader(enc);
                Assert.True(r.TryVarUInt(out ulong back));
                Assert.Equal(v, back);
                Assert.Equal(0, r.Remaining);
            }
            Assert.Equal(new byte[] { 0x80, 0x01 }, ReferenceLeb128(128UL));
            Assert.Equal(new byte[] { 0x80, 0x80, 0x01 }, ReferenceLeb128(16384UL));
        }

        // 잘린 varint: 모든 앞부분에서 false, 위치 그대로, 예외 없음
        [Fact]
        public void VarUInt_EveryTruncationFailsWithoutMoving()
        {
            foreach (ulong v in VarUIntBoundaries())
            {
                byte[] enc = ReferenceLeb128(v);
                for (int len = 0; len < enc.Length; len++)
                {
                    var r = new ByteReader(new ReadOnlySpan<byte>(enc, 0, len));
                    Assert.False(r.TryVarUInt(out ulong x));
                    Assert.Equal(0UL, x);
                    Assert.Equal(len, r.Remaining);
                    Assert.Equal(0, r.Position);
                }
            }
        }

        // 64비트를 넘는 varint 와 10바이트를 넘는 varint 는 false
        [Fact]
        public void VarUInt_OverlongOrOverflowingFails()
        {
            byte[] tenth2 = { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x02 };   // 2^64 이상
            byte[] tenthCont = { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x81, 0x00 };   // 11바이트
            byte[] elevenZeros = { 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x00 };
            foreach (byte[] bad in new[] { tenth2, tenthCont, elevenZeros })
            {
                var r = new ByteReader(bad);
                Assert.False(r.TryVarUInt(out ulong x));
                Assert.Equal(0UL, x);
                Assert.Equal(bad.Length, r.Remaining);
            }
        }

        // 아무 바이트나 읽혀도 예외가 없고, 성공한 읽기만 위치를 옮긴다(고정 씨앗 퍼징)
        [Fact]
        public void ByteReader_ArbitraryBytesNeverThrow()
        {
            FuzzAssert.BytesHold(_output, new FuzzOptions(FuzzSeed, 2000), 0, 48, DecodeAll);
        }

        private static string? DecodeAll(byte[] data)
        {
            var r = new ByteReader(data);
            int op = 0;
            int stuck = 0;
            while (r.Remaining > 0 && stuck < 9)
            {
                int before = r.Remaining;
                bool ok;
                int width;
                switch (op % 9)
                {
                    case 0: ok = r.TryU8(out _); width = 1; break;
                    case 1: ok = r.TryU16(out _); width = 2; break;
                    case 2: ok = r.TryI16(out _); width = 2; break;
                    case 3: ok = r.TryU32(out _); width = 4; break;
                    case 4: ok = r.TryI32(out _); width = 4; break;
                    case 5: ok = r.TryU64(out _); width = 8; break;
                    case 6: ok = r.TryI64(out _); width = 8; break;
                    case 7: ok = r.TryVarUInt(out _); width = -1; break;
                    default: ok = r.TryBytes(data.Length > 0 ? data[0] % 5 : 0, out _); width = data.Length > 0 ? data[0] % 5 : 0; break;
                }
                int used = before - r.Remaining;
                if (!ok && used != 0)
                {
                    return "failed read moved the position (op " + (op % 9) + ")";
                }
                if (ok && width >= 0 && used != width)
                {
                    return "read consumed " + used + " bytes, expected " + width;
                }
                if (ok && width < 0 && (used < 1 || used > 10))
                {
                    return "varint consumed " + used + " bytes";
                }
                stuck = ok ? 0 : stuck + 1;
                op++;
            }
            if (r.Remaining < 0 || r.Position + r.Remaining != data.Length)
            {
                return "position bookkeeping broken";
            }
            return null;
        }

        [Fact]
        public void StateWriterReader_RoundTripsEveryPrimitiveAtEdges()
        {
            var target = new ByteWriter();
            StateWriter w = StateWriter.ForBytes(target);
            for (int i = 0; i < 256; i++)
            {
                w.U8((byte)i);
            }
            w.Bool(false);
            w.Bool(true);
            foreach (int v in I32Edges) { w.I32(v); }
            foreach (uint v in U32Edges) { w.U32(v); }
            foreach (long v in I64Edges) { w.I64(v); }
            foreach (ulong v in U64Edges) { w.U64(v); }
            foreach (long v in I64Edges) { w.Fix(Fix64.FromRaw(v)); }
            w.Vec(new FixVec2(Fix64.FromRaw(long.MinValue), Fix64.FromRaw(long.MaxValue)));
            w.Vec(new FixVec2(Fix64.One, Fix64.FromRaw(-1)));
            int[] angles = { 0, 1, 9000, 18000, 35999 };
            foreach (int c in angles) { w.Ang(Angle.FromCenti(c)); }
            w.Count(0);
            w.Count(int.MaxValue);

            int expectedLength = 256 + 2 + 4 * (I32Edges.Length + U32Edges.Length) + 8 * (I64Edges.Length + U64Edges.Length)
                                 + 8 * I64Edges.Length + 16 * 2 + 4 * angles.Length + 4 * 2;
            Assert.Equal(expectedLength, target.Length);

            var r = new StateReader(target.Written);
            for (int i = 0; i < 256; i++)
            {
                Assert.Equal((byte)i, r.U8());
            }
            Assert.False(r.Bool());
            Assert.True(r.Bool());
            foreach (int v in I32Edges) { Assert.Equal(v, r.I32()); }
            foreach (uint v in U32Edges) { Assert.Equal(v, r.U32()); }
            foreach (long v in I64Edges) { Assert.Equal(v, r.I64()); }
            foreach (ulong v in U64Edges) { Assert.Equal(v, r.U64()); }
            foreach (long v in I64Edges) { Assert.Equal(Fix64.FromRaw(v), r.Fix()); }
            Assert.Equal(new FixVec2(Fix64.FromRaw(long.MinValue), Fix64.FromRaw(long.MaxValue)), r.Vec());
            Assert.Equal(new FixVec2(Fix64.One, Fix64.FromRaw(-1)), r.Vec());
            foreach (int c in angles) { Assert.Equal(c, r.Ang().Centi); }
            Assert.Equal(0, r.Count());
            Assert.Equal(int.MaxValue, r.Count());
            Assert.Equal(0, r.Remaining);
        }

        // 정규 직렬화는 패딩 없이 정해진 폭만 쓴다: bool 1, i32/u32/ang/count 4, i64/u64/fix 8, vec 16
        [Fact]
        public void StateWriter_FieldWidthsAreFixed()
        {
            var target = new ByteWriter();
            StateWriter w = StateWriter.ForBytes(target);
            int at = 0;
            void Expect(int width)
            {
                Assert.Equal(at + width, target.Length);
                at += width;
            }
            w.U8(7); Expect(1);
            w.Bool(true); Expect(1);
            w.I32(-1); Expect(4);
            w.U32(1); Expect(4);
            w.I64(-1); Expect(8);
            w.U64(1); Expect(8);
            w.Fix(Fix64.One); Expect(8);
            w.Vec(new FixVec2(Fix64.One, Fix64.One)); Expect(16);
            w.Ang(Angle.FromCenti(100)); Expect(4);
            w.Count(5); Expect(4);
        }
    }
}
