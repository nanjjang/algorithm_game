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
    // data/sim/enemies.json → 팩 절 5 → ContentDb.Enemy 의 왕복(M3-DAT-01: 원형 4종).
    // 기대값의 출처: spec 05 2.5 표(체력·속도·피해·비용·첫 등장·시간대·약점·저항·꼬리표, 원형 4종 [확정: G 11]),
    // spec 05 4.1~4.4 의 공격 표와 (나) 상태 기계, spec 14 3.6(id·약점·저항·꼬리표), spec 05 13.1 U-04(몸 반지름)·U-05(재사용 대기)·
    // U-28·U-34·U-36 [임시]. 정수 단위: 거리는 Raw(× 2^32), 각도는 센티도(× 100), 시간은 틱(× 60), 이동 속도는 센티 칸/초.
    // 공격 첨자는 data/sim/enemies.json 의 _note 에 적은 순서다(이끼 0 접촉 / 늑대 0 돌진 / 선돌 0 내려찍기 1 휩쓸기 2 접촉 / 술사 0 3갈래 1 접촉).
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

        // 열: id, 역할, 체력, 속도(칸/초 × 100), 피해, 비용, 첫 등장, 약점, 저항, 꼬리표, 넉백 면역, 종 상한(0 = 없음), 접촉 피해
        public static IEnumerable<object[]> Spec05Table25()
        {
            // ① 이끼 슬라임: 20, 3.0, 1, 1, 1, 항상, Fire, Frost, -
            yield return new object[] { EnemyTypeId.SlimeMoss, EnemyDef.EnemyRole.Swarm, 20, 300, 1, 1, 1, Element.Fire, Element.Ice, EnemyTag.None, false, 0, 1 };
            // ② 덤불 늑대: 45, 4.5, 1, 3, 3, 항상(해질녘에 많음), Lightning, Dark, - / 접촉 없음(spec 05 3.6, 4.2 contact_damage = 0)
            yield return new object[] { EnemyTypeId.WolfBramble, EnemyDef.EnemyRole.EvasiveCharger, 45, 450, 1, 3, 3, Element.Lightning, Element.Dark, EnemyTag.None, false, 0, 0 };
            // ③ 선돌지기: 300, 1.8, 2, 6, 5, 항상, Arcane, Lightning, Heavy / 넉백 면역, 종 상한 3(spec 05 2.5, 4.3)
            yield return new object[] { EnemyTypeId.GolemMenhir, EnemyDef.EnemyRole.ArmoredHeavy, 300, 180, 2, 6, 5, Element.Arcane, Element.Lightning, EnemyTag.Heavy, true, 3, 1 };
            // ④ 갓버섯 술사: 40, 2.5, 1, 4, 3, 항상, Frost, Dark, -
            yield return new object[] { EnemyTypeId.ShamanCap, EnemyDef.EnemyRole.RangedCover, 40, 250, 1, 4, 3, Element.Ice, Element.Dark, EnemyTag.None, false, 0, 1 };
        }

        [Theory]
        [MemberData(nameof(Spec05Table25))]
        public void Archetype_Row_MatchesSpec05Table25(EnemyTypeId id, EnemyDef.EnemyRole role, int hp, int speedCenti, int damage, int cost, int firstWave,
            Element weak, Element resist, EnemyTag tags, bool knockbackImmune, int speciesCap, int contactDamage)
        {
            EnemyDef e = RepoDb().Enemy(id);
            Assert.Equal(id, e.Id);
            Assert.Equal(role, e.Role);
            Assert.Equal(hp * OneRaw, e.BaseHp.Raw);
            Assert.Equal(speedCenti, e.MoveSpeedCenti);
            Assert.Equal(damage, e.AttackDamage);
            Assert.Equal(cost, e.Cost);
            Assert.Equal(firstWave, e.FirstWave);
            Assert.Equal(EnemyDef.TimeMask.Always, e.Times);           // 원형 4종은 모두 "항상"
            Assert.Equal(weak, e.Weak);
            Assert.Equal(resist, e.Resist);
            Assert.Equal(tags, e.BaseTags);
            Assert.Equal(knockbackImmune, e.KnockbackImmune);
            Assert.Equal(speciesCap == 0 ? (int?)null : speciesCap, e.SpeciesCap);
            Assert.Equal(contactDamage, e.ContactDamage);
        }

        [Theory]
        [InlineData(EnemyTypeId.SlimeMoss, 1_503_238_554L)]       // 0.35 × 2^32 = 1,503,238,553.6 → 반올림 (U-04)
        [InlineData(EnemyTypeId.WolfBramble, 1_717_986_918L)]     // 0.40 × 2^32 = 1,717,986,918.4
        [InlineData(EnemyTypeId.GolemMenhir, 2_576_980_378L)]     // 0.60 × 2^32 = 2,576,980,377.6
        [InlineData(EnemyTypeId.ShamanCap, 1_288_490_189L)]       // 0.30 × 2^32 = 1,288,490,188.8
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
        }

        [Fact]
        public void EnemiesNotInPack_Throw()
        {
            ContentDb db = RepoDb();
            Assert.Throws<ContentException>(() => db.Enemy(EnemyTypeId.None));
            Assert.Throws<ContentException>(() => db.Enemy(EnemyTypeId.BugLantern));      // 신규 8종은 뒤 작업
            Assert.Throws<ContentException>(() => db.Enemy(EnemyTypeId.ToadDrum));
            Assert.Throws<ContentException>(() => db.Enemy(EnemyTypeId.SporeSeedling));
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

        [Fact]
        public void SameData_SameSectionBytes_InIdOrder()
        {
            byte[] s = Section(RepoPack.Value);
            Assert.Equal(s, Section(BuildPack(TestRepo.Root)));
            Assert.Equal(4, s[0] | (s[1] << 8));
            // 적 항목: 머리 51바이트 + 공격 143바이트 × 수 + AI 64바이트(절 꼴은 ContentDb.Enemies.cs)
            Assert.Equal((int)EnemyTypeId.SlimeMoss, s[2] | (s[3] << 8));
            int wolf = 2 + EntryLength(1);
            Assert.Equal((int)EnemyTypeId.WolfBramble, s[wolf] | (s[wolf + 1] << 8));
            int golem = wolf + EntryLength(1);
            Assert.Equal((int)EnemyTypeId.GolemMenhir, s[golem] | (s[golem + 1] << 8));
            int shaman = golem + EntryLength(3);
            Assert.Equal((int)EnemyTypeId.ShamanCap, s[shaman] | (s[shaman + 1] << 8));
            Assert.Equal(shaman + EntryLength(2), s.Length);
        }

        private static int EntryLength(int attacks) => 51 + 143 * attacks + 64;

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
        public void Problems_Projectiles()
        {
            Expect(ShamanJson, null, null);                                                                  // 원본 그대로는 통과
            Expect(ShamanJson.Replace("\"damage\": 1,\n          \"damage_source\": \"projectile\"", "\"damage\": 2,\n          \"damage_source\": \"projectile\""),
                "/enemies/0/attacks/0/damage", "1칸");
            Expect(ShamanJson.Replace("\"grazeable\": true,", ""), "/enemies/0/attacks/0/grazeable", "P4");
            Expect(ShamanJson.Replace("\"damage_source\": \"projectile\"", "\"damage_source\": \"telegraph\""), "/enemies/0/attacks/0/shape", "예고 모양");
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

        // spec 05 4.1·4.2·4.4 의 줄(data/sim/enemies.json 과 같은 값. TestJson_IsTheRepoData 가 앞 둘을 대조한다)
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
                "{\n  \"enemy.nope\": \"없음\",\n  \"enemy.shaman_cap\": \"갓버섯 술사\",\n  \"enemy.slime_moss\": \"이끼 슬라임\",\n  \"enemy.wolf_bramble\": \"덤불 늑대\",\n  \"~end\": \"\"\n}\n");
            return repo;
        }
    }
}
