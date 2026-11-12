using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using Wizard.Content.Tests.Pack;
using Wizard.DataBuild.Core;
using Wizard.TestKit;
using Xunit;

namespace Wizard.Content.Tests
{
    // data/sim/scoring.json → 팩 절 11 → ContentDb.Scoring 의 왕복(M3-DAT-02).
    // 기대값은 spec 07 12.1 표(정수 원점수 칸)와 0.2(S10(w) = 9 + w)에서 온다.
    // "명세 식 재현" 테스트는 12.2 의 정수 식을 데이터 값으로 돌려 17.9 표의 숫자가 나오는지 본다(식의 구현과 벡터 테스트의 주인은 M3 웨이브·점수 작업이다).
    public sealed class ScoringPackTests
    {
        private static readonly Lazy<byte[]> RepoPack = new Lazy<byte[]>(() => BuildPack(TestRepo.Root));

        private static byte[] BuildPack(string root)
        {
            var d = new Diagnostics();
            Pipeline.Output o = Pipeline.Run(root, ModuleRegistry.Discover(), PackOptions.Default(PackOptions.DefaultDataVersion), d);
            Assert.False(d.HasErrors, string.Join("\n", d.Sorted()));
            return o.Pack!;
        }

        private static List<string> Problems(string root)
        {
            var d = new Diagnostics();
            Pipeline.Run(root, ModuleRegistry.Discover(), PackOptions.Default(PackOptions.DefaultDataVersion), d);
            return new List<string>(d.Sorted());
        }

        private static ScoringDef Scoring() => ContentDb.Build(ContentPack.Load(RepoPack.Value)).Scoring;

        [Fact]
        public void Spec07_12_1_EveryField()
        {
            ScoringDef s = Scoring();
            Assert.Equal(10, s.WaveCoefTenthsAtWave1);          // S(1) = 1 → 10
            Assert.Equal(1, s.WaveCoefTenthsPerWave);           // + 0.1 × (w − 1) → 1
            Assert.Equal(10, s.KillPerCost);                    // 처치: cost × 10
            Assert.Equal(3000, s.BossKill);                     // 보스 처치 3000
            Assert.Equal(200, s.WaveClear);                     // 웨이브 클리어 200
            Assert.Equal(2, s.FastPerSecond);                   // 빠른 처리 × 2
            Assert.Equal(3600, s.FastTargetTicks);              // 목표 일반 60초 = 3,600틱
            Assert.Equal(7200, s.FastTargetTicksBoss);          // 보스 120초 = 7,200틱
            Assert.Equal(100, s.NoHit);                         // 무피격 100
            Assert.Equal(5, s.GrazePer);                        // 스침 1회당 5
            Assert.Equal(20, s.GrazeCap);                       // 일반 웨이브 20회까지
            Assert.Equal(40, s.GrazeCapBoss);                   // 보스 웨이브 40회까지
            Assert.Equal(3648, s.PhantomFirst);                 // 허상과의 첫 만남 고정 3,648
        }

        [Fact]
        public void Spec07_0_2_S10_FromData_Is9PlusW()
        {
            // 17.9 TV-140: S10(1) = 10, S10(10) = 19, S10(30) = 39
            ScoringDef s = Scoring();
            Assert.Equal(10, S10(s, 1));
            Assert.Equal(19, S10(s, 10));
            Assert.Equal(39, S10(s, 30));
        }

        [Fact]
        public void Spec07_12_2_WaveScore_FromData_GivesTheTableValues()
        {
            ScoringDef s = Scoring();
            // TV-141: w=1, slime 16 처치(비용 합 16), 1,800틱, 무피격, 스침 3 → 535
            Assert.Equal(535L, WaveScore(s, 1, 16 * s.KillPerCost, false, true, 3, 1800));
            // TV-142: w=23, 비용 합 126, 5,400틱, 피격, 스침 25 → 4,992 (몰려옴 ×115/100 = 5,740 은 변덕 데이터 몫)
            Assert.Equal(4992L, WaveScore(s, 23, 126 * s.KillPerCost, false, false, 25, 5400));
            Assert.Equal(5740L, 4992L * 115 / 100);
            // TV-143: w=45, 비용 합 200, 3,000틱, 무피격, 스침 0 → raw60 139,200 → 12,528
            Assert.Equal(12528L, WaveScore(s, 45, 200 * s.KillPerCost, false, true, 0, 3000));
            // TV-144: w=10 가마등, 처치+클리어만 → 3,200 × 19 / 10 = 6,080 (빠른 처리 없음 = 7,200틱)
            Assert.Equal(6080L, WaveScore(s, 10, 0, true, false, 0, 7200));
            // TV-145: + 무피격 → 6,270
            Assert.Equal(6270L, WaveScore(s, 10, 0, true, true, 0, 7200));
            // TV-147: w=30 보스, 6,000틱, 무피격, 스침 50(→ 40) → raw 3,540 → 13,806
            Assert.Equal(13806L, WaveScore(s, 30, 0, true, true, 50, 6000));
        }

        [Fact]
        public void PackWithoutTheSection_Throws()
        {
            ContentDb db = Load(null);
            Assert.Throws<ContentException>(() => db.Scoring);
        }

        [Fact]
        public void SameData_SameSectionBytes()
        {
            Assert.Equal(Section(RepoPack.Value), Section(BuildPack(TestRepo.Root)));
        }

        [Fact]
        public void Problems_Schema_And_InexactCoefficient()
        {
            JsonObject o = RepoJson();
            o["graze"]!["boss_cap_typo"] = 40;
            using (TempDataRepo repo = Repo(o.ToJsonString()))
            {
                Assert.Contains(Problems(repo.Root), p => p.Contains("data/sim/scoring.json") && p.Contains("스키마"));
            }
            o = RepoJson();
            o.Remove("phantom_first");
            using (TempDataRepo repo = Repo(o.ToJsonString()))
            {
                Assert.Contains(Problems(repo.Root), p => p.Contains("data/sim/scoring.json") && p.Contains("스키마"));
            }
            o = RepoJson();
            o["wave_coef"]!["per_wave"] = 0.15m;                 // × 10 이 정수가 아니다
            using (TempDataRepo repo = Repo(o.ToJsonString()))
            {
                Assert.Contains(Problems(repo.Root), p => p.Contains("data/sim/scoring.json:/wave_coef/per_wave") && p.Contains("정수"));
            }
        }

        [Fact]
        public void BrokenSectionBytes_ThrowContentException()
        {
            byte[] good = Section(RepoPack.Value);
            Assert.Equal(13 * 4, good.Length);                  // i32 열세 개
            Assert.Throws<ContentException>(() => Load(good.AsSpan(0, good.Length - 1).ToArray()));
            var longer = new byte[good.Length + 1];
            good.CopyTo(longer, 0);
            Assert.Throws<ContentException>(() => Load(longer));
            Load(good);
        }

        private static int S10(ScoringDef s, int w) => s.WaveCoefTenthsAtWave1 + s.WaveCoefTenthsPerWave * (w - 1);

        // spec 07 12.2 WaveScore(변덕 없음, 첫 만남 아님)
        private static long WaveScore(ScoringDef s, int w, long killRaw, bool boss, bool noHit, int grazes, int elapsedTicks)
        {
            long raw60 = 60L * (killRaw + (boss ? s.BossKill : 0) + s.WaveClear + (noHit ? s.NoHit : 0)
                                + (long)s.GrazePer * Math.Min(grazes, boss ? s.GrazeCapBoss : s.GrazeCap))
                         + (long)s.FastPerSecond * Math.Max(0, (boss ? s.FastTargetTicksBoss : s.FastTargetTicks) - elapsedTicks);
            return raw60 * S10(s, w) / 600;
        }

        private static JsonObject RepoJson() => (JsonObject)JsonNode.Parse(System.IO.File.ReadAllText(TestRepo.Path("data/sim/scoring.json")))!;

        private static ContentDb Load(byte[]? section)
        {
            var sections = section == null ? new PackSection[0] : new[] { new PackSection((ushort)ContentSection.Scoring, section) };
            return ContentDb.Build(ContentPack.Load(PackWriter.Write(PackWriter.SchemaVersion, 1, new byte[PackWriter.CoreCompatLength], sections)));
        }

        private static byte[] Section(byte[] pack)
        {
            Assert.True(ContentPack.Load(pack).TryGetSection((ushort)ContentSection.Scoring, out ReadOnlyMemory<byte> s));
            return s.ToArray();
        }

        private static TempDataRepo Repo(string scoring)
        {
            var repo = new TempDataRepo();
            repo.Write("data/schema/scoring.schema.json", System.IO.File.ReadAllText(TestRepo.Path("data/schema/scoring.schema.json")));
            repo.Write("data/sim/scoring.json", scoring + "\n");
            return repo;
        }
    }
}
