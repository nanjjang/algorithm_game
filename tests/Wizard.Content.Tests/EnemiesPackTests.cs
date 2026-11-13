using System;
using System.Collections.Generic;
using Wizard.Content.Tests.Pack;
using Wizard.Core;
using Wizard.DataBuild.Core;
using Wizard.TestKit;
using Xunit;
using Kind = Wizard.Content.AttackDef.AttackKind;
using Shape = Wizard.Content.AttackDef.TelegraphShape;
using Source = Wizard.Content.AttackDef.DamageSource;

namespace Wizard.Content.Tests
{
    // data/sim/enemies.json → 팩 절 5 → ContentDb.Enemy 의 왕복(M3-DAT-01: 원형 4종, M8-DAT-01: 신규 8종과 졸개).
    // 기대값의 출처: spec 05 2.5 표(체력·속도·피해·비용·첫 등장·시간대·약점·저항·꼬리표, 원형 4종 [확정: G 11], 신규 8종 [제안]),
    // spec 05 4.1~4.12 의 공격 표와 (나) 상태 기계, spec 14 3.6(id·약점·저항·꼬리표), spec 05 13.1 U-04(몸 반지름)·U-05(재사용 대기)·
    // U-28·U-34·U-36·U-43·U-50·U-52·U-53·U-56·U-58·U-60·U-64 [임시], 졸개는 spec 06 5절(spore_queen.summon_sprouts)·spec 05 10.5.
    // 정수 단위: 거리는 Raw(× 2^32), 각도는 센티도(× 100), 시간은 틱(× 60), 이동 속도는 센티 칸/초, 탄속은 틱당 Raw(× 2^32 ÷ 60).
    // 공격 첨자는 data/sim/enemies.json 의 _note 에 적은 순서다(주 공격이 0, 접촉이 마지막. 선돌지기만 0 내려찍기 1 휩쓸기 2 접촉).
    public sealed class EnemiesPackTests
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

        // ---- spec 05 2.5 표와 한 줄씩 대조 ----

        private const EnemyDef.TimeMask Always = EnemyDef.TimeMask.Always;
        private const EnemyDef.TimeMask DayDusk = EnemyDef.TimeMask.Day | EnemyDef.TimeMask.Dusk;
        private const EnemyDef.TimeMask DuskNight = EnemyDef.TimeMask.Dusk | EnemyDef.TimeMask.Night;

        // 열: id, 역할, 체력, 속도(칸/초 × 100), 피해, 비용, 첫 등장, 시간대, 약점, 저항, 꼬리표, 넉백 면역, 종 상한(0 = 없음), 접촉 피해
        // 종 상한은 spec 05 2.5 아래 목록(M 4.3), 넉백 면역은 중량·뿌리(spec 05 2.2), 접촉 피해는 spec 05 3.6(없는 것은 늑대뿐)
        public static IEnumerable<object[]> Spec05Table25()
        {
            // ① 이끼 슬라임: 20, 3.0, 1, 1, 1, 항상, Fire, Frost, -
            yield return new object[] { EnemyTypeId.SlimeMoss, EnemyDef.EnemyRole.Swarm, 20, 300, 1, 1, 1, Always, Element.Fire, Element.Ice, EnemyTag.None, false, 0, 1 };
            // ② 덤불 늑대: 45, 4.5, 1, 3, 3, 항상(해질녘에 많음), Lightning, Dark, - / 접촉 없음(spec 05 3.6, 4.2 contact_damage = 0)
            yield return new object[] { EnemyTypeId.WolfBramble, EnemyDef.EnemyRole.EvasiveCharger, 45, 450, 1, 3, 3, Always, Element.Lightning, Element.Dark, EnemyTag.None, false, 0, 0 };
            // ③ 선돌지기: 300, 1.8, 2, 6, 5, 항상, Arcane, Lightning, Heavy / 넉백 면역, 종 상한 3(spec 05 2.5, 4.3)
            yield return new object[] { EnemyTypeId.GolemMenhir, EnemyDef.EnemyRole.ArmoredHeavy, 300, 180, 2, 6, 5, Always, Element.Arcane, Element.Lightning, EnemyTag.Heavy, true, 3, 1 };
            // ④ 갓버섯 술사: 40, 2.5, 1, 4, 3, 항상, Frost, Dark, -
            yield return new object[] { EnemyTypeId.ShamanCap, EnemyDef.EnemyRole.RangedCover, 40, 250, 1, 4, 3, Always, Element.Ice, Element.Dark, EnemyTag.None, false, 0, 1 };
            // ⑤ 꽈리 벌레: 18, 3.6, 1, 2, 2, 항상, Frost, Fire, - / 접촉 1(C-14)
            yield return new object[] { EnemyTypeId.BugLantern, EnemyDef.EnemyRole.Suicide, 18, 360, 1, 2, 2, Always, Element.Ice, Element.Fire, EnemyTag.None, false, 0, 1 };
            // ⑥ 솔침 고슴도치: 35, 2.2, 1, 4, 4, 낮·해질녘, Dark, Frost, - / 종 상한 4
            yield return new object[] { EnemyTypeId.HogNeedle, EnemyDef.EnemyRole.Sniper, 35, 220, 1, 4, 4, DayDusk, Element.Dark, Element.Ice, EnemyTag.None, false, 4, 1 };
            // ⑦ 화분 족제비: 55, 3.5(땅속), 1, 3, 6, 항상, Lightning, Fire, Burrowed(둔덕일 때) / 지상 접촉 1(U-48)
            yield return new object[] { EnemyTypeId.WeaselPot, EnemyDef.EnemyRole.Ambush, 55, 350, 1, 3, 6, Always, Element.Lightning, Element.Fire, EnemyTag.Burrowed, false, 0, 1 };
            // ⑧ 해거름 박쥐: 12, 3.6, 1, 1, 7, 해질녘·밤, Fire, Dark, Flying
            yield return new object[] { EnemyTypeId.BatDusk, EnemyDef.EnemyRole.NightSwarm, 12, 360, 1, 1, 7, DuskNight, Element.Fire, Element.Dark, EnemyTag.Flying, false, 0, 1 };
            // ⑨ 도깨비불: 35, 2.0, 1, 4, 8, 밤, Arcane, Lightning, Flying / 종 상한 4
            yield return new object[] { EnemyTypeId.WispStray, EnemyDef.EnemyRole.RingBarrage, 35, 200, 1, 4, 8, EnemyDef.TimeMask.Night, Element.Arcane, Element.Lightning, EnemyTag.Flying, false, 4, 1 };
            // ⑩ 가시덩굴: 70, 0, 1, 4, 11, 낮·해질녘, Fire, Arcane, Rooted / 넉백 면역(뿌리), 종 상한 4
            yield return new object[] { EnemyTypeId.VineThorn, EnemyDef.EnemyRole.ZoneDenial, 70, 0, 1, 4, 11, DayDusk, Element.Fire, Element.Arcane, EnemyTag.Rooted, true, 4, 1 };
            // ⑪ 엄니 멧돼지: 160, 2.4, 2, 5, 13, 낮·해질녘, Frost, Arcane, Heavy / 넉백 면역, 종 상한 3
            yield return new object[] { EnemyTypeId.BoarTusk, EnemyDef.EnemyRole.LongCharger, 160, 240, 2, 5, 13, DayDusk, Element.Ice, Element.Arcane, EnemyTag.Heavy, true, 3, 1 };
            // ⑫ 북두꺼비: 90, 2.0, 1, 5, 15, 항상, Dark, Frost, - / 종 상한 2
            yield return new object[] { EnemyTypeId.ToadDrum, EnemyDef.EnemyRole.Support, 90, 200, 1, 5, 15, Always, Element.Dark, Element.Ice, EnemyTag.None, false, 2, 1 };
        }

        [Theory]
        [MemberData(nameof(Spec05Table25))]
        public void Species_Row_MatchesSpec05Table25(EnemyTypeId id, EnemyDef.EnemyRole role, int hp, int speedCenti, int damage, int cost, int firstWave,
            EnemyDef.TimeMask times, Element weak, Element resist, EnemyTag tags, bool knockbackImmune, int speciesCap, int contactDamage)
        {
            EnemyDef e = RepoDb().Enemy(id);
            Assert.Equal(id, e.Id);
            Assert.Equal(role, e.Role);
            Assert.Equal(hp * OneRaw, e.BaseHp.Raw);
            Assert.Equal(speedCenti, e.MoveSpeedCenti);
            Assert.Equal(damage, e.AttackDamage);
            Assert.Equal(cost, e.Cost);
            Assert.Equal(firstWave, e.FirstWave);
            Assert.Equal(times, e.Times);
            Assert.Equal(weak, e.Weak);
            Assert.Equal(resist, e.Resist);
            Assert.Equal(tags, e.BaseTags);
            Assert.Equal(knockbackImmune, e.KnockbackImmune);
            Assert.Equal(speciesCap == 0 ? (int?)null : speciesCap, e.SpeciesCap);
            Assert.Equal(contactDamage, e.ContactDamage);
            Assert.False(e.IsMinion);
            Assert.Equal(id == EnemyTypeId.BatDusk ? 6 : 1, e.SpawnPackMin);       // 박쥐 6~8마리가 한 묶음(spec 05 4.8, spec 07 2.2)
            Assert.Equal(id == EnemyTypeId.BatDusk ? 8 : 1, e.SpawnPackMax);
        }

        [Fact]
        public void TwelveSpecies_AllInPack_TV_REW_01_Gold()
        {
            // spec 05 TV-REW-01: 처치 골드 = 비용 × 2 → 슬라임 2, 늑대 6, 선돌지기 12, 술사 8, 꽈리 4, 고슴도치 8, 족제비 6, 박쥐 2, 도깨비불 8, 가시덩굴 8, 멧돼지 10, 북두꺼비 10
            ContentDb db = RepoDb();
            var gold = new[] { 2, 6, 12, 8, 4, 8, 6, 2, 8, 8, 10, 10 };
            for (int i = 0; i < gold.Length; i++)
            {
                Assert.Equal(gold[i], db.Enemy((EnemyTypeId)(i + 1)).Cost * 2);
            }
        }

        [Fact]
        public void SporeSeedling_IsAMinion()
        {
            // spec 06 5절 spore_queen.summon_sprouts: 체력 20, 속도 2.5, 접촉(피해 1 [임시]). 판정 반지름 0.25 [임시].
            // 졸개: 꼬리표·상성·점수·골드·드랍 없음(spec 05 6.1, 1.3 A4, TV-AFF-14, TV-SPC-52)
            EnemyDef e = RepoDb().Enemy(EnemyTypeId.SporeSeedling);
            Assert.True(e.IsMinion);
            Assert.Equal(20 * OneRaw, e.BaseHp.Raw);
            Assert.Equal(250, e.MoveSpeedCenti);
            Assert.Equal(1, e.ContactDamage);
            Assert.Equal(0, e.Cost);
            Assert.Equal(0, e.FirstWave);
            Assert.Equal(EnemyDef.TimeMask.None, e.Times);
            Assert.Equal(Element.None, e.Weak);
            Assert.Equal(Element.None, e.Resist);
            Assert.Equal(EnemyTag.None, e.BaseTags);
            Assert.False(e.KnockbackImmune);
            Assert.Null(e.SpeciesCap);
            Assert.Equal(1_073_741_824L, e.BodyRadius.Raw);
            Assert.Single(e.Attacks);
            AssertContact(e.Attacks[0]);
            AssertAiEmpty(e.Ai);
        }

        [Theory]
        [InlineData(EnemyTypeId.SlimeMoss, 1_503_238_554L)]       // 0.35 × 2^32 = 1,503,238,553.6 → 반올림 (U-04)
        [InlineData(EnemyTypeId.WolfBramble, 1_717_986_918L)]     // 0.40 × 2^32 = 1,717,986,918.4
        [InlineData(EnemyTypeId.GolemMenhir, 2_576_980_378L)]     // 0.60 × 2^32 = 2,576,980,377.6
        [InlineData(EnemyTypeId.ShamanCap, 1_288_490_189L)]       // 0.30 × 2^32 = 1,288,490,188.8
        [InlineData(EnemyTypeId.BugLantern, 1_202_590_843L)]      // 0.28 × 2^32 = 1,202,590,842.88
        [InlineData(EnemyTypeId.HogNeedle, 1_417_339_208L)]       // 0.33 × 2^32 = 1,417,339,207.68
        [InlineData(EnemyTypeId.WeaselPot, 1_073_741_824L)]       // 0.25(지상)
        [InlineData(EnemyTypeId.BatDusk, 1_073_741_824L)]         // 0.25
        [InlineData(EnemyTypeId.WispStray, 1_073_741_824L)]       // 0.25
        [InlineData(EnemyTypeId.VineThorn, 1_503_238_554L)]       // 0.35
        [InlineData(EnemyTypeId.BoarTusk, 2_362_232_013L)]        // 0.55 × 2^32 = 2,362,232,012.8
        [InlineData(EnemyTypeId.ToadDrum, 1_589_137_900L)]        // 0.37 × 2^32 = 1,589,137,899.52
        public void BodyRadius_IsTheU04Value(EnemyTypeId id, long raw)
        {
            Assert.Equal(raw, RepoDb().Enemy(id).BodyRadius.Raw);
        }

        // ---- spec 05 4절의 공격 표 ----

        [Fact]
        public void SlimeMoss_Attacks_AreContactOnly()
        {
            EnemyDef e = RepoDb().Enemy(EnemyTypeId.SlimeMoss);
            Assert.Single(e.Attacks);
            AssertContact(e.Attacks[0]);                                 // slime_moss.contact: 예고 없음, 피해 1, 쿨다운 90 (4.1)
            AssertAiEmpty(e.Ai);
        }

        [Fact]
        public void WolfBramble_Dash_EveryField()
        {
            EnemyDef e = RepoDb().Enemy(EnemyTypeId.WolfBramble);
            Assert.Single(e.Attacks);                                    // 접촉 없음(contact_damage = 0)
            AttackDef a = e.Attacks[0];                                  // wolf_bramble.dash (4.2)
            Assert.Equal(Kind.Dash, a.Kind);
            Assert.Equal(Shape.Band, a.Shape);                           // 예고 모양 Band
            Assert.Equal(0, a.Radius.Raw);
            Assert.Equal(0, a.InnerRadius.Raw);
            Assert.Equal(1 * OneRaw, a.Width.Raw);                       // 띠 폭 1.0
            Assert.Equal(6 * OneRaw, a.Length!.Value.Raw);               // 길이 6
            Assert.Equal(0, a.FanCenti);
            Assert.Null(a.Range);
            Assert.Equal(15_032_385_536L, a.TriggerMin!.Value.Raw);      // 발동 거리 3.5 × 2^32
            Assert.Equal(23_622_320_128L, a.TriggerMax!.Value.Raw);      // 5.5 × 2^32
            Assert.Equal(0, a.CastTicks);
            Assert.Equal(30, a.TelegraphTicks);                          // 예고 0.5초
            Assert.Equal(AttackDef.LockMode.AtStart, a.Lock);            // 예고 시작 순간 방향 고정
            Assert.Equal(0, a.LockTicks);
            Assert.Equal(AttackDef.AnchorMode.Self, a.Anchor);
            Assert.Equal(20, a.ActiveTicks);                             // 돌진 20틱(6 ÷ 18초)
            Assert.Equal(1800, a.DashSpeedCenti);                        // 돌진 속도 18
            Assert.Equal(90, a.RecoveryTicks);                           // 경직 1.5초
            Assert.Equal(1, a.Damage);                                   // 피해 1(예고 공격 종별 값)
            Assert.Equal(Source.Telegraph, a.Source);
            Assert.Null(a.Proj);
            Assert.True(a.Grazeable);
            Assert.True(a.IsDash);
            Assert.False(a.Lob);
            Assert.Null(a.EscapeOverrideMilli);
            Assert.Equal(0, a.CooldownTicks);                            // 늑대 0(빈틈이 대신함, U-05)
        }

        [Fact]
        public void WolfBramble_Dodge_AiParams()
        {
            EnemyDef.AiParams ai = RepoDb().Enemy(EnemyTypeId.WolfBramble).Ai;   // wolf_bramble.dodge (4.2)
            Assert.Equal(6_442_450_944L, ai.DodgeDistance.Raw);          // 대시 1.5칸 = 1.5 × 2^32
            Assert.Equal(90, ai.DodgeCooldownTicks);                     // 재사용 1.5초
            Assert.Equal(15, ai.DodgeWindowTicks);                       // 판단 창 0.25초
            Assert.Equal(6, ai.DodgeDashTicks);                          // 6틱 [임시: U-28]
            Assert.Equal(0, ai.ArmorHalfAngleCenti);
            Assert.Equal(0, ai.CoverHpPermille);
        }

        [Fact]
        public void GolemMenhir_Slam_EveryField()
        {
            AttackDef a = RepoDb().Enemy(EnemyTypeId.GolemMenhir).Attacks[0];    // golem_menhir.slam (4.3)
            Assert.Equal(Kind.Zone, a.Kind);
            Assert.Equal(Shape.Circle, a.Shape);
            Assert.Equal(10_737_418_240L, a.Radius.Raw);                 // 반경 2.5 × 2^32
            Assert.Equal(0, a.InnerRadius.Raw);
            Assert.Equal(0, a.Width.Raw);
            Assert.Null(a.Length);
            Assert.Equal(0, a.FanCenti);
            Assert.Equal(8 * OneRaw, a.Range!.Value.Raw);                // 사거리 8
            Assert.Null(a.TriggerMin);
            Assert.Null(a.TriggerMax);
            Assert.Equal(0, a.CastTicks);
            Assert.Equal(66, a.TelegraphTicks);                          // 예고 1.1초(C-01)
            Assert.Equal(AttackDef.LockMode.AtStart, a.Lock);
            Assert.Equal(0, a.LockTicks);
            Assert.Equal(AttackDef.AnchorMode.PlayerPosAtStart, a.Anchor);   // 중심 = 예고 시작 순간의 플레이어 위치
            Assert.Equal(0, a.ActiveTicks);
            Assert.Equal(0, a.DashSpeedCenti);
            Assert.Equal(72, a.RecoveryTicks);                           // 빈틈 1.2초
            Assert.Equal(2, a.Damage);
            Assert.Equal(Source.Telegraph, a.Source);
            Assert.Null(a.Proj);
            Assert.False(a.Grazeable);
            Assert.False(a.IsDash);
            Assert.False(a.Lob);
            Assert.Null(a.EscapeOverrideMilli);
            Assert.Equal(60, a.CooldownTicks);                           // 공격 간 최소 간격 1.0초 [임시: U-05]
        }

        [Fact]
        public void GolemMenhir_Sweep_EveryField()
        {
            AttackDef a = RepoDb().Enemy(EnemyTypeId.GolemMenhir).Attacks[1];    // golem_menhir.sweep (4.3)
            Assert.Equal(Kind.Zone, a.Kind);
            Assert.Equal(Shape.Fan, a.Shape);
            Assert.Equal(3 * OneRaw, a.Radius.Raw);                      // 근접 3.0
            Assert.Equal(0, a.InnerRadius.Raw);
            Assert.Equal(0, a.Width.Raw);
            Assert.Null(a.Length);
            Assert.Equal(11_000, a.FanCenti);                            // 110도
            Assert.Equal(3 * OneRaw, a.Range!.Value.Raw);                // 3.0 이하면 휩쓸기(U-31)
            Assert.Null(a.TriggerMin);
            Assert.Null(a.TriggerMax);
            Assert.Equal(48, a.TelegraphTicks);                          // 예고 0.8초
            Assert.Equal(AttackDef.LockMode.AtStart, a.Lock);            // 방향 = 시작 순간 플레이어 쪽 고정
            Assert.Equal(AttackDef.AnchorMode.Self, a.Anchor);           // 꼭짓점 = 자기 위치
            Assert.Equal(48, a.RecoveryTicks);                           // 빈틈 0.8초
            Assert.Equal(2, a.Damage);
            Assert.Equal(Source.Telegraph, a.Source);
            Assert.Null(a.Proj);
            Assert.False(a.IsDash);
            Assert.Equal(60, a.CooldownTicks);                           // [임시: U-05]
        }

        [Fact]
        public void GolemMenhir_Contact_And_Armor()
        {
            EnemyDef e = RepoDb().Enemy(EnemyTypeId.GolemMenhir);
            Assert.Equal(3, e.Attacks.Length);
            AssertContact(e.Attacks[2]);                                 // 접촉: 피해 1, 90 (4.3 표, M 4.2)
            Assert.Equal(6000, e.Ai.ArmorHalfAngleCenti);                // 정면 120도 = ±60°
            Assert.Equal(500, e.Ai.ArmorDamagePermille);                 // 임계가 아닌 피해를 절반(3.7 × 0.5)
            Assert.Equal(0, e.Ai.DodgeDistance.Raw);
        }

        [Fact]
        public void ShamanCap_Triple_EveryField()
        {
            AttackDef a = RepoDb().Enemy(EnemyTypeId.ShamanCap).Attacks[0];      // shaman_cap.triple (4.4)
            Assert.Equal(Kind.Spread, a.Kind);
            Assert.Null(a.Shape);                                        // 시전은 예고 구역이 아니다(U-12)
            Assert.Equal(0, a.Radius.Raw);
            Assert.Equal(0, a.Width.Raw);
            Assert.Null(a.Length);
            Assert.Equal(9 * OneRaw, a.Range!.Value.Raw);                // 시전 조건: 사거리 9 안 [임시: U-34]
            Assert.Null(a.TriggerMin);
            Assert.Null(a.TriggerMax);
            Assert.Equal(24, a.CastTicks);                               // 시전 0.4초
            Assert.Equal(0, a.TelegraphTicks);
            Assert.Equal(AttackDef.AnchorMode.Self, a.Anchor);
            Assert.Equal(0, a.ActiveTicks);
            Assert.Equal(36, a.RecoveryTicks);                           // 빈틈 0.6초
            Assert.Equal(1, a.Damage);                                   // 피해 1(탄)
            Assert.Equal(Source.Projectile, a.Source);
            Assert.NotNull(a.Proj);
            Assert.Equal(465_288_124L, a.Proj!.SpeedPerTick.Raw);        // 6.5 × 2^32 ÷ 60 = 465,288,123.73 → 반올림
            Assert.Equal(3, a.Proj.Count);                               // 3갈래
            Assert.Equal(2000, a.Proj.SpreadCenti);                      // 20도 [임시: U-34]
            Assert.Equal(9 * OneRaw, a.Proj.Range.Raw);                  // 9칸 [임시: U-34]
            Assert.Equal(515_396_076L, a.Proj.HitRadius.Raw);            // 0.12 × 2^32 = 515,396,075.52 → 반올림
            Assert.True(a.Grazeable);
            Assert.False(a.IsDash);
            Assert.False(a.Lob);
            Assert.Null(a.EscapeOverrideMilli);
            Assert.Equal(120, a.CooldownTicks);                          // 시전 간격 2.0초 [임시: U-05]
        }

        [Fact]
        public void ShamanCap_Contact_And_Cover()
        {
            EnemyDef e = RepoDb().Enemy(EnemyTypeId.ShamanCap);
            Assert.Equal(2, e.Attacks.Length);
            AssertContact(e.Attacks[1]);                                 // 접촉 피해 1(spec 05 2.2, 3.6)
            EnemyDef.AiParams ai = e.Ai;
            Assert.Equal(500, ai.CoverHpPermille);                       // Hp ≤ MaxHp/2
            Assert.Equal(6_442_450_944L, ai.CoverOffset.Raw);            // × 1.5
            Assert.Equal(5 * OneRaw, ai.KeepDistanceMin.Raw);            // 5~8칸 [임시: U-36]
            Assert.Equal(8 * OneRaw, ai.KeepDistanceMax.Raw);
            Assert.Equal(7 * OneRaw, ai.KiteDistanceMin.Raw);            // 7칸 이상 [임시: U-36]
            Assert.Equal(0, ai.ArmorHalfAngleCenti);
            Assert.Equal(0, ai.DodgeCooldownTicks);
        }

        [Fact]
        public void BugLantern_Ignite_And_Sympathetic()
        {
            EnemyDef e = RepoDb().Enemy(EnemyTypeId.BugLantern);
            Assert.Equal(2, e.Attacks.Length);
            AttackDef a = e.Attacks[0];                                  // bug_lantern.ignite (4.5)
            Assert.Equal(Kind.SelfDestruct, a.Kind);                     // 터지면 자신도 죽는다
            Assert.Equal(Shape.Circle, a.Shape);
            Assert.Equal(7_730_941_133L, a.Radius.Raw);                  // 자기 중심 반경 1.8
            Assert.Equal(5_153_960_755L, a.Range!.Value.Raw);            // 1.2칸 안이면 점화
            Assert.Null(a.Length);
            Assert.Equal(54, a.TelegraphTicks);                          // 예고 0.9초(M 8.1)
            Assert.Equal(AttackDef.LockMode.AtStart, a.Lock);
            Assert.Equal(AttackDef.AnchorMode.Self, a.Anchor);
            Assert.Equal(0, a.RecoveryTicks);
            Assert.Equal(1, a.Damage);
            Assert.Equal(Source.Telegraph, a.Source);
            Assert.Null(a.Proj);
            Assert.False(a.IsDash);
            Assert.False(a.Lob);
            Assert.Null(a.LeavesHazard);
            Assert.Equal(0, a.CooldownTicks);
            AssertContact(e.Attacks[1]);                                 // 접촉 1(C-14)
            Assert.Equal(20 * OneRaw, e.Ai.SympatheticDamage.Raw);       // 유폭: 적에게 20(× H(w))
            Assert.Equal(24, e.Ai.SympatheticDelayTicks);                // 0.4초
        }

        [Fact]
        public void HogNeedle_Snipe_EveryField()
        {
            EnemyDef e = RepoDb().Enemy(EnemyTypeId.HogNeedle);
            Assert.Equal(2, e.Attacks.Length);
            AttackDef a = e.Attacks[0];                                  // hog_needle.snipe (4.6)
            Assert.Equal(Kind.AimedShot, a.Kind);
            Assert.Equal(Shape.AimLine, a.Shape);                        // 조준선
            Assert.Equal(1_717_986_918L, a.Width.Raw);                   // 폭 0.4
            Assert.Null(a.Length);                                       // 전장 끝까지
            Assert.Equal(7 * OneRaw, a.TriggerMin!.Value.Raw);           // 7 ≤ d ≤ 10 에서 쏜다
            Assert.Equal(10 * OneRaw, a.TriggerMax!.Value.Raw);
            Assert.Equal(60, a.TelegraphTicks);                          // 1.0초 = 추적 36 + 고정 24
            Assert.Equal(AttackDef.LockMode.LastNTicks, a.Lock);
            Assert.Equal(24, a.LockTicks);                               // 마지막 0.4초 고정(R4)
            Assert.Equal(AttackDef.AnchorMode.Self, a.Anchor);
            Assert.Equal(84, a.RecoveryTicks);                           // 빈틈 1.4초
            Assert.Equal(1, a.Damage);
            Assert.Equal(Source.Projectile, a.Source);
            Assert.Equal(1_145_324_612L, a.Proj!.SpeedPerTick.Raw);      // 가시 속도 16 = 16 × 2^32 ÷ 60
            Assert.Equal(1, a.Proj.Count);
            Assert.Equal(0, a.Proj.SpreadCenti);
            Assert.Equal(0, a.Proj.Range.Raw);                           // 벽에 박히면 사라진다(사거리 제한 없음)
            Assert.Equal(515_396_076L, a.Proj.HitRadius.Raw);            // 0.12 [임시: U-43]
            Assert.True(a.Grazeable);
            Assert.False(a.IsDash);
            Assert.Equal(60, a.CooldownTicks);                           // 조준 간격 1.0초 [임시: U-05]
            AssertContact(e.Attacks[1]);
            Assert.Equal(7 * OneRaw, e.Ai.KeepDistanceMin.Raw);          // 7~10칸 유지
            Assert.Equal(10 * OneRaw, e.Ai.KeepDistanceMax.Raw);
        }

        [Fact]
        public void WeaselPot_Erupt_And_Burrow()
        {
            EnemyDef e = RepoDb().Enemy(EnemyTypeId.WeaselPot);
            Assert.Equal(2, e.Attacks.Length);
            AttackDef a = e.Attacks[0];                                  // weasel_pot.erupt (4.7)
            Assert.Equal(Kind.Zone, a.Kind);
            Assert.Equal(Shape.Circle, a.Shape);
            Assert.Equal(5_153_960_755L, a.Radius.Raw);                  // 반경 1.2
            Assert.Equal(2 * OneRaw, a.Range!.Value.Raw);                // 플레이어 2칸 안
            Assert.Equal(42, a.TelegraphTicks);                          // 예고 0.7초
            Assert.Equal(AttackDef.AnchorMode.PlayerPosAtStart, a.Anchor);   // 그 순간의 플레이어 위치
            Assert.Equal(1, a.Damage);
            Assert.Equal(Source.Telegraph, a.Source);
            Assert.Equal(0, a.RecoveryTicks);                            // 지상 96틱은 빈틈이 아니다(U-51)
            Assert.Equal(0, a.CooldownTicks);
            AssertContact(e.Attacks[1]);                                 // 지상 접촉 1 [임시: U-48]
            EnemyDef.AiParams ai = e.Ai;
            Assert.Equal(96, ai.SurfaceTicks);                           // 지상 1.6초
            Assert.Equal(30, ai.DigTicks);                               // 파고들기 0.5초
            Assert.Equal(240, ai.BurrowMaxTicks);                        // 땅속 최대 4초
            Assert.Equal(180, ai.BurrowRetargetTicks);                   // 3초 더 [임시: U-50]
        }

        [Fact]
        public void BatDusk_ContactOnly_Wave_Pack()
        {
            EnemyDef e = RepoDb().Enemy(EnemyTypeId.BatDusk);
            Assert.Single(e.Attacks);
            AssertContact(e.Attacks[0]);                                 // 예고 없음(R2: 3.6 < V)
            Assert.Equal(OneRaw, e.Ai.WaveAmplitude.Raw);                // 진폭 1칸
            Assert.Equal(72, e.Ai.WavePeriodTicks);                      // 주기 1.2초
            Assert.Equal(6, e.SpawnPackMin);                             // 6~8마리(TV-SPC-31)
            Assert.Equal(8, e.SpawnPackMax);
        }

        [Fact]
        public void WispStray_Ring_And_Blink()
        {
            EnemyDef e = RepoDb().Enemy(EnemyTypeId.WispStray);
            Assert.Equal(2, e.Attacks.Length);
            AttackDef a = e.Attacks[0];                                  // wisp_stray.ring (4.9, TV-SPC-34)
            Assert.Equal(Kind.RingShot, a.Kind);
            Assert.Null(a.Shape);                                        // 시전은 예고 구역이 아니다(U-12)
            Assert.Equal(36, a.CastTicks);                               // 시전 0.6초
            Assert.Equal(0, a.TelegraphTicks);
            Assert.Equal(60, a.RecoveryTicks);                           // 빈틈 1.0초
            Assert.Equal(1, a.Damage);
            Assert.Equal(Source.Projectile, a.Source);
            Assert.Equal(322_122_547L, a.Proj!.SpeedPerTick.Raw);        // 탄 속도 4.5
            Assert.Equal(8, a.Proj.Count);                               // 8방향
            Assert.Equal(4500, a.Proj.SpreadCenti);                      // 45도 간격
            Assert.Equal(9 * OneRaw, a.Proj.Range.Raw);                  // 사거리 9
            Assert.Equal(515_396_076L, a.Proj.HitRadius.Raw);            // 0.12(소)
            Assert.True(a.Grazeable);
            Assert.Equal(150, a.CooldownTicks);                          // 시전 간격 2.5초 [임시: U-05]
            AssertContact(e.Attacks[1]);
            EnemyDef.AiParams ai = e.Ai;                                 // wisp_stray.blink (TV-SPC-32·33)
            Assert.Equal(300, ai.BlinkPeriodTicks);                      // 5초마다
            Assert.Equal(18, ai.VanishTicks);                            // 사라지기 0.3초
            Assert.Equal(18, ai.AppearTicks);                            // 나타나기 0.3초
            Assert.Equal(30, ai.NoCastAfterAppearTicks);                 // 0.5초 시전 금지
            Assert.Equal(5 * OneRaw, ai.BlinkDistanceMin.Raw);           // 5~7칸
            Assert.Equal(7 * OneRaw, ai.BlinkDistanceMax.Raw);
            Assert.Equal(6 * OneRaw, ai.BlinkRetryDistance.Raw);         // [임시: U-56]
            Assert.Equal(16, ai.BlinkCandidates);
            Assert.Equal(OneRaw, ai.BlinkWallMargin.Raw);
            Assert.Equal(5 * OneRaw, ai.KeepDistanceMin.Raw);            // 평소 5~7칸 [임시: U-53]
            Assert.Equal(7 * OneRaw, ai.KeepDistanceMax.Raw);
        }

        [Fact]
        public void VineThorn_Seed_ThornField_Emerge()
        {
            EnemyDef e = RepoDb().Enemy(EnemyTypeId.VineThorn);
            Assert.Equal(2, e.Attacks.Length);
            AttackDef a = e.Attacks[0];                                  // vine_thorn.seed (4.10)
            Assert.Equal(Kind.Lob, a.Kind);
            Assert.True(a.Lob);                                          // 포물선, 설치 벽을 넘는다(X5)
            Assert.Equal(Shape.Circle, a.Shape);
            Assert.Equal(6_442_450_944L, a.Radius.Raw);                  // 반경 1.5
            Assert.Null(a.Range);
            Assert.Equal(60, a.TelegraphTicks);                          // 예고 1.0초(= 비행 시간, U-58)
            Assert.Equal(AttackDef.AnchorMode.PlayerPosAtStart, a.Anchor);
            Assert.Equal(1, a.Damage);
            Assert.Equal(Source.Telegraph, a.Source);
            Assert.Null(a.Proj);
            Assert.Equal(210, a.CooldownTicks);                          // 시전 간격 3.5초(TV-SPC-38)
            AttackDef.HazardDef h = a.LeavesHazard!;                     // 가시밭(TV-SPC-36·37)
            Assert.Equal(AttackDef.HazardKind.ThornField, h.Kind);
            Assert.Equal(6_442_450_944L, h.Radius.Raw);                  // 반경 1.5
            Assert.Equal(240, h.DurationTicks);                          // 4초
            Assert.Equal(3, h.MaxCount);                                 // 전장 동시 3개
            Assert.Equal(90, h.ContactCooldownTicks);                    // 접촉 규칙 1.5초에 1회
            AssertContact(e.Attacks[1]);
            Assert.Equal(60, e.Ai.EmergeTicks);                          // 흙 금 1.0초(TV-SPC-35)
            Assert.Equal(2 * OneRaw, e.Ai.SpawnWallMargin.Raw);          // 벽에서 2칸 이상
            Assert.Equal(3 * OneRaw, e.Ai.SpawnPlayerMargin.Raw);        // 플레이어 3칸 이상 [임시: U-60]
        }

        [Fact]
        public void BoarTusk_Charge_EveryField()
        {
            EnemyDef e = RepoDb().Enemy(EnemyTypeId.BoarTusk);
            Assert.Equal(2, e.Attacks.Length);
            AttackDef a = e.Attacks[0];                                  // boar_tusk.charge (4.11)
            Assert.Equal(Kind.Dash, a.Kind);
            Assert.Equal(Shape.Band, a.Shape);
            Assert.Equal(6_871_947_674L, a.Width.Raw);                   // 띠 폭 1.6
            Assert.Null(a.Length);                                       // 벽에 닿을 때까지
            Assert.Equal(54, a.TelegraphTicks);                          // 예고 0.9초
            Assert.Equal(AttackDef.LockMode.AtStart, a.Lock);            // 예고 시작 순간 방향 고정
            Assert.Equal(AttackDef.AnchorMode.Self, a.Anchor);
            Assert.Equal(0, a.ActiveTicks);                              // 벽에 닿으면 끝난다
            Assert.Equal(1600, a.DashSpeedCenti);                        // 돌진 속도 16
            Assert.Equal(120, a.RecoveryTicks);                          // 벽 충돌 기절 2.0초(TV-SPC-41)
            Assert.Equal(2, a.Damage);
            Assert.Equal(Source.Telegraph, a.Source);
            Assert.True(a.IsDash);
            Assert.True(a.Grazeable);
            Assert.Equal(180, a.CooldownTicks);                          // 돌진 간격 3.0초 [임시: U-05]
            AssertContact(e.Attacks[1]);
            Assert.Equal(1500, e.Ai.StunDamagePermille);                 // 기절 중 받는 피해 × 1.5
            Assert.Equal(10 * OneRaw, e.Ai.PathDamage.Raw);              // 경로의 적 10(× H(w))
        }

        [Fact]
        public void ToadDrum_Tongue_And_Croak()
        {
            EnemyDef e = RepoDb().Enemy(EnemyTypeId.ToadDrum);
            Assert.Equal(2, e.Attacks.Length);
            AttackDef a = e.Attacks[0];                                  // toad_drum.tongue (4.12)
            Assert.Equal(Kind.Tongue, a.Kind);
            Assert.Equal(Shape.Band, a.Shape);
            Assert.Equal(3_435_973_837L, a.Width.Raw);                   // 띠 폭 0.8
            Assert.Equal(15_032_385_536L, a.Length!.Value.Raw);          // 길이 3.5
            Assert.Equal(3 * OneRaw, a.Range!.Value.Raw);                // 3칸 안에서만(TV-SPC-51)
            Assert.Equal(36, a.TelegraphTicks);                          // 예고 0.6초
            Assert.Equal(60, a.RecoveryTicks);                           // 빈틈 1.0초
            Assert.Equal(1, a.Damage);
            Assert.Equal(Source.Telegraph, a.Source);
            Assert.False(a.IsDash);
            Assert.Equal(90, a.CooldownTicks);                           // 혀 간격 [임시: U-05]
            AssertContact(e.Attacks[1]);
            EnemyDef.AiParams ai = e.Ai;                                 // toad_drum.croak (TV-SPC-46·47, 3.8)
            Assert.Equal(360, ai.CroakPeriodTicks);                      // 6초마다
            Assert.Equal(120, ai.CroakTicks);                            // 2.0초
            Assert.Equal(4 * OneRaw, ai.BuffRadius.Raw);                 // 반경 4
            Assert.Equal(240, ai.BuffTicks);                             // 4초
            Assert.Equal(1200, ai.BuffSpeedPermille);                    // +20%
            Assert.Equal(370, ai.BuffSpeedCapCenti);                     // 최종 3.7 상한
            Assert.Equal(700, ai.BuffRecoveryPermille);                  // 빈틈 −30%
            Assert.Equal(30 * OneRaw, ai.CroakBreakDamage.Raw);          // 누적 30(× H(w))
            Assert.Equal(60, ai.CroakStaggerTicks);                      // 1.0초 경직
            Assert.Equal(8 * OneRaw, ai.FollowRadius.Raw);               // [임시: U-64]
            Assert.Equal(2 * OneRaw, ai.FollowOffset.Raw);
        }

        // 접촉(spec 05 1.1 P3, 3.6, 3.7): 예고·탄 없음, 피해 1, 같은 적에게서 90틱에 1회
        private static void AssertContact(AttackDef a)
        {
            Assert.Equal(Kind.Contact, a.Kind);
            Assert.Null(a.Shape);
            Assert.Equal(0, a.TelegraphTicks);
            Assert.Equal(0, a.RecoveryTicks);
            Assert.Equal(1, a.Damage);
            Assert.Equal(Source.Contact, a.Source);
            Assert.Null(a.Proj);
            Assert.Null(a.Range);
            Assert.False(a.Grazeable);
            Assert.False(a.IsDash);
            Assert.Equal(90, a.CooldownTicks);
        }

        private static void AssertAiEmpty(EnemyDef.AiParams ai)
        {
            Assert.Equal(0, ai.DodgeDistance.Raw);
            Assert.Equal(0, ai.DodgeCooldownTicks);
            Assert.Equal(0, ai.DodgeWindowTicks);
            Assert.Equal(0, ai.DodgeDashTicks);
            Assert.Equal(0, ai.ArmorHalfAngleCenti);
            Assert.Equal(0, ai.ArmorDamagePermille);
            Assert.Equal(0, ai.CoverHpPermille);
            Assert.Equal(0, ai.CoverOffset.Raw);
            Assert.Equal(0, ai.KeepDistanceMin.Raw);
            Assert.Equal(0, ai.KeepDistanceMax.Raw);
            Assert.Equal(0, ai.KiteDistanceMin.Raw);
            Assert.Equal(0, ai.SympatheticDamage.Raw);
            Assert.Equal(0, ai.SurfaceTicks);
            Assert.Equal(0, ai.WavePeriodTicks);
            Assert.Equal(0, ai.BlinkPeriodTicks);
            Assert.Equal(0, ai.EmergeTicks);
            Assert.Equal(0, ai.StunDamagePermille);
            Assert.Equal(0, ai.CroakPeriodTicks);
            Assert.Equal(0, ai.FollowRadius.Raw);
        }

        [Fact]
        public void EnemiesNotInPack_Throw()
        {
            ContentDb db = RepoDb();
            Assert.Throws<ContentException>(() => db.Enemy(EnemyTypeId.None));
            Assert.Throws<ContentException>(() => db.Enemy((EnemyTypeId)13));
            Assert.Throws<ContentException>(() => db.Enemy((EnemyTypeId)999));
        }

        [Fact]
        public void PackWithoutTheSection_Throws()
        {
            ContentDb db = ContentDb.Build(ContentPack.Load(PackWriter.Write(PackWriter.SchemaVersion, 1, new byte[PackWriter.CoreCompatLength],
                Array.Empty<PackSection>())));
            Assert.Throws<ContentException>(() => db.Enemy(EnemyTypeId.SlimeMoss));
        }

        // ---- 결정성 ----

        // EnemyTypeId 오름차순으로 든 종과 그 공격 수(data/sim/enemies.json 의 _note)
        private static readonly (EnemyTypeId Id, int Attacks)[] PackOrder =
        {
            (EnemyTypeId.SlimeMoss, 1), (EnemyTypeId.WolfBramble, 1), (EnemyTypeId.GolemMenhir, 3), (EnemyTypeId.ShamanCap, 2),
            (EnemyTypeId.BugLantern, 2), (EnemyTypeId.HogNeedle, 2), (EnemyTypeId.WeaselPot, 2), (EnemyTypeId.BatDusk, 1),
            (EnemyTypeId.WispStray, 2), (EnemyTypeId.VineThorn, 2), (EnemyTypeId.BoarTusk, 2), (EnemyTypeId.ToadDrum, 2),
            (EnemyTypeId.SporeSeedling, 1),
        };

        [Fact]
        public void SameData_SameSectionBytes_InIdOrder()
        {
            byte[] s = Section(RepoPack.Value);
            Assert.Equal(s, Section(BuildPack(TestRepo.Root)));
            Assert.Equal(PackOrder.Length, s[0] | (s[1] << 8));
            int at = 2;
            foreach ((EnemyTypeId id, int attacks) in PackOrder)
            {
                Assert.Equal((int)id, s[at] | (s[at + 1] << 8));
                Assert.Equal(attacks, s[at + HeadLength - 1]);
                at += EntryLength(attacks);
            }
            Assert.Equal(at, s.Length);
        }

        // 적 항목(절 꼴은 ContentDb.Enemies.cs): 머리 51바이트(공격 수 u8 까지) + 공격 165바이트 × 수 + AI 248바이트 + 꼬리 9바이트(졸개, 무리 수)
        private const int HeadLength = 51;
        private const int AttackLength = 165;
        private const int AiLength = 248;
        private const int TailLength = 9;

        private static int EntryLength(int attacks) => HeadLength + AttackLength * attacks + AiLength + TailLength;

        // n 번째 항목(0부터)의 시작
        private static int EntryStart(int n)
        {
            int at = 2;
            for (int i = 0; i < n; i++)
            {
                at += EntryLength(PackOrder[i].Attacks);
            }
            return at;
        }

        [Fact]
        public void ReversedSourceOrder_GivesTheSameBytes()
        {
            using TempDataRepo repo = Repo("[" + WolfJson + ", " + SlimeJson + "]");
            using TempDataRepo forward = Repo("[" + SlimeJson + ", " + WolfJson + "]");
            Assert.Equal(Section(BuildPack(forward.Root)), Section(BuildPack(repo.Root)));
        }

        [Fact]
        public void TestJson_IsTheRepoData()
        {
            // 진단 시험에 쓰는 두 줄이 저장소 데이터와 같은 바이트를 낸다(시험의 원본이 명세에서 멀어지지 않게)
            using TempDataRepo repo = Repo("[" + SlimeJson + ", " + WolfJson + "]");
            byte[] s = Section(BuildPack(repo.Root));
            byte[] full = Section(RepoPack.Value);
            int len = 2 * EntryLength(1);
            Assert.Equal(2, s[0] | (s[1] << 8));
            Assert.Equal(full.AsSpan(2, len).ToArray(), s.AsSpan(2).ToArray());
        }

        // ---- 진단 ----

        [Fact]
        public void Problems_Ids_Duplicates_Affinity_Tags()
        {
            Expect(SlimeJson.Replace("enemy.slime_moss", "enemy.nope"), "/enemies/0/id", "enemy.nope");
            Expect(SlimeJson + ", " + SlimeJson, "/enemies/1/id", "두 번");
            Expect(SlimeJson.Replace("\"resist\": \"element.ice\"", "\"resist\": \"element.fire\""), "/enemies/0/resist", "같다");
            Expect(SlimeJson.Replace("\"weak\": \"element.fire\"", "\"weak\": \"element.neutral\""), "/enemies/0/weak", "무속성");
            Expect(SlimeJson.Replace("\"tags\": []", "\"tags\": [\"tag.heavy\"]"), "/enemies/0/knockback_immune", "꼬리표");
            Expect(SlimeJson.Replace("\"tags\": []", "\"tags\": [\"tag.elite\"]"), "/enemies/0/tags/0", "엘리트");
            Expect(SlimeJson.Replace("\"tags\": []", "\"tags\": [\"tag.nope\"]"), "/enemies/0/tags/0", "tag.nope");
            Expect(SlimeJson.Replace("\"tod.night\"", "\"tod.boss\""), "/enemies/0/times/2", "보스 웨이브");
            // 접촉 공격의 피해는 접촉 피해와 같다 / 접촉 피해가 있는데 접촉 공격이 없다
            Expect(SlimeJson.Replace("\"contact_damage\": 1", "\"contact_damage\": 0"), "/enemies/0/attacks/0/damage", "contact_damage");
            Expect(ShamanJson.Replace(",\n    " + ContactAttack, ""), "/enemies/0/contact_damage", "contact");
            // 무리 수가 거꾸로다
            Expect(SlimeJson.Replace("\"body_radius\": 0.35,", "\"body_radius\": 0.35, \"spawn_pack\": [8, 6],"), "/enemies/0/spawn_pack", "무리");
            // 일반 적에 시간대가 없다 / 비용 0
            Expect(SlimeJson.Replace("\"times\": [\"tod.day\", \"tod.dusk\", \"tod.night\"]", "\"times\": []"), "/enemies/0/times", "시간대");
            Expect(SlimeJson.Replace("\"cost\": 1,", "\"cost\": 0,"), "/enemies/0/cost", "1 이상");
        }

        [Fact]
        public void Problems_Minion()
        {
            Expect(SeedlingJson, null, null);                                                                // 원본 그대로는 통과
            // 졸개 번호가 아닌데 졸개 표지 / 졸개 번호인데 표지가 없다
            Expect(SlimeJson.Replace("\"role\": \"swarm\"", "\"minion\": true, \"role\": \"swarm\""), "/enemies/0/minion", "졸개");
            Expect(SeedlingJson.Replace("\"minion\": true, ", ""), "/enemies/0/minion", "졸개");
            // 졸개의 비용·시간대·상성·꼬리표
            Expect(SeedlingJson.Replace("\"cost\": 0,", "\"cost\": 1,"), "/enemies/0/cost", "졸개");
            Expect(SeedlingJson.Replace("\"times\": []", "\"times\": [\"tod.day\"]"), "/enemies/0/times", "졸개");
            Expect(SeedlingJson.Replace("\"weak\": \"element.neutral\"", "\"weak\": \"element.fire\""), "/enemies/0/weak", "상성");
            Expect(SeedlingJson.Replace("\"tags\": []", "\"tags\": [\"tag.flying\"]"), "/enemies/0/tags", "꼬리표");
        }

        [Fact]
        public void Problems_Schema()
        {
            Expect(SlimeJson.Replace("\"cost\": 1,", "\"cost\": 1, \"speed\": 3,"), "/enemies/0", "스키마");      // 모르는 필드
            Expect(SlimeJson.Replace("\"cost\": 1,", ""), "/enemies/0", "스키마");                                 // 빠진 필드
            Expect(SlimeJson.Replace("\"attack_damage\": 1", "\"attack_damage\": 4"), "/enemies/0/attack_damage", "스키마");
            Expect(SlimeJson.Replace("\"kind\": \"contact\"", "\"kind\": \"beam\""), "/enemies/0/attacks/0/kind", "스키마");
        }

        [Fact]
        public void Problems_Attacks()
        {
            // 모양이 있는데 예고가 없다
            Expect(WolfJson.Replace("\"telegraph_seconds\": 0.5,", ""), "/enemies/0/attacks/0/telegraph_seconds", "예고");
            // 돌진 시간 × 속도 ≠ 길이 × 60
            Expect(WolfJson.Replace("\"active_ticks\": 20", "\"active_ticks\": 21"), "/enemies/0/attacks/0/active_ticks", "돌진");
            // 돌진인데 is_dash 가 아니다
            Expect(WolfJson.Replace("\"is_dash\": true", "\"is_dash\": false"), "/enemies/0/attacks/0/is_dash", "is_dash");
            // 돌진은 스침 대상이다
            Expect(WolfJson.Replace("\"grazeable\": true", "\"grazeable\": false"), "/enemies/0/attacks/0/grazeable", "P4");
            // 띠에 폭이 없다
            Expect(WolfJson.Replace("\"width\": 1.0,", ""), "/enemies/0/attacks/0/width", "폭");
            // last_n_ticks 의 n 이 예고보다 길다
            Expect(WolfJson.Replace("\"lock\": \"at_start\"", "\"lock\": \"last_n_ticks\", \"lock_seconds\": 0.6"), "/enemies/0/attacks/0/lock_seconds", "n");
            // 거리 조건이 거꾸로다
            Expect(WolfJson.Replace("\"trigger_min\": 3.5", "\"trigger_min\": 6"), "/enemies/0/attacks/0/trigger_max", "거리 조건");
            // 접촉 공격의 피해 분류가 접촉이 아니다
            Expect(SlimeJson.Replace("\"damage_source\": \"contact\"", "\"damage_source\": \"telegraph\""), "/enemies/0/attacks/0/damage_source", "contact");
            // 같은 열쇠가 두 번
            Expect(SlimeJson.Replace("\"attacks\": [", "\"attacks\": [" + ContactAttack + ","), "/enemies/0/attacks/1/key", "두 번");
        }

        [Fact]
        public void Problems_DashToWall_Lob_Hazard()
        {
            Expect(BoarJson, null, null);                                                                    // 원본 그대로는 통과
            Expect(VineJson, null, null);
            // 벽까지 달리는 돌진(length null)의 돌진 시간은 0
            Expect(BoarJson.Replace("\"active_ticks\": 0", "\"active_ticks\": 30"), "/enemies/0/attacks/0/active_ticks", "length null");
            // 길이가 있는 돌진의 시간은 ceil(길이 × 60 ÷ 속도): 박쥐 여왕 급강하 5칸 ÷ 18 = 16.67 → 17(spec 05 U-76)
            Expect(BoarJson.Replace("\"length\": null", "\"length\": 5").Replace("\"active_ticks\": 0", "\"active_ticks\": 17").Replace("\"dash_speed\": 16", "\"dash_speed\": 18"), null, null);
            Expect(BoarJson.Replace("\"length\": null", "\"length\": 5").Replace("\"active_ticks\": 0", "\"active_ticks\": 16").Replace("\"dash_speed\": 16", "\"dash_speed\": 18"),
                "/enemies/0/attacks/0/active_ticks", "ceil");
            // 포물선 공격 ⇔ lob
            Expect(VineJson.Replace("\"lob\": true,", ""), "/enemies/0/attacks/0/lob", "lob");
            // 남는 구역은 예고 모양이 있는 공격에만
            Expect(ShamanJson.Replace("\"cooldown_seconds\": 2.0 }", "\"cooldown_seconds\": 2.0, \"leaves_hazard\": " + ThornField + " }"), "/enemies/0/attacks/0/leaves_hazard", "예고 모양");
            Expect(VineJson.Replace("\"kind\": \"thorn_field\"", "\"kind\": \"lava\""), "/enemies/0/attacks/0/leaves_hazard", "스키마");
        }

        [Fact]
        public void Problems_Projectiles()
        {
            Expect(ShamanJson, null, null);                                                                  // 원본 그대로는 통과
            Expect(ShamanJson.Replace("\"damage\": 1,\n          \"damage_source\": \"projectile\"", "\"damage\": 2,\n          \"damage_source\": \"projectile\""),
                "/enemies/0/attacks/0/damage", "1칸");
            Expect(ShamanJson.Replace("\"grazeable\": true,", ""), "/enemies/0/attacks/0/grazeable", "P4");
            Expect(ShamanJson.Replace("\"damage_source\": \"projectile\"", "\"damage_source\": \"telegraph\""), "/enemies/0/attacks/0/shape", "예고 모양");
            // 탄의 사거리 null = 벽까지(고슴도치 가시)는 받는다
            Expect(ShamanJson.Replace("\"range\": 9, \"hit_radius\"", "\"range\": null, \"hit_radius\""), null, null);
        }

        private static void Expect(string enemiesArrayBody, string? pointer, string? text)
        {
            using TempDataRepo repo = Repo("[" + enemiesArrayBody + "]");
            List<string> problems = Problems(repo.Root);
            if (pointer == null)
            {
                Assert.Empty(problems);
                return;
            }
            Assert.Contains(problems, p => p.Contains("data/sim/enemies.json:" + pointer) && p.Contains(text!));
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
            // 첫 항목(이끼 슬라임, 절 머리 2바이트 뒤): u16 id(2..3) u8 role(4) i64 hp(5..12) i32 × 5(13..32) u8 time(33) u8 weak(34) u8 resist(35)
            //   u16 tags(36..37) u8 kb(38) u8 has_cap(39) i32 cap(40..43) i64 body(44..51) u8 공격 수(52)
            //   공격 0: u8 kind(53) u8 has_shape(54) u8 shape(55) i64 × 3(56..79) u8 has_length(80) i64 length(81..88) …
            Assert.Throws<ContentException>(() => Load(Patched(good, 2, 0)));          // id None
            Assert.Throws<ContentException>(() => Load(Patched(good, 2, 0xEE)));       // 모르는 id
            Assert.Throws<ContentException>(() => Load(Patched(good, 4, 0xEE)));       // 모르는 역할
            Assert.Throws<ContentException>(() => Load(Patched(good, 33, 0)));         // 시간대 없음
            Assert.Throws<ContentException>(() => Load(Patched(good, 33, 8)));         // 모르는 시간대 비트
            Assert.Throws<ContentException>(() => Load(Patched(good, 34, 0xEE)));      // 모르는 속성
            Assert.Throws<ContentException>(() => Load(Patched(good, 36, 0x80)));      // 모르는 꼬리표 비트
            Assert.Throws<ContentException>(() => Load(Patched(good, 38, 2)));         // bool 이 0/1 이 아니다
            Assert.Throws<ContentException>(() => Load(Patched(good, 40, 1)));         // 종 상한 없음인데 값이 있다
            Assert.Throws<ContentException>(() => Load(Patched(good, 52, 0)));         // 공격 없음
            Assert.Throws<ContentException>(() => Load(Patched(good, 53, 0xEE)));      // 모르는 공격 종류
            Assert.Throws<ContentException>(() => Load(Patched(good, 55, 1)));         // 모양 없음인데 값이 있다
            Load(Patched(good, 54, 1));                                                // 모양 있음(Circle 0)은 꼴로는 읽힌다. 모양과 예고의 짝은 변환기가 본다
            // 순서 위반: 같은 길이인 앞 두 항목(이끼, 늑대)을 바꾼다
            int entry = EntryLength(1);
            var swapped = (byte[])good.Clone();
            Array.Copy(good, 2 + entry, swapped, 2, entry);
            Array.Copy(good, 2, swapped, 2 + entry, entry);
            Assert.Throws<ContentException>(() => Load(swapped));

            // 첫 항목의 꼬리: u8 is_minion, i32 spawn_pack_min, i32 spawn_pack_max
            int tail = 2 + entry - TailLength;
            Assert.Throws<ContentException>(() => Load(Patched(good, tail, 2)));      // bool 이 0/1 이 아니다
            Assert.Throws<ContentException>(() => Load(Patched(good, tail + 1, 0)));  // 무리 수 0
            Assert.Throws<ContentException>(() => Load(Patched(good, tail + 1, 2)));  // 무리 수 아래 끝 > 위 끝
            Load(Patched(good, tail, 1));                                            // 졸개 표지는 꼴로는 읽힌다(짝은 변환기가 본다)
            // 첫 항목 공격 0 의 남는 구역(없음): u8 has(공격 시작 + 143) u8 kind i64 radius …
            int hazard = 2 + HeadLength + 143;
            Assert.Throws<ContentException>(() => Load(Patched(good, hazard + 2, 1)));    // 없음인데 반지름이 있다
            Assert.Throws<ContentException>(() => Load(Patched(good, hazard, 2)));        // bool 이 0/1 이 아니다
            // 가시덩굴(10번째 항목, 첨자 9)의 씨앗: 남는 구역 있음 → 모르는 종류
            int vine = EntryStart(9) + HeadLength + 143;
            Assert.Equal(1, good[vine]);
            Assert.Throws<ContentException>(() => Load(Patched(good, vine + 1, 0xEE)));
            // 졸개가 아닌 종의 시간대가 비었다(첫 항목) / 졸개(마지막 항목)는 시간대가 비어도 읽힌다
            Assert.Throws<ContentException>(() => Load(Patched(good, 33, 0)));
            int seedling = EntryStart(PackOrder.Length - 1);
            Assert.Equal(0, good[seedling + 31]);                                    // 졸개의 time_mask
        }

        private static byte[] Patched(byte[] good, int at, byte value)
        {
            var b = (byte[])good.Clone();
            b[at] = value;
            return b;
        }

        private static ContentDb Load(byte[] section) =>
            ContentDb.Build(ContentPack.Load(PackWriter.Write(PackWriter.SchemaVersion, 1, new byte[PackWriter.CoreCompatLength],
                new[] { new PackSection((ushort)ContentSection.Enemies, section) })));

        private static byte[] Section(byte[] pack)
        {
            Assert.True(ContentPack.Load(pack).TryGetSection((ushort)ContentSection.Enemies, out ReadOnlyMemory<byte> s));
            return s.ToArray();
        }

        // spec 05 4.1·4.2·4.4·4.10·4.11, spec 06 5절의 줄(data/sim/enemies.json 과 같은 값. TestJson_IsTheRepoData 가 앞 둘을 대조한다)
        private const string ThornField =
            "{ \"kind\": \"thorn_field\", \"radius\": 1.5, \"duration_seconds\": 4, \"max_count\": 3, \"contact_cooldown_seconds\": 1.5 }";

        private const string VineJson =
            "{ \"id\": \"enemy.vine_thorn\", \"role\": \"zone_denial\", \"base_hp\": 70, \"move_speed\": 0, \"attack_damage\": 1, \"contact_damage\": 1,\n" +
            "  \"cost\": 4, \"first_wave\": 11, \"times\": [\"tod.day\", \"tod.dusk\"], \"weak\": \"element.fire\", \"resist\": \"element.arcane\",\n" +
            "  \"tags\": [\"tag.rooted\"], \"knockback_immune\": true, \"species_cap\": 4, \"body_radius\": 0.35,\n" +
            "  \"attacks\": [{ \"key\": \"seed\", \"kind\": \"lob\", \"shape\": \"circle\", \"radius\": 1.5, \"telegraph_seconds\": 1.0, \"lock\": \"at_start\",\n" +
            "    \"anchor\": \"player_pos_at_start\", \"damage\": 1, \"damage_source\": \"telegraph\", \"lob\": true, \"cooldown_seconds\": 3.5, \"leaves_hazard\": " + ThornField + " },\n" +
            "    " + ContactAttack + "] }";

        private const string BoarJson =
            "{ \"id\": \"enemy.boar_tusk\", \"role\": \"long_charger\", \"base_hp\": 160, \"move_speed\": 2.4, \"attack_damage\": 2, \"contact_damage\": 1,\n" +
            "  \"cost\": 5, \"first_wave\": 13, \"times\": [\"tod.day\", \"tod.dusk\"], \"weak\": \"element.ice\", \"resist\": \"element.arcane\",\n" +
            "  \"tags\": [\"tag.heavy\"], \"knockback_immune\": true, \"species_cap\": 3, \"body_radius\": 0.55,\n" +
            "  \"attacks\": [{ \"key\": \"charge\", \"kind\": \"dash\", \"shape\": \"band\", \"width\": 1.6, \"length\": null, \"telegraph_seconds\": 0.9, \"lock\": \"at_start\",\n" +
            "    \"anchor\": \"self\", \"active_ticks\": 0, \"dash_speed\": 16, \"recovery_seconds\": 2.0, \"damage\": 2, \"damage_source\": \"telegraph\",\n" +
            "    \"grazeable\": true, \"is_dash\": true, \"cooldown_seconds\": 3.0 },\n" +
            "    " + ContactAttack + "] }";

        private const string SeedlingJson =
            "{ \"id\": \"enemy.spore_seedling\", \"minion\": true, \"role\": \"swarm\", \"base_hp\": 20, \"move_speed\": 2.5, \"attack_damage\": 1, \"contact_damage\": 1,\n" +
            "  \"cost\": 0, \"first_wave\": 0, \"times\": [], \"weak\": \"element.neutral\", \"resist\": \"element.neutral\",\n" +
            "  \"tags\": [], \"knockback_immune\": false, \"species_cap\": null, \"body_radius\": 0.25,\n" +
            "  \"attacks\": [" + ContactAttack + "] }";

        private const string ContactAttack =
            "{ \"key\": \"contact\", \"kind\": \"contact\", \"damage\": 1, \"damage_source\": \"contact\", \"cooldown_seconds\": 1.5 }";

        private const string SlimeJson =
            "{ \"id\": \"enemy.slime_moss\", \"role\": \"swarm\", \"base_hp\": 20, \"move_speed\": 3.0, \"attack_damage\": 1, \"contact_damage\": 1,\n" +
            "  \"cost\": 1, \"first_wave\": 1, \"times\": [\"tod.day\", \"tod.dusk\", \"tod.night\"], \"weak\": \"element.fire\", \"resist\": \"element.ice\",\n" +
            "  \"tags\": [], \"knockback_immune\": false, \"species_cap\": null, \"body_radius\": 0.35,\n" +
            "  \"attacks\": [" + ContactAttack + "] }";

        private const string WolfJson =
            "{ \"id\": \"enemy.wolf_bramble\", \"role\": \"evasive_charger\", \"base_hp\": 45, \"move_speed\": 4.5, \"attack_damage\": 1, \"contact_damage\": 0,\n" +
            "  \"cost\": 3, \"first_wave\": 3, \"times\": [\"tod.day\", \"tod.dusk\", \"tod.night\"], \"weak\": \"element.lightning\", \"resist\": \"element.dark\",\n" +
            "  \"tags\": [], \"knockback_immune\": false, \"species_cap\": null, \"body_radius\": 0.40,\n" +
            "  \"attacks\": [{ \"key\": \"dash\", \"kind\": \"dash\", \"shape\": \"band\", \"width\": 1.0, \"length\": 6, \"trigger_min\": 3.5, \"trigger_max\": 5.5,\n" +
            "    \"telegraph_seconds\": 0.5, \"lock\": \"at_start\", \"anchor\": \"self\", \"active_ticks\": 20, \"dash_speed\": 18, \"recovery_seconds\": 1.5,\n" +
            "    \"damage\": 1, \"damage_source\": \"telegraph\", \"grazeable\": true, \"is_dash\": true, \"cooldown_seconds\": 0 }],\n" +
            "  \"ai\": { \"dodge_distance\": 1.5, \"dodge_cooldown_seconds\": 1.5, \"dodge_window_seconds\": 0.25, \"dodge_dash_seconds\": 0.1 } }";

        private const string ShamanJson =
            "{ \"id\": \"enemy.shaman_cap\", \"role\": \"ranged_cover\", \"base_hp\": 40, \"move_speed\": 2.5, \"attack_damage\": 1, \"contact_damage\": 1,\n" +
            "  \"cost\": 4, \"first_wave\": 3, \"times\": [\"tod.day\", \"tod.dusk\", \"tod.night\"], \"weak\": \"element.ice\", \"resist\": \"element.dark\",\n" +
            "  \"tags\": [], \"knockback_immune\": false, \"species_cap\": null, \"body_radius\": 0.30,\n" +
            "  \"attacks\": [{ \"key\": \"triple\", \"kind\": \"spread\", \"range\": 9, \"cast_seconds\": 0.4, \"anchor\": \"self\", \"recovery_seconds\": 0.6,\n" +
            "          \"damage\": 1,\n          \"damage_source\": \"projectile\",\n" +
            "    \"proj\": { \"speed\": 6.5, \"count\": 3, \"spread_degrees\": 20, \"range\": 9, \"hit_radius\": 0.12 }, \"grazeable\": true, \"cooldown_seconds\": 2.0 },\n" +
            "    " + ContactAttack + "] }";

        private static TempDataRepo Repo(string enemiesArray)
        {
            var repo = new TempDataRepo();
            repo.Write("data/schema/enemies.schema.json", System.IO.File.ReadAllText(TestRepo.Path("data/schema/enemies.schema.json")));
            repo.Write("data/sim/enemies.json", "{ \"enemies\": " + enemiesArray + " }\n");
            repo.Write("data/names/names.ko.json",
                "{\n  \"enemy.boar_tusk\": \"엄니 멧돼지\",\n  \"enemy.nope\": \"없음\",\n  \"enemy.shaman_cap\": \"갓버섯 술사\",\n  \"enemy.slime_moss\": \"이끼 슬라임\",\n" +
                "  \"enemy.spore_seedling\": \"갓버섯 새싹\",\n  \"enemy.vine_thorn\": \"가시덩굴\",\n  \"enemy.wolf_bramble\": \"덤불 늑대\",\n  \"~end\": \"\"\n}\n");
            return repo;
        }
    }
}
