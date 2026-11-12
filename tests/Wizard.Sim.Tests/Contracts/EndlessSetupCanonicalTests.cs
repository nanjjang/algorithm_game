using System;
using System.Linq;
using Wizard.Content;
using Wizard.Core;
using Xunit;

namespace Wizard.Sim.Tests.Contracts
{
    // CONTRACTS 3.2·11.1, DECISIONS-TECH T-46: EndlessSetup 의 정규 바이트 = .wzrc 머리말.
    // 기대 바이트는 11.1절의 칸 배치를 손으로 옮긴 것이다(구현의 출력에서 베끼지 않았다).
    public sealed class EndlessSetupCanonicalTests
    {
        private const int HeaderLength = 68;

        // 11.1절의 칸 순서 그대로. 수는 리틀 엔디언(1.4절), uuid 는 문자열 hex 순서(0.3절)
        private static readonly byte[] SampleBytes =
        {
            0x57, 0x5A, 0x52, 0x43,                                                     // magic "WZRC"
            0x01, 0x00,                                                                 // format_version u16 = 1
            0x00, 0x11, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77,                             // core_version [16]
            0x88, 0x99, 0xAA, 0xBB, 0xCC, 0xDD, 0xEE, 0xFF,
            0x04, 0x03, 0x02, 0x01,                                                     // data_version u32 = 0x01020304
            0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08,                             // run_id [16]
            0x09, 0x0A, 0x0B, 0x0C, 0x0D, 0x0E, 0x0F, 0x10,
            0x88, 0x77, 0x66, 0x55, 0x44, 0x33, 0x22, 0x11,                             // seed u64 = 0x1122334455667788
            0x01,                                                                       // kind u8 = Weekly
            0x05, 0x01,                                                                 // start_wave u16 = 261
            0x01,                                                                       // phantom_first u8
            0x00,                                                                       // season_tag_on u8
            0x03, 0x07, 0x02,                                                           // character, stage, slots_extra
            0x02, 0x05, 0x09,                                                           // mutation_count, mutations
            0x04,                                                                       // core
            0x03, 0x06, 0x00, 0x0C,                                                     // link_count, links
            0x05, 0x80,                                                                 // unlocked_arts u16 = 0x8005
        };

        private static EndlessSetup Sample() => new EndlessSetup
        {
            CoreVersion = "00112233445566778899aabbccddeeff",
            DataVersion = 0x01020304,
            RunId = Guid.Parse("01020304-0506-0708-090a-0b0c0d0e0f10"),
            Seed = 0x1122334455667788UL,
            Kind = RunKind.Weekly,
            StartWave = 261,
            PhantomFirst = true,
            SeasonTagOn = false,
            Wand = new WandSnapshot
            {
                Character = CharacterId.Char3,
                Stage = 7,
                SlotsExtra = 2,
                Mutations = new[] { (MutationId)5, (MutationId)9 },
                Core = (BlockId)4,
                Links = new[] { (BlockId)6, (BlockId)0, (BlockId)12 },
            },
            UnlockedArts = (ArtMask)0x8005,
        };

        private static byte[] Write(EndlessSetup s)
        {
            var w = new ByteWriter();
            s.WriteCanonical(w);
            return w.ToArray();
        }

        private static void AssertSameSetup(EndlessSetup expected, EndlessSetup actual)
        {
            Assert.Equal(expected.CoreVersion, actual.CoreVersion);
            Assert.Equal(expected.DataVersion, actual.DataVersion);
            Assert.Equal(expected.RunId, actual.RunId);
            Assert.Equal(expected.Seed, actual.Seed);
            Assert.Equal(expected.Kind, actual.Kind);
            Assert.Equal(expected.StartWave, actual.StartWave);
            Assert.Equal(expected.PhantomFirst, actual.PhantomFirst);
            Assert.Equal(expected.SeasonTagOn, actual.SeasonTagOn);
            Assert.Equal(expected.Wand.Character, actual.Wand.Character);
            Assert.Equal(expected.Wand.Stage, actual.Wand.Stage);
            Assert.Equal(expected.Wand.SlotsExtra, actual.Wand.SlotsExtra);
            Assert.Equal(expected.Wand.Mutations, actual.Wand.Mutations);
            Assert.Equal(expected.Wand.Core, actual.Wand.Core);
            Assert.Equal(expected.Wand.Links, actual.Wand.Links);
            Assert.Equal(expected.UnlockedArts, actual.UnlockedArts);
        }

        [Fact]
        public void SampleBytes_AreTheLayoutOfContracts11_1()
        {
            // 고정 칸 55바이트 + 지팡이의 가변 칸(변이 2, 연결 3) + unlocked_arts
            Assert.Equal(HeaderLength, SampleBytes.Length);
        }

        [Fact]
        public void WriteCanonical_EmitsTheWzrcHeaderBytes()
        {
            Assert.Equal(SampleBytes, Write(Sample()));
        }

        [Fact]
        public void TryReadCanonical_ReadsEveryField()
        {
            Assert.True(EndlessSetup.TryReadCanonical(SampleBytes, out EndlessSetup? read));
            AssertSameSetup(Sample(), read!);
        }

        [Fact]
        public void WriteThenRead_RoundTripsAndRewritesTheSameBytes()
        {
            byte[] first = Write(Sample());
            Assert.True(EndlessSetup.TryReadCanonical(first, out EndlessSetup? read));
            Assert.Equal(first, Write(read!));
        }

        // phantom_first 가 season_tag_on 보다 먼저다(11.1절). 클래스의 칸 순서와 다르다
        [Fact]
        public void PhantomFirstComesBeforeSeasonTagOn()
        {
            EndlessSetup s = Sample();
            s.PhantomFirst = false;
            s.SeasonTagOn = true;
            byte[] bytes = Write(s);
            Assert.Equal(0, bytes[53]);
            Assert.Equal(1, bytes[54]);
            Assert.True(EndlessSetup.TryReadCanonical(bytes, out EndlessSetup? read));
            Assert.False(read!.PhantomFirst);
            Assert.True(read.SeasonTagOn);
        }

        [Fact]
        public void EmptyMutationsAndLinks_RoundTrip()
        {
            EndlessSetup s = Sample();
            s.Wand.Mutations = Array.Empty<MutationId>();
            s.Wand.Links = Array.Empty<BlockId>();
            byte[] bytes = Write(s);
            Assert.Equal(HeaderLength - 5, bytes.Length);
            Assert.True(EndlessSetup.TryReadCanonical(bytes, out EndlessSetup? read));
            AssertSameSetup(s, read!);
        }

        [Fact]
        public void LongestMutationsAndLinks_RoundTrip()
        {
            EndlessSetup s = Sample();
            s.Wand.Mutations = Enumerable.Range(0, 255).Select(i => (MutationId)i).ToArray();
            s.Wand.Links = Enumerable.Range(0, 255).Select(i => (BlockId)(255 - i)).ToArray();
            s.Wand.Stage = 255;
            s.Wand.SlotsExtra = 255;
            s.StartWave = ushort.MaxValue;
            s.DataVersion = int.MaxValue;
            byte[] bytes = Write(s);
            Assert.True(EndlessSetup.TryReadCanonical(bytes, out EndlessSetup? read));
            AssertSameSetup(s, read!);
        }

        // 이 코어의 버전 글자 열이 그대로 실린다("SimVersion.CoreVersion 과 같은 꼴")
        [Fact]
        public void SimVersionCoreVersion_IsWritable()
        {
            EndlessSetup s = Sample();
            s.CoreVersion = SimVersion.CoreVersion;
            Assert.True(EndlessSetup.TryReadCanonical(Write(s), out EndlessSetup? read));
            Assert.Equal(SimVersion.CoreVersion, read!.CoreVersion);
        }

        [Fact]
        public void EveryRunKindRoundTrips_AndTheNextValueIsRejected()
        {
            byte max = 0;
            foreach (RunKind kind in Enum.GetValues<RunKind>())
            {
                EndlessSetup s = Sample();
                s.Kind = kind;
                Assert.True(EndlessSetup.TryReadCanonical(Write(s), out EndlessSetup? read));
                Assert.Equal(kind, read!.Kind);
                max = Math.Max(max, (byte)kind);
            }

            byte[] bytes = (byte[])SampleBytes.Clone();
            bytes[50] = (byte)(max + 1);
            Assert.False(EndlessSetup.TryReadCanonical(bytes, out _));

            EndlessSetup bad = Sample();
            bad.Kind = (RunKind)(max + 1);
            Assert.Throws<InvalidOperationException>(() => Write(bad));
        }

        [Fact]
        public void WriteCanonical_AppendsToTheWriter()
        {
            var w = new ByteWriter();
            w.U8(0xEE);
            Sample().WriteCanonical(w);
            w.U8(0x01);                                   // 기록 본문의 첫 꼬리표가 바로 뒤에 온다
            byte[] all = w.ToArray();
            Assert.Equal(1 + HeaderLength + 1, all.Length);
            Assert.Equal(0xEE, all[0]);
            Assert.Equal(SampleBytes, all.Skip(1).Take(HeaderLength).ToArray());
        }

        [Fact]
        public void TryReadCanonical_RejectsEveryTruncation()
        {
            for (int n = 0; n < SampleBytes.Length; n++)
            {
                ReadOnlySpan<byte> cut = SampleBytes.AsSpan(0, n);
                Assert.False(EndlessSetup.TryReadCanonical(cut, out EndlessSetup? a), "길이 " + n);
                Assert.Null(a);
                Assert.False(EndlessSetup.TryReadCanonicalPrefix(cut, out EndlessSetup? b, out int length), "길이 " + n);
                Assert.Null(b);
                Assert.Equal(0, length);
            }
        }

        [Fact]
        public void TrailingBytes_FailTheWholeReadButNotThePrefixRead()
        {
            byte[] file = SampleBytes.Concat(new byte[] { 0x01, 0x00, 0x00, 0x00 }).ToArray();
            Assert.False(EndlessSetup.TryReadCanonical(file, out EndlessSetup? whole));
            Assert.Null(whole);

            Assert.True(EndlessSetup.TryReadCanonicalPrefix(file, out EndlessSetup? prefix, out int length));
            Assert.Equal(HeaderLength, length);
            AssertSameSetup(Sample(), prefix!);

            Assert.True(EndlessSetup.TryReadCanonicalPrefix(SampleBytes, out _, out int exact));
            Assert.Equal(HeaderLength, exact);
        }

        [Theory]
        [InlineData(0, 0x58)]      // magic
        [InlineData(3, 0x44)]      // magic: "WZRD" 가 아니라 "WZRC"
        [InlineData(4, 0x00)]      // format_version 0
        [InlineData(4, 0x02)]      // format_version 2
        [InlineData(5, 0x01)]      // format_version 257
        [InlineData(25, 0x80)]     // data_version 이 int 범위 밖
        [InlineData(50, 0x02)]     // kind
        [InlineData(53, 0x02)]     // phantom_first 가 0·1 이 아니다
        [InlineData(54, 0xFF)]     // season_tag_on 이 0·1 이 아니다
        [InlineData(58, 0x03)]     // mutation_count 가 늘면 뒤 칸이 모자란다
        [InlineData(62, 0x04)]     // link_count 가 늘면 뒤 칸이 모자란다
        [InlineData(62, 0x02)]     // link_count 가 줄면 바이트가 남는다
        public void TryReadCanonical_RejectsNonCanonicalBytes(int index, int value)
        {
            byte[] bytes = (byte[])SampleBytes.Clone();
            Assert.NotEqual(value, (int)bytes[index]);
            bytes[index] = (byte)value;
            Assert.False(EndlessSetup.TryReadCanonical(bytes, out EndlessSetup? read));
            Assert.Null(read);
        }

        // 한 설정에 바이트 열 하나: 받아들인 바이트는 다시 쓰면 그대로 나온다.
        // 서버의 1차 검사가 머리말을 run.setup_canonical 과 바이트로 대조하므로(8.3절) 같은 설정의 다른 표기가 있으면 안 된다
        [Fact]
        public void AnyAcceptedSingleByteChange_RewritesToExactlyThoseBytes()
        {
            int accepted = 0;
            int rejected = 0;
            for (int i = 0; i < SampleBytes.Length; i++)
            {
                foreach (byte value in new[] { (byte)(SampleBytes[i] ^ 0x01), (byte)(SampleBytes[i] ^ 0x80), (byte)0x00, (byte)0xFF })
                {
                    if (value == SampleBytes[i])
                    {
                        continue;
                    }
                    byte[] bytes = (byte[])SampleBytes.Clone();
                    bytes[i] = value;
                    if (EndlessSetup.TryReadCanonical(bytes, out EndlessSetup? read))
                    {
                        Assert.Equal(bytes, Write(read!));
                        accepted++;
                    }
                    else
                    {
                        Assert.Null(read);
                        rejected++;
                    }
                }
            }
            Assert.True(accepted > 0 && rejected > 0);
        }

        // 서버가 받는 기록 파일의 머리말은 공격자의 입력이다(3.8절). 어떤 바이트에도 예외가 없다
        [Fact]
        public void TryRead_NeverThrowsOnArbitraryBytes()
        {
            var rng = new Random(20261112);
            for (int round = 0; round < 20000; round++)
            {
                byte[] bytes;
                if ((round & 1) == 0)
                {
                    bytes = new byte[rng.Next(0, 96)];
                    rng.NextBytes(bytes);
                }
                else
                {
                    // 맞는 머리말에서 몇 바이트만 바꾸고 길이를 흔든다(깊은 칸까지 닿게)
                    bytes = new byte[SampleBytes.Length + rng.Next(-4, 5)];
                    Array.Copy(SampleBytes, bytes, Math.Min(SampleBytes.Length, bytes.Length));
                    for (int k = rng.Next(0, 4); k > 0; k--)
                    {
                        bytes[rng.Next(bytes.Length)] = (byte)rng.Next(256);
                    }
                }
                if (EndlessSetup.TryReadCanonical(bytes, out EndlessSetup? whole))
                {
                    Assert.Equal(bytes, Write(whole!));
                }
                if (EndlessSetup.TryReadCanonicalPrefix(bytes, out EndlessSetup? prefix, out int length))
                {
                    Assert.Equal(bytes.Take(length).ToArray(), Write(prefix!));
                }
            }
        }

        [Fact]
        public void RandomSetups_RoundTrip()
        {
            var rng = new Random(46);
            const string hex = "0123456789abcdef";
            for (int round = 0; round < 2000; round++)
            {
                byte[] guid = new byte[16];
                rng.NextBytes(guid);
                var s = new EndlessSetup
                {
                    CoreVersion = new string(Enumerable.Range(0, 32).Select(_ => hex[rng.Next(16)]).ToArray()),
                    DataVersion = rng.Next(),
                    RunId = new Guid(guid),
                    Seed = ((ulong)(uint)rng.Next(int.MinValue, int.MaxValue) << 32) | (uint)rng.Next(int.MinValue, int.MaxValue),
                    Kind = (RunKind)rng.Next(2),
                    StartWave = rng.Next(0, 65536),
                    PhantomFirst = rng.Next(2) == 1,
                    SeasonTagOn = rng.Next(2) == 1,
                    Wand = new WandSnapshot
                    {
                        Character = (CharacterId)rng.Next(256),
                        Stage = rng.Next(256),
                        SlotsExtra = rng.Next(256),
                        Mutations = Enumerable.Range(0, rng.Next(0, 9)).Select(_ => (MutationId)rng.Next(256)).ToArray(),
                        Core = (BlockId)rng.Next(256),
                        Links = Enumerable.Range(0, rng.Next(0, 9)).Select(_ => (BlockId)rng.Next(256)).ToArray(),
                    },
                    UnlockedArts = (ArtMask)rng.Next(65536),
                };
                byte[] bytes = Write(s);
                Assert.True(EndlessSetup.TryReadCanonical(bytes, out EndlessSetup? read));
                AssertSameSetup(s, read!);
                Assert.Equal(bytes, Write(read!));
            }
        }

        private static readonly (string What, Action<EndlessSetup> Break)[] Unwritable =
        {
            ("CoreVersion 이 비었다(기본값)", s => s.CoreVersion = string.Empty),
            ("CoreVersion 이 null", s => s.CoreVersion = null!),
            ("CoreVersion 이 31자", s => s.CoreVersion = "00112233445566778899aabbccddeef"),
            ("CoreVersion 이 33자", s => s.CoreVersion = "00112233445566778899aabbccddeeff0"),
            ("CoreVersion 이 대문자", s => s.CoreVersion = "00112233445566778899AABBCCDDEEFF"),
            ("CoreVersion 에 hex 가 아닌 글자", s => s.CoreVersion = "0011223344556677-899aabbccddeeff"),
            ("DataVersion 이 음수", s => s.DataVersion = -1),
            ("StartWave 가 음수", s => s.StartWave = -1),
            ("StartWave 가 u16 밖", s => s.StartWave = 65536),
            ("Wand 가 null", s => s.Wand = null!),
            ("Stage 가 음수", s => s.Wand.Stage = -1),
            ("Stage 가 u8 밖", s => s.Wand.Stage = 256),
            ("SlotsExtra 가 음수", s => s.Wand.SlotsExtra = -1),
            ("SlotsExtra 가 u8 밖", s => s.Wand.SlotsExtra = 256),
            ("Mutations 가 null", s => s.Wand.Mutations = null!),
            ("Mutations 가 256개", s => s.Wand.Mutations = new MutationId[256]),
            ("Links 가 null", s => s.Wand.Links = null!),
            ("Links 가 256개", s => s.Wand.Links = new BlockId[256]),
        };

        // 형식에 담기지 않는 설정은 잘린 바이트를 남기지 않고 던진다
        [Fact]
        public void WriteCanonical_ThrowsAndWritesNothing()
        {
            Assert.Equal(18, Unwritable.Length);
            foreach ((string what, Action<EndlessSetup> breakIt) in Unwritable)
            {
                EndlessSetup s = Sample();
                breakIt(s);
                var w = new ByteWriter();
                w.U8(0xEE);
                bool threw = false;
                try
                {
                    s.WriteCanonical(w);
                }
                catch (InvalidOperationException)
                {
                    threw = true;
                }
                Assert.True(threw, what);
                Assert.True(w.Length == 1, what);
            }
        }

        [Fact]
        public void WriteCanonical_RejectsNullWriter()
        {
            Assert.Throws<ArgumentNullException>(() => Sample().WriteCanonical(null!));
        }

        // 새로 만든 설정은 CoreVersion 이 비어 있어 쓰지 못한다(버전을 잊고 쓰는 일을 막는다)
        [Fact]
        public void NewSetup_HasNoCanonicalBytesUntilCoreVersionIsSet()
        {
            var s = new EndlessSetup();
            Assert.Throws<InvalidOperationException>(() => Write(s));
            s.CoreVersion = SimVersion.CoreVersion;
            Assert.True(EndlessSetup.TryReadCanonical(Write(s), out EndlessSetup? read));
            Assert.Equal(1, read!.StartWave);
        }

        [Fact]
        public void MethodShapesMatchContract()
        {
            ContractShape.HasPublicInstanceMethod(typeof(EndlessSetup), "WriteCanonical", typeof(void), typeof(ByteWriter));
            ContractShape.HasPublicStaticMethod(typeof(EndlessSetup), "TryReadCanonical", typeof(bool),
                typeof(ReadOnlySpan<byte>), typeof(EndlessSetup).MakeByRefType());
            ContractShape.HasPublicStaticMethod(typeof(EndlessSetup), "TryReadCanonicalPrefix", typeof(bool),
                typeof(ReadOnlySpan<byte>), typeof(EndlessSetup).MakeByRefType(), typeof(int).MakeByRefType());
            Assert.Equal((ushort)1, EndlessSetup.CanonicalFormatVersion);
        }
    }
}
