using System;
using System.Text;
using Wizard.Core;
using Wizard.Core.Hash;
using Xunit;

namespace Wizard.Core.Tests.Codec
{
    // spec 01 3.4, 14.1(T-C11~T-C13), CONTRACTS 1.4. 기대값은 명세 표와 FNV 공개 기준 벡터(Fowler/Noll/Vo 의 test_fnv 표)에서 왔다.
    public sealed class FnvHashTests
    {
        // 세 길(StateHasher.Compute, StateWriter.ForHash, StateWriter.ForBytes)이 같은 바이트에서 같은 해시를 낸다
        private static void AssertAllPaths(byte[] input, ulong expected)
        {
            Assert.Equal(expected, StateHasher.Compute(input));

            StateWriter hashOnly = StateWriter.ForHash();
            foreach (byte b in input)
            {
                hashOnly.U8(b);
            }
            Assert.Equal(expected, hashOnly.Hash);

            var target = new ByteWriter();
            StateWriter bytes = StateWriter.ForBytes(target);
            foreach (byte b in input)
            {
                bytes.U8(b);
            }
            Assert.Equal(expected, bytes.Hash);
            Assert.Equal(input, target.Written.ToArray());

            ulong incremental = StateHasher.Offset;
            foreach (byte b in input)
            {
                incremental = StateHasher.Append(incremental, b);
            }
            Assert.Equal(expected, incremental);
        }

        [Fact]
        public void S01_T_C11__EmptyInputIsOffsetBasis()
        {
            AssertAllPaths(Array.Empty<byte>(), 0xcbf29ce484222325UL);
        }

        [Fact]
        public void S01_T_C12__SingleLetterA()
        {
            AssertAllPaths(Encoding.ASCII.GetBytes("a"), 0xaf63dc4c8601ec8cUL);
        }

        [Fact]
        public void S01_T_C13__Foobar()
        {
            AssertAllPaths(Encoding.ASCII.GetBytes("foobar"), 0x85944171f73967e8UL);
        }

        // FNV 공개 기준 벡터(FNV-1a 64) 가운데 명세 표에 없는 것
        [Theory]
        [InlineData("b", 0xaf63df4c8601f1a5UL)]
        [InlineData("c", 0xaf63de4c8601eff2UL)]
        [InlineData("fo", 0x08985907b541d342UL)]
        [InlineData("foo", 0xdcb27518fed9d577UL)]
        [InlineData("foob", 0xdd120e790c2512afUL)]
        [InlineData("fooba", 0xcac165afa2fef40aUL)]
        public void PublicReferenceVectors(string text, ulong expected)
        {
            AssertAllPaths(Encoding.ASCII.GetBytes(text), expected);
        }

        [Fact]
        public void PublicReferenceVector_SingleZeroByte()
        {
            AssertAllPaths(new byte[] { 0x00 }, 0xaf63bd4c8601b7dfUL);
        }

        [Fact]
        public void Constants_MatchSpec()
        {
            Assert.Equal(0xcbf29ce484222325UL, StateHasher.Offset);
            Assert.Equal(0x100000001b3UL, StateHasher.Prime);
        }

        // 이어 세기: 둘로 나눠 센 것과 한 번에 센 것이 같다
        [Fact]
        public void Append_SplitEqualsWhole()
        {
            byte[] data = Encoding.ASCII.GetBytes("foobar");
            for (int cut = 0; cut <= data.Length; cut++)
            {
                ulong h = StateHasher.Append(StateHasher.Offset, new ReadOnlySpan<byte>(data, 0, cut));
                h = StateHasher.Append(h, new ReadOnlySpan<byte>(data, cut, data.Length - cut));
                Assert.Equal(0x85944171f73967e8UL, h);
            }
        }

        // 정규 직렬화의 해시 = 그 바이트의 FNV-1a 64 (상태를 바이트로 받은 쪽이 같은 해시를 다시 셀 수 있다)
        [Fact]
        public void StateWriterBytesHash_EqualsHashOfWrittenBytes()
        {
            var target = new ByteWriter();
            StateWriter w = StateWriter.ForBytes(target);
            w.Bool(true);
            w.U32(0xDEADBEEFU);
            w.I64(long.MinValue);
            w.Fix(Fix64.FromRaw(-12884901888L));
            w.Vec(new FixVec2(Fix64.One, Fix64.FromRaw(-1)));
            w.Ang(Angle.FromCenti(-4500));
            w.Count(3);
            Assert.Equal(StateHasher.Compute(target.Written), w.Hash);
        }
    }
}
