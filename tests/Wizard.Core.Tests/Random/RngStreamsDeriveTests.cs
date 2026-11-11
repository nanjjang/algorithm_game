using System;
using Wizard.Core;
using Xunit;

namespace Wizard.Core.Tests.Random
{
    // spec 07 14.2, spec 01 3.3·14.1, CONTRACTS 1.3·0.3("난수 흐름: spec 07 방식, 흐름 표는 1.3절 하나"), DECISIONS-TECH T-09.
    // spec 01의 "StreamId"는 0.3절에 따라 RngStream 번호로 읽는다.
    public sealed class RngStreamsDeriveTests
    {
        private const ulong RunSeed = 0x0123456789ABCDEFUL;

        [Fact]
        public void S01_T_C10__SameRunSeedStreams1And3DifferAndRebuildRepeats()
        {
            // 같은 run_seed, StreamId 1과 3: 첫 출력이 서로 다르다
            Pcg32 s1 = RngStreams.Derive(RunSeed, (RngStream)1, 1);
            Pcg32 s3 = RngStreams.Derive(RunSeed, (RngStream)3, 1);
            Assert.NotEqual(s1.NextU32(), s3.NextU32());

            // spec 01 3.3의 글자 그대로(new Pcg32(run_seed, StreamId))도 다르다
            Assert.NotEqual(new Pcg32(RunSeed, 1UL).NextU32(), new Pcg32(RunSeed, 3UL).NextU32());

            // 같은 StreamId로 다시 만들면 같은 수열
            foreach (ushort id in new ushort[] { 1, 3 })
            {
                Pcg32 a = RngStreams.Derive(RunSeed, (RngStream)id, 1);
                Pcg32 b = RngStreams.Derive(RunSeed, (RngStream)id, 1);
                for (int i = 0; i < 256; i++)
                {
                    Assert.Equal(a.NextU32(), b.NextU32());
                }
                Assert.Equal(a.State, b.State);
                Assert.Equal(a.Inc, b.Inc);
            }

            // 흐름 1과 3은 앞 256개 안에서 한 번도 같은 자리에 같은 값을 내지 않을 만큼 다르다(우연 일치 허용 폭 4)
            Pcg32 x = RngStreams.Derive(RunSeed, (RngStream)1, 1);
            Pcg32 y = RngStreams.Derive(RunSeed, (RngStream)3, 1);
            int same = 0;
            for (int i = 0; i < 256; i++)
            {
                if (x.NextU32() == y.NextU32())
                {
                    same++;
                }
            }
            Assert.True(same <= 4);
        }

        // SplitMix64 공개 기준값: x = 0의 첫 출력, 씨앗 1234567에서 상태를 0x9E3779B97F4A7C15씩 밀며 낸 다섯 출력
        // (Vigna의 splitmix64.c, 상태형 next()와 SplitMix64(state + k·γ)가 같다).
        [Fact]
        public void SplitMix64_MatchesPublicReference()
        {
            Assert.Equal(0xE220A8397B1DCDAFUL, RngStreams.SplitMix64(0UL));

            ulong[] expected = { 6457827717110365317UL, 3203168211198807973UL, 9817491932198370423UL, 4593380528125082431UL, 16408922859458223821UL };
            ulong state = 1234567UL;
            foreach (ulong e in expected)
            {
                Assert.Equal(e, RngStreams.SplitMix64(state));
                state = unchecked(state + 0x9E3779B97F4A7C15UL);
            }
        }

        // 계약의 식을 손으로 섞은 키로 확인한다: seed ^ (stream << 48) ^ ((uint)wave << 16) ^ (ushort)sub, seq = stream
        [Fact]
        public void Derive_KeyMixingFollowsContract()
        {
            AssertDerivedFrom(RunSeed ^ 0x0004_0000_0003_0002UL, 4UL, RngStreams.Derive(RunSeed, RngStream.Loot, 3, 2));
            AssertDerivedFrom(RunSeed ^ 0x0004_0000_0006_FFFFUL, 4UL, RngStreams.Derive(RunSeed, RngStream.Loot, 6, 0xFFFF));
            AssertDerivedFrom(0x000C_0000_0001_0000UL, 12UL, RngStreams.Derive(0UL, RngStream.DuelWild, 1));
            // 웨이브는 uint로 바꿔 16비트 올린다: 32비트 전체가 16~47비트에 들고 흐름 칸(48~63)에 번지지 않는다
            AssertDerivedFrom(0x0002_FFFF_FFFF_0000UL, 2UL, RngStreams.Derive(0UL, RngStream.WaveSpawn, -1));
            AssertDerivedFrom(0x0002_0001_2345_0000UL, 2UL, RngStreams.Derive(0UL, RngStream.WaveSpawn, 0x12345));
            // 보조는 ushort로 자른다(0x1FFFF → 0xFFFF)
            AssertDerivedFrom(0x0007_0000_0002_FFFFUL, 7UL, RngStreams.Derive(0UL, RngStream.EnemyAi, 2, 0x1FFFF));
            // 보조를 주지 않으면 0
            Pcg32 omitted = RngStreams.Derive(RunSeed, RngStream.Modifier, 21);
            Pcg32 zero = RngStreams.Derive(RunSeed, RngStream.Modifier, 21, 0);
            Assert.Equal(zero.State, omitted.State);
            Assert.Equal(zero.Inc, omitted.Inc);
        }

        // 회귀 고정값. 기대값은 구현과 따로 만든 BigInt 참조 모형(spec 07 14.2 식 + spec 01 3.3의 PCG32)으로 계산했다.
        [Theory]
        [InlineData(0x0123456789ABCDEFUL, (ushort)1, 1, 0, 0x19539ea172da2834UL, 3UL, new uint[] { 0x454e63d6u, 0x94024d48u, 0x78f57621u, 0x13f750f1u })]
        [InlineData(0x0123456789ABCDEFUL, (ushort)3, 1, 0, 0xf7b620cb01aae9ccUL, 7UL, new uint[] { 0xdb0e9347u, 0xa96935c9u, 0xd6cfdb14u, 0xc71db683u })]
        [InlineData(0x0123456789ABCDEFUL, (ushort)4, 6, 0xFFFF, 0xd6bcd0aaea4aec9bUL, 9UL, new uint[] { 0xe7282ef5u, 0x0425e1c4u, 0x19ae76dbu, 0x211a22eeu })]
        [InlineData(0UL, (ushort)2, 13, 0, 0x8c3b0792c6bb8d57UL, 5UL, new uint[] { 0x49c043b2u, 0xd131537fu, 0x8cc67183u, 0x1470601du })]
        [InlineData(0xFFFFFFFFFFFFFFFFUL, (ushort)12, 3, 7, 0x1902fef4cf187227UL, 25UL, new uint[] { 0xc40be2d1u, 0x1e6a4024u, 0x32b074dau, 0xa92fe4b3u })]
        public void Derive_RegressionValues(ulong seed, ushort stream, int wave, int sub, ulong state, ulong inc, uint[] outputs)
        {
            Pcg32 rng = RngStreams.Derive(seed, (RngStream)stream, wave, sub);
            Assert.Equal(state, rng.State);
            Assert.Equal(inc, rng.Inc);
            foreach (uint o in outputs)
            {
                Assert.Equal(o, rng.NextU32());
            }
        }

        // spec 07 14.2: 웨이브마다 독립이라 앞 웨이브의 소비량이 뒤에 번지지 않는다
        [Fact]
        public void Derive_WaveIndependence_ConsumptionDoesNotLeak()
        {
            uint[] fresh = FirstOutputs(RngStreams.Derive(RunSeed, RngStream.WaveSpawn, 8), 16);

            // 웨이브 7에서 얼마를 쓰든 웨이브 8의 흐름은 같다
            foreach (int used in new[] { 0, 1, 17, 1000 })
            {
                Pcg32 w7 = RngStreams.Derive(RunSeed, RngStream.WaveSpawn, 7);
                for (int i = 0; i < used; i++)
                {
                    w7.NextInt(1000);
                }
                Assert.Equal(fresh, FirstOutputs(RngStreams.Derive(RunSeed, RngStream.WaveSpawn, 8), 16));
            }

            // 흐름, 웨이브, 보조, 씨앗 가운데 하나만 달라도 다른 수열
            uint[] baseSeq = FirstOutputs(RngStreams.Derive(RunSeed, RngStream.Loot, 7, 0), 8);
            Assert.NotEqual(baseSeq, FirstOutputs(RngStreams.Derive(RunSeed, RngStream.HpShard, 7, 0), 8));
            Assert.NotEqual(baseSeq, FirstOutputs(RngStreams.Derive(RunSeed, RngStream.Loot, 8, 0), 8));
            Assert.NotEqual(baseSeq, FirstOutputs(RngStreams.Derive(RunSeed, RngStream.Loot, 7, 1), 8));
            Assert.NotEqual(baseSeq, FirstOutputs(RngStreams.Derive(RunSeed, RngStream.Loot, 7, 0xFFFF), 8));
            Assert.NotEqual(baseSeq, FirstOutputs(RngStreams.Derive(RunSeed + 1UL, RngStream.Loot, 7, 0), 8));
        }

        // 모든 흐름 × 웨이브 1~100 × 보조 {0, 1, 0xFFFF}에서 첫 두 출력의 쌍이 서로 겹치지 않는다
        [Fact]
        public void Derive_AllStreamsWavesSubs_AreDistinct()
        {
            var seen = new System.Collections.Generic.HashSet<ulong>();
            int[] subs = { 0, 1, 0xFFFF };
            for (ushort s = 1; s <= 12; s++)
            {
                for (int w = 1; w <= 100; w++)
                {
                    foreach (int sub in subs)
                    {
                        Pcg32 rng = RngStreams.Derive(RunSeed, (RngStream)s, w, sub);
                        ulong pair = ((ulong)rng.NextU32() << 32) | rng.NextU32();
                        Assert.True(seen.Add(pair));
                    }
                }
            }
        }

        // 흐름 번호는 계약(CONTRACTS 1.3)의 표다. 추가만 하고 바꾸지 않는다.
        [Fact]
        public void RngStream_NumbersAreFixed()
        {
            Assert.Equal(typeof(ushort), Enum.GetUnderlyingType(typeof(RngStream)));
            Assert.Equal(1, (int)RngStream.WaveComp);
            Assert.Equal(2, (int)RngStream.WaveSpawn);
            Assert.Equal(3, (int)RngStream.Modifier);
            Assert.Equal(4, (int)RngStream.Loot);
            Assert.Equal(5, (int)RngStream.HpShard);
            Assert.Equal(6, (int)RngStream.Event);
            Assert.Equal(7, (int)RngStream.EnemyAi);
            Assert.Equal(8, (int)RngStream.Mutation);
            Assert.Equal(9, (int)RngStream.Weather);
            Assert.Equal(10, (int)RngStream.ArtUnlock);
            Assert.Equal(11, (int)RngStream.BossAi);
            Assert.Equal(12, (int)RngStream.DuelWild);
            Assert.Equal(12, Enum.GetValues(typeof(RngStream)).Length);
        }

        // 두 번 실행, 저장·복원 뒤 진행, 직렬화 왕복: 세계가 보관하는 흐름은 State·Inc 두 필드가 전부다(CONTRACTS 1.3).
        [Fact]
        public void Stream_TwoRuns_SnapshotRestore_SerializationRoundTrip()
        {
            ulong hashA = RunAndHash(RngStreams.Derive(RunSeed, RngStream.EnemyAi, 5));
            ulong hashB = RunAndHash(RngStreams.Derive(RunSeed, RngStream.EnemyAi, 5));
            Assert.Equal(hashA, hashB);

            Pcg32 live = RngStreams.Derive(RunSeed, RngStream.BossAi, 30);
            for (int i = 0; i < 37; i++)
            {
                live.NextInt(9);
            }
            Pcg32 snapshot = live;                      // 값 복사가 스냅숏이다
            uint[] ahead = new uint[64];
            for (int i = 0; i < ahead.Length; i++)
            {
                ahead[i] = (uint)live.NextInt(1000) ^ live.NextU32();
            }
            live = snapshot;                            // 복원
            for (int i = 0; i < ahead.Length; i++)
            {
                Assert.Equal(ahead[i], (uint)live.NextInt(1000) ^ live.NextU32());
            }

            // 직렬화 왕복
            live = snapshot;
            var bytes = new ByteWriter();
            var w = StateWriter.ForBytes(bytes);
            w.U64(live.State);
            w.U64(live.Inc);
            byte[] saved = bytes.ToArray();
            Assert.Equal(16, saved.Length);
            var r = new StateReader(saved);
            var restored = new Pcg32(0UL, 0UL);
            restored.State = r.U64();
            restored.Inc = r.U64();
            Assert.Equal(0, r.Remaining);
            for (int i = 0; i < 64; i++)
            {
                Assert.Equal(live.NextU32(), restored.NextU32());
            }
        }

        private static ulong RunAndHash(Pcg32 rng)
        {
            var h = StateWriter.ForHash();
            for (int i = 0; i < 500; i++)
            {
                h.I32(rng.NextInt(97));
                h.I32(rng.NextRange(-50, 50));
                h.Bool(rng.Chance(3, 10));
                h.Fix(rng.NextFix01());
                h.U64(rng.State);
            }
            return h.Hash;
        }

        private static void AssertDerivedFrom(ulong mixedKey, ulong seq, Pcg32 actual)
        {
            Pcg32 expected = new Pcg32(RngStreams.SplitMix64(mixedKey), seq);
            Assert.Equal(expected.State, actual.State);
            Assert.Equal(expected.Inc, actual.Inc);
            Assert.Equal((seq << 1) | 1UL, actual.Inc);
        }

        private static uint[] FirstOutputs(Pcg32 rng, int count)
        {
            var result = new uint[count];
            for (int i = 0; i < count; i++)
            {
                result[i] = rng.NextU32();
            }
            return result;
        }
    }
}
