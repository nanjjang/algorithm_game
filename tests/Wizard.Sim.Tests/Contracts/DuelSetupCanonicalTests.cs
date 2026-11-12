using System;
using System.Linq;
using Wizard.Content;
using Wizard.Core;
using Wizard.Sim.Duel;
using Xunit;

namespace Wizard.Sim.Tests.Contracts
{
    // CONTRACTS 3.2·11.1, DECISIONS-TECH T-48: DuelSetup 의 정규 바이트 = WZDR 머리말.
    // 기대 바이트는 11.1절의 칸 배치를 손으로 옮긴 것이다(구현의 출력에서 베끼지 않았다).
    public sealed class DuelSetupCanonicalTests
    {
        private const int HeaderLength = 99;
        private const int OptionsAt = 50;       // magic 4, format_version 2, core_version 16, data_version 4, match_id 16, seed 8 다음
        private const int Player0At = 66;       // options 16바이트 다음
        private const int Player1At = 85;       // 첫 플레이어 19바이트(변이 2, 연결 3) 다음

        // 11.1절의 칸 순서 그대로. 수는 리틀 엔디언(1.4절), uuid 는 문자열 hex 순서(0.3절)
        private static readonly byte[] SampleBytes =
        {
            0x57, 0x5A, 0x44, 0x52,                                                     // magic "WZDR"
            0x01, 0x00,                                                                 // format_version u16 = 1
            0x00, 0x11, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77,                             // core_version [16]
            0x88, 0x99, 0xAA, 0xBB, 0xCC, 0xDD, 0xEE, 0xFF,
            0x04, 0x03, 0x02, 0x01,                                                     // data_version u32 = 0x01020304
            0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08,                             // match_id [16]
            0x09, 0x0A, 0x0B, 0x0C, 0x0D, 0x0E, 0x0F, 0x10,
            0x88, 0x77, 0x66, 0x55, 0x44, 0x33, 0x22, 0x11,                             // seed u64 = 0x1122334455667788
            0x05,                                                                       // rounds u8 = 5
            0x78, 0x00,                                                                 // round_seconds u16 = 120
            0xDC, 0x05,                                                                 // hp_mult_permille u16 = 1500
            0x0D,                                                                       // enhance_cap u8 = 13
            0xED, 0x00,                                                                 // il_cap i16 = 237
            0x01,                                                                       // allow_mutations u8
            0xFF, 0xFF,                                                                 // hero_block_cap i16 = −1(없음)
            0x00,                                                                       // equalize u8
            0x01,                                                                       // wild_intrusion u8
            0x00,                                                                       // thorns_off u8
            0x03,                                                                       // floor_color u8 = Night
            0x01,                                                                       // spectate u8
            0x00,                                                                       // 플레이어 0: slot
            0x02, 0x09, 0x01,                                                           // character, stage, slots_extra
            0x02, 0x05, 0x09,                                                           // mutation_count, mutations
            0x04,                                                                       // core
            0x03, 0x06, 0x00, 0x0C,                                                     // link_count, links
            0x0B,                                                                       // actual_stage u8 = 11
            0x2C, 0x01, 0x00, 0x00,                                                     // max_hp_points u32 = 300
            0x05, 0x80,                                                                 // unlocked_arts u16 = 0x8005
            0x01,                                                                       // 플레이어 1: slot
            0x05, 0x00, 0x00,                                                           // character, stage, slots_extra
            0x00,                                                                       // mutation_count
            0x07,                                                                       // core
            0x00,                                                                       // link_count
            0x00,                                                                       // actual_stage
            0x45, 0x23, 0x01, 0x00,                                                     // max_hp_points u32 = 0x00012345
            0x00, 0x00,                                                                 // unlocked_arts
        };

        private static DuelSetup Sample() => new DuelSetup
        {
            CoreVersion = "00112233445566778899aabbccddeeff",
            DataVersion = 0x01020304,
            MatchId = Guid.Parse("01020304-0506-0708-090a-0b0c0d0e0f10"),
            Seed = 0x1122334455667788UL,
            Options = new DuelRoomOptions
            {
                Rounds = 5,
                RoundSeconds = 120,
                HpMultPermille = 1500,
                EnhanceCap = 13,
                IlCap = 237,
                AllowMutations = true,
                HeroBlockCap = DuelRoomOptions.NoCap,
                Equalize = false,
                WildIntrusion = true,
                ThornsOff = false,
                FloorColor = TimeOfDay.Night,
                Spectate = true,
            },
            Players = new[]
            {
                new DuelPlayerSetup
                {
                    Slot = new PlayerSlot(0),
                    AppliedWand = new WandSnapshot
                    {
                        Character = CharacterId.Char2,
                        Stage = 9,
                        SlotsExtra = 1,
                        Mutations = new[] { (MutationId)5, (MutationId)9 },
                        Core = (BlockId)4,
                        Links = new[] { (BlockId)6, (BlockId)0, (BlockId)12 },
                    },
                    ActualStage = 11,
                    MaxHpPoints = 300,
                    UnlockedArts = (ArtMask)0x8005,
                },
                new DuelPlayerSetup
                {
                    Slot = new PlayerSlot(1),
                    AppliedWand = new WandSnapshot { Character = CharacterId.Char5, Core = (BlockId)7 },
                    ActualStage = 0,
                    MaxHpPoints = 0x00012345,
                    UnlockedArts = 0,
                },
            },
        };

        private static byte[] Write(DuelSetup s)
        {
            var w = new ByteWriter();
            s.WriteCanonical(w);
            return w.ToArray();
        }

        private static void AssertSameSetup(DuelSetup expected, DuelSetup actual)
        {
            Assert.Equal(expected.CoreVersion, actual.CoreVersion);
            Assert.Equal(expected.DataVersion, actual.DataVersion);
            Assert.Equal(expected.MatchId, actual.MatchId);
            Assert.Equal(expected.Seed, actual.Seed);
            DuelRoomOptions e = expected.Options, a = actual.Options;
            Assert.Equal(e.Rounds, a.Rounds);
            Assert.Equal(e.RoundSeconds, a.RoundSeconds);
            Assert.Equal(e.HpMultPermille, a.HpMultPermille);
            Assert.Equal(e.EnhanceCap, a.EnhanceCap);
            Assert.Equal(e.IlCap, a.IlCap);
            Assert.Equal(e.AllowMutations, a.AllowMutations);
            Assert.Equal(e.HeroBlockCap, a.HeroBlockCap);
            Assert.Equal(e.Equalize, a.Equalize);
            Assert.Equal(e.WildIntrusion, a.WildIntrusion);
            Assert.Equal(e.ThornsOff, a.ThornsOff);
            Assert.Equal(e.FloorColor, a.FloorColor);
            Assert.Equal(e.Spectate, a.Spectate);
            Assert.Equal(expected.Players.Length, actual.Players.Length);
            for (int i = 0; i < expected.Players.Length; i++)
            {
                DuelPlayerSetup x = expected.Players[i], y = actual.Players[i];
                Assert.Equal(x.Slot, y.Slot);
                Assert.Equal(x.AppliedWand.Character, y.AppliedWand.Character);
                Assert.Equal(x.AppliedWand.Stage, y.AppliedWand.Stage);
                Assert.Equal(x.AppliedWand.SlotsExtra, y.AppliedWand.SlotsExtra);
                Assert.Equal(x.AppliedWand.Mutations, y.AppliedWand.Mutations);
                Assert.Equal(x.AppliedWand.Core, y.AppliedWand.Core);
                Assert.Equal(x.AppliedWand.Links, y.AppliedWand.Links);
                Assert.Equal(x.ActualStage, y.ActualStage);
                Assert.Equal(x.MaxHpPoints, y.MaxHpPoints);
                Assert.Equal(x.UnlockedArts, y.UnlockedArts);
            }
        }

        [Fact]
        public void SampleBytes_AreTheLayoutOfContracts11_1()
        {
            // 봉투와 경기 50 + 옵션 16 + 플레이어 19(변이 2, 연결 3) + 플레이어 14(변이 0, 연결 0)
            Assert.Equal(HeaderLength, SampleBytes.Length);
            Assert.Equal(0x05, SampleBytes[OptionsAt]);
            Assert.Equal(0x00, SampleBytes[Player0At]);
            Assert.Equal(0x01, SampleBytes[Player1At]);
        }

        [Fact]
        public void WriteCanonical_EmitsTheWzdrHeaderBytes()
        {
            Assert.Equal(SampleBytes, Write(Sample()));
        }

        [Fact]
        public void TryReadCanonical_ReadsEveryField()
        {
            Assert.True(DuelSetup.TryReadCanonical(SampleBytes, out DuelSetup? read));
            AssertSameSetup(Sample(), read!);
        }

        [Fact]
        public void WriteThenRead_RoundTripsAndRewritesTheSameBytes()
        {
            byte[] first = Write(Sample());
            Assert.True(DuelSetup.TryReadCanonical(first, out DuelSetup? read));
            Assert.Equal(first, Write(read!));
        }

        // 입력 지연 D는 정규 바이트에 없다(CONTRACTS 0.3, 10.5). 머리말 뒤의 칸은 정규 바이트 밖이다
        [Fact]
        public void TrailingBytes_FailTheWholeReadButNotThePrefixRead()
        {
            byte[] file = SampleBytes.Concat(new byte[] { 0x02, 0x03, 0xFF }).ToArray();   // input_delay 두 칸과 그 뒤
            Assert.False(DuelSetup.TryReadCanonical(file, out DuelSetup? whole));
            Assert.Null(whole);

            Assert.True(DuelSetup.TryReadCanonicalPrefix(file, out DuelSetup? prefix, out int length));
            Assert.Equal(HeaderLength, length);
            AssertSameSetup(Sample(), prefix!);
        }

        [Fact]
        public void TryReadCanonical_RejectsEveryTruncation()
        {
            for (int n = 0; n < SampleBytes.Length; n++)
            {
                ReadOnlySpan<byte> cut = SampleBytes.AsSpan(0, n);
                Assert.False(DuelSetup.TryReadCanonical(cut, out DuelSetup? a), "길이 " + n);
                Assert.Null(a);
                Assert.False(DuelSetup.TryReadCanonicalPrefix(cut, out DuelSetup? b, out int length), "길이 " + n);
                Assert.Null(b);
                Assert.Equal(0, length);
            }
        }

        [Theory]
        [InlineData(0, 0x58)]      // magic
        [InlineData(3, 0x43)]      // magic: "WZRC"(.wzrc)가 아니라 "WZDR"
        [InlineData(4, 0x02)]      // format_version 2
        [InlineData(5, 0x01)]      // format_version 257
        [InlineData(25, 0x80)]     // data_version 이 int 범위 밖
        [InlineData(57, 0x80)]     // il_cap 이 −1 보다 작다
        [InlineData(58, 0x02)]     // allow_mutations 가 0·1 이 아니다
        [InlineData(59, 0xFE)]     // hero_block_cap = −2
        [InlineData(61, 0x02)]     // equalize 가 0·1 이 아니다
        [InlineData(62, 0x02)]     // wild_intrusion
        [InlineData(63, 0xFF)]     // thorns_off
        [InlineData(65, 0x02)]     // spectate
        [InlineData(66, 0x01)]     // 첫 플레이어의 slot 은 0
        [InlineData(85, 0x00)]     // 둘째 플레이어의 slot 은 1
        [InlineData(70, 0x03)]     // mutation_count 가 늘면 뒤 칸이 밀린다
        [InlineData(74, 0x02)]     // link_count 가 줄면 바이트가 남는다
        [InlineData(82, 0x80)]     // max_hp_points 가 int 범위 밖
        public void TryReadCanonical_RejectsNonCanonicalBytes(int index, int value)
        {
            byte[] bytes = (byte[])SampleBytes.Clone();
            Assert.NotEqual(value, (int)bytes[index]);
            bytes[index] = (byte)value;
            Assert.False(DuelSetup.TryReadCanonical(bytes, out DuelSetup? read));
            Assert.Null(read);
        }

        // 한 설정에 바이트 열 하나: 받아들인 바이트는 다시 쓰면 그대로 나온다
        [Fact]
        public void AnyAcceptedSingleByteChange_RewritesToExactlyThoseBytes()
        {
            int accepted = 0;
            int rejected = 0;
            for (int i = 0; i < SampleBytes.Length; i++)
            {
                foreach (byte value in new[] { (byte)(SampleBytes[i] ^ 0x01), (byte)(SampleBytes[i] ^ 0x80), (byte)0x00, (byte)0xFF })
                {
                    byte[] bytes = (byte[])SampleBytes.Clone();
                    bytes[i] = value;
                    if (DuelSetup.TryReadCanonical(bytes, out DuelSetup? read))
                    {
                        accepted++;
                        Assert.Equal(bytes, Write(read!));
                    }
                    else
                    {
                        rejected++;
                        Assert.Null(read);
                    }
                }
            }
            Assert.True(accepted > 100);
            Assert.True(rejected > 20);
        }

        [Fact]
        public void NoCapAndLargestValues_RoundTrip()
        {
            DuelSetup s = Sample();
            s.Options.IlCap = DuelRoomOptions.NoCap;
            s.Options.HeroBlockCap = short.MaxValue;
            s.Options.Rounds = byte.MaxValue;
            s.Options.RoundSeconds = ushort.MaxValue;
            s.Options.HpMultPermille = ushort.MaxValue;
            s.Options.EnhanceCap = byte.MaxValue;
            s.DataVersion = int.MaxValue;
            s.Players[1].AppliedWand.Mutations = Enumerable.Range(0, 255).Select(i => (MutationId)i).ToArray();
            s.Players[1].AppliedWand.Links = Enumerable.Range(0, 255).Select(i => (BlockId)(255 - i)).ToArray();
            s.Players[1].ActualStage = byte.MaxValue;
            s.Players[1].MaxHpPoints = int.MaxValue;
            byte[] bytes = Write(s);
            Assert.True(DuelSetup.TryReadCanonical(bytes, out DuelSetup? read));
            AssertSameSetup(s, read!);
            Assert.Equal(bytes, Write(read!));
        }

        // 이 코어의 버전 글자 열이 그대로 실린다
        [Fact]
        public void SimVersionCoreVersion_IsWritable()
        {
            DuelSetup s = Sample();
            s.CoreVersion = SimVersion.CoreVersion;
            Assert.True(DuelSetup.TryReadCanonical(Write(s), out DuelSetup? read));
            Assert.Equal(SimVersion.CoreVersion, read!.CoreVersion);
        }

        [Fact]
        public void WriteCanonical_ThrowsAndWritesNothingWhenTheSetupDoesNotFit()
        {
            void Rejects(Action<DuelSetup> change)
            {
                DuelSetup s = Sample();
                change(s);
                var w = new ByteWriter();
                w.U8(0xEE);
                Assert.Throws<InvalidOperationException>(() => s.WriteCanonical(w));
                Assert.Equal(1, w.Length);
            }

            Rejects(s => s.CoreVersion = string.Empty);                       // 새 설정의 기본값: 채우기 전에는 쓰지 못한다
            Rejects(s => s.CoreVersion = "00112233445566778899AABBCCDDEEFF");   // 대문자
            Rejects(s => s.CoreVersion = null!);
            Rejects(s => s.DataVersion = -1);
            Rejects(s => s.Options = null!);
            Rejects(s => s.Options.Rounds = 256);
            Rejects(s => s.Options.Rounds = -1);
            Rejects(s => s.Options.RoundSeconds = 65536);
            Rejects(s => s.Options.HpMultPermille = -1);
            Rejects(s => s.Options.EnhanceCap = 256);
            Rejects(s => s.Options.IlCap = -2);
            Rejects(s => s.Options.IlCap = 32768);
            Rejects(s => s.Options.HeroBlockCap = -2);
            Rejects(s => s.Players = null!);
            Rejects(s => s.Players = new[] { s.Players[0] });
            Rejects(s => s.Players = new[] { s.Players[0], s.Players[1], s.Players[1] });
            Rejects(s => s.Players[1] = null!);
            Rejects(s => s.Players = new[] { s.Players[1], s.Players[0] });   // Players[i].Slot == i
            Rejects(s => s.Players[0].AppliedWand = null!);
            Rejects(s => s.Players[0].AppliedWand.Stage = 256);
            Rejects(s => s.Players[0].AppliedWand.SlotsExtra = -1);
            Rejects(s => s.Players[0].AppliedWand.Mutations = new MutationId[256]);
            Rejects(s => s.Players[0].AppliedWand.Links = null!);
            Rejects(s => s.Players[1].ActualStage = 256);
            Rejects(s => s.Players[1].MaxHpPoints = -1);
            Assert.Throws<ArgumentNullException>(() => Sample().WriteCanonical(null!));
        }

        [Fact]
        public void TryRead_NoExceptionOnRandomBytes()
        {
            var rng = new Random(0x5E7);
            for (int i = 0; i < 200_000; i++)
            {
                byte[] bytes;
                if ((i & 1) == 0)
                {
                    bytes = new byte[rng.Next(0, 140)];
                    rng.NextBytes(bytes);
                }
                else
                {
                    bytes = (byte[])SampleBytes.Clone();
                    int n = rng.Next(1, 4);
                    for (int k = 0; k < n; k++)
                    {
                        bytes[rng.Next(0, bytes.Length)] = (byte)rng.Next(0, 256);
                    }
                }
                if (DuelSetup.TryReadCanonicalPrefix(bytes, out DuelSetup? read, out int length))
                {
                    Assert.Equal(bytes.Take(length).ToArray(), Write(read!));
                }
            }
        }
    }
}
