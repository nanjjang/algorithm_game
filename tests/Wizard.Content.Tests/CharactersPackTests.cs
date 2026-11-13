using System;
using System.Collections.Generic;
using Wizard.Content.Tests.Pack;
using Wizard.Core;
using Wizard.DataBuild.Core;
using Wizard.Sim.Heat;
using Wizard.Sim.World;
using Wizard.TestKit;
using Xunit;

namespace Wizard.Content.Tests
{
    // data/sim/characters.json → 팩 절 1 → ContentDb.Character 의 왕복(M1-CONT-03 기준값, M5-DAT-01 다섯 캐릭터).
    // 기대값은 명세의 표에서 온다: spec 01 5.1(기준값 열·D1~D5 열), 2.2(Raw 상수), 2.3(틱당 이동량), 2.4(HU), 2.5(틱),
    // 5.6(몸 반지름·스침 가속), spec 04 2.2(캐릭터별 값)·9.2(강화 배율)·9.4(임계 시작)·15.1(벡터 N1~N10), spec 14 3.2(id).
    public sealed class CharactersPackTests
    {
        // 칸 → Raw(spec 01 2.2 표의 값)
        private const long Raw015 = 644_245_094;
        private const long Raw030 = 1_288_490_189;
        private const long Raw060 = 2_576_980_378;
        private const long Raw075 = 3_221_225_472;

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

        [Fact]
        public void Baseline_RoundTrip_EveryField_IsTheSpecInteger()
        {
            CharacterDef c = RepoDb().Character(CharacterId.Baseline);
            Assert.Equal(CharacterId.Baseline, c.Id);
            Assert.Equal(Element.None, c.Element);               // spec 01 5.1 "—"
            Assert.Equal(PassiveId.None, c.Passive);
            Assert.Equal(WandTraitId.None, c.WandTrait);
            Assert.Equal(ArtId.None, c.UniqueArt);
            Assert.Equal(6, c.MaxHpCells);
            Assert.Equal(500, c.WalkSpeedCenti);                 // 5.0 칸/초
            Assert.Equal(425, c.StanceSpeedCenti);               // 4.25 칸/초
            Assert.Equal(Raw015, c.HitRadius.Raw);               // 0.15
            Assert.Equal(Raw030, c.BodyRadius.Raw);              // 0.30 (spec 01 5.6)
            Assert.Equal(Raw060, c.GrazeRadius.Raw);             // 0.6
            Assert.Equal(60, c.InvulnTicks);                     // 1초
            Assert.Equal(5, c.CrystalCap);
            Assert.Equal(15, c.ComboPerCrystal);
            Assert.Equal(1, c.CrystalLossOnHit);
            Assert.Equal(4, c.BaseLinkSlots);
            Assert.Equal(2, c.MaxMutations);
            Assert.Equal(90, c.CoolHuPerTick);                   // 초당 45 → 틱당 90 HU (spec 01 2.4)
            Assert.Equal(12, c.CoolDelayTicks);                  // 0.2초
            Assert.Equal(10_200, c.CritStartHu);                 // 85 (spec 01 2.4)
            Assert.Equal(180, c.LockTicks);                      // 3.0초
            Assert.Equal(90, c.GrazeBuffTicks);                  // 1.5초
            Assert.Equal(1200, c.GrazeBuffPermille);             // +20% (spec 01 5.6)
            Assert.NotNull(c.Duel);
        }

        // 저장소 데이터에는 기준값과 다섯 캐릭터가 있고 그 밖의 id 는 없다
        [Fact]
        public void RepoData_HasBaselineAndFiveCharacters_OtherIdsThrow()
        {
            ContentDb db = RepoDb();
            Assert.Equal(CharacterId.Baseline, db.Character(CharacterId.Baseline).Id);
            foreach (CharacterId c in Five)
            {
                Assert.Equal(c, db.Character(c).Id);
            }
            Assert.Throws<ContentException>(() => db.Character((CharacterId)6));
            Assert.Throws<ContentException>(() => db.Character((CharacterId)200));
        }

        private static readonly CharacterId[] Five =
        {
            CharacterId.Char1, CharacterId.Char2, CharacterId.Char3, CharacterId.Char4, CharacterId.Char5,
        };

        // spec 04 2.2 표의 한 열(+ spec 01 5.1의 같은 열). 순서: char.1~char.5
        private sealed class Row
        {
            public CharacterId Id;
            public Element Element;
            public PassiveId Passive;
            public WandTraitId Trait;
            public ArtId Unique;
            public int Hp, Walk, Stance, Cap, Combo, Loss, Slots, Mutations, Cool, Crit, Lock, GrazeTicks;
            public long GrazeRaw;
        }

        private static readonly Row[] Table =
        {
            // 1 달구: 체력 8, 4.7/3.76, 결정 5·15, 손실 1, 슬롯 4, 변이 2, 냉각 45, 임계 80(지팡이 특성 반영), 잠금 3.0, 감지 0.6, 가속 1.5
            new Row { Id = CharacterId.Char1, Element = Element.Fire, Passive = PassiveId.Overflow, Trait = WandTraitId.HeatAt80, Unique = ArtId.Kiln,
                      Hp = 8, Walk = 470, Stance = 376, Cap = 5, Combo = 15, Loss = 1, Slots = 4, Mutations = 2, Cool = 90, Crit = 9_600, Lock = 180,
                      GrazeRaw = Raw060, GrazeTicks = 90 },
            // 2 초롱: 체력 4, 5.6/5.04, 결정 5·12, 손실 1, 슬롯 4, 변이 3, 냉각 50, 임계 85, 잠금 2.5, 감지 0.75, 가속 2.0
            new Row { Id = CharacterId.Char2, Element = Element.Lightning, Passive = PassiveId.LightningRod, Trait = WandTraitId.ThreeMutations, Unique = ArtId.BoltDash,
                      Hp = 4, Walk = 560, Stance = 504, Cap = 5, Combo = 12, Loss = 1, Slots = 4, Mutations = 3, Cool = 100, Crit = 10_200, Lock = 150,
                      GrazeRaw = Raw075, GrazeTicks = 120 },
            // 3 갈피: 체력 6, 5.0/4.25, 결정 4·15, 손실 1, 슬롯 5, 변이 2, 냉각 45, 임계 85, 잠금 3.5, 감지 0.6, 가속 1.5
            new Row { Id = CharacterId.Char3, Element = Element.Arcane, Passive = PassiveId.Underline, Trait = WandTraitId.DepthDecay, Unique = ArtId.CopyCircle,
                      Hp = 6, Walk = 500, Stance = 425, Cap = 4, Combo = 15, Loss = 1, Slots = 5, Mutations = 2, Cool = 90, Crit = 10_200, Lock = 210,
                      GrazeRaw = Raw060, GrazeTicks = 90 },
            // 4 성에: 체력 7, 4.5/4.5, 결정 6·15, 손실 0(얼린 결정), 슬롯 4, 변이 2, 냉각 45, 임계 90, 잠금 3.0, 감지 0.6, 가속 1.5
            new Row { Id = CharacterId.Char4, Element = Element.Ice, Passive = PassiveId.ThinIce, Trait = WandTraitId.KeepCrystal, Unique = ArtId.IceScreen,
                      Hp = 7, Walk = 450, Stance = 450, Cap = 6, Combo = 15, Loss = 0, Slots = 4, Mutations = 2, Cool = 90, Crit = 10_800, Lock = 180,
                      GrazeRaw = Raw060, GrazeTicks = 90 },
            // 5 그믐: 체력 5, 5.2/4.42, 결정 7·15, 손실 2, 슬롯 4, 변이 2, 냉각 45, 임계 85, 잠금 3.0, 감지 0.6, 가속 1.5
            new Row { Id = CharacterId.Char5, Element = Element.Dark, Passive = PassiveId.ShadowContract, Trait = WandTraitId.OnCredit, Unique = ArtId.ShadowHand,
                      Hp = 5, Walk = 520, Stance = 442, Cap = 7, Combo = 15, Loss = 2, Slots = 4, Mutations = 2, Cool = 90, Crit = 10_200, Lock = 180,
                      GrazeRaw = Raw060, GrazeTicks = 90 },
        };

        // spec 04 15.1-1: 다섯 CharacterDef 의 필드가 2.2 표와 같다. 벡터가 적은 필드(체력·이동·자세·결정 상한·결정당 콤보·슬롯·냉각·잠금·감지)에
        // 더해 표의 나머지 줄(속성·패시브·특성·고유 비기, 냉각 대기, 임계, 손실, 피격 반지름, 무적, 스침 가속)과 spec 01 5.1의 변이 수·몸 반지름까지 본다.
        // 감지 반지름은 명세가 u(1/6000칸)로 적었다(3600·4500u = 0.6·0.75칸). CONTRACTS 0.3에 따라 Raw 로 바꿔 읽는다(spec 01 2.2 표의 값)
        [Fact]
        public void S04_N1__FiveCharacterDefs_MatchTheTable()
        {
            ContentDb db = RepoDb();
            Assert.Equal(5, Table.Length);
            foreach (Row w in Table)
            {
                CharacterDef c = db.Character(w.Id);
                Assert.Equal(w.Id, c.Id);
                Assert.Equal(w.Element, c.Element);
                Assert.Equal(w.Passive, c.Passive);
                Assert.Equal(w.Trait, c.WandTrait);
                Assert.Equal(w.Unique, c.UniqueArt);
                Assert.Equal(w.Hp, c.MaxHpCells);
                Assert.Equal(w.Walk, c.WalkSpeedCenti);
                Assert.Equal(w.Stance, c.StanceSpeedCenti);
                Assert.Equal(Raw015, c.HitRadius.Raw);           // 0.15칸, 다섯 모두
                Assert.Equal(Raw030, c.BodyRadius.Raw);          // 0.30칸, spec 01 5.6 [임시]
                Assert.Equal(w.GrazeRaw, c.GrazeRadius.Raw);
                Assert.Equal(60, c.InvulnTicks);                 // 1.0초
                Assert.Equal(w.Cap, c.CrystalCap);
                Assert.Equal(w.Combo, c.ComboPerCrystal);
                Assert.Equal(w.Loss, c.CrystalLossOnHit);
                Assert.Equal(w.Slots, c.BaseLinkSlots);
                Assert.Equal(w.Mutations, c.MaxMutations);
                Assert.Equal(w.Cool, c.CoolHuPerTick);
                Assert.Equal(12, c.CoolDelayTicks);              // 0.2초
                Assert.Equal(w.Crit, c.CritStartHu);
                Assert.Equal(w.Lock, c.LockTicks);
                Assert.Equal(w.GrazeTicks, c.GrazeBuffTicks);
                Assert.Equal(1200, c.GrazeBuffPermille);         // +20%
                Assert.True(c.CrystalCap >= 3);                  // 만개(최소 3결정)가 된다(spec 04 2.4 [임시])
                Assert.NotNull(c.Duel);
            }
        }

        // spec 04 15.1-2: 캐릭터 1(지팡이는 그 캐릭터만 쓴다: 끓는 부지깽이), 변이 없음 → 임계 시작 9,600(80).
        // 변이 없음은 HeatRules 의 효과 임계 시작 0(없음)이다(spec 04 9.4, spec 01 7.3)
        [Fact]
        public void S04_N2__Char1WithWand1_NoMutation_CritStart9600()
        {
            Assert.Equal(9_600, HeatRules.CritStartHu(RepoDb().Character(CharacterId.Char1).CritStartHu, 0));
        }

        // spec 04 15.1-3: 캐릭터 4 → 10,800(90)
        [Fact]
        public void S04_N3__Char4_CritStart10800()
        {
            Assert.Equal(10_800, HeatRules.CritStartHu(RepoDb().Character(CharacterId.Char4).CritStartHu, 0));
        }

        // 과민 회로의 임계 시작 70 = 8,400 HU(spec 04 9.4, 11절. 변이 데이터는 이 작업의 절이 아니라 명세 값을 그대로 쓴다)
        private const int HypersensitiveCritHu = 70 * 120;

        // spec 04 15.1-4: 캐릭터 1 + 과민 회로 → 8,400(70). 가장 낮은 값(GDD 9)
        [Fact]
        public void S04_N4__Char1WithHypersensitive_CritStart8400()
        {
            Assert.Equal(8_400, HeatRules.CritStartHu(RepoDb().Character(CharacterId.Char1).CritStartHu, HypersensitiveCritHu));
        }

        // spec 04 15.1-5: 캐릭터 4 + 과민 회로 → 8,400(70)
        [Fact]
        public void S04_N5__Char4WithHypersensitive_CritStart8400()
        {
            Assert.Equal(8_400, HeatRules.CritStartHu(RepoDb().Character(CharacterId.Char4).CritStartHu, HypersensitiveCritHu));
        }

        // spec 04 9.2 표: 단계 → (1.15^n Q16.16, 결투 1 + 0.03n Q16.16)
        private static readonly int[][] EnhanceTable =
        {
            new[] { 65536, 65536 }, new[] { 75366, 67502 }, new[] { 86671, 69468 }, new[] { 99672, 71434 }, new[] { 114623, 73400 },
            new[] { 131816, 75366 }, new[] { 151589, 77332 }, new[] { 174327, 79299 }, new[] { 200476, 81265 }, new[] { 230548, 83231 },
            new[] { 265130, 85197 }, new[] { 304899, 87163 }, new[] { 350634, 89129 }, new[] { 403229, 91095 },
        };

        // Q32.32 → Q16.16(가장 가까운 정수). 명세 표가 Q16.16 반올림이다
        private static long Q16(Fix64 v) => (v.Raw + (1L << 15)) >> 16;

        // 결투 배율 1 + 0.03 × 단계 = (100 + 3n) / 100(spec 04 9.2, online 4.2). 코어에 결투 배율 함수가 아직 없어 정확한 분수로 만든다
        private static Fix64 DuelEnhance(int stage) => Fix64.FromRatio(100 + 3 * stage, 100);

        // spec 04 15.1-7: 단계 13 → 무한 웨이브 6.1528(Q16.16 403,229), 결투 1.39(Q16.16 91,095)
        [Fact]
        public void S04_N7__Stage13_Endless403229_Duel91095()
        {
            Assert.Equal(403_229, Q16(FixMath.PowTable(PowBase.Enhance115, 13)));
            Assert.Equal(91_095, Q16(DuelEnhance(13)));
        }

        // spec 04 15.1-8: 단계 0..13 → 9.2 표 전 행
        [Fact]
        public void S04_N8__Stages0To13_MatchTheEnhanceTable()
        {
            for (int stage = 0; stage <= 13; stage++)
            {
                Assert.Equal(EnhanceTable[stage][0], Q16(FixMath.PowTable(PowBase.Enhance115, stage)));
                Assert.Equal(EnhanceTable[stage][1], Q16(DuelEnhance(stage)));
            }
        }

        // 흰뿔의 서리밭 −30%(spec 01 5.6, monsters 1.2). 적 데이터는 이 작업의 절이 아니라 명세 값을 쓴다
        private const int SlowPermille = 700;
        private const int NoSlowPermille = 1000;

        // spec 04 15.1-9: 자세 3.76 −30% → 2.63. 명세는 u/틱(263u, 0 쪽으로 버림)으로 적었지만 CONTRACTS 0.3에 따라 코어는 U 단위를 쓰지 않고
        // 틱당 Raw(spec 01 2.3: round(SpeedCenti × mulPermille × 2^32 / 6,000,000))로 셈한다. 정확한 값 2.632의 축 이동량 = 188,405,899(spec 01 2.3 표)
        [Fact]
        public void S04_N9__Char1StanceMinus30_Is2_63()
        {
            CharacterDef c = RepoDb().Character(CharacterId.Char1);
            Assert.Equal(376, c.StanceSpeedCenti);
            Assert.Equal(188_405_899, Movement.AxisStepRaw(c.StanceSpeedCenti, Movement.MovePermille(c, false, SlowPermille)));
        }

        // spec 04 15.1-10: 초롱 걷기 5.6 + 가속 +20% → 6.72, 자세 5.04 → 6.05(정확히 6.048). spec 01 2.3 표: 481,036,337 / 432,932,703
        [Fact]
        public void S04_N10__Char2GrazeHaste_Walk6_72_Stance6_05()
        {
            CharacterDef c = RepoDb().Character(CharacterId.Char2);
            int mul = Movement.MovePermille(c, true, NoSlowPermille);
            Assert.Equal(1200, mul);
            Assert.Equal(481_036_337, Movement.AxisStepRaw(c.WalkSpeedCenti, mul));
            Assert.Equal(432_932_703, Movement.AxisStepRaw(c.StanceSpeedCenti, mul));
        }

        // 원본의 캐릭터 줄 순서를 바꿔도 절 바이트가 같다(CharacterId 오름차순으로 쓴다)
        [Fact]
        public void ReversedCharacterOrder_GivesTheSameBytes()
        {
            string json = System.IO.File.ReadAllText(TestRepo.Path("data/sim/characters.json"));
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var rows = new List<string>();
            foreach (System.Text.Json.JsonElement row in doc.RootElement.GetProperty("characters").EnumerateArray())
            {
                rows.Add(row.GetRawText());
            }
            rows.Reverse();
            using TempDataRepo repo = Repo(doc.RootElement.GetProperty("baseline").GetRawText(), string.Join(", ", rows));
            repo.Write("data/names/names.ko.json",
                "{\n  \"char.1\": \"제자 1\",\n  \"char.2\": \"제자 2\",\n  \"char.3\": \"제자 3\",\n  \"char.4\": \"제자 4\",\n  \"char.5\": \"제자 5\",\n  \"~end\": \"\"\n}\n");
            Assert.Equal(Section(RepoPack.Value), Section(BuildPack(repo.Root)));
        }

        [Fact]
        public void PackWithoutTheSection_Throws()
        {
            byte[] empty = PackWriter.Write(PackWriter.SchemaVersion, 1, new byte[PackWriter.CoreCompatLength], new PackSection[0]);
            ContentDb db = ContentDb.Build(ContentPack.Load(empty));
            Assert.Throws<ContentException>(() => db.Character(CharacterId.Baseline));
        }

        [Fact]
        public void SameData_SameSectionBytes()
        {
            Assert.Equal(Section(RepoPack.Value), Section(BuildPack(TestRepo.Root)));
        }

        // characters[] 의 줄(M5-DAT-01 이 채운다)도 같은 절에 같은 꼴로 들어간다. 값은 spec 04 2.2 의 char.3 열과 spec 01 5.1 갈피 열
        [Fact]
        public void CharacterRow_RoundTrip_UsesContentIds()
        {
            using TempDataRepo repo = Repo(BaselineJson, Char3Json);
            ContentDb db = ContentDb.Build(ContentPack.Load(BuildPack(repo.Root)));
            CharacterDef c = db.Character(CharacterId.Char3);
            Assert.Equal(CharacterId.Char3, c.Id);
            Assert.Equal(Element.Arcane, c.Element);
            Assert.Equal(PassiveId.Underline, c.Passive);
            Assert.Equal(WandTraitId.DepthDecay, c.WandTrait);
            Assert.Equal(ArtId.CopyCircle, c.UniqueArt);
            Assert.Equal(6, c.MaxHpCells);
            Assert.Equal(500, c.WalkSpeedCenti);
            Assert.Equal(425, c.StanceSpeedCenti);
            Assert.Equal(Raw015, c.HitRadius.Raw);
            Assert.Equal(Raw030, c.BodyRadius.Raw);
            Assert.Equal(Raw060, c.GrazeRadius.Raw);
            Assert.Equal(60, c.InvulnTicks);
            Assert.Equal(4, c.CrystalCap);
            Assert.Equal(15, c.ComboPerCrystal);
            Assert.Equal(1, c.CrystalLossOnHit);
            Assert.Equal(5, c.BaseLinkSlots);
            Assert.Equal(2, c.MaxMutations);
            Assert.Equal(90, c.CoolHuPerTick);
            Assert.Equal(12, c.CoolDelayTicks);
            Assert.Equal(10_200, c.CritStartHu);
            Assert.Equal(210, c.LockTicks);                      // 3.5초
            Assert.Equal(90, c.GrazeBuffTicks);
            Assert.Equal(1200, c.GrazeBuffPermille);
            Assert.Equal(180, db.Character(CharacterId.Baseline).LockTicks);
        }

        [Fact]
        public void CoolPerSecond_50_Is100HuPerTick()
        {
            // spec 01 2.4: 냉각 초당 50(초롱) → 틱당 100 HU
            using TempDataRepo repo = Repo(BaselineJson.Replace("\"cool_per_second\": 45", "\"cool_per_second\": 50"), null);
            Assert.Equal(100, ContentDb.Build(ContentPack.Load(BuildPack(repo.Root))).Character(CharacterId.Baseline).CoolHuPerTick);
        }

        [Fact]
        public void DuplicateCharacter_IsAProblem()
        {
            using TempDataRepo repo = Repo(BaselineJson, Char3Json + ", " + Char3Json);
            Assert.Contains(Problems(repo.Root), p => p.Contains("data/sim/characters.json:/characters/1/id") && p.Contains("두 번"));
        }

        [Fact]
        public void UnknownReferenceIds_AreProblems()
        {
            using TempDataRepo repo = Repo(BaselineJson, Char3Json.Replace("passive.underline", "passive.nope").Replace("char.3", "char.9"));
            List<string> p = Problems(repo.Root);
            Assert.Contains(p, x => x.Contains("/characters/0/id") && x.Contains("char.9"));
            using TempDataRepo repo2 = Repo(BaselineJson, Char3Json.Replace("passive.underline", "passive.nope"));
            Assert.Contains(Problems(repo2.Root), x => x.Contains("/characters/0/passive") && x.Contains("passive.nope"));
        }

        [Fact]
        public void Schema_RejectsUnknownField_MissingField_AndTooFewCrystals()
        {
            using (TempDataRepo repo = Repo(BaselineJson.Replace("\"max_hp_cells\": 6,", "\"max_hp_cells\": 6, \"depth_decay\": 0.85,"), null))
            {
                Assert.Contains(Problems(repo.Root), p => p.Contains("data/sim/characters.json") && p.Contains("스키마"));
            }
            using (TempDataRepo repo = Repo(BaselineJson.Replace("\"lock_seconds\": 3.0,", ""), null))
            {
                Assert.Contains(Problems(repo.Root), p => p.Contains("data/sim/characters.json") && p.Contains("스키마"));
            }
            using (TempDataRepo repo = Repo(BaselineJson.Replace("\"crystal_cap\": 5", "\"crystal_cap\": 2"), null))
            {
                Assert.Contains(Problems(repo.Root), p => p.Contains("data/sim/characters.json:/baseline/crystal_cap") && p.Contains("스키마"));
            }
        }

        [Fact]
        public void BrokenSectionBytes_ThrowContentException()
        {
            byte[] good = Section(RepoPack.Value);
            // 모자람
            Assert.Throws<ContentException>(() => Load(good.AsSpan(0, good.Length - 1).ToArray()));
            Assert.Throws<ContentException>(() => Load(new byte[1]));
            // 남음
            var longer = new byte[good.Length + 1];
            good.CopyTo(longer, 0);
            Assert.Throws<ContentException>(() => Load(longer));
            // 모르는 열거형 값(첫 항목의 element 자리: u16 개수 + u8 id 뒤)
            byte[] badEnum = (byte[])good.Clone();
            badEnum[3] = 250;
            Assert.Throws<ContentException>(() => Load(badEnum));
            // 같은 id 두 번(순서 위반)
            byte[] entry = good.AsSpan(2).ToArray();
            var twice = new byte[2 + entry.Length * 2];
            twice[0] = 2;
            entry.CopyTo(twice, 2);
            entry.CopyTo(twice, 2 + entry.Length);
            Assert.Throws<ContentException>(() => Load(twice));
            // 그대로는 읽힌다
            Load(good);
        }

        private static ContentDb Load(byte[] section) =>
            ContentDb.Build(ContentPack.Load(PackWriter.Write(PackWriter.SchemaVersion, 1, new byte[PackWriter.CoreCompatLength],
                new[] { new PackSection((ushort)ContentSection.Characters, section) })));

        private static byte[] Section(byte[] pack)
        {
            Assert.True(ContentPack.Load(pack).TryGetSection((ushort)ContentSection.Characters, out ReadOnlyMemory<byte> s));
            return s.ToArray();
        }

        // 기준값 열(spec 01 5.1)
        private const string BaselineJson =
            "{ \"max_hp_cells\": 6, \"walk_speed\": 5.0, \"stance_speed\": 4.25, \"hit_radius\": 0.15, \"body_radius\": 0.30, \"graze_radius\": 0.6,\n" +
            "  \"invuln_seconds\": 1.0, \"crystal_cap\": 5, \"combo_per_crystal\": 15, \"crystal_loss_on_hit\": 1, \"base_link_slots\": 4, \"max_mutations\": 2,\n" +
            "  \"cool_per_second\": 45, \"cool_delay_seconds\": 0.2, \"crit_start\": 85, \"lock_seconds\": 3.0, \"graze_buff_seconds\": 1.5, \"graze_buff_speed\": 1.2 }";

        // char.3 (spec 04 2.2, spec 01 5.1 갈피 열, spec 14 3.2)
        private const string Char3Json =
            "{ \"id\": \"char.3\", \"element\": \"element.arcane\", \"passive\": \"passive.underline\", \"wand_trait\": \"wand_trait.depth_decay\", \"unique_art\": \"art.copy_circle\",\n" +
            "  \"max_hp_cells\": 6, \"walk_speed\": 5.0, \"stance_speed\": 4.25, \"hit_radius\": 0.15, \"body_radius\": 0.30, \"graze_radius\": 0.6,\n" +
            "  \"invuln_seconds\": 1.0, \"crystal_cap\": 4, \"combo_per_crystal\": 15, \"crystal_loss_on_hit\": 1, \"base_link_slots\": 5, \"max_mutations\": 2,\n" +
            "  \"cool_per_second\": 45, \"cool_delay_seconds\": 0.2, \"crit_start\": 85, \"lock_seconds\": 3.5, \"graze_buff_seconds\": 1.5, \"graze_buff_speed\": 1.2 }";

        private static TempDataRepo Repo(string baseline, string? rows)
        {
            var repo = new TempDataRepo();
            repo.Write("data/schema/characters.schema.json", System.IO.File.ReadAllText(TestRepo.Path("data/schema/characters.schema.json")));
            repo.Write("data/sim/characters.json", "{ \"baseline\": " + baseline + (rows == null ? "" : ", \"characters\": [" + rows + "]") + " }\n");
            repo.Write("data/names/names.ko.json", "{\n  \"char.3\": \"제자 3\",\n  \"~end\": \"\"\n}\n");
            return repo;
        }
    }
}
