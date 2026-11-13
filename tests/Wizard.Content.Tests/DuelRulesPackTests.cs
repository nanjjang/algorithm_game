using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using Wizard.Content.Tests.Pack;
using Wizard.Core;
using Wizard.DataBuild.Core;
using Wizard.TestKit;
using Xunit;

namespace Wizard.Content.Tests
{
    // data/sim/duel_rules.json → 팩 절 13 → ContentDb.DuelRules·CharacterDef.Duel 의 왕복(M14-DAT-01).
    // 기대값은 spec 10 2.1~2.4 상수표(틱/고정 환산 칸), 3.4·3.5·22.2절의 [임시] 값, 9.2 방 옵션 표, 7.2~7.3 캐릭터별 결투 값
    // (world-characters 5.4), spec 04 8절에서 온다. 각도는 센티도(도 × 100), 반지름은 Q32.32 Raw(칸 × 2^32, 반올림).
    public sealed class DuelRulesPackTests
    {
        private const long RawPerCell = 4294967296L;                // 2^32

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

        private static ContentDb Db() => ContentDb.Build(ContentPack.Load(RepoPack.Value));

        private static DuelRuleSet Rules() => Db().DuelRules;

        // ── spec 10 2.1 경기 형식 ──
        [Fact]
        public void Spec10_2_1_MatchFormat()
        {
            DuelRuleSet d = Rules();
            Assert.Equal(2, d.Players);                          // players 1 대 1
            Assert.Equal(2, d.RoundsToWin);                      // 3판 2선승
            Assert.Equal(5400, d.RoundLengthTicks);              // 90초
            Assert.Equal(180, d.CountdownTicks);                 // 3초
            Assert.Equal(100, d.HpPerCell);                      // 100 × 체력 칸
            Assert.Equal(0, d.RoundStartHeatHu);                 // 열 0
            Assert.Equal(1, d.RoundStartCrystals);               // 결정 1개
            Assert.Equal(1800, d.OvertimeTicks);                 // 30초
            Assert.Equal(2000, d.OvertimeDamagePermille);        // 받는 피해 2배
            Assert.Equal(180, d.IntermissionTicks);              // 라운드 사이 3초 [임시 U-26]
        }

        [Fact]
        public void Spec10_2_1_And_3_4_Thorns()
        {
            DuelRuleSet d = Rules();
            Assert.Equal(3600, d.ThornStartTicks);               // 60초부터
            Assert.Equal(300, d.ThornStepTicks);                 // 5초에 1칸
            Assert.Equal(30, d.ThornDamageIntervalTicks);        // 0.5초마다
            Assert.Equal(40, d.ThornDamagePermille);             // 최대 체력의 4%
            Assert.Equal(6, d.ThornRingMax);                     // THORN_RING_MAX = 6 [임시 U-03]
            // thornStart = roundLength − 1800 [임시 U-02]: 90초 → 3,600, 60초 → 1,800, 120초 → 5,400
            Assert.Equal(1800, d.ThornLeadTicks);
            Assert.Equal(d.ThornStartTicks, d.RoundLengthTicks - d.ThornLeadTicks);
            Assert.Equal(1800, 60 * 60 - d.ThornLeadTicks);
            Assert.Equal(5400, 120 * 60 - d.ThornLeadTicks);
        }

        [Fact]
        public void Spec10_2_1_And_3_5_WildIntrusion()
        {
            DuelRuleSet d = Rules();
            Assert.Equal(EnemyTypeId.SlimeMoss, d.WildEnemy);    // 이끼 슬라임(M 2.1)
            Assert.Equal(1200, d.WildSpawnIntervalTicks);        // 20초마다
            Assert.Equal(4, d.WildSpawnCount);                   // 4마리
            Assert.Equal(100, d.WildContactDamage);              // 100
            Assert.Equal(60, d.WildContactCooldownTicks);        // 같은 플레이어에게 60틱에 1번 [임시 U-27]
            // 네 구석 (1.5,1.5) (22.5,1.5) (1.5,12.5) (22.5,12.5) [임시 U-27]. 1.5칸 = 6,442,450,944
            Assert.Equal(new[]
            {
                new FixVec2(Fix64.FromRaw(6442450944L), Fix64.FromRaw(6442450944L)),
                new FixVec2(Fix64.FromRaw(45 * RawPerCell / 2), Fix64.FromRaw(6442450944L)),
                new FixVec2(Fix64.FromRaw(6442450944L), Fix64.FromRaw(25 * RawPerCell / 2)),
                new FixVec2(Fix64.FromRaw(45 * RawPerCell / 2), Fix64.FromRaw(25 * RawPerCell / 2)),
            }, d.WildSpawnPoints);
        }

        // ── spec 10 2.2 결투에서 바뀌는 전투 수치 ──
        [Fact]
        public void Spec10_2_2_CombatNumbers()
        {
            DuelRuleSet d = Rules();
            Assert.Equal(30, d.EnhancePerStagePermille);         // 1 + 0.03 × 단계 → (1000 + 30s)/1000
            Assert.Equal(1390, 1000 + 13 * d.EnhancePerStagePermille);   // +13 = 1.39배 (4.1)
            Assert.Equal(1000, d.ElementMultPermille);           // 속성 배율 없음(1)
            Assert.Equal(30, d.BurstWindowTicks);                // 0.5초
            Assert.Equal(300, d.BurstCapPermille);               // 최대 체력의 30%
            Assert.Equal(0, d.PostHitInvulnTicks);               // 피격 뒤 무적 없음
            Assert.Equal(1288490189L, d.HitRadius.Raw);          // 0.30, Q32.32 1,288,490,189
            Assert.Equal(1717986918L, d.HitCenterOffsetY.Raw);   // 0.4, Q32.32 1,717,986,918
            Assert.Equal(15, d.ChainLightningAimTicks);          // 0.25초 조준선
            Assert.Equal(0, d.ChainLightningJumps);              // 전이 없음
            Assert.Equal(500, d.HomingTurnPermille);             // 선회각 절반
            Assert.Equal(90, d.HomingDurationTicks);             // 1.5초 뒤 유도 끊김
            Assert.Equal(150, d.IceSpikeSlowPermille);           // −15%
            Assert.Equal(60, d.IceSpikeSlowTicks);               // 1초
            Assert.Equal(100, d.FrostSlowPermille);              // −10%
            Assert.Equal(60, d.FrostSlowTicks);
            Assert.Equal(80, d.ErosionBonusPermille);            // +8% (108/100)
            Assert.Equal(180, d.ErosionTicks);                   // 3초
            Assert.Equal(3, d.StatusApplyDelayTicks);            // 적중 3틱 뒤
            Assert.Equal(2, d.ArtPrepTicks);                     // 판정 뒤 준비 2틱
            Assert.Equal(150, d.BulletCapPerPlayer);             // 한 사람당 150
            Assert.Equal(1500, d.UnstableCoreRadiusPermille);    // 반경 +50% = 150/100
            Assert.Equal(180, d.GlassWandOffTicks);              // 맞은 뒤 3초 임계 +1 꺼짐
        }

        // 2.2의 recoil_knockback·lightspear_stun 은 공용 비기의 결투 값이라 arts.json 의 duel 칸이 가진다(spec 03 16절)
        [Fact]
        public void Spec10_2_2_ArtDuelNumbersLiveInArts()
        {
            ContentDb db = Db();
            Assert.Equal(6442450944L, db.Art(ArtId.Recoil).Duel.Knockback.Raw);    // 1.5칸, Q32.32 6,442,450,944
            Assert.Equal(30, db.Art(ArtId.ThunderSpear).Duel.StunTicks);          // 0.5초
        }

        // ── spec 10 2.3 스침과 벌칙 ──
        [Fact]
        public void Spec10_2_3_GrazeAndPenalty()
        {
            DuelRuleSet d = Rules();
            Assert.Equal(60, d.GrazeAcceptTicks);                // 상대 한 명당 1.0초에 1회
            Assert.Equal(1, d.GrazeCombo);                       // +1
            Assert.Equal(60, d.PenaltyIntervalTicks);            // 1.0초에 1회
            Assert.Equal(60, d.PenaltyWindowTicks);              // 1.0초
            // graze_detect_radius: 캐릭터 값 그대로 0.6 / 초롱 0.75 → 2,576,980,378 / 3,221,225,472
            ContentDb db = Db();
            Assert.Equal(2576980378L, db.Character(CharacterId.Char1).GrazeRadius.Raw);
            Assert.Equal(3221225472L, db.Character(CharacterId.Char2).GrazeRadius.Raw);
        }

        // ── spec 10 2.4 조준 (각도는 센티도) ──
        [Fact]
        public void Spec10_2_4_Aim()
        {
            DuelRuleSet d = Rules();
            Assert.Equal(1000, d.AimAssistMaxCenti);             // 최대 10도
            Assert.Equal(1500, d.AimAssistFallbackCenti);        // 15도
            Assert.Equal(2147483648L, d.AimLineLength.Raw);      // 0.5칸
            Assert.Equal(2500, d.TwistedBarrelJitterCenti);      // ±25도
        }

        // ── spec 10 9.2 방 옵션의 선택지와 기본값(기본 [임시] U-31) ──
        [Fact]
        public void Spec10_9_2_RoomOptions()
        {
            DuelRuleSet.RoomRules r = Rules().Room;
            Assert.Equal(new[] { 1, 3, 5 }, r.RoundsChoices);
            Assert.Equal(3, r.DefaultRounds);
            Assert.Equal(new[] { 60, 90, 120 }, r.RoundSecondsChoices);
            Assert.Equal(90, r.DefaultRoundSeconds);
            Assert.Equal(new[] { 700, 1000, 1500 }, r.HpMultPermilleChoices);
            Assert.Equal(1000, r.DefaultHpMultPermille);
            Assert.Equal(0, r.EnhanceCapMin);
            Assert.Equal(13, r.EnhanceCapMax);
            Assert.Equal(13, r.DefaultEnhanceCap);
            Assert.Equal(0, r.IlCapMin);
            Assert.Equal(237, r.IlCapMax);
            Assert.Equal(DuelRuleSet.RoomRules.NoCap, r.DefaultIlCap);
            Assert.True(r.DefaultAllowMutations);
            Assert.Equal(0, r.HeroBlockCapMin);
            Assert.Equal(8, r.HeroBlockCapMax);
            Assert.Equal(DuelRuleSet.RoomRules.NoCap, r.DefaultHeroBlockCap);
            Assert.False(r.DefaultEqualize);
            Assert.False(r.DefaultWildIntrusion);
            Assert.False(r.DefaultThornsOff);
            Assert.Equal(new[] { TimeOfDay.Day, TimeOfDay.Dusk, TimeOfDay.Night }, r.FloorColorChoices);
            Assert.Equal(TimeOfDay.Day, r.DefaultFloorColor);
            Assert.False(r.DefaultSpectate);
            Assert.Equal(6, r.SpectatorMax);                     // 2.5 room_spectator_max
            Assert.Equal(-1, DuelRuleSet.RoomRules.NoCap);       // DuelRoomOptions.NoCap(CONTRACTS 3.9)과 같은 값
        }

        // 방 기본값이 자동 매치의 형식과 같다(2.1의 3판 2선승·90초 = 9.2의 기본 3판·90초)
        [Fact]
        public void Spec10_9_2_DefaultsMatchFormat()
        {
            DuelRuleSet d = Rules();
            Assert.Equal(d.RoundsToWin, (d.Room.DefaultRounds + 1) / 2);
            Assert.Equal(d.RoundLengthTicks, d.Room.DefaultRoundSeconds * 60);
        }

        // ── spec 10 7.2 결투 체력과 벌칙 1회의 결정 손실 ──
        [Theory]
        [InlineData(CharacterId.Char1, 800, 1, 1)]
        [InlineData(CharacterId.Char2, 400, 1, 1)]
        [InlineData(CharacterId.Char3, 600, 1, 1)]
        [InlineData(CharacterId.Char4, 700, 1, 2)]             // 금 규칙(2회에 1)
        [InlineData(CharacterId.Char5, 500, 2, 1)]             // 벌칙 1회에 2개
        public void Spec10_7_2_DuelHpAndPenaltyLoss(CharacterId id, int duelHp, int loss, int penaltiesPerLoss)
        {
            ContentDb db = Db();
            DuelRuleSet d = db.DuelRules;
            CharacterDef c = db.Character(id);
            Assert.Equal(duelHp, d.HpPerCell * c.MaxHpCells);
            DuelCharacterOverride o = d.Characters[(int)id]!;
            Assert.Equal(id, o.Character);
            Assert.Equal(loss, o.PenaltyCrystalLoss);
            Assert.Equal(penaltiesPerLoss, o.PenaltiesPerCrystalLoss);
            Assert.Same(o, c.Duel);                              // CharacterDef.Duel 과 같은 객체
        }

        // ── spec 10 7.3 능력별 결투 값(패시브·지팡이 특성) ──
        [Fact]
        public void Spec10_7_3_Char1_Overflow()
        {
            DuelCharacterOverride o = Db().Character(CharacterId.Char1).Duel;
            Assert.Equal(1500, o.OverflowDamagePermille);        // 1.5T 그대로
            Assert.Equal(10737418240L, o.OverflowRadius.Raw);    // 반경 2.5 (W 4.1) = 2.5 × 2^32
            Assert.Equal(RawPerCell, o.OverflowKnockback.Raw);   // 넉백 1칸
            Assert.False(o.OverflowClearsBullets);               // 상대 탄 안 지움
            AssertNoRod(o);
            AssertNoThinIce(o);
            Assert.Equal(0, o.ContractPerCrystalPermille);
            Assert.Equal(0, o.DebtPenaltyExtraCells);
        }

        [Fact]
        public void Spec10_7_3_Char2_LightningRod()
        {
            DuelCharacterOverride o = Db().Character(CharacterId.Char2).Duel;
            Assert.Equal(500, o.RodDamagePermille);              // 0.5T
            Assert.Equal(120, o.RodCooldownTicks);               // 2초에 1회
            Assert.Equal(15, o.RodAimTicks);                     // 0.25초
            Assert.Equal(5 * RawPerCell, o.RodRange.Raw);        // 반경 5
            Assert.Equal(2147483648L, o.RodMarkerRadius.Raw);    // 지름 1칸 → 반경 0.5
            AssertNoOverflow(o);
            AssertNoThinIce(o);
        }

        [Fact]
        public void Spec10_7_3_Char3_Unchanged()
        {
            DuelCharacterOverride o = Db().Character(CharacterId.Char3).Duel;
            AssertNoOverflow(o);
            AssertNoRod(o);
            AssertNoThinIce(o);
            Assert.Equal(0, o.ContractPerCrystalPermille);
            Assert.Equal(0, o.DebtPenaltyExtraCells);
        }

        [Fact]
        public void Spec10_7_3_Char4_ThinIce()
        {
            DuelCharacterOverride o = Db().Character(CharacterId.Char4).Duel;
            Assert.Equal(6, o.ThinIceHits);                      // 6번 맞히면
            Assert.Equal(300, o.ThinIceSlowPermille);            // 이동 −30%
            Assert.Equal(60, o.ThinIceSlowTicks);                // 1.0초
            Assert.Equal(360, o.ThinIceImmuneTicks);             // 6초 재적용 없음
            Assert.False(o.ThinIceFreezes);                      // 얼지 않는다
            AssertNoOverflow(o);
            AssertNoRod(o);
        }

        [Fact]
        public void Spec10_7_3_Char5_ContractAndCredit()
        {
            DuelCharacterOverride o = Db().Character(CharacterId.Char5).Duel;
            Assert.Equal(30, o.ContractPerCrystalPermille);      // 1개당 +3%
            Assert.Equal(210, 7 * o.ContractPerCrystalPermille); // 7개에서 +21%
            Assert.Equal(1, o.DebtPenaltyExtraCells);            // +1칸
            Assert.Equal(100, o.DebtPenaltyExtraCells * Rules().HpPerCell);   // = +100
            AssertNoOverflow(o);
            AssertNoRod(o);
            AssertNoThinIce(o);
        }

        // 7.3 고유 비기의 결투 줄은 arts.json 의 duel 칸에 있다(spec 03 14절). 여기서는 7.3 표와 맞는지만 대조한다
        [Fact]
        public void Spec10_7_3_UniqueArtDuelValuesLiveInArts()
        {
            ContentDb db = Db();
            Assert.Equal(100, db.Art(ArtId.Kiln).Duel.SlowPermille);              // 불가마 상대 감속 −10%
            Assert.Equal(18, db.Art(ArtId.BoltDash).Duel.StaggerTicks);           // 벼락 질주 경직 0.3초
            Assert.Equal(12, db.Art(ArtId.BoltDash).Duel.InvulnTicks);            // 무적 0.2초 그대로
            Assert.Equal(8, db.Art(ArtId.CopyCircle).Duel.MaxCopies);             // 베껴 쓰기 최대 8번
            Assert.Equal(150, db.Art(ArtId.IceScreen).Duel.WallHp);               // 얼음 병풍 체력 150
            Assert.Equal(2000, db.Art(ArtId.IceScreen).Duel.DamagePermille);      // 파편 2T
            Assert.Equal(240, db.Art(ArtId.IceScreen).Duel.DurationTicks);        // 지속 4초 그대로
            Assert.Equal(2 * RawPerCell, db.Art(ArtId.ShadowHand).Duel.PullDistance.Raw);   // 당김 2칸
            Assert.Equal(0, db.Art(ArtId.ShadowHand).Duel.StaggerTicks);          // 경직 없음
            Assert.Equal(18, db.Art(ArtId.ShadowHand).Duel.TelegraphTicks);       // 0.3초 범위 표시
        }

        [Fact]
        public void Characters_IndexedByIdBaselineEmpty()
        {
            DuelRuleSet d = Rules();
            Assert.Equal(6, d.Characters.Length);
            Assert.Null(d.Characters[(int)CharacterId.Baseline]);
            Assert.Equal(new DuelCharacterOverride().PenaltyCrystalLoss, Db().Character(CharacterId.Baseline).Duel.PenaltyCrystalLoss);
        }

        private static void AssertNoOverflow(DuelCharacterOverride o)
        {
            Assert.Equal(0, o.OverflowDamagePermille);
            Assert.Equal(0L, o.OverflowRadius.Raw);
            Assert.Equal(0L, o.OverflowKnockback.Raw);
            Assert.False(o.OverflowClearsBullets);
        }

        private static void AssertNoRod(DuelCharacterOverride o)
        {
            Assert.Equal(0, o.RodDamagePermille);
            Assert.Equal(0, o.RodCooldownTicks);
            Assert.Equal(0, o.RodAimTicks);
            Assert.Equal(0L, o.RodRange.Raw);
            Assert.Equal(0L, o.RodMarkerRadius.Raw);
        }

        private static void AssertNoThinIce(DuelCharacterOverride o)
        {
            Assert.Equal(0, o.ThinIceHits);
            Assert.Equal(0, o.ThinIceSlowPermille);
            Assert.Equal(0, o.ThinIceSlowTicks);
            Assert.Equal(0, o.ThinIceImmuneTicks);
            Assert.False(o.ThinIceFreezes);
        }

        // ── 팩·절 ──
        [Fact]
        public void PackWithoutTheSection_Throws()
        {
            ContentDb db = Load(null);
            Assert.Throws<ContentException>(() => db.DuelRules);
        }

        [Fact]
        public void SameData_SameSectionBytes()
        {
            Assert.Equal(Section(RepoPack.Value), Section(BuildPack(TestRepo.Root)));
        }

        [Fact]
        public void ReversedCharacterOrder_GivesTheSameBytes()
        {
            JsonObject o = RepoJson();
            var arr = (JsonArray)o["characters"]!;
            var rows = new List<JsonNode>();
            foreach (JsonNode? n in arr)
            {
                rows.Add(n!.DeepClone());
            }
            rows.Reverse();
            var rev = new JsonArray();
            foreach (JsonNode n in rows)
            {
                rev.Add(n);
            }
            o["characters"] = rev;
            using (TempDataRepo repo = Repo(o.ToJsonString()))
            {
                Assert.Empty(Problems(repo.Root));
                Assert.Equal(Section(RepoPack.Value), Section(BuildPack(repo.Root)));
            }
        }

        [Fact]
        public void Problems_CrossChecks()
        {
            Expect(o => o["thorns"]!["lead_seconds"] = 20, "/thorns/lead_seconds");
            Expect(o => o["room"]!["rounds"]!["default"] = 4, "/room/rounds/default");
            Expect(o => o["room"]!["rounds"]!["choices"] = new JsonArray(1, 2, 3), "/room/rounds/choices");
            Expect(o => o["room"]!["rounds"]!["choices"] = new JsonArray(5, 3, 1), "/room/rounds/choices");
            Expect(o => o["room"]!["round_seconds"]!["default"] = 60, "/room/round_seconds/default");
            Expect(o => o["room"]!["hp_multiplier"]!["default"] = 2, "/room/hp_multiplier/default");
            Expect(o => o["room"]!["enhance_cap"]!["default"] = 14, "/room/enhance_cap");
            Expect(o => o["room"]!["enhance_cap"]!["default"] = null, "/room/enhance_cap/default");
            Expect(o => o["room"]!["il_cap"]!["default"] = 300, "/room/il_cap/default");
            Expect(o => o["room"]!["floor_color"]!["default"] = "tod.boss", "/room/floor_color/default");
            Expect(o => o["room"]!["floor_color"]!["choices"] = new JsonArray("tod.day", "tod.day"), "/room/floor_color/choices");
            Expect(o => o["wild"]!["enemy"] = "enemy.nothing", "/wild/enemy");
            Expect(o => o["wild"]!["spawn_points"]![0]!["x"] = 24, "/wild/spawn_points/0");
            Expect(o => o["characters"]![0]!["character"] = "char.9", "/characters/0/character");
            Expect(o => o["characters"]![1]!["character"] = "char.1", "/characters/1/character");
            Expect(o => ((JsonArray)o["characters"]!).RemoveAt(4), "/characters");
        }

        [Fact]
        public void Problems_Schema()
        {
            Expect(o => o["combat"]!["hit_radius"] = 0, "스키마");
            Expect(o => o["thorns"]!["ring_max"] = 7, "스키마");
            Expect(o => o["match"]!["players"] = 3, "스키마");
            Expect(o => ((JsonObject)o["aim"]!).Remove("assist_max_degrees"), "스키마");
            Expect(o => o["combat"]!["respawn"] = true, "스키마");                // 모르는 필드
            Expect(o => o["characters"]![0]!["overflow"]!["speed"] = 1, "스키마");
            Expect(o => o["characters"]![0]!["character"] = "enemy.slime_moss", "스키마");
        }

        private static void Expect(Action<JsonObject> edit, string what)
        {
            JsonObject o = RepoJson();
            edit(o);
            using (TempDataRepo repo = Repo(o.ToJsonString()))
            {
                List<string> p = Problems(repo.Root);
                Assert.Contains(p, s => s.Contains("data/sim/duel_rules.json") && s.Contains(what));
            }
        }

        [Fact]
        public void BrokenSectionBytes_ThrowContentException()
        {
            byte[] good = Section(RepoPack.Value);
            Assert.Throws<ContentException>(() => Load(good.AsSpan(0, good.Length - 1).ToArray()));
            var longer = new byte[good.Length + 1];
            good.CopyTo(longer, 0);
            Assert.Throws<ContentException>(() => Load(longer));
            Assert.Throws<ContentException>(() => Load(new byte[0]));

            // 야생 적(match 10 + thorns 6 개의 i32 뒤 u16)이 None, 모르는 값
            byte[] bad = (byte[])good.Clone();
            bad[16 * 4] = 250;
            bad[16 * 4 + 1] = 250;
            Assert.Throws<ContentException>(() => Load(bad));
            bad[16 * 4] = 0;
            bad[16 * 4 + 1] = 0;
            Assert.Throws<ContentException>(() => Load(bad));

            // 캐릭터 줄: 마지막 줄의 u8 id 를 Baseline(0), 모르는 값, 앞 줄과 같은 값으로
            int rowLength = 1 + 4 * 2 + 4 + 8 + 8 + 1 + 4 * 3 + 8 + 8 + 4 * 4 + 1 + 4 + 4;
            int last = good.Length - rowLength;
            foreach (byte v in new byte[] { 0, 9, 4 })
            {
                bad = (byte[])good.Clone();
                bad[last] = v;
                Assert.Throws<ContentException>(() => Load(bad));
            }
            // 마지막 줄의 thin_ice_freezes(bool)를 2로
            bad = (byte[])good.Clone();
            bad[last + 1 + 4 * 2 + 4 + 8 + 8 + 1 + 4 * 3 + 8 + 8 + 4 * 4] = 2;
            Assert.Throws<ContentException>(() => Load(bad));
            // 마지막 줄의 penalties_per_crystal_loss 를 0으로
            bad = (byte[])good.Clone();
            WriteLe(bad, last + 1 + 4, 0);
            Assert.Throws<ContentException>(() => Load(bad));

            Load(good);
        }

        // 캐릭터 절 없이 결투 절만 있어도 읽힌다(CharacterDef 는 없다)
        [Fact]
        public void DuelSectionAlone_Loads()
        {
            ContentDb db = Load(Section(RepoPack.Value));
            Assert.Equal(150, db.DuelRules.BulletCapPerPlayer);
            Assert.Throws<ContentException>(() => db.Character(CharacterId.Char1));
        }

        private static void WriteLe(byte[] b, int at, int v)
        {
            for (int i = 0; i < 4; i++)
            {
                b[at + i] = (byte)(v >> (8 * i));
            }
        }

        private static JsonObject RepoJson() => (JsonObject)JsonNode.Parse(System.IO.File.ReadAllText(TestRepo.Path("data/sim/duel_rules.json")))!;

        private static ContentDb Load(byte[]? section)
        {
            var sections = section == null ? new PackSection[0] : new[] { new PackSection((ushort)ContentSection.DuelRules, section) };
            return ContentDb.Build(ContentPack.Load(PackWriter.Write(PackWriter.SchemaVersion, 1, new byte[PackWriter.CoreCompatLength], sections)));
        }

        private static byte[] Section(byte[] pack)
        {
            Assert.True(ContentPack.Load(pack).TryGetSection((ushort)ContentSection.DuelRules, out ReadOnlyMemory<byte> s));
            return s.ToArray();
        }

        private static TempDataRepo Repo(string duelRules)
        {
            var repo = new TempDataRepo();
            repo.Write("data/schema/duel_rules.schema.json", System.IO.File.ReadAllText(TestRepo.Path("data/schema/duel_rules.schema.json")));
            repo.Write("data/sim/duel_rules.json", duelRules + "\n");
            return repo;
        }
    }
}
