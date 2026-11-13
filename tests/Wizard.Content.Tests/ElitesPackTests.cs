using System;
using System.Collections.Generic;
using System.IO;
using Wizard.Content.Tests.Pack;
using Wizard.DataBuild.Core;
using Wizard.TestKit;
using Xunit;
using Ability = Wizard.Content.EliteDef.EliteAbility;
using Kind = Wizard.Content.AttackDef.AttackKind;
using Shape = Wizard.Content.AttackDef.TelegraphShape;
using Source = Wizard.Content.AttackDef.DamageSource;

namespace Wizard.Content.Tests
{
    // data/sim/elites.json → 팩 절 18 → ContentDb.Elite 의 왕복(M8-DAT-01: 엘리트 12종).
    // 기대값의 출처: spec 05 7.1(공통), 7.2(엘리트 12종 표), 7.3(능력 의사코드와 경계), 10.3·10.6(검산표의 엘리트 줄),
    // 11.16 TV-ELT-*, 13.1 U-05·U-69~U-78 [임시], spec 14 3.7(id elite.<바탕 키>, 코드 200 + 종).
    // 정수 단위: 거리는 Raw(× 2^32), 각도는 센티도(× 100), 시간은 틱(× 60), 배율은 천분율, 탄속은 틱당 Raw(× 2^32 ÷ 60).
    public sealed class ElitesPackTests
    {
        private const long OneRaw = 4_294_967_296L;            // 2^32 (spec 14 0.3)

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

        private static ContentDb RepoDb() => ContentDb.Build(ContentPack.Load(RepoPack.Value));

        // ---- spec 05 7.2 표와 한 줄씩 대조 ----

        // 열: 바탕 종, 고유 능력, 공격 수(spec 05 7.2 의 능력이 쓰는 공격: 대신하는 것 + 이어 나오는 것 + 따로 쓰는 것), 표시 코드(spec 14 3.7)
        public static IEnumerable<object[]> Spec05Table72()
        {
            yield return new object[] { EnemyTypeId.SlimeMoss, Ability.Split, 0, 201 };          // 왕이끼 슬라임
            yield return new object[] { EnemyTypeId.WolfBramble, Ability.Howl, 0, 202 };         // 우두머리 늑대
            yield return new object[] { EnemyTypeId.GolemMenhir, Ability.Shards, 1, 203 };       // 고인돌지기: 파편 고리
            yield return new object[] { EnemyTypeId.ShamanCap, Ability.WideSpread, 1, 204 };     // 큰갓 술사: 5갈래
            yield return new object[] { EnemyTypeId.BugLantern, Ability.BigBlast, 1, 205 };      // 왕꽈리 벌레: 큰 점화
            yield return new object[] { EnemyTypeId.HogNeedle, Ability.TripleAim, 0, 206 };      // 삼연 고슴도치
            yield return new object[] { EnemyTypeId.WeaselPot, Ability.EarthBands, 1, 207 };     // 화분 어미: 흙 띠
            yield return new object[] { EnemyTypeId.BatDusk, Ability.Summon, 1, 208 };           // 박쥐 여왕: 급강하
            yield return new object[] { EnemyTypeId.WispStray, Ability.TwinRing, 0, 209 };       // 쌍둥이불
            yield return new object[] { EnemyTypeId.VineThorn, Ability.Bloom, 1, 210 };          // 꽃 핀 가시덩굴: 씨앗
            yield return new object[] { EnemyTypeId.BoarTusk, Ability.Bounce, 0, 211 };          // 외엄니
            yield return new object[] { EnemyTypeId.ToadDrum, Ability.BigDrum, 0, 212 };         // 큰북 두꺼비
        }

        [Theory]
        [MemberData(nameof(Spec05Table72))]
        public void Elite_Row_MatchesSpec05Table72(EnemyTypeId baseType, Ability ability, int attacks, int code)
        {
            EliteDef e = RepoDb().Elite(baseType);
            Assert.Equal(baseType, e.BaseType);
            Assert.Equal(ability, e.Ability);
            Assert.Equal(attacks, e.Attacks.Length);
            Assert.Equal(code, ContentIds.ToEliteCode(baseType));
            // 엘리트의 공격에 접촉은 없다(바탕 종의 접촉을 그대로 쓴다, spec 05 7.1)
            foreach (EliteDef.EliteAttack a in e.Attacks)
            {
                Assert.NotEqual(Kind.Contact, a.Attack.Kind);
            }
        }

        [Fact]
        public void TV_ELT_04__CostAndGold_AreBaseTimesThree()
        {
            // spec 05 7.1, TV-ELT-04: 비용 ×3, 골드 ×3(= 비용 × 2 × 3). ×3 은 웨이브 곡선(elite_cost_mul)이 가진다.
            // 슬라임 3·6, 늑대 9·18, 선돌지기 18·36, 술사 12·24, 꽈리 6·12, 고슴도치 12·24, 족제비 9·18, 박쥐 3·6, 도깨비불 12·24, 가시덩굴 12·24, 멧돼지 15·30, 북두꺼비 15·30
            ContentDb db = RepoDb();
            int mul = db.WaveCurve.EliteCostMul;
            var cost = new[] { 3, 9, 18, 12, 6, 12, 9, 3, 12, 12, 15, 15 };
            var gold = new[] { 6, 18, 36, 24, 12, 24, 18, 6, 24, 24, 30, 30 };
            for (int i = 0; i < cost.Length; i++)
            {
                var id = (EnemyTypeId)(i + 1);
                db.Elite(id);
                Assert.Equal(cost[i], db.Enemy(id).Cost * mul);
                Assert.Equal(gold[i], db.Enemy(id).Cost * mul * 2);
            }
            Assert.Equal(3, db.WaveCurve.EliteHpMul);                    // 체력 ×3(spec 05 7.1)
        }

        [Fact]
        public void TV_ELT_03__EliteGolem_SpeedAndTelegraph_AreTheBase()
        {
            // 엘리트 선돌지기: 비용 18 / 골드 36 / 속도 1.8 / 내려찍기 66틱. 엘리트는 속도·예고를 바꾸지 않는다(spec 05 7.1)
            ContentDb db = RepoDb();
            EliteDef e = db.Elite(EnemyTypeId.GolemMenhir);
            EnemyDef b = db.Enemy(EnemyTypeId.GolemMenhir);
            Assert.Equal(18, b.Cost * db.WaveCurve.EliteCostMul);
            Assert.Equal(180, b.MoveSpeedCenti);
            Assert.Equal(66, b.Attacks[0].TelegraphTicks);
            foreach (EliteDef.EliteAttack a in e.Attacks)
            {
                Assert.True(a.ReplacesIndex < 0);                       // 내려찍기를 대신하지 않는다(파편이 뒤에 이어 나온다)
            }
        }

        [Fact]
        public void SlimeKing_Split()
        {
            EliteDef.AbilityParams p = RepoDb().Elite(EnemyTypeId.SlimeMoss).Params;   // 7.3, TV-ELT-05
            Assert.Equal(3, p.SplitCount);                               // 3마리
            Assert.Equal(2_147_483_648L, p.SplitRadius.Raw);             // 반지름 0.5 [임시: U-69]
            Assert.Equal(0, p.SplitStartAngleCenti);                     // 시작각 0도
            AssertOnly(p, nameof(p.SplitCount), nameof(p.SplitRadius));
        }

        [Fact]
        public void WolfAlpha_Howl_TV_ELT_06()
        {
            EliteDef.AbilityParams p = RepoDb().Elite(EnemyTypeId.WolfBramble).Params;
            Assert.Equal(60, p.HowlTicks);                               // 울부짖음 1.0초
            Assert.Equal(6 * OneRaw, p.HowlRadius.Raw);                  // 반경 6
            Assert.Equal(15, p.HowlStepTicks);                           // 0.25초 간격 → h+60, h+75, h+90
            Assert.Equal(2, p.ChainCount);                               // 자신은 돌진 2회
            Assert.Equal(0, p.ChainGapTicks);                            // 사이 0 [임시: U-71]
            AssertOnly(p, nameof(p.HowlTicks), nameof(p.HowlRadius), nameof(p.HowlStepTicks), nameof(p.ChainCount));
            // 두 번째 예고 0.5초 = 바탕 돌진과 같다
            Assert.Equal(30, RepoDb().Enemy(EnemyTypeId.WolfBramble).Attacks[0].TelegraphTicks);
        }

        [Fact]
        public void GolemDolmen_Shards_TV_ELT_07()
        {
            EliteDef e = RepoDb().Elite(EnemyTypeId.GolemMenhir);
            EliteDef.EliteAttack x = Assert.Single(e.Attacks);
            Assert.Equal(-1, x.ReplacesIndex);
            Assert.Equal(0, x.FollowsIndex);                             // 내려찍기(첨자 0)가 끝난 자리에서
            AttackDef a = x.Attack;
            Assert.Equal(Kind.RingShot, a.Kind);
            Assert.Null(a.Shape);
            Assert.Equal(1, a.Damage);                                   // 피해 1(탄)
            Assert.Equal(Source.Projectile, a.Source);
            Assert.Equal(357_913_941L, a.Proj!.SpeedPerTick.Raw);        // 속도 5 = 5 × 2^32 ÷ 60
            Assert.Equal(8, a.Proj.Count);                               // 8발
            Assert.Equal(4500, a.Proj.SpreadCenti);                      // 45도 간격(3칸 지점 2.36칸)
            Assert.Equal(9 * OneRaw, a.Proj.Range.Raw);                  // 사거리 9 [임시: U-72]
            Assert.Equal(773_094_113L, a.Proj.HitRadius.Raw);            // 반지름 0.18(대, 파편) [임시: U-72]
            Assert.True(a.Grazeable);                                    // 스침 대상
            Assert.Equal(8000, e.Params.ArmorHalfAngleCenti);            // 철갑 160도 = 반각 80도
            AssertOnly(e.Params, nameof(e.Params.ArmorHalfAngleCenti));
        }

        [Fact]
        public void ShamanBigcap_FiveWay_TV_ELT_08()
        {
            EliteDef e = RepoDb().Elite(EnemyTypeId.ShamanCap);
            EliteDef.EliteAttack x = Assert.Single(e.Attacks);
            Assert.Equal(0, x.ReplacesIndex);                            // 3갈래(첨자 0) 대신
            Assert.Equal(-1, x.FollowsIndex);
            AttackDef a = x.Attack;
            Assert.Equal(Kind.Spread, a.Kind);
            Assert.Equal(5, a.Proj!.Count);                              // 5갈래
            Assert.Equal(2000, a.Proj.SpreadCenti);                      // 간격 20도
            // 나머지는 바탕과 같다(속도 6.5, 시전 0.4초, 빈틈 0.6초, 사거리 9, 판정 0.12, 간격 2.0초)
            AttackDef b = RepoDb().Enemy(EnemyTypeId.ShamanCap).Attacks[0];
            Assert.Equal(b.Proj!.SpeedPerTick.Raw, a.Proj.SpeedPerTick.Raw);
            Assert.Equal(b.CastTicks, a.CastTicks);
            Assert.Equal(b.RecoveryTicks, a.RecoveryTicks);
            Assert.Equal(b.Range!.Value.Raw, a.Range!.Value.Raw);
            Assert.Equal(b.Proj.Range.Raw, a.Proj.Range.Raw);
            Assert.Equal(b.Proj.HitRadius.Raw, a.Proj.HitRadius.Raw);
            Assert.Equal(b.CooldownTicks, a.CooldownTicks);
            Assert.True(a.Grazeable);
            AssertOnly(e.Params);
        }

        [Fact]
        public void BugKing_BigBlast_TV_ELT_09()
        {
            EliteDef e = RepoDb().Elite(EnemyTypeId.BugLantern);
            EliteDef.EliteAttack x = Assert.Single(e.Attacks);
            Assert.Equal(0, x.ReplacesIndex);                            // 점화(첨자 0) 대신
            AttackDef a = x.Attack;
            Assert.Equal(Kind.SelfDestruct, a.Kind);
            Assert.Equal(Shape.Circle, a.Shape);
            Assert.Equal(11_166_914_970L, a.Radius.Raw);                 // 반경 2.6
            Assert.Equal(66, a.TelegraphTicks);                          // 예고 1.1초
            Assert.Equal(5_153_960_755L, a.Range!.Value.Raw);            // 점화 거리 1.2(바탕과 같음 [임시])
            Assert.Equal(1, a.Damage);
            Assert.Equal(Source.Telegraph, a.Source);
            Assert.Equal(60 * OneRaw, e.Params.SympatheticDamage.Raw);   // 유폭이 적에게 60(× H(w))
            AssertOnly(e.Params, nameof(e.Params.SympatheticDamage));
            Assert.Equal(24, RepoDb().Enemy(EnemyTypeId.BugLantern).Ai.SympatheticDelayTicks);   // 지연은 바탕과 같은 24틱
        }

        [Fact]
        public void HogTriple_TV_ELT_10()
        {
            EliteDef e = RepoDb().Elite(EnemyTypeId.HogNeedle);
            Assert.Empty(e.Attacks);                                     // 각 조준선은 바탕 저격(60틱, 마지막 24틱 고정)
            Assert.Equal(3, e.Params.AimCount);                          // 조준선 3개
            Assert.Equal(30, e.Params.AimStepTicks);                     // s, s+30, s+60
            AssertOnly(e.Params, nameof(e.Params.AimCount), nameof(e.Params.AimStepTicks));
            AttackDef b = RepoDb().Enemy(EnemyTypeId.HogNeedle).Attacks[0];
            Assert.Equal(60, b.TelegraphTicks);
            Assert.Equal(24, b.LockTicks);
        }

        [Fact]
        public void WeaselMother_EarthBands_TV_ELT_11()
        {
            EliteDef e = RepoDb().Elite(EnemyTypeId.WeaselPot);
            EliteDef.EliteAttack x = Assert.Single(e.Attacks);
            Assert.Equal(-1, x.ReplacesIndex);
            Assert.Equal(0, x.FollowsIndex);                             // 솟구침(첨자 0) 판정 틱에 솟은 자리에서
            AttackDef a = x.Attack;
            Assert.Equal(Kind.Zone, a.Kind);
            Assert.Equal(Shape.Band, a.Shape);
            Assert.Equal(3_435_973_837L, a.Width.Raw);                   // 폭 0.8
            Assert.Equal(3 * OneRaw, a.Length!.Value.Raw);               // 길이 3
            Assert.Equal(36, a.TelegraphTicks);                          // 예고 0.6초
            Assert.Equal(1, a.Damage);
            Assert.Equal(Source.Telegraph, a.Source);
            Assert.Equal(4, e.Params.BandCount);                         // 네 방향 ±x, ±y [임시: U-74]
            Assert.Equal(0, e.Params.BandStartAngleCenti);
            AssertOnly(e.Params, nameof(e.Params.BandCount));
        }

        [Fact]
        public void BatQueen_SummonAndDive_TV_ELT_12()
        {
            EliteDef e = RepoDb().Elite(EnemyTypeId.BatDusk);
            EliteDef.EliteAttack x = Assert.Single(e.Attacks);
            Assert.Equal(-1, x.ReplacesIndex);                           // 따로 쓰는 공격
            Assert.Equal(-1, x.FollowsIndex);
            AttackDef a = x.Attack;                                      // 급강하(7.3)
            Assert.Equal(Kind.Dash, a.Kind);
            Assert.Equal(Shape.Band, a.Shape);
            Assert.Equal(3_435_973_837L, a.Width.Raw);                   // 띠 폭 0.8
            Assert.Equal(5 * OneRaw, a.Length!.Value.Raw);               // 길이 5
            Assert.Equal(30, a.TelegraphTicks);                          // 예고 0.5초
            Assert.Equal(AttackDef.LockMode.AtStart, a.Lock);            // 방향 시작 순간 고정
            Assert.Equal(1800, a.DashSpeedCenti);                        // 속도 18 [임시: U-76]
            Assert.Equal(17, a.ActiveTicks);                             // ceil(5 × 60 ÷ 18) = ceil(16.67)
            Assert.Equal(1, a.Damage);                                   // 피해 1(예고 공격)
            Assert.Equal(Source.Telegraph, a.Source);
            Assert.True(a.IsDash);                                       // 돌진 목록(spec 05 6.1)
            Assert.True(a.Grazeable);
            Assert.Equal(240, a.CooldownTicks);                          // 급강하 간격 4초 [임시: U-05]
            Assert.Equal(4, e.Params.SummonCount);                       // 4마리
            Assert.Equal(OneRaw, e.Params.SummonRadius.Raw);             // 반지름 1
            Assert.Equal(new[] { 600, 200 }, e.Params.SummonHpPermille); // 60%, 20%를 처음 지날 때 [임시: U-75]
            AssertOnly(e.Params, nameof(e.Params.SummonCount), nameof(e.Params.SummonRadius), nameof(e.Params.SummonHpPermille));
        }

        [Fact]
        public void WispTwin_TV_ELT_13()
        {
            EliteDef e = RepoDb().Elite(EnemyTypeId.WispStray);
            Assert.Empty(e.Attacks);                                     // 바탕 고리 8발을 두 번
            Assert.Equal(2, e.Params.RingRepeat);
            Assert.Equal(30, e.Params.RingRepeatStepTicks);              // 30틱 간격
            Assert.Equal(2250, e.Params.RingRepeatAngleCenti);           // 두 번째 +22.5도
            AssertOnly(e.Params, nameof(e.Params.RingRepeat), nameof(e.Params.RingRepeatStepTicks), nameof(e.Params.RingRepeatAngleCenti));
        }

        [Fact]
        public void VineBloom_TV_ELT_14()
        {
            EliteDef e = RepoDb().Elite(EnemyTypeId.VineThorn);
            EliteDef.EliteAttack x = Assert.Single(e.Attacks);
            Assert.Equal(0, x.ReplacesIndex);                            // 씨앗(첨자 0) 대신
            AttackDef a = x.Attack;
            Assert.Equal(Kind.Lob, a.Kind);
            Assert.True(a.Lob);
            Assert.Equal(6_442_450_944L, a.Radius.Raw);                  // 반경 1.5(바탕과 같음)
            Assert.Equal(60, a.TelegraphTicks);
            Assert.Equal(360, a.LeavesHazard!.DurationTicks);            // 가시밭 6초
            Assert.Equal(AttackDef.HazardKind.ThornField, a.LeavesHazard.Kind);
            Assert.Equal(3, a.LeavesHazard.MaxCount);                    // 전장 동시 3개(공통)
            Assert.Equal(2, e.Params.SeedCount);                         // 씨앗 2개
            Assert.Equal(3 * OneRaw, e.Params.SeedLeadDistance.Raw);     // 이동 방향 3칸 앞: (10,7) → (13,7)
            Assert.Equal(2_147_483_648L, e.Params.SeedWallInset.Raw);    // 벽 안 0.5칸 [임시]
            AssertOnly(e.Params, nameof(e.Params.SeedCount), nameof(e.Params.SeedLeadDistance), nameof(e.Params.SeedWallInset));
        }

        [Fact]
        public void BoarOnetusk_TV_ELT_15()
        {
            EliteDef e = RepoDb().Elite(EnemyTypeId.BoarTusk);
            Assert.Empty(e.Attacks);
            Assert.Equal(1, e.Params.BounceCount);                       // 한 번 튕긴다, 두 번째 충돌에서 기절
            Assert.Equal(30, e.Params.BounceStopTicks);                  // 튕김 전 0.5초
            AssertOnly(e.Params, nameof(e.Params.BounceCount), nameof(e.Params.BounceStopTicks));
            AttackDef b = RepoDb().Enemy(EnemyTypeId.BoarTusk).Attacks[0];
            Assert.Equal(54, b.TelegraphTicks);                          // 새 띠 예고 0.9초 = 바탕 돌진
            Assert.Equal(120, b.RecoveryTicks);                          // 기절 120틱
        }

        [Fact]
        public void ToadBigdrum_TV_ELT_16()
        {
            EliteDef e = RepoDb().Elite(EnemyTypeId.ToadDrum);
            Assert.Empty(e.Attacks);
            Assert.Equal(6 * OneRaw, e.Params.CroakRadius.Raw);          // 울음 반경 6
            Assert.Equal(60 * OneRaw, e.Params.CroakBreakDamage.Raw);    // 끊기 60(× H(w)): w 15 에서 165.79
            Assert.True(e.Params.CroakBreakOnly);                        // 빙결·기절로는 취소 안 됨(X9)
            AssertOnly(e.Params, nameof(e.Params.CroakRadius), nameof(e.Params.CroakBreakDamage), nameof(e.Params.CroakBreakOnly));
        }

        // 그 능력이 쓰지 않는 매개변수는 0 이다(EliteDef.AbilityParams). 0 이 아닌 칸의 이름을 모은다
        private static void AssertOnly(EliteDef.AbilityParams p, params string[] nonZero)
        {
            var found = new List<string>();
            void Check(string name, bool set)
            {
                if (set)
                {
                    found.Add(name);
                }
            }
            Check(nameof(p.SplitCount), p.SplitCount != 0);
            Check(nameof(p.SplitRadius), p.SplitRadius.Raw != 0);
            Check(nameof(p.SplitStartAngleCenti), p.SplitStartAngleCenti != 0);
            Check(nameof(p.HowlTicks), p.HowlTicks != 0);
            Check(nameof(p.HowlRadius), p.HowlRadius.Raw != 0);
            Check(nameof(p.HowlStepTicks), p.HowlStepTicks != 0);
            Check(nameof(p.ChainCount), p.ChainCount != 0);
            Check(nameof(p.ChainGapTicks), p.ChainGapTicks != 0);
            Check(nameof(p.ArmorHalfAngleCenti), p.ArmorHalfAngleCenti != 0);
            Check(nameof(p.SympatheticDamage), p.SympatheticDamage.Raw != 0);
            Check(nameof(p.AimCount), p.AimCount != 0);
            Check(nameof(p.AimStepTicks), p.AimStepTicks != 0);
            Check(nameof(p.BandCount), p.BandCount != 0);
            Check(nameof(p.BandStartAngleCenti), p.BandStartAngleCenti != 0);
            Check(nameof(p.SummonCount), p.SummonCount != 0);
            Check(nameof(p.SummonRadius), p.SummonRadius.Raw != 0);
            Check(nameof(p.SummonHpPermille), p.SummonHpPermille.Length != 0);
            Check(nameof(p.RingRepeat), p.RingRepeat != 0);
            Check(nameof(p.RingRepeatStepTicks), p.RingRepeatStepTicks != 0);
            Check(nameof(p.RingRepeatAngleCenti), p.RingRepeatAngleCenti != 0);
            Check(nameof(p.SeedCount), p.SeedCount != 0);
            Check(nameof(p.SeedLeadDistance), p.SeedLeadDistance.Raw != 0);
            Check(nameof(p.SeedWallInset), p.SeedWallInset.Raw != 0);
            Check(nameof(p.BounceCount), p.BounceCount != 0);
            Check(nameof(p.BounceStopTicks), p.BounceStopTicks != 0);
            Check(nameof(p.CroakRadius), p.CroakRadius.Raw != 0);
            Check(nameof(p.CroakBreakDamage), p.CroakBreakDamage.Raw != 0);
            Check(nameof(p.CroakBreakOnly), p.CroakBreakOnly);
            Assert.Equal(nonZero, found.ToArray());
        }

        [Fact]
        public void ElitesNotInPack_Throw()
        {
            ContentDb db = RepoDb();
            Assert.Throws<ContentException>(() => db.Elite(EnemyTypeId.None));
            Assert.Throws<ContentException>(() => db.Elite(EnemyTypeId.SporeSeedling));      // 졸개에는 엘리트가 없다
            Assert.Throws<ContentException>(() => db.Elite((EnemyTypeId)999));
        }

        [Fact]
        public void PackWithoutTheSection_Throws()
        {
            ContentDb db = ContentDb.Build(ContentPack.Load(PackWriter.Write(PackWriter.SchemaVersion, 1, new byte[PackWriter.CoreCompatLength],
                Array.Empty<PackSection>())));
            Assert.Throws<ContentException>(() => db.Elite(EnemyTypeId.SlimeMoss));
        }

        // ---- 결정성 ----

        // 바탕 id 오름차순으로 든 엘리트의 (공격 수, 체력 문턱 수)
        private static readonly (EnemyTypeId Id, int Attacks, int Thresholds)[] PackOrder =
        {
            (EnemyTypeId.SlimeMoss, 0, 0), (EnemyTypeId.WolfBramble, 0, 0), (EnemyTypeId.GolemMenhir, 1, 0), (EnemyTypeId.ShamanCap, 1, 0),
            (EnemyTypeId.BugLantern, 1, 0), (EnemyTypeId.HogNeedle, 0, 0), (EnemyTypeId.WeaselPot, 1, 0), (EnemyTypeId.BatDusk, 1, 2),
            (EnemyTypeId.WispStray, 0, 0), (EnemyTypeId.VineThorn, 1, 0), (EnemyTypeId.BoarTusk, 0, 0), (EnemyTypeId.ToadDrum, 0, 0),
        };

        // 엘리트 항목(절 꼴은 ContentDb.Elites.cs): 머리 4바이트 + (첨자 8 + 공격 165)바이트 × 수 + 매개변수 138바이트 + 문턱 4바이트 × 수
        private const int EliteHead = 4;
        private const int EliteAttackLength = 8 + 165;
        private const int ParamsLength = 138;

        private static int EntryLength(int attacks, int thresholds) => EliteHead + EliteAttackLength * attacks + ParamsLength + 4 * thresholds;

        [Fact]
        public void SameData_SameSectionBytes_InIdOrder()
        {
            byte[] s = Section(RepoPack.Value);
            Assert.Equal(s, Section(BuildPack(TestRepo.Root)));
            Assert.Equal(PackOrder.Length, s[0] | (s[1] << 8));
            int at = 2;
            foreach ((EnemyTypeId id, int attacks, int thresholds) in PackOrder)
            {
                Assert.Equal((int)id, s[at] | (s[at + 1] << 8));
                Assert.Equal(attacks, s[at + 3]);
                at += EntryLength(attacks, thresholds);
            }
            Assert.Equal(at, s.Length);
        }

        [Fact]
        public void ReversedSourceOrder_GivesTheSameBytes()
        {
            using TempDataRepo forward = Repo("[" + SlimeKingJson + ", " + ShamanBigcapJson + "]");
            using TempDataRepo reversed = Repo("[" + ShamanBigcapJson + ", " + SlimeKingJson + "]");
            Assert.Equal(Section(BuildPack(forward.Root)), Section(BuildPack(reversed.Root)));
        }

        [Fact]
        public void TestJson_IsTheRepoData()
        {
            // 진단 시험에 쓰는 두 줄이 저장소 데이터와 같은 바이트를 낸다(시험의 원본이 명세에서 멀어지지 않게)
            using TempDataRepo repo = Repo("[" + SlimeKingJson + ", " + ShamanBigcapJson + "]");
            byte[] s = Section(BuildPack(repo.Root));
            byte[] full = Section(RepoPack.Value);
            Assert.Equal(2, s[0] | (s[1] << 8));
            int slime = EntryLength(0, 0);
            Assert.Equal(full.AsSpan(2, slime).ToArray(), s.AsSpan(2, slime).ToArray());
            int shamanAt = 2 + EntryLength(0, 0) * 2 + EntryLength(1, 0);   // 이끼, 늑대, 선돌지기 다음
            Assert.Equal(full.AsSpan(shamanAt, EntryLength(1, 0)).ToArray(), s.AsSpan(2 + slime).ToArray());
        }

        // ---- 진단(절 안) ----

        [Fact]
        public void Problems_Ids_Duplicates_Abilities()
        {
            Expect(SlimeKingJson, null, null);                                                               // 원본 그대로는 통과
            Expect(SlimeKingJson.Replace("elite.slime_moss", "elite.nope"), "/elites/0/id", "elite.nope");
            Expect(SlimeKingJson.Replace("elite.slime_moss", "elite.spore_seedling"), "/elites/0/id", "엘리트 id");
            Expect(SlimeKingJson + ", " + SlimeKingJson, "/elites/1/id", "두 번");
            // 같은 능력을 두 엘리트가 갖는다(종마다 하나)
            Expect(SlimeKingJson + ", " + SlimeKingJson.Replace("elite.slime_moss", "elite.wolf_bramble"), "/elites/1/ability", "두 엘리트");
            Expect(SlimeKingJson.Replace("\"ability\": \"split\"", "\"ability\": \"nope\""), "/elites/0/ability", "스키마");
        }

        [Fact]
        public void Problems_Params()
        {
            // 능력이 쓰는 매개변수가 빠졌다 / 쓰지 않는 매개변수가 있다
            Expect(SlimeKingJson.Replace("\"split_count\": 3, ", ""), "/elites/0/params", "split_count");
            Expect(SlimeKingJson.Replace("\"split_count\": 3, ", "\"split_count\": 3, \"aim_count\": 3, "), "/elites/0/params/aim_count", "쓰지 않는다");
            // 체력 문턱은 내림차순
            Expect(BatQueenJson, null, null);
            Expect(BatQueenJson.Replace("[0.6, 0.2]", "[0.2, 0.6]"), "/elites/0/params/summon_hp_ratios/1", "내림차순");
            Expect(BatQueenJson.Replace("[0.6, 0.2]", "[0.6, 0.6]"), "/elites/0/params/summon_hp_ratios/1", "내림차순");
        }

        [Fact]
        public void Problems_Attacks()
        {
            // 능력과 공격의 짝: 큰갓은 대신하는 공격 하나
            Expect(ShamanBigcapJson.Replace("\"replaces\": 0,", "\"follows\": 0,"), "/elites/0/attacks", "대신하는 것 1개");
            Expect(SlimeKingJson.Replace("\"attacks\": []", "\"attacks\": [" + BatDive + "]"), "/elites/0/attacks", "split");
            // replaces 와 follows 를 같이
            Expect(ShamanBigcapJson.Replace("\"replaces\": 0,", "\"replaces\": 0, \"follows\": 0,"), "/elites/0/attacks/0/follows", "둘 중 하나");
            // 엘리트 공격에 접촉은 없다
            Expect(BatQueenJson.Replace(BatDive, "{ \"attack\": { \"key\": \"contact\", \"kind\": \"contact\", \"damage\": 1, \"damage_source\": \"contact\", \"cooldown_seconds\": 1.5 } }"),
                "/elites/0/attacks/0/attack/kind", "접촉");
            // 공격 한 줄의 규칙은 적과 같다(돌진 시간 = ceil(길이 × 60 ÷ 속도))
            Expect(BatQueenJson.Replace("\"active_ticks\": 17", "\"active_ticks\": 16"), "/elites/0/attacks/0/attack/active_ticks", "ceil");
        }

        // ---- 교차 검사(enemies.json ↔ elites.json) ----

        [Fact]
        public void CrossCheck_BaseSpeciesAndIndices()
        {
            // 바탕 종이 enemies.json 에 없다
            ExpectWith(EnemiesWithout("enemy.slime_moss"), SlimeKingJson, "/elites/0/id", "바탕 종");
            // 첨자가 바탕 공격 밖이다(술사의 공격은 2개)
            Expect(ShamanBigcapJson.Replace("\"replaces\": 0,", "\"replaces\": 2,"), "/elites/0/attacks/0/replaces", "2개");
            // 대신하는 바탕 공격과 종류가 다르다(술사 첨자 1 은 접촉)
            Expect(ShamanBigcapJson.Replace("\"replaces\": 0,", "\"replaces\": 1,"), "/elites/0/attacks/0/replaces", "contact");
            // 이어 나오는 바탕 공격에 예고 구역이 없다(술사 3갈래는 시전만)
            Expect(GolemShardsOn("elite.shaman_cap"), "/elites/0/attacks/0/follows", "예고 구역");
        }

        [Fact]
        public void CrossCheck_RepoData_Passes()
        {
            using DataSet data = DataLoader.Load(TestRepo.Root, new Diagnostics());
            var d = new Diagnostics();
            new Wizard.DataBuild.ElitesConverter.CrossCheck().Validate(data, d);
            Assert.False(d.HasErrors, string.Join("\n", d.Sorted()));
        }

        // ---- 회피 검산(spec 05 10절)에 엘리트 공격이 든다 ----

        [Fact]
        public void Evasion_RepoElites_Pass_And_AreCollected()
        {
            using DataSet data = DataLoader.Load(TestRepo.Root, new Diagnostics());
            var input = new Wizard.DataBuild.Validate.Evasion.EvasionInput();
            new Wizard.DataBuild.ElitesConverter.EvasionSource().Collect(data, input, new Diagnostics());
            // 파편, 5갈래, 왕꽈리 점화, 흙 띠, 급강하, 꽃 핀 씨앗 + 쌍둥이불 두 고리를 합친 줄
            Assert.Equal(7, input.Attacks.Count);
            Assert.Contains(input.Attacks, a => a.Owner == "elite.wisp_stray" && a.Proj != null && a.Proj.SpreadDegrees == 22.5m);
            var d = new Diagnostics();
            new Wizard.DataBuild.Validate.Evasion.EvasionValidator().Validate(data, d);
            Assert.False(d.HasErrors, string.Join("\n", d.Sorted()));
        }

        [Fact]
        public void Evasion_EliteR1Violation_FailsCheck()
        {
            // 왕꽈리 반경 2.6 을 바탕 예고 0.9초로 두면 R1 위반: (900 − 300) × 3760 = 2,256,000 < 2,750,000(spec 05 10.3 "1.1초로 늘린 값")
            using TempDataRepo repo = CopyOfRepoData();
            SetAllStanceSpeeds(repo, "3.76");                           // 명세의 V(가장 느린 자세 속도, spec 05 1.2)
            Replace(repo, "data/sim/elites.json", "\"telegraph_seconds\": 1.1,", "\"telegraph_seconds\": 0.9,");
            List<string> problems = Problems(repo.Root);
            Assert.Contains(problems, p => p.Contains("data/sim/elites.json:/elites/4/attacks/0/attack") && p.Contains("elite.bug_lantern 의 공격 ignite: R1 위반"));
        }

        [Fact]
        public void Evasion_TwinRingSpacing_IsChecked()
        {
            // 쌍둥이불 두 고리 사이가 1.0칸 미만이면 R3 위반(spec 05 10.6 [검산 추가]): 어긋남 5도 → 3칸 지점 0.26칸
            using TempDataRepo repo = CopyOfRepoData();
            Replace(repo, "data/sim/elites.json", "\"ring_repeat_angle_degrees\": 22.5", "\"ring_repeat_angle_degrees\": 5");
            List<string> problems = Problems(repo.Root);
            Assert.Contains(problems, p => p.Contains("data/sim/elites.json:/elites/8/params") && p.Contains("elite.wisp_stray") && p.Contains("R3 위반"));
            Assert.Equal(22.5m, Wizard.DataBuild.ElitesConverter.EvasionSource.CombinedGapDegrees(45m, 8, 22.5m, 2));
            Assert.Equal(5m, Wizard.DataBuild.ElitesConverter.EvasionSource.CombinedGapDegrees(45m, 8, 5m, 2));
        }

        // ---- 깨진 절 바이트 ----

        [Fact]
        public void BrokenSectionBytes_ThrowContentException()
        {
            byte[] good = Section(RepoPack.Value);
            Load(good);
            Assert.Throws<ContentException>(() => Load(good.AsSpan(0, good.Length - 1).ToArray()));
            var longer = new byte[good.Length + 1];
            good.CopyTo(longer, 0);
            Assert.Throws<ContentException>(() => Load(longer));
            // 첫 항목(왕이끼, 절 머리 2바이트 뒤): u16 base(2..3) u8 ability(4) u8 공격 수(5) 매개변수(6..) … 마지막 u8 croak_break_only
            Assert.Throws<ContentException>(() => Load(Patched(good, 2, 0)));          // 바탕 None
            Assert.Throws<ContentException>(() => Load(Patched(good, 2, 101)));        // 졸개(엘리트 없음)
            Assert.Throws<ContentException>(() => Load(Patched(good, 4, 0xEE)));       // 모르는 능력
            Assert.Throws<ContentException>(() => Load(Patched(good, 2 + EntryLength(0, 0) - 1, 2)));   // bool 이 0/1 이 아니다
            // 순서 위반: 같은 길이인 앞 두 항목(왕이끼, 우두머리 늑대)을 바꾼다
            int entry = EntryLength(0, 0);
            var swapped = (byte[])good.Clone();
            Array.Copy(good, 2 + entry, swapped, 2, entry);
            Array.Copy(good, 2, swapped, 2 + entry, entry);
            Assert.Throws<ContentException>(() => Load(swapped));
            // 세 번째 항목(고인돌지기)의 공격: i32 replaces(−1) i32 follows(0)
            int golem = 2 + 2 * entry + EliteHead;
            Assert.Throws<ContentException>(() => Load(Patched(good, golem, 0, 4)));   // replaces 0 + follows 0: 둘 다 있다
            Assert.Throws<ContentException>(() => Load(Patched(good, golem, 0xFE)));   // replaces −2
            Assert.Throws<ContentException>(() => Load(Patched(good, golem + 8, 0xEE)));   // 공격 종류를 모른다
        }

        private static byte[] Patched(byte[] good, int at, byte value, int count = 1)
        {
            var b = (byte[])good.Clone();
            for (int i = 0; i < count; i++)
            {
                b[at + i] = value;
            }
            return b;
        }

        private static ContentDb Load(byte[] section) =>
            ContentDb.Build(ContentPack.Load(PackWriter.Write(PackWriter.SchemaVersion, 1, new byte[PackWriter.CoreCompatLength],
                new[] { new PackSection((ushort)ContentSection.Elites, section) })));

        private static byte[] Section(byte[] pack)
        {
            Assert.True(ContentPack.Load(pack).TryGetSection((ushort)ContentSection.Elites, out ReadOnlyMemory<byte> s));
            return s.ToArray();
        }

        // ---- 시험 데이터(data/sim/elites.json 과 같은 값. TestJson_IsTheRepoData 가 앞 둘을 대조한다) ----

        private const string SlimeKingJson =
            "{ \"id\": \"elite.slime_moss\", \"ability\": \"split\", \"attacks\": [],\n" +
            "  \"params\": { \"split_count\": 3, \"split_radius\": 0.5, \"split_start_angle_degrees\": 0 } }";

        private const string ShamanBigcapJson =
            "{ \"id\": \"elite.shaman_cap\", \"ability\": \"wide_spread\",\n" +
            "  \"attacks\": [{ \"replaces\": 0, \"attack\": { \"key\": \"wide\", \"kind\": \"spread\", \"range\": 9, \"cast_seconds\": 0.4, \"lock\": \"at_start\", \"anchor\": \"self\",\n" +
            "    \"recovery_seconds\": 0.6, \"damage\": 1, \"damage_source\": \"projectile\",\n" +
            "    \"proj\": { \"speed\": 6.5, \"count\": 5, \"spread_degrees\": 20, \"range\": 9, \"hit_radius\": 0.12 }, \"grazeable\": true, \"cooldown_seconds\": 2.0 } }],\n" +
            "  \"params\": {} }";

        private const string BatDive =
            "{ \"attack\": { \"key\": \"dive\", \"kind\": \"dash\", \"shape\": \"band\", \"width\": 0.8, \"length\": 5, \"telegraph_seconds\": 0.5, \"lock\": \"at_start\",\n" +
            "    \"anchor\": \"self\", \"active_ticks\": 17, \"dash_speed\": 18, \"recovery_seconds\": 0, \"damage\": 1, \"damage_source\": \"telegraph\",\n" +
            "    \"grazeable\": true, \"is_dash\": true, \"cooldown_seconds\": 4.0 } }";

        private const string BatQueenJson =
            "{ \"id\": \"elite.bat_dusk\", \"ability\": \"summon\", \"attacks\": [" + BatDive + "],\n" +
            "  \"params\": { \"summon_count\": 4, \"summon_radius\": 1, \"summon_hp_ratios\": [0.6, 0.2] } }";

        private static string GolemShardsOn(string id) =>
            "{ \"id\": \"" + id + "\", \"ability\": \"shards\",\n" +
            "  \"attacks\": [{ \"follows\": 0, \"attack\": { \"key\": \"shards\", \"kind\": \"ring_shot\", \"anchor\": \"self\", \"damage\": 1, \"damage_source\": \"projectile\",\n" +
            "    \"proj\": { \"speed\": 5, \"count\": 8, \"spread_degrees\": 45, \"range\": 9, \"hit_radius\": 0.18 }, \"grazeable\": true, \"cooldown_seconds\": 0 } }],\n" +
            "  \"params\": { \"armor_half_angle_degrees\": 80 } }";

        private static void Expect(string elitesArrayBody, string? pointer, string? text) =>
            ExpectWith(File.ReadAllText(TestRepo.Path("data/sim/enemies.json")), elitesArrayBody, pointer, text);

        private static void ExpectWith(string enemiesJson, string elitesArrayBody, string? pointer, string? text)
        {
            using TempDataRepo repo = Repo("[" + elitesArrayBody + "]", enemiesJson);
            List<string> problems = Problems(repo.Root);
            if (pointer == null)
            {
                Assert.Empty(problems);
                return;
            }
            Assert.Contains(problems, p => p.Contains("data/sim/elites.json:" + pointer) && p.Contains(text!));
        }

        // 저장소의 enemies.json 에서 그 적 한 줄을 뺀 것(바탕 종이 없는 엘리트 시험)
        private static string EnemiesWithout(string id)
        {
            using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(TestRepo.Path("data/sim/enemies.json")));
            var rows = new List<string>();
            foreach (System.Text.Json.JsonElement e in doc.RootElement.GetProperty("enemies").EnumerateArray())
            {
                if (e.GetProperty("id").GetString() != id)
                {
                    rows.Add(e.GetRawText());
                }
            }
            return "{ \"enemies\": [" + string.Join(", ", rows) + "] }\n";
        }

        // 적 절과 엘리트 절만 둔 임시 저장소(캐릭터 파일이 없어 회피 검산은 V 에 기대지 않는 줄만 본다)
        private static TempDataRepo Repo(string elitesArray, string? enemiesJson = null)
        {
            var repo = new TempDataRepo();
            repo.Write("data/schema/enemies.schema.json", File.ReadAllText(TestRepo.Path("data/schema/enemies.schema.json")));
            repo.Write("data/schema/elites.schema.json", File.ReadAllText(TestRepo.Path("data/schema/elites.schema.json")));
            repo.Write("data/sim/enemies.json", enemiesJson ?? File.ReadAllText(TestRepo.Path("data/sim/enemies.json")));
            repo.Write("data/sim/elites.json", "{ \"elites\": " + elitesArray + " }\n");
            repo.Write("data/names/names.ko.json", File.ReadAllText(TestRepo.Path("data/names/names.ko.json")));
            return repo;
        }

        // 저장소의 data/ 를 통째로 복사한 임시 저장소(V 를 구할 캐릭터 파일까지 있다)
        private static TempDataRepo CopyOfRepoData()
        {
            var repo = new TempDataRepo();
            CopyDir(TestRepo.Path("data"), Path.Combine(repo.Root, "data"));
            return repo;
        }

        // from 이 꼭 한 번 나와야 한다(시험이 엉뚱한 곳을 고치지 않게)
        private static void Replace(TempDataRepo repo, string relative, string from, string to)
        {
            string text = File.ReadAllText(Path.Combine(repo.Root, relative));
            int at = text.IndexOf(from, StringComparison.Ordinal);
            Assert.True(at >= 0, relative + " 에 '" + from + "' 가 없다");
            Assert.True(text.IndexOf(from, at + from.Length, StringComparison.Ordinal) < 0, relative + " 에 '" + from + "' 가 둘 이상이다");
            repo.Write(relative, text.Substring(0, at) + to + text.Substring(at + from.Length));
        }

        // 캐릭터 줄 모두의 자세 속도를 같은 값으로 둔다 → 회피 검산의 V 가 그 값이 된다
        private static void SetAllStanceSpeeds(TempDataRepo repo, string value)
        {
            const string rel = "data/sim/characters.json";
            string text = File.ReadAllText(Path.Combine(repo.Root, rel));
            string changed = System.Text.RegularExpressions.Regex.Replace(text, "\"stance_speed\": [0-9.]+", "\"stance_speed\": " + value,
                System.Text.RegularExpressions.RegexOptions.CultureInvariant);
            Assert.NotEqual(text, changed);
            repo.Write(rel, changed);
        }

        private static void CopyDir(string from, string to)
        {
            Directory.CreateDirectory(to);
            foreach (string f in Directory.GetFiles(from))
            {
                File.Copy(f, Path.Combine(to, Path.GetFileName(f)), true);
            }
            foreach (string d in Directory.GetDirectories(from))
            {
                CopyDir(d, Path.Combine(to, Path.GetFileName(d)));
            }
        }
    }
}
