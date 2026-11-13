using System;
using System.Collections.Generic;
using System.Text;
using Wizard.Content.Tests.Pack;
using Wizard.DataBuild.Core;
using Wizard.TestKit;
using Xunit;

namespace Wizard.Content.Tests
{
    // data/sim/arts.json → 팩 절 3 → ContentDb.Art 의 왕복(M5-DAT-01).
    // 기대값의 출처: spec 03 2.2(id·순서), 5.2(경로·슬롯), 12.1(종류·난이도·비용·외상·부모 대체·시작 해금), 15.3(선행 기술·최소 웨이브),
    // 12.2(공용 효과), 14.1~14.5(고유 효과), 16절과 14절의 결투 줄(결투 값), 1.3(칸의 Raw), 1.4(초 → 틱), U-14·U-16·U-18(임시 값).
    // spec 14 3.4(문자열 id), CONTRACTS 2.1(열거형 정본).
    public sealed class ArtsPackTests
    {
        // 칸 → Raw(spec 03 1.3 표의 값. 0.5·1·1.5·2·2.5·3·4·4.5·12칸은 정확히 표현된다)
        private const long Raw05 = 2_147_483_648;
        private const long Raw1 = 4_294_967_296;
        private const long Raw15 = 6_442_450_944;
        private const long Raw2 = 8_589_934_592;
        private const long Raw25 = 10_737_418_240;
        private const long Raw3 = 12_884_901_888;
        private const long Raw4 = 17_179_869_184;
        private const long Raw45 = 19_327_352_832;
        private const long Raw12 = 51_539_607_552;

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

        // spec 03 12.1 표 + 5.2 경로 + 15.3 선행 기술. 경로는 글자(F 앞, B 뒤, L 좌, R 우), 경로끼리는 '|', 빈 글자 = 루트
        private sealed class Row
        {
            public ArtId Id;
            public ArtDef.ArtKind Kind;
            public CharacterId Owner;
            public string Paths = "";
            public bool Just;
            public int Difficulty;
            public bool All;                // 결정 전부(AllMin3)
            public int Cost, MinCost;
            public bool Credit;
            public ArtId Fallback;
            public bool AtStart;
            public ArtId Prereq;
            public int MinWave;
        }

        private const ArtDef.ArtKind Common = ArtDef.ArtKind.Common, Secret = ArtDef.ArtKind.Secret, Unique = ArtDef.ArtKind.Unique;

        private static readonly Row[] Table =
        {
            new Row { Id = ArtId.Burst, Kind = Common, Paths = "", Difficulty = 1, Cost = 1, Credit = true, AtStart = true },
            new Row { Id = ArtId.Recoil, Kind = Common, Paths = "B", Difficulty = 2, Cost = 1, Credit = true, AtStart = true },
            new Row { Id = ArtId.Fan, Kind = Common, Paths = "F", Difficulty = 2, Cost = 1, Credit = true, Prereq = ArtId.Burst },
            new Row { Id = ArtId.Wings, Kind = Common, Paths = "L|R", Difficulty = 2, Cost = 1, Credit = true, Prereq = ArtId.Burst },
            new Row { Id = ArtId.Afterimage, Kind = Common, Paths = "LL|RR", Difficulty = 3, Cost = 2, Credit = true, Prereq = ArtId.Wings },
            new Row { Id = ArtId.Spear, Kind = Common, Paths = "FF", Difficulty = 3, Cost = 2, Credit = true, Prereq = ArtId.Fan },
            new Row { Id = ArtId.Vent, Kind = Common, Paths = "BF", Difficulty = 3, Cost = 2, Credit = true, Prereq = ArtId.Recoil },
            new Row { Id = ArtId.Octastar, Kind = Common, Paths = "FB", Difficulty = 3, Cost = 2, Credit = true, Prereq = ArtId.Fan },
            new Row { Id = ArtId.ThunderSpear, Kind = Secret, Paths = "FF", Just = true, Difficulty = 5, Cost = 1, Credit = true, Prereq = ArtId.Spear, MinWave = 4 },
            new Row { Id = ArtId.Bloom, Kind = Secret, Paths = "FBBF", Difficulty = 5, All = true, MinCost = 3, Credit = false, Prereq = ArtId.Octastar, MinWave = 6 },
            new Row { Id = ArtId.Kiln, Kind = Unique, Owner = CharacterId.Char1, Paths = "BB", Difficulty = 3, Cost = 2, Credit = true, Fallback = ArtId.Recoil, AtStart = true },
            new Row { Id = ArtId.BoltDash, Kind = Unique, Owner = CharacterId.Char2, Paths = "LF|RF", Difficulty = 3, Cost = 2, Credit = true, Fallback = ArtId.Wings, AtStart = true },
            new Row { Id = ArtId.CopyCircle, Kind = Unique, Owner = CharacterId.Char3, Paths = "FL|FR", Difficulty = 3, Cost = 2, Credit = true, Fallback = ArtId.Fan, AtStart = true },
            new Row { Id = ArtId.IceScreen, Kind = Unique, Owner = CharacterId.Char4, Paths = "BL|BR", Difficulty = 3, Cost = 2, Credit = true, Fallback = ArtId.Recoil, AtStart = true },
            new Row { Id = ArtId.ShadowHand, Kind = Unique, Owner = CharacterId.Char5, Paths = "LR|RL", Difficulty = 3, Cost = 2, Credit = true, Fallback = ArtId.Wings, AtStart = true },
        };

        private static string Letters(ArtDef.CommandStep[][] paths)
        {
            var sb = new StringBuilder();
            for (int p = 0; p < paths.Length; p++)
            {
                if (p > 0)
                {
                    sb.Append('|');
                }
                foreach (ArtDef.CommandStep s in paths[p])
                {
                    sb.Append(s == ArtDef.CommandStep.Forward ? 'F' : s == ArtDef.CommandStep.Back ? 'B' : s == ArtDef.CommandStep.Left ? 'L' : 'R');
                }
            }
            return sb.ToString();
        }

        // spec 03 12.1 표의 15줄 × 모든 칸(종류, 주인, 경로, 섬, 난이도, 비용, 외상, 부모 대체, 시작 해금, 선행, 최소 웨이브)
        [Fact]
        public void FifteenArts_MatchSpec03Table_RowByRow()
        {
            ContentDb db = RepoDb();
            Assert.Equal(15, Table.Length);
            foreach (Row w in Table)
            {
                ArtDef a = db.Art(w.Id);
                Assert.Equal(w.Id, a.Id);
                Assert.Equal(w.Kind, a.Kind);
                Assert.Equal(w.Kind == Unique, a.IsUnique);
                Assert.Equal(w.Owner, a.Owner);
                Assert.Equal(w.Paths, Letters(a.Paths));
                Assert.Equal(w.Just, a.IsJust);
                Assert.Equal(w.Difficulty, a.Difficulty);
                Assert.Equal(w.All ? ArtDef.ArtCostKind.AllCrystals : ArtDef.ArtCostKind.Fixed, a.CostKind);
                Assert.Equal(w.All, a.TakesAllCrystals);
                Assert.Equal(w.Cost, a.Cost);
                Assert.Equal(w.MinCost, a.MinCost);
                Assert.Equal(w.Credit, a.CreditAllowed);
                Assert.Equal(w.Fallback, a.FallbackParent);
                Assert.Equal(w.AtStart, a.Unlock.AtStart);
                Assert.Equal(w.Prereq, a.Unlock.Prereq);
                Assert.Equal(w.MinWave, a.Unlock.MinWave);
            }
        }

        // spec 03 14절·spec 04 5.1: 고유 비기는 전부 결정 2개·난이도 3, 처음부터 쓴다(DECISIONS 1.23), 주인은 캐릭터의 UniqueArt 와 같다
        [Fact]
        public void UniqueArts_TwoCrystals_Difficulty3_UnlockedFromStart_OwnedByTheirCharacter()
        {
            ContentDb db = RepoDb();
            foreach (CharacterId c in new[] { CharacterId.Char1, CharacterId.Char2, CharacterId.Char3, CharacterId.Char4, CharacterId.Char5 })
            {
                ArtDef a = db.Art(db.Character(c).UniqueArt);
                Assert.True(a.IsUnique);
                Assert.Equal(c, a.Owner);
                Assert.Equal(2, a.Cost);
                Assert.Equal(3, a.Difficulty);
                Assert.True(a.Unlock.AtStart);
                Assert.Equal(ArtId.None, a.Unlock.Prereq);
            }
        }

        // spec 03 12.2: 공용 효과
        [Fact]
        public void CommonEffects_MatchSpec03_12_2()
        {
            ContentDb db = RepoDb();

            ArtDef.EffectParams burst = db.Art(ArtId.Burst).Effect;          // 트리 1회, 2T
            Assert.Equal(2000, burst.DamagePermille);
            Assert.Equal(1, burst.BranchCount);

            ArtDef.EffectParams recoil = db.Art(ArtId.Recoil).Effect;        // 트리 1회, 넉백 2칸, 뒤로 2칸, 돌진 취소 0.8초 경직
            Assert.Equal(1000, recoil.DamagePermille);
            Assert.Equal(1, recoil.BranchCount);
            Assert.Equal(Raw2, recoil.Knockback.Raw);
            Assert.Equal(Raw2, recoil.SelfMove.Raw);
            Assert.Equal(48, recoil.StaggerTicks);

            ArtDef.EffectParams fan = db.Art(ArtId.Fan).Effect;              // 세 갈래, 중앙 100%, 좌우 70%, ±15도 [임시 U-16]
            Assert.Equal(1000, fan.DamagePermille);
            Assert.Equal(700, fan.SideDamagePermille);
            Assert.Equal(3, fan.BranchCount);
            Assert.Equal(1500, fan.BranchAngleCenti);

            ArtDef.EffectParams wings = db.Art(ArtId.Wings).Effect;          // 좌우 −90·+90도, 각 100% [임시 U-16]
            Assert.Equal(1000, wings.DamagePermille);
            Assert.Equal(0, wings.SideDamagePermille);
            Assert.Equal(2, wings.BranchCount);
            Assert.Equal(9000, wings.BranchAngleCenti);

            ArtDef.EffectParams after = db.Art(ArtId.Afterimage).Effect;     // 2.5칸 순간 이동, 0.15초(9틱) 무적, 피해 없음
            Assert.Equal(Raw25, after.SelfMove.Raw);
            Assert.Equal(9, after.InvulnTicks);
            Assert.Equal(0, after.DamagePermille);

            ArtDef.EffectParams spear = db.Art(ArtId.Spear).Effect;          // 3T, 폭 0.5, 사거리 12, 철갑 무시, 준비 0.08초 = 5틱 [임시 U-14]
            Assert.Equal(3000, spear.DamagePermille);
            Assert.Equal(Raw05, spear.Width.Raw);
            Assert.Equal(Raw12, spear.Range.Raw);
            Assert.True(spear.IgnoresArmor);
            Assert.Equal(5, spear.PrepTicks);

            ArtDef.EffectParams vent = db.Art(ArtId.Vent).Effect;            // 반경 3, T × 열 ÷ 25, 탄 소거 3 [임시 U-18], 준비 0.06초 = 4틱 [임시 U-14]
            Assert.Equal(Raw3, vent.Radius.Raw);
            Assert.Equal(25 * 120, vent.HeatPerTHu);
            Assert.Equal(Raw3, vent.ClearRadius.Raw);
            Assert.Equal(4, vent.PrepTicks);

            ArtDef.EffectParams octa = db.Art(ArtId.Octastar).Effect;        // 8방향 45도, 정면 100%, 나머지 60%
            Assert.Equal(1000, octa.DamagePermille);
            Assert.Equal(600, octa.SideDamagePermille);
            Assert.Equal(8, octa.BranchCount);
            Assert.Equal(4500, octa.BranchAngleCenti);

            ArtDef.EffectParams thunder = db.Art(ArtId.ThunderSpear).Effect; // 6T, 창 모양, 1초 기절, 보스 0.3초 동작 멈춤(14.6)
            Assert.Equal(6000, thunder.DamagePermille);
            Assert.Equal(Raw05, thunder.Width.Raw);
            Assert.Equal(Raw12, thunder.Range.Raw);
            Assert.True(thunder.IgnoresArmor);
            Assert.Equal(60, thunder.StunTicks);
            Assert.Equal(18, thunder.BossHaltTicks);
            Assert.Equal(0, thunder.PrepTicks);                               // ⑨는 0(U-14)

            ArtDef.EffectParams bloom = db.Art(ArtId.Bloom).Effect;          // 팔방 × 2배(정면 200%, 나머지 120%), 0.12초 = 7.2틱, 탄 소거 3, 준비 0.25초 = 15틱
            Assert.Equal(2000, bloom.DamagePermille);
            Assert.Equal(1200, bloom.SideDamagePermille);
            Assert.Equal(8, bloom.BranchCount);
            Assert.Equal(4500, bloom.BranchAngleCenti);
            Assert.Equal(7200, bloom.RepeatIntervalPermilleTicks);
            Assert.Equal(Raw3, bloom.ClearRadius.Raw);
            Assert.Equal(15, bloom.PrepTicks);
        }

        // spec 03 14.1~14.5(spec 04 5.2~5.6): 고유 효과
        [Fact]
        public void UniqueEffects_MatchSpec03_14()
        {
            ContentDb db = RepoDb();

            ArtDef.EffectParams kiln = db.Art(ArtId.Kiln).Effect;            // 반경 2.5, 4초, 0.5초마다 0.25T, −20%, 열 16 = 1,920 HU, 동시 1
            Assert.Equal(Raw25, kiln.Radius.Raw);
            Assert.Equal(240, kiln.DurationTicks);
            Assert.Equal(30, kiln.IntervalTicks);
            Assert.Equal(250, kiln.DamagePermille);
            Assert.Equal(200, kiln.SlowPermille);
            Assert.Equal(1920, kiln.HeatAbsorbHu);
            Assert.Equal(1, kiln.MaxConcurrent);

            ArtDef.EffectParams dash = db.Art(ArtId.BoltDash).Effect;        // 4칸, 길 폭 1.0, 3T, 경직 0.6초 = 36, 무적 0.2초 = 12
            Assert.Equal(Raw4, dash.SelfMove.Raw);
            Assert.Equal(Raw1, dash.Width.Raw);
            Assert.Equal(3000, dash.DamagePermille);
            Assert.Equal(36, dash.StaggerTicks);
            Assert.Equal(12, dash.InvulnTicks);

            ArtDef.EffectParams copy = db.Art(ArtId.CopyCircle).Effect;      // 6초 = 360, 50%, 0.3초 = 18, 12번, 동시 1
            Assert.Equal(360, copy.DurationTicks);
            Assert.Equal(500, copy.DamagePermille);
            Assert.Equal(18, copy.IntervalTicks);
            Assert.Equal(12, copy.MaxCopies);
            Assert.Equal(1, copy.MaxConcurrent);

            ArtDef.EffectParams screen = db.Art(ArtId.IceScreen).Effect;     // 1.5칸 앞, 폭 3 × 두께 0.5, 4초, 파편 3×2칸 3T, 돌진 경직 1.5초 = 90, 동시 1
            Assert.Equal(Raw15, screen.Offset.Raw);
            Assert.Equal(Raw3, screen.Width.Raw);
            Assert.Equal(Raw05, screen.Thickness.Raw);
            Assert.Equal(Raw2, screen.ShardLength.Raw);
            Assert.Equal(240, screen.DurationTicks);
            Assert.Equal(3000, screen.DamagePermille);
            Assert.Equal(90, screen.StaggerTicks);
            Assert.Equal(1, screen.MaxConcurrent);
            Assert.Equal(0, screen.WallHp);                                    // 무한 웨이브는 체력 없음(∞)

            ArtDef.EffectParams hand = db.Art(ArtId.ShadowHand).Effect;      // 정면 3칸, 반경 4.5, 당김 3칸, 3T, 경직 0.5초 = 30
            Assert.Equal(Raw3, hand.Offset.Raw);
            Assert.Equal(Raw45, hand.Radius.Raw);
            Assert.Equal(Raw3, hand.PullDistance.Raw);
            Assert.Equal(3000, hand.DamagePermille);
            Assert.Equal(30, hand.StaggerTicks);
            Assert.Equal(0, hand.TelegraphTicks);
        }

        // spec 03 16절·14절의 결투 줄(WC 5.4): 결투 값은 그 칸만 다르고 나머지는 무한 웨이브와 같다
        [Fact]
        public void DuelEffects_OverrideOnlyTheDuelLines()
        {
            ContentDb db = RepoDb();
            Assert.Equal(Raw15, db.Art(ArtId.Recoil).Duel.Knockback.Raw);      // 넉백 1.5칸
            Assert.Equal(Raw2, db.Art(ArtId.Recoil).Duel.SelfMove.Raw);        // 그대로
            Assert.Equal(30, db.Art(ArtId.ThunderSpear).Duel.StunTicks);       // 기절 0.5초
            Assert.Equal(6000, db.Art(ArtId.ThunderSpear).Duel.DamagePermille);
            Assert.Equal(100, db.Art(ArtId.Kiln).Duel.SlowPermille);           // 상대 감속 −10%
            Assert.Equal(1920, db.Art(ArtId.Kiln).Duel.HeatAbsorbHu);          // 열 흡수 16 그대로
            Assert.Equal(250, db.Art(ArtId.Kiln).Duel.DamagePermille);         // 지대 피해 그대로
            Assert.Equal(18, db.Art(ArtId.BoltDash).Duel.StaggerTicks);        // 경직 0.3초
            Assert.Equal(12, db.Art(ArtId.BoltDash).Duel.InvulnTicks);         // 무적 0.2초 그대로
            Assert.Equal(3000, db.Art(ArtId.BoltDash).Duel.DamagePermille);    // 3T 그대로
            Assert.Equal(8, db.Art(ArtId.CopyCircle).Duel.MaxCopies);          // 최대 8번(4T)
            Assert.Equal(360, db.Art(ArtId.CopyCircle).Duel.DurationTicks);    // 6초 그대로
            Assert.Equal(500, db.Art(ArtId.CopyCircle).Duel.DamagePermille);   // 50% 그대로
            Assert.Equal(150, db.Art(ArtId.IceScreen).Duel.WallHp);            // 벽 체력 150
            Assert.Equal(2000, db.Art(ArtId.IceScreen).Duel.DamagePermille);   // 파편 2T
            Assert.Equal(240, db.Art(ArtId.IceScreen).Duel.DurationTicks);     // 4초 그대로
            Assert.Equal(Raw2, db.Art(ArtId.ShadowHand).Duel.PullDistance.Raw);// 당김 2칸
            Assert.Equal(0, db.Art(ArtId.ShadowHand).Duel.StaggerTicks);       // 경직 없음
            Assert.Equal(18, db.Art(ArtId.ShadowHand).Duel.TelegraphTicks);    // 발동 전 0.3초
            Assert.Equal(3000, db.Art(ArtId.ShadowHand).Duel.DamagePermille);  // 3T 그대로
            // 결투 줄이 없는 비기는 Effect 와 같은 값이다
            Assert.Equal(db.Art(ArtId.Burst).Effect.DamagePermille, db.Art(ArtId.Burst).Duel.DamagePermille);
            Assert.Equal(db.Art(ArtId.Bloom).Effect.RepeatIntervalPermilleTicks, db.Art(ArtId.Bloom).Duel.RepeatIntervalPermilleTicks);
        }

        [Fact]
        public void MissingIds_AndPackWithoutTheSection_Throw()
        {
            Assert.Throws<ContentException>(() => RepoDb().Art(ArtId.None));
            Assert.Throws<ContentException>(() => RepoDb().Art((ArtId)16));
            Assert.Throws<ContentException>(() => RepoDb().Art((ArtId)200));
            byte[] empty = PackWriter.Write(PackWriter.SchemaVersion, 1, new byte[PackWriter.CoreCompatLength], new PackSection[0]);
            Assert.Throws<ContentException>(() => ContentDb.Build(ContentPack.Load(empty)).Art(ArtId.Burst));
        }

        [Fact]
        public void SameData_SameSectionBytes()
        {
            Assert.Equal(Section(RepoPack.Value), Section(BuildPack(TestRepo.Root)));
        }

        // 원본의 줄 순서를 바꿔도 절 바이트가 같다(ArtId 오름차순으로 쓴다)
        [Fact]
        public void ReversedSourceOrder_GivesTheSameBytes()
        {
            List<string> rows = RepoRows();
            rows.Reverse();
            using TempDataRepo repo = Repo(rows);
            Assert.Equal(Section(RepoPack.Value), Section(BuildPack(repo.Root)));
        }

        [Fact]
        public void UnknownAndDuplicateIds_AreProblems()
        {
            List<string> rows = RepoRows();
            rows[0] = rows[0].Replace("\"art.burst\"", "\"art.nope\"");
            using (TempDataRepo repo = Repo(rows))
            {
                Assert.Contains(Problems(repo.Root), p => p.Contains("data/sim/arts.json:/arts/0/id") && p.Contains("art.nope"));
            }
            List<string> twice = RepoRows();
            twice.Add(twice[1]);
            using (TempDataRepo repo = Repo(twice))
            {
                Assert.Contains(Problems(repo.Root), p => p.Contains("data/sim/arts.json:/arts/15/id") && p.Contains("두 번"));
            }
        }

        [Fact]
        public void KindOwnerAndFallbackRules_AreProblems()
        {
            // 고유인데 주인이 없다
            using (TempDataRepo repo = Repo(Replace(10, "\"owner\": \"char.1\",", "")))
            {
                Assert.Contains(Problems(repo.Root), p => p.Contains("/arts/10/owner") && p.Contains("주인"));
            }
            // 공용에 주인이 있다
            using (TempDataRepo repo = Repo(Replace(0, "\"kind\": \"common\",", "\"kind\": \"common\", \"owner\": \"char.1\",")))
            {
                Assert.Contains(Problems(repo.Root), p => p.Contains("/arts/0/owner"));
            }
            // 부모 대체가 경로의 부모 노드 기술이 아니다(불가마 BB 의 부모는 B = 반동 사격)
            using (TempDataRepo repo = Repo(Replace(10, "\"fallback\": \"art.recoil\"", "\"fallback\": \"art.fan\"")))
            {
                Assert.Contains(Problems(repo.Root), p => p.Contains("/arts/10/paths/0") && p.Contains("부모 노드"));
            }
            // 고유 비기의 자리가 공용 경로와 겹친다(불가마를 FF 에)
            using (TempDataRepo repo = Repo(Replace(10, "[[\"B\", \"B\"]]", "[[\"F\", \"F\"]]")))
            {
                Assert.Contains(Problems(repo.Root), p => p.Contains("/arts/10/paths/0") && p.Contains("겹친다"));
            }
            // 고유 비기는 처음부터 쓴다
            using (TempDataRepo repo = Repo(Replace(10, "\"unlock\": { \"at_start\": true }", "\"unlock\": { \"prereq\": \"art.recoil\" }")))
            {
                Assert.Contains(Problems(repo.Root), p => p.Contains("/arts/10/unlock") && p.Contains("처음부터"));
            }
            // 선행 기술이 절에 없다
            List<string> noFan = RepoRows();
            noFan.RemoveAt(2);
            using (TempDataRepo repo = Repo(noFan))
            {
                List<string> p = Problems(repo.Root);
                Assert.Contains(p, x => x.Contains("/unlock/prereq") && x.Contains("art.fan"));
                Assert.Contains(p, x => x.Contains("/fallback") && x.Contains("art.fan"));
            }
            // 만개의 cost "all" 에는 min_cost 가 있어야 한다
            using (TempDataRepo repo = Repo(Replace(9, "\"min_cost\": 3,", "")))
            {
                Assert.Contains(Problems(repo.Root), p => p.Contains("/arts/9/min_cost"));
            }
        }

        [Fact]
        public void Schema_RejectsUnknownField_AndBadPathLetter()
        {
            using (TempDataRepo repo = Repo(Replace(0, "\"difficulty\": 1,", "\"difficulty\": 1, \"name\": \"x\",")))
            {
                Assert.Contains(Problems(repo.Root), p => p.Contains("data/sim/arts.json") && p.Contains("스키마"));
            }
            using (TempDataRepo repo = Repo(Replace(1, "[[\"B\"]]", "[[\"X\"]]")))
            {
                Assert.Contains(Problems(repo.Root), p => p.Contains("data/sim/arts.json") && p.Contains("스키마"));
            }
            using (TempDataRepo repo = Repo(Replace(0, "\"effect\": { \"damage\": 2, \"branches\": 1 }", "\"effect\": { \"damage\": 2, \"branches\": 1, \"speed\": 3 }")))
            {
                Assert.Contains(Problems(repo.Root), p => p.Contains("data/sim/arts.json") && p.Contains("스키마"));
            }
        }

        [Fact]
        public void BrokenSectionBytes_ThrowContentException()
        {
            byte[] good = Section(RepoPack.Value);
            Assert.Throws<ContentException>(() => Load(good.AsSpan(0, good.Length - 1).ToArray()));    // 모자람
            Assert.Throws<ContentException>(() => Load(new byte[1]));
            var longer = new byte[good.Length + 1];
            good.CopyTo(longer, 0);
            Assert.Throws<ContentException>(() => Load(longer));                                       // 남음
            byte[] badKind = (byte[])good.Clone();
            badKind[3] = 9;                                                                             // 첫 항목의 kind(u16 개수 + u8 id 뒤)
            Assert.Throws<ContentException>(() => Load(badKind));
            byte[] ownerMismatch = (byte[])good.Clone();
            ownerMismatch[4] = (byte)CharacterId.Char1;                                                 // 공용(마력 방출)에 주인
            Assert.Throws<ContentException>(() => Load(ownerMismatch));
            byte[] badBool = (byte[])good.Clone();
            badBool[5] = 2;                                                                             // is_just
            Assert.Throws<ContentException>(() => Load(badBool));
            byte[] noneId = (byte[])good.Clone();
            noneId[2] = 0;                                                                              // id None
            Assert.Throws<ContentException>(() => Load(noneId));
            byte[] fewer = (byte[])good.Clone();                                                        // 개수만 하나 줄이면 남음
            fewer[0] = 14;
            Assert.Throws<ContentException>(() => Load(fewer));
            // 같은 id 두 번(순서 위반): 첫 항목(마력 방출)을 두 번. 항목 길이 = 고정 25 + 경로 2(개수 1, 빈 경로 길이 1) + 효과 157 × 2
            const int burstEntry = 25 + 2 + 157 * 2;
            byte[] entry = good.AsSpan(2, burstEntry).ToArray();
            Assert.Equal((byte)ArtId.Recoil, good[2 + burstEntry]);                                     // 다음 항목은 반동 사격
            var twice = new byte[2 + burstEntry * 2];
            twice[0] = 2;
            entry.CopyTo(twice, 2);
            entry.CopyTo(twice, 2 + burstEntry);
            Assert.Throws<ContentException>(() => Load(twice));
            var once = new byte[2 + burstEntry];
            once[0] = 1;
            entry.CopyTo(once, 2);
            Assert.Equal(ArtId.Burst, Load(once).Art(ArtId.Burst).Id);
            Load(good);
        }

        private static ContentDb Load(byte[] section) =>
            ContentDb.Build(ContentPack.Load(PackWriter.Write(PackWriter.SchemaVersion, 1, new byte[PackWriter.CoreCompatLength],
                new[] { new PackSection((ushort)ContentSection.Arts, section) })));

        private static byte[] Section(byte[] pack)
        {
            Assert.True(ContentPack.Load(pack).TryGetSection((ushort)ContentSection.Arts, out ReadOnlyMemory<byte> s));
            return s.ToArray();
        }

        // 저장소 arts.json 의 줄(원본 그대로의 JSON 문자열)
        private static List<string> RepoRows()
        {
            string json = System.IO.File.ReadAllText(TestRepo.Path("data/sim/arts.json"));
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var rows = new List<string>();
            foreach (System.Text.Json.JsonElement row in doc.RootElement.GetProperty("arts").EnumerateArray())
            {
                rows.Add(row.GetRawText());
            }
            return rows;
        }

        private static List<string> Replace(int row, string from, string to)
        {
            List<string> rows = RepoRows();
            Assert.Contains(from, rows[row]);
            rows[row] = rows[row].Replace(from, to);
            return rows;
        }

        private static TempDataRepo Repo(List<string> rows)
        {
            var repo = new TempDataRepo();
            repo.Write("data/schema/arts.schema.json", System.IO.File.ReadAllText(TestRepo.Path("data/schema/arts.schema.json")));
            repo.Write("data/sim/arts.json", "{ \"arts\": [" + string.Join(",\n", rows) + "] }\n");
            repo.Write("data/names/names.ko.json", System.IO.File.ReadAllText(TestRepo.Path("data/names/names.ko.json")));
            return repo;
        }
    }
}
