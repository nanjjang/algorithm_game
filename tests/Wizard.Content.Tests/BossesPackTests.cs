using System;
using System.Collections.Generic;
using Wizard.DataBuild.Core;
using Wizard.TestKit;
using Xunit;
using Graze = Wizard.Content.BossPatternDef.GrazeMask;
using Key = Wizard.Content.BossPatternDef.PatternKey;
using Kind = Wizard.Content.AttackDef.AttackKind;
using Ov = Wizard.Content.BossDef.OverrideKey;
using Shape = Wizard.Content.AttackDef.TelegraphShape;
using Source = Wizard.Content.AttackDef.DamageSource;
using Step = Wizard.Content.BossPatternDef.StepDef;
using StepKind = Wizard.Content.BossPatternDef.StepKind;

namespace Wizard.Content.Tests
{
    // data/sim/bosses.json → 팩 절 bosses → ContentDb.Boss 의 왕복(M8-DAT-02: 보스 6종).
    // 기대값의 출처: spec 06 1.3(BossDef 표), 2.2·2.3·2.4·2.6·2.8·2.11·2.12(공통 규칙, 2회차 표, 연사형 표), 3~8절(보스별 기본·패턴 표·단계 표·세부 규칙),
    // 9.2·12절(첫 만남의 코어 시각), spec 14 3.8(id·코드·순번·속성·약점·저항). 명세가 정하지 않은 값은 데이터의 _note 에 [임시]로 적힌 값이고 그 줄에 [임시] 라고 적었다.
    // 정수 단위: 거리는 Raw(× 2^32), 각도는 센티도(× 100), 시간은 틱(× 60), 배율은 천분율, 탄속은 틱당 Raw(× 2^32 ÷ 60, 짝수 반올림).
    public sealed class BossesPackTests
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

        private static ContentDb RepoDb() => ContentDb.Build(ContentPack.Load(RepoPack.Value));

        private static BossDef Boss(BossId id) => RepoDb().Boss(id);

        // 칸 → Raw(단위 환산, spec 14 0.3)
        private static long Raw(decimal cells) => (long)decimal.Round(cells * OneRaw, MidpointRounding.ToEven);

        // 칸/초 → 틱당 Raw(단위 환산, spec 14 0.3)
        private static long PerTick(decimal cellsPerSecond) => (long)decimal.Round(cellsPerSecond * OneRaw / 60m, MidpointRounding.ToEven);

        private static BossPatternDef Pattern(BossDef b, Key key)
        {
            BossPatternDef? p = b.FindPattern(key);
            Assert.True(p != null, ContentIds.ToId(b.Id) + " 에 패턴 " + key + " 가 없다");
            return p!;
        }

        private static AttackDef Attack(Step s)
        {
            Assert.Equal(StepKind.Attack, s.Kind);
            Assert.NotNull(s.Attack);
            return s.Attack!;
        }

        private static void AssertOverride(BossDef.PatternOverride o, Key pattern, int step, Ov key, long value)
        {
            Assert.Equal(pattern, o.Pattern);
            Assert.Equal(step, o.Step);
            Assert.Equal(key, o.Key);
            Assert.Equal(value, o.Value);
        }

        // ---- spec 06 2절 공통 규칙 ----

        [Fact]
        public void Common_MatchesSpec06_2()
        {
            BossDef.CommonDef c = Boss(BossId.KilnBoar).Common;
            Assert.Equal(72, c.IntroTicks);                    // 2.8·2.9 등장 1.2초
            Assert.Equal(36, c.PhaseTransitionTicks);          // 2.8 단계 전환 0.6초
            Assert.Equal(90, c.WaitTicks);                     // 2.11 패턴 사이 1.5초
            Assert.Equal(30, c.NoCandidateWaitTicks);          // 2.11 고를 것이 없으면 30틱 더 [임시]
            Assert.Equal(3, c.R5RetryTicks);                   // 2.8 R5Blocked 3틱마다 [임시]
            Assert.Equal(120, c.R5RepickTicks);                // 2.8 120틱 넘게 막히면 다른 패턴 [임시]
            Assert.Equal(1200, c.RageStackTicks);              // 2.12 20초마다 1스택
            Assert.Equal(5, c.RageMaxStacks);                  // 2.12 최대 5스택
            Assert.Equal(100, c.RageStepPermille);             // 2.12 스택당 −10%
            Assert.Equal(10800, c.FinalRageTicks);             // 2.12 최종 격노 180초
            Assert.Equal(1, c.FinalRageDamagePlus);            // 2.7·2.12 모든 공격 +1칸
            Assert.Equal(18, c.ActionHaltTicks);               // 2.3 ⑨ 뇌광창 기절 → 0.3초 동작 멈춤
            Assert.Equal(90, c.ContactCooldownTicks);          // 2.4 같은 적의 접촉 피해 1.5초에 1회

            // 모든 보스가 같은 공통 규칙을 가리킨다
            ContentDb db = RepoDb();
            foreach (BossId id in AllBosses())
            {
                Assert.Same(db.Boss(BossId.KilnBoar).Common, db.Boss(id).Common);
            }
        }

        [Fact]
        public void Common_WaitTicksByRage_MatchesSpec06_2_11()
        {
            // 2.11 WaitTicks: 90 × (10 − n) ÷ 10 → 90, 81, 72, 63, 54, 45
            BossDef.CommonDef c = Boss(BossId.KilnBoar).Common;
            var expected = new[] { 90, 81, 72, 63, 54, 45 };
            for (int n = 0; n <= c.RageMaxStacks; n++)
            {
                Assert.Equal(expected[n], c.WaitTicks * (1000 - c.RageStepPermille * n) / 1000);
            }
        }

        private static IEnumerable<BossId> AllBosses()
        {
            yield return BossId.KilnBoar;
            yield return BossId.MirrorPhantom;
            yield return BossId.SporeQueen;
            yield return BossId.StormOwl;
            yield return BossId.FrostGoat;
            yield return BossId.NightSerpent;
        }

        // ---- spec 06 1.3 BossDef 표 + spec 14 3.8 ----

        // 열: id, 순번, 기본 체력, 속도(센티 칸/초), 서리밭 위 속도(0 = 없음), 접촉 피해, 속성, 약점, 저항, 단계 수, 몸 판정 반지름(칸)
        public static IEnumerable<object[]> Spec06Table13()
        {
            yield return new object[] { BossId.KilnBoar, 0, 3000, 220, 0, 2, Element.Fire, Element.Ice, Element.Fire, 2, 1.0m };          // 반지름 [임시]
            yield return new object[] { BossId.MirrorPhantom, 1, 4000, 250, 0, 1, Element.Arcane, Element.Dark, Element.Arcane, 3, 0.5m }; // 접촉 1 [임시: 16절], 반지름 [임시]
            yield return new object[] { BossId.SporeQueen, 2, 4500, 100, 0, 1, Element.None, Element.Fire, Element.Lightning, 2, 1.0m };   // 반지름 [임시]
            yield return new object[] { BossId.StormOwl, 3, 5000, 300, 0, 1, Element.Lightning, Element.Arcane, Element.Lightning, 2, 1.0m }; // 반지름 [임시]
            yield return new object[] { BossId.FrostGoat, 4, 5500, 300, 240, 1, Element.Ice, Element.Lightning, Element.Ice, 2, 1.0m };     // 반지름 [임시]
            yield return new object[] { BossId.NightSerpent, 5, 6500, 350, 0, 1, Element.Dark, Element.Lightning, Element.Dark, 2, 0.75m }; // 머리 0.75 [임시: 8.1]
        }

        [Theory]
        [MemberData(nameof(Spec06Table13))]
        public void Boss_Row_MatchesSpec06Table13(BossId id, int rotation, int baseHp, int speedCenti, int frostCenti, int contact,
            Element element, Element weak, Element resist, int phases, decimal bodyRadius)
        {
            BossDef b = Boss(id);
            Assert.Equal(id, b.Id);
            Assert.Equal(rotation, b.RotationIndex);
            Assert.Equal(baseHp, b.BaseHp);
            Assert.Equal(10, b.HpScaleAnchorWave);             // 2.2 기본 10(허상의 대안 20 은 QA)
            Assert.Equal(speedCenti, b.MoveSpeedCenti);
            Assert.Equal(frostCenti == 0 ? (int?)null : frostCenti, b.MoveSpeedOnFrostCenti);
            Assert.Equal(contact, b.ContactDamage);
            Assert.Equal(element, b.Element);
            Assert.Equal(weak, b.Weak);
            Assert.Equal(resist, b.Resist);
            Assert.Equal(phases, b.Phases.Length);
            Assert.Equal(Raw(bodyRadius), b.BodyRadius.Raw);
            Assert.True(b.MoveSpeedCenti < 376, "R2: 보스 속도는 V 3.76 미만(spec 06 1.3, 8.6)");
        }

        [Fact]
        public void Bosses_Codes_MatchSpec14_3_8()
        {
            Assert.Equal("boss.kiln_boar", ContentIds.ToId(BossId.KilnBoar));
            Assert.Equal("boss.mirror_phantom", ContentIds.ToId(BossId.MirrorPhantom));
            Assert.Equal("boss.spore_queen", ContentIds.ToId(BossId.SporeQueen));
            Assert.Equal("boss.storm_owl", ContentIds.ToId(BossId.StormOwl));
            Assert.Equal("boss.frost_goat", ContentIds.ToId(BossId.FrostGoat));
            Assert.Equal("boss.night_serpent", ContentIds.ToId(BossId.NightSerpent));
            int code = 301;
            foreach (BossId id in AllBosses())
            {
                Assert.Equal(code++, (int)id);
            }
        }

        [Fact]
        public void Phases_EnterPercents_MatchSpec06()
        {
            // 3.3(100/60), 4.3(100/60/25), 5.3(100/50), 6.3(100/50), 7.3(100/50), 8.3(100/40)
            AssertPhases(BossId.KilnBoar, 100, 60);
            AssertPhases(BossId.MirrorPhantom, 100, 60, 25);
            AssertPhases(BossId.SporeQueen, 100, 50);
            AssertPhases(BossId.StormOwl, 100, 50);
            AssertPhases(BossId.FrostGoat, 100, 50);
            AssertPhases(BossId.NightSerpent, 100, 40);
        }

        private static void AssertPhases(BossId id, params int[] percents)
        {
            BossDef b = Boss(id);
            Assert.Equal(percents.Length, b.Phases.Length);
            for (int i = 0; i < percents.Length; i++)
            {
                Assert.Equal(i + 1, b.Phases[i].Index);
                Assert.Equal(percents[i], b.Phases[i].EnterAtHpPercent);
                Assert.True((b.Phases[i].Tags & EnemyTag.Boss) != 0, "보스는 늘 꼬리표 보스(spec 06 2.3)");
            }
        }

        private static void AssertPhase(BossDef.PhaseDef ph, Key[] patterns, Key[] passives, EnemyTag tags)
        {
            Assert.Equal(patterns, ph.Patterns);
            Assert.Equal(passives, ph.Passives);
            Assert.Equal(tags, ph.Tags);
        }

        // ---- 3절 가마등 ----

        [Fact]
        public void KilnBoar_PhasesAndPatterns_MatchSpec06_3()
        {
            BossDef b = Boss(BossId.KilnBoar);
            // 3.3: 1단계 삼연 돌진·발구르기, 2단계 + 낙석, 돌진 4회
            AssertPhase(b.Phases[0], new[] { Key.TripleCharge, Key.Stomp }, new Key[0], EnemyTag.Boss);
            AssertPhase(b.Phases[1], new[] { Key.TripleCharge, Key.Stomp, Key.Rockfall }, new Key[0], EnemyTag.Boss);
            Assert.Empty(b.Phases[0].Overrides);
            Assert.Single(b.Phases[1].Overrides);
            AssertOverride(b.Phases[1].Overrides[0], Key.TripleCharge, 1, Ov.Repeat, 3);        // 첫 돌진 1 + 이어지는 3 = 4회
            Assert.Equal(new[] { Key.TripleCharge, Key.Stomp, Key.Rockfall }, KeysOf(b));

            // 3.2 삼연 돌진: 띠 폭 2.0, 첫 1.0초, 2·3번째 0.7초, 매번 예고 시작 때 방향 고정, 16칸/초, 피해 2, 스침 돌진, 마지막에 2.5초 기절 ×1.5
            BossPatternDef tc = Pattern(b, Key.TripleCharge);
            Assert.False(tc.IsPassive);
            Assert.Equal(Graze.Charge, tc.Graze);
            Assert.Equal(150, tc.RecoveryTicks);
            Assert.Equal(1500, tc.RecoveryDamagePermille);
            Assert.Equal(2, tc.Steps.Length);
            AttackDef first = Attack(tc.Steps[0]);
            AttackDef next = Attack(tc.Steps[1]);
            foreach (AttackDef a in new[] { first, next })
            {
                Assert.Equal(Kind.Dash, a.Kind);
                Assert.Equal(Shape.Band, a.Shape);
                Assert.Equal(Raw(2.0m), a.Width.Raw);
                Assert.Null(a.Length);                                     // 벽까지
                Assert.Equal(AttackDef.LockMode.AtStart, a.Lock);
                Assert.Equal(1600, a.DashSpeedCenti);
                Assert.Equal(2, a.Damage);
                Assert.Equal(Source.Telegraph, a.Source);
                Assert.True(a.IsDash);
                Assert.True(a.Grazeable);
                // 불씨 띠: 폭 1.0(반폭 0.5), 3초, 접촉 규칙
                Assert.NotNull(a.LeavesHazard);
                Assert.Equal(AttackDef.HazardKind.EmberStrip, a.LeavesHazard!.Kind);
                Assert.Equal(Raw(0.5m), a.LeavesHazard.Radius.Raw);
                Assert.Equal(180, a.LeavesHazard.DurationTicks);
                Assert.Equal(90, a.LeavesHazard.ContactCooldownTicks);
            }
            Assert.Equal(60, first.TelegraphTicks);
            Assert.Equal(42, next.TelegraphTicks);
            Assert.Equal(1, tc.Steps[0].Repeat);
            Assert.Equal(2, tc.Steps[1].Repeat);                          // 2·3번째
            Assert.True(tc.Steps[1].TelegraphEachRepeat);
            Assert.Equal(Raw(1.5m), tc.Steps[0].ClearHazardMargin.Raw);   // 3.4 예고 띠 양옆 1.5칸
            Assert.Equal(Raw(1.5m), tc.Steps[1].ClearHazardMargin.Raw);

            // 3.2 발구르기: 0.9초 + 반경 2 충격(피해 2) + 12방향 고리 탄(속도 5, 피해 1), 빈틈 0.8초
            BossPatternDef st = Pattern(b, Key.Stomp);
            Assert.Equal(Graze.Bullet, st.Graze);
            Assert.Equal(48, st.RecoveryTicks);
            AttackDef shock = Attack(st.Steps[0]);
            Assert.Equal(Kind.Zone, shock.Kind);
            Assert.Equal(Shape.Circle, shock.Shape);
            Assert.Equal(Raw(2.0m), shock.Radius.Raw);
            Assert.Equal(54, shock.TelegraphTicks);
            Assert.Equal(2, shock.Damage);
            Assert.Equal(AttackDef.AnchorMode.Self, shock.Anchor);
            AttackDef ring = Attack(st.Steps[1]);
            Assert.Equal(Kind.RingShot, ring.Kind);
            Assert.Equal(1, ring.Damage);
            Assert.Equal(Source.Projectile, ring.Source);
            Assert.Equal(PerTick(5m), ring.Proj!.SpeedPerTick.Raw);
            Assert.Equal(12, ring.Proj.Count);
            Assert.Equal(3000, ring.Proj.SpreadCenti);
            Assert.Equal(Raw(0.18m), ring.Proj.HitRadius.Raw);           // 2.4 대 탄 0.18
            Assert.True(st.Steps[1].RapidFire);                          // 2.12 2회차 두 번째 고리
            Assert.Equal(12, st.Steps[1].MinIntervalTicks);

            // 3.2·3.4 낙석: 반경 1.5 원 6개(1개 플레이어 위치), 1.0초, 피해 1, 포물선, 바위 4초 장애물
            BossPatternDef rf = Pattern(b, Key.Rockfall);
            Assert.Equal(Graze.Zone, rf.Graze);
            Assert.Equal(0, rf.RecoveryTicks);
            Step rs = rf.Steps[0];
            AttackDef rock = Attack(rs);
            Assert.Equal(Kind.Lob, rock.Kind);
            Assert.True(rock.Lob);
            Assert.Equal(Shape.Circle, rock.Shape);
            Assert.Equal(Raw(1.5m), rock.Radius.Raw);
            Assert.Equal(60, rock.TelegraphTicks);
            Assert.Equal(1, rock.Damage);
            Assert.Equal(AttackDef.AnchorMode.PlayerPosAtStart, rock.Anchor);
            Assert.Equal(6, rs.Count);
            Assert.NotNull(rs.Placement);
            Assert.Equal(1, rs.Placement!.AtPlayer);
            Assert.Equal(Raw(3.0m), rs.Placement.MinPlayerDistance.Raw); // 3.4 [임시] 반지름 합
            Assert.Null(rs.Placement.MaxPlayerDistance);
            Assert.Equal(Raw(4.0m), rs.Placement.MinSpacing.Raw);        // 3.4 [임시] 가장자리 사이 1.0
            Assert.Equal(Raw(1.5m), rs.Placement.WallMargin.Raw);        // 3.4 [임시]
            Assert.Equal(32, rs.Placement.MaxTries);                     // 3.4 [임시]
            Assert.NotNull(rs.Obstacle);
            Assert.Equal(BossPatternDef.ObstacleKind.Boulder, rs.Obstacle!.Kind);
            Assert.Equal(Raw(0.5m), rs.Obstacle.Radius.Raw);             // 3.4 [임시]
            Assert.Equal(240, rs.Obstacle.DurationTicks);
            Assert.True(rs.Obstacle.BlocksPlayerShots);
            Assert.True(rs.Obstacle.BlocksEnemyShots);
            Assert.Equal(PerTick(6m), rs.Obstacle.Shards!.SpeedPerTick.Raw);   // 3.4 파편 6발 속도 6, 60도
            Assert.Equal(6, rs.Obstacle.Shards.Count);
            Assert.Equal(6000, rs.Obstacle.Shards.SpreadCenti);

            // 2.6 2회차: 발구르기 고리 두 겹(두 번째는 0.5초 뒤, 15도 어긋남)
            Assert.Equal(3, b.SecondCycle.Length);
            AssertOverride(b.SecondCycle[0], Key.Stomp, 1, Ov.Repeat, 2);
            AssertOverride(b.SecondCycle[1], Key.Stomp, 1, Ov.RepeatIntervalTicks, 30);
            AssertOverride(b.SecondCycle[2], Key.Stomp, 1, Ov.RepeatAngleCenti, 1500);

            Assert.Null(b.PhantomCopy);
            Assert.Null(b.FirstEncounter);
            Assert.Null(b.Body);
        }

        private static Key[] KeysOf(BossDef b)
        {
            var keys = new Key[b.Patterns.Length];
            for (int i = 0; i < keys.Length; i++)
            {
                keys[i] = b.Patterns[i].Key;
            }
            return keys;
        }

        // ---- 4절 스승의 허상, 9절 첫 만남 ----

        [Fact]
        public void MirrorPhantom_PhasesAndPatterns_MatchSpec06_4()
        {
            BossDef b = Boss(BossId.MirrorPhantom);
            // 4.3 모델링: 복사 사격·졸개 부르기는 상시. 2단계 따라 쓰기, 3단계 따라 쓰기 + 만개 흉내
            var passives = new[] { Key.CopyFire, Key.SummonSlimes };
            AssertPhase(b.Phases[0], new Key[0], passives, EnemyTag.Boss);
            AssertPhase(b.Phases[1], new[] { Key.MimicArt }, passives, EnemyTag.Boss);
            AssertPhase(b.Phases[2], new[] { Key.MimicArt, Key.BloomMimic }, passives, EnemyTag.Boss);
            Assert.Equal(new[] { Key.CopyFire, Key.SummonSlimes, Key.MimicArt, Key.BloomMimic }, KeysOf(b));

            BossPatternDef cf = Pattern(b, Key.CopyFire);
            Assert.True(cf.IsPassive);
            Assert.Equal(Graze.Bullet, cf.Graze);
            Assert.Equal(StepKind.Copy, cf.Steps[0].Kind);
            Assert.Empty(cf.Steps[0].Copy!.Arts);                        // 트리 1회
            Assert.Equal(ArtId.None, cf.Steps[0].Copy!.Fallback);
            Assert.Equal(0, cf.Steps[0].Repeat);                          // 상시
            Assert.True(cf.Steps[0].RapidFire);                          // 2.12 연사형
            Assert.Equal(10, cf.Steps[0].MinIntervalTicks);              // 4.4 하한 10틱

            // 4.2 30초마다 이끼 슬라임 6마리
            BossPatternDef ss = Pattern(b, Key.SummonSlimes);
            Assert.True(ss.IsPassive);
            Assert.Equal(Graze.None, ss.Graze);
            Assert.Equal(StepKind.Summon, ss.Steps[0].Kind);
            Assert.Equal(EnemyTypeId.SlimeMoss, ss.Steps[0].Summon!.Enemy);
            Assert.Equal(6, ss.Steps[0].Summon!.Count);
            Assert.Equal(0, ss.Steps[0].Repeat);
            Assert.Equal(1800, ss.Steps[0].RepeatIntervalTicks);

            // 4.3·4.4 따라 쓰기: ③ 부채 전개, ④ 양익 사격, ⑧ 팔방 전개, 없으면 ① 마력 방출. 0.8초 전 화살표, 빈틈 0 [임시]
            BossPatternDef ma = Pattern(b, Key.MimicArt);
            Assert.False(ma.IsPassive);
            Assert.Equal(new[] { ArtId.Fan, ArtId.Wings, ArtId.Octastar }, ma.Steps[0].Copy!.Arts);
            Assert.Equal(ArtId.Burst, ma.Steps[0].Copy!.Fallback);
            Assert.Equal(48, ma.Steps[0].Copy!.WarnTicks);
            Assert.Equal(0, ma.RecoveryTicks);

            // 4.3 만개 흉내: 예고 1.2초 → 팔방 전개 0.5초 간격 3회 → 2.0초 빈틈
            BossPatternDef bm = Pattern(b, Key.BloomMimic);
            Assert.Equal(new[] { ArtId.Octastar }, bm.Steps[0].Copy!.Arts);
            Assert.Equal(72, bm.Steps[0].Copy!.WarnTicks);
            Assert.Equal(3, bm.Steps[0].Repeat);
            Assert.Equal(30, bm.Steps[0].RepeatIntervalTicks);
            Assert.Equal(120, bm.RecoveryTicks);

            // 4.3 이동 [임시]: 거리 5칸 유지, 벽에서 1.5칸 안쪽
            Assert.Equal(Raw(5m), b.Movement.KeepDistance.Raw);
            Assert.Equal(Raw(1.5m), b.Movement.WallMargin.Raw);

            // 2.6 2회차 강화는 변이 복사(PhantomCopy.CopyMutationsFromCycle)라 덮어쓰기가 없다
            Assert.Empty(b.SecondCycle);
            Assert.Null(b.Body);
        }

        [Fact]
        public void MirrorPhantom_CopyRules_MatchSpec06_4_4()
        {
            BossDef.PhantomCopyDef? pc = Boss(BossId.MirrorPhantom).PhantomCopy;
            Assert.NotNull(pc);
            Assert.Equal(2500, pc!.IntervalPermille);                    // 플레이어 간격 × 2.5
            Assert.Equal(12, pc.MaxProjectiles);                         // 발사 1회 최대 12개
            Assert.Equal(PerTick(6m), pc.SpeedPerTick.Raw);              // 속도 6
            Assert.Equal(1, pc.Damage);                                  // 피해 1
            Assert.Equal(1, pc.BounceCap);                               // 벽 튕김 1회
            Assert.Equal(600, pc.ExplosionRadiusPermille);               // 폭발 반경 60%
            Assert.Equal(Raw(1.5m), pc.ExplosionRadiusMax.Raw);          // 최대 1.5칸
            Assert.Equal(48, pc.ExplosionTelegraphTicks);                // 0.8초 예고 원
            Assert.Equal(1, pc.ExplosionDamage);                         // [임시]
            Assert.Equal(36, pc.ChainLineTelegraphTicks);                // 연쇄 번개 → 0.6초 조준 예고선
            Assert.Equal(Raw(0.5m), pc.ChainLineWidth.Raw);              // [임시: 16절]
            Assert.Equal(Raw(7m), pc.ChainLineLength.Raw);               // [임시: 16절]
            Assert.Equal(1910, pc.SplitMinAngleCenti);                   // 1/3 rad = 19.099도 → 올림
            Assert.Equal(2, pc.CopyMutationsFromCycle);                  // 2회차(웨이브 80)부터 변이 복사
        }

        [Fact]
        public void MirrorPhantom_FirstEncounter_MatchesSpec06_9()
        {
            BossDef.FirstEncounterDef? fe = Boss(BossId.MirrorPhantom).FirstEncounter;
            Assert.NotNull(fe);
            Assert.Equal(50, fe!.HpFloorPercent);                        // 9.2 체력 바 50%에서 멈춘다
            Assert.Equal(2700, fe.MaxFightTicks);                        // 9.2 최대 45초
            Assert.Equal(5, fe.RageStacks);                              // 9.2 5스택으로 시작
            Assert.Equal(3, fe.StartPhase);                              // 9.2 phase = 3
            Assert.Equal(180, fe.FinisherTicks);                         // 9.5 되비침 3.0초
            Assert.Equal(114, fe.FinisherHpZeroTick);                    // 12절 114 체력 칸이 깨짐
            Assert.Equal(240, fe.CollapseTicks);                         // 9.6 쓰러짐 4.0초
            // 9.2 대기 45틱 = 1.5초 × 0.5(5스택)
            BossDef.CommonDef c = Boss(BossId.MirrorPhantom).Common;
            Assert.Equal(45, c.WaitTicks * (1000 - c.RageStepPermille * fe.RageStacks) / 1000);
        }

        // ---- 5절 천 갓 ----

        [Fact]
        public void SporeQueen_PhasesAndPatterns_MatchSpec06_5()
        {
            BossDef b = Boss(BossId.SporeQueen);
            // 5.3: 1단계 보스 + 뿌리(중앙 반경 1칸 [임시]), 2단계 보스, 고리 4겹 + 도약
            AssertPhase(b.Phases[0], new[] { Key.SporeRing, Key.SporeCloud, Key.SummonSprouts }, new Key[0], EnemyTag.Boss | EnemyTag.Rooted);
            AssertPhase(b.Phases[1], new[] { Key.SporeRing, Key.SporeCloud, Key.SummonSprouts, Key.Leap }, new Key[0], EnemyTag.Boss);
            Assert.Equal(Raw(1m), b.Phases[0].AnchorRadius.Raw);
            Assert.Equal(0L, b.Phases[1].AnchorRadius.Raw);
            Assert.Single(b.Phases[1].Overrides);
            AssertOverride(b.Phases[1].Overrides[0], Key.SporeRing, 0, Ov.Repeat, 4);

            // 5.2 포자 고리: 0.7초 → 16방향 3겹(0.6초 간격, 11.25도 어긋남), 속도 4, 빈틈 1.5초
            BossPatternDef sr = Pattern(b, Key.SporeRing);
            Assert.Equal(Graze.Bullet, sr.Graze);
            Assert.Equal(90, sr.RecoveryTicks);
            AttackDef ring = Attack(sr.Steps[0]);
            Assert.Equal(Kind.RingShot, ring.Kind);
            Assert.Null(ring.Shape);
            Assert.Equal(42, ring.CastTicks);
            Assert.Equal(0, ring.TelegraphTicks);
            Assert.Equal(PerTick(4m), ring.Proj!.SpeedPerTick.Raw);
            Assert.Equal(16, ring.Proj.Count);
            Assert.Equal(2250, ring.Proj.SpreadCenti);
            Assert.Equal(1, ring.Damage);
            Assert.Equal(3, sr.Steps[0].Repeat);
            Assert.Equal(36, sr.Steps[0].RepeatIntervalTicks);
            Assert.Equal(1125, sr.Steps[0].RepeatAngleCenti);
            Assert.True(sr.Steps[0].RapidFire);
            Assert.Equal(15, sr.Steps[0].MinIntervalTicks);

            // 5.2 포자 구름: 반경 2.0 원 3개, 1.0초, 판정 1 [임시], 5초 남는 안개, 동시에 6개까지
            BossPatternDef sc = Pattern(b, Key.SporeCloud);
            Assert.Equal(Graze.Zone, sc.Graze);
            AttackDef cloud = Attack(sc.Steps[0]);
            Assert.Equal(Shape.Circle, cloud.Shape);
            Assert.Equal(Raw(2.0m), cloud.Radius.Raw);
            Assert.Equal(60, cloud.TelegraphTicks);
            Assert.Equal(1, cloud.Damage);
            Assert.Equal(3, sc.Steps[0].Count);
            Assert.Equal(AttackDef.HazardKind.SporeCloud, cloud.LeavesHazard!.Kind);
            Assert.Equal(300, cloud.LeavesHazard.DurationTicks);
            Assert.Equal(6, cloud.LeavesHazard.MaxCount);
            Assert.Equal(1, sc.Steps[0].Placement!.AtPlayer);            // 5.4 [임시] 1개는 플레이어 위치
            Assert.Equal(Raw(4.0m), sc.Steps[0].Placement!.MinSpacing.Raw);

            // 5.2 새싹: 금 4곳 1.0초 → 갓버섯 새싹 4마리, 8마리까지
            BossPatternDef sp = Pattern(b, Key.SummonSprouts);
            Assert.Equal(Graze.None, sp.Graze);
            Assert.Equal(EnemyTypeId.SporeSeedling, sp.Steps[0].Summon!.Enemy);
            Assert.Equal(4, sp.Steps[0].Summon!.Count);
            Assert.Equal(8, sp.Steps[0].Summon!.FieldCap);
            Assert.Equal(60, sp.Steps[0].Summon!.WarnTicks);

            // 5.2 도약: 그림자 원 반경 3.0, 1.2초 → 착지 2 + 8방향 탄(속도 5), 빈틈 1.5초
            BossPatternDef lp = Pattern(b, Key.Leap);
            Assert.Equal(Graze.Zone | Graze.Bullet, lp.Graze);
            Assert.Equal(90, lp.RecoveryTicks);
            AttackDef land = Attack(lp.Steps[0]);
            Assert.Equal(Raw(3.0m), land.Radius.Raw);
            Assert.Equal(72, land.TelegraphTicks);
            Assert.Equal(2, land.Damage);
            Assert.Equal(AttackDef.AnchorMode.PlayerPosAtStart, land.Anchor);
            AttackDef burst = Attack(lp.Steps[1]);
            Assert.Equal(8, burst.Proj!.Count);
            Assert.Equal(4500, burst.Proj.SpreadCenti);
            Assert.Equal(PerTick(5m), burst.Proj.SpeedPerTick.Raw);

            // 2.6 2회차: 포자 구름이 0.8칸/초로 흐른다
            Assert.Single(b.SecondCycle);
            AssertOverride(b.SecondCycle[0], Key.SporeCloud, 0, Ov.HazardDriftPerTickRaw, PerTick(0.8m));
        }

        // ---- 6절 밤눈 ----

        [Fact]
        public void StormOwl_PhasesAndPatterns_MatchSpec06_6()
        {
            BossDef b = Boss(BossId.StormOwl);
            AssertPhase(b.Phases[0], new[] { Key.Glide, Key.FeatherFan, Key.LightningStrike }, new Key[0], EnemyTag.Boss | EnemyTag.Flying);
            AssertPhase(b.Phases[1], new[] { Key.Glide, Key.FeatherFan, Key.LightningStrike, Key.LightningPlow }, new Key[0], EnemyTag.Boss | EnemyTag.Flying);
            Assert.Single(b.Phases[1].Overrides);
            AssertOverride(b.Phases[1].Overrides[0], Key.Glide, 0, Ov.Count, 2);            // 6.3 두 줄 동시, 십자

            // 6.2 활강: 띠 폭 2.0, 0.9초, 3회 연속, 피해 2, 세 번째 뒤 2.0초
            BossPatternDef gl = Pattern(b, Key.Glide);
            Assert.Equal(Graze.Charge, gl.Graze);
            Assert.Equal(120, gl.RecoveryTicks);
            AttackDef g = Attack(gl.Steps[0]);
            Assert.Equal(Kind.Dash, g.Kind);
            Assert.Equal(Shape.Band, g.Shape);
            Assert.Equal(Raw(2.0m), g.Width.Raw);
            Assert.Equal(54, g.TelegraphTicks);
            Assert.Equal(2, g.Damage);
            Assert.Equal(1600, g.DashSpeedCenti);                       // 6.4 [임시] 16칸/초
            Assert.Equal(3, gl.Steps[0].Repeat);
            Assert.True(gl.Steps[0].TelegraphEachRepeat);

            // 6.2 깃털 부채: 0.5초 → 5갈래(20도, 속도 7) 0.4초 간격 3번, 빈틈 0.8초
            BossPatternDef ff = Pattern(b, Key.FeatherFan);
            Assert.Equal(Graze.Bullet, ff.Graze);
            Assert.Equal(48, ff.RecoveryTicks);
            AttackDef fan = Attack(ff.Steps[0]);
            Assert.Equal(Kind.Spread, fan.Kind);
            Assert.Equal(30, fan.CastTicks);
            Assert.Equal(PerTick(7m), fan.Proj!.SpeedPerTick.Raw);
            Assert.Equal(5, fan.Proj.Count);
            Assert.Equal(2000, fan.Proj.SpreadCenti);
            Assert.Equal(3, ff.Steps[0].Repeat);
            Assert.Equal(24, ff.Steps[0].RepeatIntervalTicks);
            Assert.True(ff.Steps[0].RapidFire);
            Assert.Equal(9, ff.Steps[0].MinIntervalTicks);

            // 6.2 낙뢰: 반경 1.5, 각 0.8초, 0.4초 간격 4번, 피해 1(예고 구역이라 격노로 줄지 않는다)
            BossPatternDef ls = Pattern(b, Key.LightningStrike);
            Assert.Equal(Graze.Zone, ls.Graze);
            AttackDef strike = Attack(ls.Steps[0]);
            Assert.Equal(Raw(1.5m), strike.Radius.Raw);
            Assert.Equal(48, strike.TelegraphTicks);
            Assert.Equal(1, strike.Damage);
            Assert.Equal(4, ls.Steps[0].Repeat);
            Assert.Equal(24, ls.Steps[0].RepeatIntervalTicks);
            Assert.False(ls.Steps[0].RapidFire);

            // 6.2·6.4 번개 밭갈이: 폭 2 띠 7줄, 1.0초, 0.8초 뒤 나머지 줄, 피해 2, 빈틈 1.0초
            BossPatternDef lp = Pattern(b, Key.LightningPlow);
            Assert.Equal(60, lp.RecoveryTicks);
            Assert.Equal(2, lp.Steps.Length);
            foreach (Step s in lp.Steps)
            {
                AttackDef a = Attack(s);
                Assert.Equal(Shape.Band, a.Shape);
                Assert.Equal(Raw(2.0m), a.Width.Raw);
                Assert.Equal(60, a.TelegraphTicks);
                Assert.Equal(2, a.Damage);
                Assert.Equal(7, s.Count);
                Assert.False(s.RapidFire);
            }
            Assert.Equal(0, lp.Steps[0].StartDelayTicks);
            Assert.Equal(48, lp.Steps[1].StartDelayTicks);

            // 2.6 2회차: 낙뢰 6번
            Assert.Single(b.SecondCycle);
            AssertOverride(b.SecondCycle[0], Key.LightningStrike, 0, Ov.Repeat, 6);
        }

        // ---- 7절 흰뿔 ----

        [Fact]
        public void FrostGoat_PhasesAndPatterns_MatchSpec06_7()
        {
            BossDef b = Boss(BossId.FrostGoat);
            AssertPhase(b.Phases[0], new[] { Key.IcePillar, Key.HornSwing, Key.FrostBreath, Key.HornCharge }, new Key[0], EnemyTag.Boss);
            AssertPhase(b.Phases[1], new[] { Key.IcePillar, Key.HornSwing, Key.FrostBreath, Key.HornCharge, Key.FrostField }, new Key[0], EnemyTag.Boss);
            Assert.Single(b.Phases[1].Overrides);
            AssertOverride(b.Phases[1].Overrides[0], Key.FrostBreath, 0, Ov.SweepCenti, 12000);   // 7.3 120도

            // 7.2·7.7 얼음 기둥: 반경 1.0 원 4개, 0.8초, 피해 1, 체력 80, 6개까지, 깨지면 8방향(속도 6)
            BossPatternDef ip = Pattern(b, Key.IcePillar);
            Assert.Equal(Graze.Zone, ip.Graze);
            AttackDef pillar = Attack(ip.Steps[0]);
            Assert.Equal(Raw(1.0m), pillar.Radius.Raw);
            Assert.Equal(48, pillar.TelegraphTicks);
            Assert.Equal(1, pillar.Damage);
            Assert.Equal(4, ip.Steps[0].Count);
            BossPatternDef.ObstacleDef ob = ip.Steps[0].Obstacle!;
            Assert.Equal(BossPatternDef.ObstacleKind.IcePillar, ob.Kind);
            Assert.Equal(80, ob.Hp);
            Assert.Equal(6, ob.MaxCount);
            Assert.Equal(0, ob.DurationTicks);
            Assert.Equal(Raw(1.0m), ob.Radius.Raw);                      // 7.7 [임시]
            Assert.True(ob.BlocksPlayerShots && ob.BlocksEnemyShots);
            Assert.Equal(8, ob.Shards!.Count);
            Assert.Equal(4500, ob.Shards.SpreadCenti);
            Assert.Equal(PerTick(6m), ob.Shards.SpeedPerTick.Raw);
            BossPatternDef.PlacementDef pl = ip.Steps[0].Placement!;     // 7.7 [임시] 거리 2~5칸, 서로 ≥ 3.0
            Assert.Equal(0, pl.AtPlayer);
            Assert.Equal(Raw(2m), pl.MinPlayerDistance.Raw);
            Assert.Equal(Raw(5m), pl.MaxPlayerDistance!.Value.Raw);
            Assert.Equal(Raw(3.0m), pl.MinSpacing.Raw);

            // 7.2·7.6 뿔 휘두르기: 90도 부채꼴 길이 5, 1.0초(서리밭 안 1.2초), 피해 2, 빈틈 1.0초
            BossPatternDef hs = Pattern(b, Key.HornSwing);
            AttackDef swing = Attack(hs.Steps[0]);
            Assert.Equal(Shape.Fan, swing.Shape);
            Assert.Equal(9000, swing.FanCenti);
            Assert.Equal(Raw(5m), swing.Radius.Raw);
            Assert.Equal(60, swing.TelegraphTicks);
            Assert.Equal(72, hs.Steps[0].FrostTelegraphTicks);
            Assert.Equal(2, swing.Damage);
            Assert.Equal(60, hs.RecoveryTicks);

            // 7.2·7.5 서리 숨결: 0.8초 → 90도를 2.0초(초당 45도), 초당 4발(15틱), 속도 5.5, 사거리 8, 빈틈 1.2초
            BossPatternDef fb = Pattern(b, Key.FrostBreath);
            Assert.Equal(Graze.Bullet, fb.Graze);
            Assert.Equal(72, fb.RecoveryTicks);
            AttackDef breath = Attack(fb.Steps[0]);
            Assert.Equal(48, breath.CastTicks);
            Assert.Equal(PerTick(5.5m), breath.Proj!.SpeedPerTick.Raw);
            Assert.Equal(Raw(8m), breath.Proj.Range.Raw);
            BossPatternDef.StreamDef stream = fb.Steps[0].Stream!;
            Assert.Equal(15, stream.IntervalTicks);
            Assert.Equal(9000, stream.SweepCenti);
            Assert.Equal(75, stream.SweepCentiPerTick);                  // 750 mdeg/틱
            Assert.Equal(120, stream.SweepCenti / stream.SweepCentiPerTick);
            Assert.Equal(160, 12000 / stream.SweepCentiPerTick);         // 7.3 2단계 2.67초 = 160틱
            Assert.True(fb.Steps[0].RapidFire);
            Assert.Equal(11, fb.Steps[0].MinIntervalTicks);              // 2.12 회전 줄기 R3 하한

            // 7.2 뿔 돌진: 띠 폭 1.6, 0.9초, 피해 2, 스침 돌진·탄, 벽에 닿고 1.5초
            BossPatternDef hc = Pattern(b, Key.HornCharge);
            Assert.Equal(Graze.Charge | Graze.Bullet, hc.Graze);
            Assert.Equal(90, hc.RecoveryTicks);
            AttackDef charge = Attack(hc.Steps[0]);
            Assert.Equal(Kind.Dash, charge.Kind);
            Assert.Equal(Raw(1.6m), charge.Width.Raw);
            Assert.Equal(54, charge.TelegraphTicks);
            Assert.Equal(2, charge.Damage);
            Assert.Equal(1600, charge.DashSpeedCenti);                  // [임시] 원문 없음

            // 7.2 서리밭: 반경 2.5 원 3개, 1.0초, 8초, 피해 0, 이동 −30%
            BossPatternDef ff = Pattern(b, Key.FrostField);
            Assert.Equal(Graze.None, ff.Graze);
            AttackDef field = Attack(ff.Steps[0]);
            Assert.Equal(Raw(2.5m), field.Radius.Raw);
            Assert.Equal(60, field.TelegraphTicks);
            Assert.Equal(0, field.Damage);
            Assert.Equal(3, ff.Steps[0].Count);
            Assert.Equal(AttackDef.HazardKind.FrostField, field.LeavesHazard!.Kind);
            Assert.Equal(480, field.LeavesHazard.DurationTicks);
            Assert.Equal(700, ff.Steps[0].HazardSpeedPermille);

            // 2.6 2회차: 기둥 파편 12방향
            Assert.Equal(2, b.SecondCycle.Length);
            AssertOverride(b.SecondCycle[0], Key.IcePillar, 0, Ov.ShardCount, 12);
            AssertOverride(b.SecondCycle[1], Key.IcePillar, 0, Ov.ShardSpreadCenti, 3000);
        }

        // ---- 8절 밤삼킴 ----

        [Fact]
        public void NightSerpent_PhasesAndPatterns_MatchSpec06_8()
        {
            BossDef b = Boss(BossId.NightSerpent);
            AssertPhase(b.Phases[0], new[] { Key.Coil, Key.DarkSpit, Key.Burrow }, new Key[0], EnemyTag.Boss);
            AssertPhase(b.Phases[1], new[] { Key.Coil, Key.DarkSpit, Key.Burrow }, new[] { Key.ScaleVolley }, EnemyTag.Boss);
            // 8.3·8.4 2단계 고리 반경 4.0(폭 1.0 → 바깥 4.5, 안 3.5)
            Assert.Equal(2, b.Phases[1].Overrides.Length);
            AssertOverride(b.Phases[1].Overrides[0], Key.Coil, 0, Ov.RadiusRaw, Raw(4.5m));
            AssertOverride(b.Phases[1].Overrides[1], Key.Coil, 0, Ov.InnerRadiusRaw, Raw(3.5m));

            // 8.2·8.4 또아리: 반경 4.5 고리 예고 1.2초 → 4초 가둠(몸 접촉 1) → 무는 띠 폭 1.5, 0.8초, 물기 2 → 머리 1.5초 멈춤
            BossPatternDef co = Pattern(b, Key.Coil);
            Assert.Equal(Graze.Charge, co.Graze);
            Assert.Equal(90, co.RecoveryTicks);
            AttackDef ring = Attack(co.Steps[0]);
            Assert.Equal(Shape.Ring, ring.Shape);
            Assert.Equal(Raw(5.0m), ring.Radius.Raw);
            Assert.Equal(Raw(4.0m), ring.InnerRadius.Raw);
            Assert.Equal(72, ring.TelegraphTicks);
            Assert.Equal(240, ring.ActiveTicks);
            Assert.Equal(1, ring.Damage);
            Assert.Equal(Source.Lingering, ring.Source);
            Assert.Equal(650, ring.EscapeOverrideMilli);                 // [임시] 고리 폭의 절반 + 피격 반지름
            AttackDef bite = Attack(co.Steps[1]);
            Assert.Equal(Kind.Dash, bite.Kind);
            Assert.Equal(Raw(1.5m), bite.Width.Raw);
            Assert.Equal(48, bite.TelegraphTicks);
            Assert.Equal(2, bite.Damage);

            // 8.2·8.4 어둠 침: 조준선 0.7초(마지막 0.4초 고정) → 3발 0.5초 간격, 속도 9, 빈틈 0.6초
            BossPatternDef ds = Pattern(b, Key.DarkSpit);
            Assert.Equal(36, ds.RecoveryTicks);
            AttackDef spit = Attack(ds.Steps[0]);
            Assert.Equal(Kind.AimedShot, spit.Kind);
            Assert.Equal(Shape.AimLine, spit.Shape);
            Assert.Equal(42, spit.TelegraphTicks);
            Assert.Equal(AttackDef.LockMode.LastNTicks, spit.Lock);
            Assert.Equal(24, spit.LockTicks);
            Assert.Equal(PerTick(9m), spit.Proj!.SpeedPerTick.Raw);
            Assert.Equal(3, ds.Steps[0].Repeat);
            Assert.Equal(30, ds.Steps[0].RepeatIntervalTicks);
            Assert.True(ds.Steps[0].RapidFire);
            Assert.Equal(7, ds.Steps[0].MinIntervalTicks);

            // 8.2 잠행: 반경 2.0, 각 0.9초, 3번, 피해 2, 세 번째에 머리 2.0초
            BossPatternDef bu = Pattern(b, Key.Burrow);
            Assert.Equal(Graze.Zone, bu.Graze);
            Assert.Equal(120, bu.RecoveryTicks);
            AttackDef rise = Attack(bu.Steps[0]);
            Assert.Equal(Raw(2.0m), rise.Radius.Raw);
            Assert.Equal(54, rise.TelegraphTicks);
            Assert.Equal(2, rise.Damage);
            Assert.Equal(3, bu.Steps[0].Repeat);
            Assert.True(bu.Steps[0].TelegraphEachRepeat);

            // 8.2·8.4 비늘 탄(2단계 상시): 깜박 0.6초 → 마디 10개가 1발씩(속도 4), 1.5초 간격
            BossPatternDef sv = Pattern(b, Key.ScaleVolley);
            Assert.True(sv.IsPassive);
            AttackDef scale = Attack(sv.Steps[0]);
            Assert.Equal(36, scale.CastTicks);
            Assert.Equal(PerTick(4m), scale.Proj!.SpeedPerTick.Raw);
            Assert.Equal(10, sv.Steps[0].Count);
            Assert.Equal(0, sv.Steps[0].Repeat);
            Assert.Equal(90, sv.Steps[0].RepeatIntervalTicks);
            Assert.True(sv.Steps[0].RapidFire);
            Assert.Equal(15, sv.Steps[0].MinIntervalTicks);

            // 8.1 몸: 머리 0.75, 마디·꼬리 0.5 [임시], 마디 10, 간격 1.0 [임시], 마디 ×0.5, 꼬리 = 마디 [임시], 한 발당 3마디
            BossDef.BodyDef body = b.Body!;
            Assert.Equal(Raw(0.75m), body.HeadRadius.Raw);
            Assert.Equal(Raw(0.5m), body.SegmentRadius.Raw);
            Assert.Equal(Raw(0.5m), body.TailRadius.Raw);
            Assert.Equal(10, body.SegmentCount);
            Assert.Equal(Raw(1.0m), body.SegmentSpacing.Raw);
            Assert.Equal(500, body.SegmentDamagePermille);
            Assert.Equal(500, body.TailDamagePermille);
            Assert.Equal(3, body.MaxSegmentHits);

            // 2.6 2회차: 어둠 침 5발
            Assert.Single(b.SecondCycle);
            AssertOverride(b.SecondCycle[0], Key.DarkSpit, 0, Ov.Repeat, 5);
        }

        // ---- spec 06 2.12 연사형 패턴 표 ----

        // 열: 보스, 패턴, 단계, 줄어드는 기본 간격(틱), R3 하한(틱)
        public static IEnumerable<object[]> Spec06RapidFireTable()
        {
            yield return new object[] { BossId.StormOwl, Key.FeatherFan, 0, 24, 9 };
            yield return new object[] { BossId.FrostGoat, Key.FrostBreath, 0, 15, 11 };
            yield return new object[] { BossId.NightSerpent, Key.DarkSpit, 0, 30, 7 };
            yield return new object[] { BossId.NightSerpent, Key.ScaleVolley, 0, 90, 15 };
            yield return new object[] { BossId.SporeQueen, Key.SporeRing, 0, 36, 15 };
            yield return new object[] { BossId.KilnBoar, Key.Stomp, 1, 30, 12 };    // 2회차 두 번째 고리(간격은 2회차 덮어쓰기)
        }

        [Theory]
        [MemberData(nameof(Spec06RapidFireTable))]
        public void RapidFire_Row_MatchesSpec06_2_12(BossId id, Key key, int step, int baseTicks, int minTicks)
        {
            BossDef b = Boss(id);
            Step s = Pattern(b, key).Steps[step];
            Assert.True(s.RapidFire);
            Assert.Equal(minTicks, s.MinIntervalTicks);
            int interval = s.Stream != null ? s.Stream.IntervalTicks : s.RepeatIntervalTicks;
            foreach (BossDef.PatternOverride o in b.SecondCycle)
            {
                if (o.Pattern == key && o.Step == step && o.Key == Ov.RepeatIntervalTicks)
                {
                    interval = (int)o.Value;
                }
            }
            Assert.Equal(baseTicks, interval);
        }

        [Fact]
        public void RapidFire_OnlyTheSpecTableAndPhantomCopy()
        {
            // 2.12: 위 표의 여섯 줄 + 허상 복사 사격만 연사형이다. 예고 구역을 잇달아 까는 패턴(낙뢰·밭갈이)은 아니다
            int n = 0;
            foreach (BossId id in AllBosses())
            {
                foreach (BossPatternDef p in Boss(id).Patterns)
                {
                    foreach (Step s in p.Steps)
                    {
                        if (s.RapidFire)
                        {
                            n++;
                            Assert.True(s.MinIntervalTicks > 0);
                        }
                        else
                        {
                            Assert.Equal(0, s.MinIntervalTicks);
                        }
                    }
                }
            }
            Assert.Equal(7, n);
        }

        [Fact]
        public void TelegraphedDamage_IsAtMostTwo_BeforeGrowth()
        {
            // 2.7: 종별 값은 1 또는 2. 웨이브 성장 +1 과 최종 격노 +1 을 더해도 상한 3(성장과 상한은 waves.json 의 몫)
            foreach (BossId id in AllBosses())
            {
                foreach (BossPatternDef p in Boss(id).Patterns)
                {
                    foreach (Step s in p.Steps)
                    {
                        if (s.Attack != null)
                        {
                            Assert.InRange(s.Attack.Damage, 0, 2);
                        }
                    }
                }
            }
        }

        [Fact]
        public void Pack_IsByteIdentical_AcrossBuilds()
        {
            Assert.Equal(BuildPack(TestRepo.Root), BuildPack(TestRepo.Root));
        }
    }
}
