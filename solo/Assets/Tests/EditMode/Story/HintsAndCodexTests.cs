using System.Collections.Generic;
using NUnit.Framework;
using Wizard.Solo.Data;
using Wizard.Solo.Meta;
using Wizard.Solo.Meta.Enhance;
using Wizard.Solo.Screens.Enhance;
using Wizard.Solo.Screens.Hints;
using Wizard.Solo.Sim;

namespace Wizard.Solo.Tests.Story
{
    /// <summary>
    /// 문맥 힌트(HintRules·HintBook), 설명 서식(Data/Describe), 조작·도감(CodexModel), 강화 안내의 "운이 나쁜 10%"(EnhanceTutorialOddsCalc).
    /// 문구에 숫자가 없고 데이터에서 값이 들어가는지, 힌트가 진짜 판에서만·처음 한 번만 뜨는지, 막힘 감지가 창 안의 횟수로 도는지 본다.
    /// </summary>
    public sealed class HintsAndCodexTests
    {
        static SimWorld World(RunMode mode)
        {
            var w = new SimWorld(TestContent.Db, SimModuleSet.Stubs());
            w.Run.Config = new RunConfig { Mode = mode };
            return w;
        }

        static List<string> Fire(HintRules r, SimWorld w, SimEvent e)
        {
            var into = new List<string>();
            r.OnEvent(w, e, into);
            return into;
        }

        [Test]
        public void Hints__FirstTimeEventsMapToHints_OnlyInRealRuns()
        {
            var r = new HintRules(HintTuning.From(TestContent.Db));
            var real = World(RunMode.Endless);
            CollectionAssert.AreEqual(new[] { HintIds.FirstLock }, Fire(r, real, new SimEvent { Type = SimEventType.HeatLockEntered, Slot = 0 }));
            CollectionAssert.AreEqual(new[] { HintIds.FirstCrystal }, Fire(r, real, new SimEvent { Type = SimEventType.CrystalGained, Slot = 0 }));
            CollectionAssert.AreEqual(new[] { HintIds.FirstGraze }, Fire(r, real, new SimEvent { Type = SimEventType.GrazeAwarded, Slot = 0 }));
            CollectionAssert.AreEqual(new[] { HintIds.FirstElite }, Fire(r, real, new SimEvent { Type = SimEventType.EnemySpawned, A = 1 }));
            Assert.IsEmpty(Fire(r, real, new SimEvent { Type = SimEventType.EnemySpawned, A = 0 }), "보통 적은 아니다");
            CollectionAssert.AreEqual(new[] { HintIds.FirstBoss }, Fire(r, real, new SimEvent { Type = SimEventType.BossIntroBegin }));
            CollectionAssert.AreEqual(new[] { HintIds.FirstArtUnlock }, Fire(r, real, new SimEvent { Type = SimEventType.ArtUnlocked, Slot = 0 }));
            CollectionAssert.AreEqual(new[] { HintIds.FirstAffinity }, Fire(r, real, new SimEvent { Type = SimEventType.AffinityDiscovered, Slot = 0 }));
            CollectionAssert.AreEqual(new[] { HintIds.FirstFizzle },
                Fire(r, real, new SimEvent { Type = SimEventType.ArtFizzled, A = (int)ArtFizzleReason.NoCrystal, Slot = 0 }));
            Assert.IsEmpty(Fire(r, real, new SimEvent { Type = SimEventType.ArtFizzled, A = (int)ArtFizzleReason.FullBloomLate, Slot = 0 }),
                "만개 늦음은 결정 부족이 아니다");

            var tut = World(RunMode.Tutorial);
            var prac = World(RunMode.Practice);
            Assert.IsEmpty(Fire(r, tut, new SimEvent { Type = SimEventType.HeatLockEntered, Slot = 0 }), "튜토리얼은 이미 가르친다");
            Assert.IsEmpty(Fire(r, prac, new SimEvent { Type = SimEventType.CrystalGained, Slot = 0 }));
        }

        [Test]
        public void Hints__StuckDetection_DryFireAndFizzleWithinWindow_OncePerRun()
        {
            var t = HintTuning.From(TestContent.Db);
            var r = new HintRules(t);
            var w = World(RunMode.Endless);
            int window = SimConst.Ticks(t.DryFireWindowSeconds);
            var got = new List<string>();
            // 창 밖으로 띄엄띄엄 → 막힘 아님
            for (int i = 0; i < t.DryFireCount; i++) got.AddRange(Fire(r, w, new SimEvent { Type = SimEventType.FireDryWhileLocked, Tick = i * (window + 1), Slot = 0 }));
            Assert.IsEmpty(got);
            int t0 = 100000;
            for (int i = 0; i < t.DryFireCount; i++) got.AddRange(Fire(r, w, new SimEvent { Type = SimEventType.FireDryWhileLocked, Tick = t0 + i, Slot = 0 }));
            CollectionAssert.AreEqual(new[] { HintIds.StuckDryFire }, got);
            got.Clear();
            for (int i = 0; i < t.DryFireCount; i++) got.AddRange(Fire(r, w, new SimEvent { Type = SimEventType.FireDryWhileLocked, Tick = t0 + 10 + i, Slot = 0 }));
            Assert.IsEmpty(got, "판마다 한 번");
            r.BeginRun();
            for (int i = 0; i < t.FizzleCount; i++)
                got.AddRange(Fire(r, w, new SimEvent { Type = SimEventType.ArtFizzled, A = (int)ArtFizzleReason.NoCrystal, Tick = 10 + i, Slot = 0 }));
            CollectionAssert.Contains(got, HintIds.StuckFizzle);
        }

        [Test]
        public void Hints__BookShowsFirstHintsOnce_StuckUpToMax_ResetClears_SettingTurnsOff()
        {
            var p = new ProfileData();
            Assert.IsTrue(HintBook.CanShow(p, HintIds.FirstLock, 3));
            HintBook.MarkShown(p, HintIds.FirstLock, null);
            Assert.IsFalse(HintBook.CanShow(p, HintIds.FirstLock, 3), "처음 힌트는 한 번");
            for (int i = 0; i < 3; i++) { Assert.IsTrue(HintBook.CanShow(p, HintIds.StuckFizzle, 3)); HintBook.MarkShown(p, HintIds.StuckFizzle, null); }
            Assert.IsFalse(HintBook.CanShow(p, HintIds.StuckFizzle, 3), "막힘 힌트는 상한까지");
            HintBook.ResetAll(p, null);
            Assert.IsTrue(HintBook.CanShow(p, HintIds.FirstLock, 3));
            Assert.IsTrue(HintBook.CanShow(p, HintIds.StuckFizzle, 3));

            var s = new SettingsData();
            Assert.IsTrue(HintBook.Enabled(s), "기본 켬");
            SettingsOptions.Set(s, OptionKeys.ContextHints, false);
            Assert.IsFalse(HintBook.Enabled(s));
        }

        [Test]
        public void Hints__EveryHintHasTitleAndBody_BodiesHaveNoDigits()
        {
            var names = TestContent.Names;
            var all = new List<string>(HintIds.FirstTime);
            all.AddRange(HintIds.Stuck);
            all.Add(HintHost.FirstEnhance);
            foreach (var id in all)
            {
                Assert.IsTrue(names.Has(HintIds.TitleKey(id)), id);
                Assert.IsTrue(names.Has(HintIds.BodyKey(id)), id);
                string raw = RawOf(HintIds.BodyKey(id));
                Assert.IsFalse(HasDigit(raw), "문구에 숫자를 적지 않는다(값은 데이터에서): " + id + " = " + raw);
            }
        }

        static string RawOf(string key)
        {
            var nt = TestContent.Names as NameTable;
            // 원문 틀(값을 채우기 전)을 본다: Format 없이 Get은 이름 열쇠만 풀고 값 자리는 그대로 둔다.
            return nt != null ? nt.Get(key) : key;
        }

        static bool HasDigit(string s)
        {
            // 값 자리 {…} 안은 빼고 본다.
            int depth = 0;
            foreach (char c in s)
            {
                if (c == '{') depth++;
                else if (c == '}') depth--;
                else if (depth == 0 && c >= '0' && c <= '9') return true;
            }
            return false;
        }

        [Test]
        public void Describe__ArtsWhimsMutationsAffinity_FilledFromData()
        {
            var db = TestContent.Db;
            var names = TestContent.Names;
            foreach (var a in db.Arts)
            {
                string line = ArtDescriber.OneLine(names, db, a.Id);
                Assert.IsNotNull(line, a.Id);
                Assert.IsFalse(line.Contains("{"), "채우지 않은 자리: " + a.Id + " = " + line);
                Assert.IsFalse(HasDigit(RawOf(ArtDescriber.Key(a.Id))), "문구에 숫자 금지: " + a.Id);
                Assert.IsNotNull(ArtDescriber.CostLine(names, db, a.Id), a.Id);
            }
            StringAssert.Contains(TestContent.Db.Art(Ids.Art.Spear).Effect.Damage.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture),
                ArtDescriber.OneLine(names, db, Ids.Art.Spear), "꿰뚫는 창의 nT는 arts.json 값");
            foreach (var m in db.Modifiers)
            {
                string line = WhimDescriber.OneLine(names, db, m.Id);
                Assert.IsNotNull(line, m.Id);
                Assert.IsFalse(line.Contains("{"), m.Id + " = " + line);
                Assert.IsFalse(HasDigit(RawOf(WhimDescriber.Key(m.Id))), m.Id);
            }
            Assert.AreEqual(8, db.Modifiers.Count, "변덕 8종");
            StringAssert.Contains("30", WhimDescriber.OneLine(names, db, "whim.thick_hide"), "두꺼운 가죽 = 체력 +30%(modifiers.json 1.3)");
            foreach (var id in CodexModel.MutationIds)
            {
                string g = MutationDescriber.Gain(names, db, id), c = MutationDescriber.Cost(names, db, id);
                Assert.IsNotNull(g, id); Assert.IsNotNull(c, id);
                Assert.IsFalse(g.Contains("{") || c.Contains("{"), id);
                Assert.IsFalse(HasDigit(RawOf(MutationDescriber.GainKey(id))) || HasDigit(RawOf(MutationDescriber.CostKey(id))), id);
            }
            StringAssert.Contains("1.25", AffinityDescriber.Weak(names, db), "약점 ×1.25(elements.json)");
            StringAssert.Contains("0.8", AffinityDescriber.Resist(names, db), "저항 ×0.8(elements.json)");
        }

        [Test]
        public void Codex__EverySectionHasContent_NoUnfilledSlots()
        {
            var db = TestContent.Db;
            var names = TestContent.Names;
            var args = CodexModel.Args(db, db.BaselineCharacter, null, k => k.ToString());
            foreach (var sec in CodexModel.Sections)
            {
                Assert.IsTrue(names.Has(CodexModel.TabKey(sec)), sec);
                var rows = CodexModel.Rows(sec, names, db, Ids.Char.C1, args);
                Assert.IsNotEmpty(rows, sec);
                foreach (var r in rows)
                {
                    Assert.IsFalse((r.Body ?? string.Empty).Contains("{"), sec + ": " + r.Body);
                    Assert.IsFalse((r.Side ?? string.Empty).Contains("{"), sec + ": " + r.Side);
                }
            }
            Assert.AreEqual(db.Blocks.Count + 1, CodexModel.Rows(CodexModel.Blocks, names, db, Ids.Char.C1, args).Count, "블록 16종 + 설명 한 줄");
            Assert.AreEqual(db.TrieArts(Ids.Char.C1).Count + 3, CodexModel.Rows(CodexModel.Arts, names, db, Ids.Char.C1, args).Count, "그 캐릭터의 트라이 비기 + 설명 세 줄");
        }

        // ------------------------------------------------------------ 강화 안내: 평균과 운 나쁜 10%

        [Test]
        public void EnhanceTutorial__OddsMatchExpectedTable_UnluckyIsWorse_Deterministic()
        {
            var cfg = TestContent.Db.Enhance;
            var t = EnhanceTutorialTuning.From(TestContent.Db);
            Assert.AreEqual(4, t.Pages.Count, "4쪽마다 가리키는 곳이 있다");
            var first = EnhanceTutorialOddsCalc.Compute(cfg, 0, 0, 0, t);
            Assert.IsTrue(first.Valid);
            Assert.AreEqual(1.0, first.AvgTries, 1e-9, "+0 → +1은 늘 성공(100%)");
            Assert.AreEqual(1, first.UnluckyTries);

            foreach (int stage in new[] { 3, 6, 9 })
            {
                var o = EnhanceTutorialOddsCalc.Compute(cfg, stage, 0, stage, t);
                var table = EnhanceRules.ExpectedTable(cfg, stage, 0, stage, stage + 1);
                double expected = table.Find(x => x.TargetStage == stage + 1).ExpectedAttempts;
                Assert.AreEqual(expected, o.AvgTries, expected * 0.06, "+" + stage + ": 몬테카를로 평균이 기댓값 표와 맞는다");
                Assert.GreaterOrEqual(o.UnluckyTries, (int)System.Math.Floor(o.AvgTries), "운 나쁜 10%는 평균보다 많다");
                var again = EnhanceTutorialOddsCalc.Compute(cfg, stage, 0, stage, t);
                Assert.AreEqual(o.UnluckyTries, again.UnluckyTries, "고정 씨앗: 늘 같은 값");
                Assert.AreEqual(o.AvgGold, again.AvgGold, 1e-9);
            }
        }

        [Test]
        public void EnhanceTutorial__PagesHaveTitleAndBody_NoDigitsInText()
        {
            var names = TestContent.Names;
            for (int pg = 1; pg <= 4; pg++)
            {
                Assert.IsTrue(names.Has(EnhanceTutorialScreen.PageTitlePrefix + pg + ".title"), "page " + pg);
                string body = EnhanceNameKeys.TutorialBody(pg);
                Assert.IsTrue(names.Has(body), body);
                Assert.IsFalse(HasDigit(RawOf(body)), "쪽 본문에 숫자를 적지 않는다(enhance.json에서): " + pg);
            }
            Assert.IsTrue(names.Has(EnhanceTutorialKeys.Odds));
            var m = new EnhanceTutorialModel(4);
            string filled = m.Body(names, new object[] { "cap", 95 });
            StringAssert.Contains("95", filled);
        }

        [Test]
        public void Hints__EachHintBelongsToItsOwnScreen_RunHintsNeverOnSettlementOrMenus()
        {
            Assert.IsTrue(HintContexts.Accepts(HintIds.FirstLoot, Wizard.Solo.UI.ScreenId.LootPick));
            Assert.IsFalse(HintContexts.Accepts(HintIds.FirstLoot, Wizard.Solo.UI.ScreenId.Camp), "전리품 힌트는 야영에 남지 않는다");
            Assert.IsTrue(HintContexts.Accepts(HintIds.FirstCamp, Wizard.Solo.UI.ScreenId.Camp));
            Assert.IsFalse(HintContexts.Accepts(HintIds.FirstCamp, Wizard.Solo.UI.ScreenId.Settlement), "야영 힌트는 정산에 뜨지 않는다");
            Assert.IsFalse(HintContexts.Accepts(HintIds.FirstCamp, Wizard.Solo.UI.ScreenId.Battle));
            Assert.IsTrue(HintContexts.Accepts(HintIds.FirstSettlement, Wizard.Solo.UI.ScreenId.Settlement));
            Assert.IsTrue(HintContexts.Accepts(HintIds.FirstBag, Wizard.Solo.UI.ScreenId.Settlement));
            Assert.IsTrue(HintContexts.Accepts(HintIds.FirstDrop, Wizard.Solo.UI.ScreenId.Enhance));
            Assert.IsTrue(HintContexts.Accepts(HintHost.FirstEnhance, Wizard.Solo.UI.ScreenId.WandPicker));
            foreach (var id in new[] { HintIds.FirstLock, HintIds.FirstCrystal, HintIds.FirstBoss, HintIds.StuckDryFire, HintIds.StuckFizzle })
            {
                Assert.IsTrue(HintContexts.Accepts(id, Wizard.Solo.UI.ScreenId.Battle), id);
                foreach (var other in new[] { Wizard.Solo.UI.ScreenId.Settlement, Wizard.Solo.UI.ScreenId.MainMenu, Wizard.Solo.UI.ScreenId.LootPick, Wizard.Solo.UI.ScreenId.Camp, Wizard.Solo.UI.ScreenId.Enhance })
                    Assert.IsFalse(HintContexts.Accepts(id, other), id + " @ " + other);
            }
            var all = new List<string>(HintIds.FirstTime);
            all.AddRange(HintIds.Stuck);
            all.Add(HintHost.FirstEnhance);
            foreach (var id in all)
            {
                Assert.IsFalse(HintContexts.Accepts(id, Wizard.Solo.UI.ScreenId.MainMenu), id);
                Assert.IsFalse(HintContexts.Accepts(id, Wizard.Solo.UI.ScreenId.Title), id);
            }
        }

        [Test]
        public void Hints__SpotsPerScreenFromData_CardAutoClosesWithinTenSeconds()
        {
            var t = HintTuning.From(TestContent.Db);
            Assert.Greater(t.CardSeconds, 0f);
            Assert.LessOrEqual(t.CardSeconds, 10f, "쪽지는 일정 시간 뒤 스스로 접힌다");
            foreach (var sc in new[] { Wizard.Solo.UI.ScreenId.Battle, Wizard.Solo.UI.ScreenId.LootPick, Wizard.Solo.UI.ScreenId.Camp, Wizard.Solo.UI.ScreenId.Settlement, Wizard.Solo.UI.ScreenId.Enhance, Wizard.Solo.UI.ScreenId.WandPicker })
            {
                Assert.IsTrue(t.Spots.ContainsKey(sc), "안전 자리: " + sc);
                var s = t.SpotFor(sc);
                Assert.GreaterOrEqual(s.X, 0f);
                Assert.LessOrEqual(s.X + s.W, 1920f, sc + " 화면 밖");
            }
            // 전리품: 카드 열 위 오른쪽(Hints.json _note) — 카드 열(x < 1480) 오른쪽, 화면 안.
            var lp = t.SpotFor(Wizard.Solo.UI.ScreenId.LootPick);
            Assert.GreaterOrEqual(lp.Y, 0f);
            Assert.GreaterOrEqual(lp.X, 1480f);
            float shortH = HintHost.BodyHeight("가나다", 300f), longH = HintHost.BodyHeight(new string('가', 80), 300f);
            Assert.Greater(longH, shortH, "본문이 길면 쪽지가 길어진다");
        }

        [Test]
        public void LootPreview__DummyBlindBlocks_AreBounceFrostHomingPierceTrigger()
        {
            var db = TestContent.Db;
            foreach (var id in new[] { "block.wall_bounce", "block.frost", "block.homing", "block.pierce", "block.on_hit_trigger" })
                Assert.IsTrue(BlockDescriber.DummyBlind(db.Block(id)), id);
            foreach (var id in new[] { "block.magic_bolt", "block.fireball", "block.double_size", "block.triple_heat", "block.split_two" })
                Assert.IsFalse(BlockDescriber.DummyBlind(db.Block(id)), id);
            Assert.IsFalse(BlockDescriber.DummyBlind(null));
        }

        [Test]
        public void Enhance__ReduceFlashCapsFlashScaleAtThirtyPercent()
        {
            Assert.AreEqual(100, EnhanceFlashGuard.EffectiveFlashScalePct(100, false));
            Assert.AreEqual(30, EnhanceFlashGuard.EffectiveFlashScalePct(100, true));
            Assert.AreEqual(20, EnhanceFlashGuard.EffectiveFlashScalePct(20, true), "이미 낮으면 그대로");
        }

        [Test]
        public void Enhance__MutationDescriberGivesGainAndCostForEveryMutation()
        {
            var db = TestContent.Db;
            var names = TestContent.Names;
            foreach (var id in new[] { "mutation.twisted_barrel", "mutation.glass_wand", "mutation.gluttony", "mutation.mirror_image", "mutation.hypersensitive_circuit", "mutation.unstable_core" })
            {
                Assert.IsNotNull(MutationDescriber.Gain(names, db, id), id);
                Assert.IsNotNull(MutationDescriber.Cost(names, db, id), id);
            }
        }
    }
}
