using System;
using Wizard.Core;
using Wizard.Core.Hash;
using Xunit;

namespace Wizard.Core.Tests.Codec
{
    // CLAUDE.md 3절(Step 안 할당 금지), M1-CORE-04 수락 조건: 쓰기 경로에 할당이 없다(할당 바이트 0 단언).
    // ByteWriter 는 재사용 버퍼다. 용량이 찬 뒤(한 번 써서 늘린 뒤 Reset) 같은 양을 다시 쓰면 관리 힙 할당이 0 이어야 한다.
    // 재는 구간 안에는 람다, 박싱, 문자열을 두지 않는다. 첫 호출의 JIT 를 빼려고 같은 일을 한 번 먼저 돌린다.
    public sealed class CodecAllocationTests
    {
        private const int Rounds = 64;

        private static void WriteEverything(ByteWriter w, ReadOnlySpan<byte> blob)
        {
            for (int i = 0; i < Rounds; i++)
            {
                w.U8((byte)i);
                w.U16((ushort)(i * 257));
                w.I16((short)-i);
                w.U32(0xA1B2C3D4U ^ (uint)i);
                w.I32(-i);
                w.U64(0x0102030405060708UL + (ulong)i);
                w.I64(long.MinValue + i);
                w.VarUInt(ulong.MaxValue >> (i % 64));
                w.Bytes(blob);
            }
        }

        private static void WriteState(ref StateWriter w)
        {
            for (int i = 0; i < Rounds; i++)
            {
                w.U8((byte)i);
                w.Bool((i & 1) == 0);
                w.I32(-i);
                w.U32((uint)i);
                w.I64(long.MaxValue - i);
                w.U64((ulong)i << 40);
                w.Fix(Fix64.FromRaw(i));
                w.Vec(new FixVec2(Fix64.FromRaw(i), Fix64.FromRaw(-i)));
                w.Ang(Angle.FromCenti(i * 100));
                w.Count(i);
            }
        }

        private static ulong ReadEverything(ReadOnlySpan<byte> data, int blobLength)
        {
            var r = new ByteReader(data);
            ulong acc = 0;
            for (int i = 0; i < Rounds; i++)
            {
                r.TryU8(out byte a);
                r.TryU16(out ushort b);
                r.TryI16(out short c);
                r.TryU32(out uint d);
                r.TryI32(out int e);
                r.TryU64(out ulong f);
                r.TryI64(out long g);
                r.TryVarUInt(out ulong h);
                r.TryBytes(blobLength, out ReadOnlySpan<byte> s);
                acc += (ulong)a + b + (ulong)(ushort)c + d + (ulong)(uint)e + f + (ulong)g + h + (ulong)s.Length;
            }
            r.TryU64(out _);   // 끝을 넘는 읽기도 할당 없이 false
            return acc;
        }

        // 재는 방법이 할당을 실제로 잡는지 먼저 본다(차가운 버퍼는 늘어나며 할당한다)
        [Fact]
        public void Probe_DetectsGrowthAllocation()
        {
            byte[] blob = { 1, 2, 3, 4, 5, 6, 7 };
            var w = new ByteWriter(16);
            long before = GC.GetAllocatedBytesForCurrentThread();
            WriteEverything(w, blob);
            long after = GC.GetAllocatedBytesForCurrentThread();
            Assert.True(after - before > 0);
        }

        [Fact]
        public void ByteWriter_WarmWritePathAllocatesNothing()
        {
            byte[] blob = { 1, 2, 3, 4, 5, 6, 7 };
            var w = new ByteWriter();
            WriteEverything(w, blob);   // 용량을 키우고 JIT 를 끝낸다
            int length = w.Length;
            w.Reset();
            WriteEverything(w, blob);
            w.Reset();

            long before = GC.GetAllocatedBytesForCurrentThread();
            WriteEverything(w, blob);
            long after = GC.GetAllocatedBytesForCurrentThread();

            Assert.Equal(0L, after - before);
            Assert.Equal(length, w.Length);
        }

        [Fact]
        public void StateWriter_HashModeAllocatesNothing()
        {
            StateWriter warm = StateWriter.ForHash();
            WriteState(ref warm);

            long before = GC.GetAllocatedBytesForCurrentThread();
            StateWriter w = StateWriter.ForHash();
            WriteState(ref w);
            ulong hash = w.Hash;
            long after = GC.GetAllocatedBytesForCurrentThread();

            Assert.Equal(0L, after - before);
            Assert.Equal(warm.Hash, hash);
        }

        [Fact]
        public void StateWriter_BytesModeIntoWarmBufferAllocatesNothing()
        {
            var target = new ByteWriter();
            StateWriter warm = StateWriter.ForBytes(target);
            WriteState(ref warm);
            target.Reset();

            long before = GC.GetAllocatedBytesForCurrentThread();
            StateWriter w = StateWriter.ForBytes(target);
            WriteState(ref w);
            ulong hash = w.Hash;
            ulong rehash = StateHasher.Compute(target.Written);
            long after = GC.GetAllocatedBytesForCurrentThread();

            Assert.Equal(0L, after - before);
            Assert.Equal(warm.Hash, hash);
            Assert.Equal(hash, rehash);
        }

        [Fact]
        public void ByteReader_ReadPathAllocatesNothing()
        {
            byte[] blob = { 9, 8, 7 };
            var w = new ByteWriter();
            WriteEverything(w, blob);
            byte[] data = w.ToArray();
            ulong expected = ReadEverything(data, blob.Length);

            long before = GC.GetAllocatedBytesForCurrentThread();
            ulong got = ReadEverything(data, blob.Length);
            long after = GC.GetAllocatedBytesForCurrentThread();

            Assert.Equal(0L, after - before);
            Assert.Equal(expected, got);
        }
    }
}
