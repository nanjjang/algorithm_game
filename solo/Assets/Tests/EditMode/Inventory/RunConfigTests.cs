using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Wizard.Solo.Meta;
using Wizard.Solo.Meta.Inventory;
using Wizard.Solo.Sim;

namespace Wizard.Solo.Tests.Inventory
{
    /// <summary>
    /// 판 설정 만들기(BuildRunConfig·BuildPracticeConfig)와 프로필 조립 → LoadoutSpec 변환, 출발 조건(08 5.4절 CanDepart).
    /// 계약 벡터는 작업 지시(G13)와 IWandService 주석, 기대값은 01 4.5·4.6절, 03 SL-3·SL-6, 08 5.4·8.5절, 00 문서 5.5절·D-56·D-59.
    /// </summary>
    public class RunConfigTests
    {
        InvFixture f;

        [SetUp]
        public void SetUp() { f = new InvFixture(); }

        // ------------------------------------------------------------ 변환: Links 4 + Extra 2 → 6칸

        [Test]
        public void S03_SL_3__LoadoutDataToSpecLinksPlusExtra()
        {
            var lo = new LoadoutData { Core = Ids.Block.MagicBolt };
            lo.Links.AddRange(new[] { Ids.Block.Pierce, null, null, Ids.Block.Frost });
            lo.Extra.AddRange(new[] { null, Ids.Block.Homing });
            var spec = InventoryRules.ToSpec(lo, 4, 2);
            Assert.AreEqual(Ids.Block.MagicBolt, spec.Core);
            Assert.AreEqual(6, spec.Links.Count);
            CollectionAssert.AreEqual(new[] { Ids.Block.Pierce, null, null, Ids.Block.Frost, null, Ids.Block.Homing }, spec.Links);
        }

        [Test]
        public void S03_SL_3__ShortStoredExtraIsPaddedWithEmpty()
        {
            var lo = new LoadoutData { Core = Ids.Block.MagicBolt };
            lo.Links.AddRange(new string[] { null, null, null, null });
            var spec = InventoryRules.ToSpec(lo, 4, 3);
            Assert.AreEqual(7, spec.Links.Count);
            Assert.IsNull(spec.Links[6]);
        }

        [Test]
        public void S03_SL_3__BlockBeyondSlotCountIsInvalid()
        {
            var lo = new LoadoutData { Core = Ids.Block.MagicBolt };
            lo.Links.AddRange(new string[] { null, null, null, null, Ids.Block.Pierce });
            Assert.IsNull(InventoryRules.ToSpec(lo, 4, 0));
        }

        [Test]
        public void S07_BuildRunConfig__WandWithTwoExtraGivesSixLinks()
        {
            f.AddLibrary(Ids.Block.Homing, 1);
            var w = f.Starter(1);
            w.SlotsExtra = 2;
            var lo = w.Loadout.Clone();
            lo.Extra.Add(null); lo.Extra.Add(Ids.Block.Homing);
            Assert.IsTrue(f.Wands.SetLoadout(w.Id, lo).Ok);
            var r = f.Wands.BuildRunConfig(1, w.Id, 1, 42);
            Assert.IsTrue(r.Ok, r.Error.ToString());
            Assert.AreEqual(6, r.Value.LinkSlotCount);
            Assert.AreEqual(6, r.Value.Loadout.Links.Count);
            Assert.AreEqual(Ids.Block.Homing, r.Value.Loadout.Links[5]);
        }

        // ------------------------------------------------------------ BuildRunConfig

        [Test]
        public void S01_4_5__BuildRunConfigFillsConfigAndCountsStart()
        {
            var p = f.P;
            var w = f.Starter(3);
            w.Stage = 4;
            w.Mutations.Add(new MutationEntry { Id = Ids.Mutation.Gluttony, Seq = 1 });
            p.KnownAffinities.Add(AffinityKey.Of("enemy.slime_moss", Affinity.Weak));
            p.Flags.PhantomMet = true;          // 첫 만남을 치렀고 복수 전 → Pursuing
            p.Flags.RevengeDoneAt = null;
            p.Flags.CrowBundleGiven = true;
            int runs = p.RunsStarted;
            int saves = f.Store.SaveCount;

            var r = f.Wands.BuildRunConfig(3, w.Id, 1, 12345UL);
            Assert.IsTrue(r.Ok, r.Error.ToString());
            var cfg = r.Value;
            Assert.AreEqual(RunMode.Endless, cfg.Mode);
            Assert.AreEqual(12345UL, cfg.Seed);
            Assert.IsFalse(string.IsNullOrEmpty(cfg.RunId));
            Assert.AreEqual("char.3", cfg.CharId);
            Assert.AreEqual(w.Id, cfg.WandId);
            Assert.AreEqual(4, cfg.WandStage);
            Assert.AreEqual(5, cfg.LinkSlotCount);   // char.3 기본 5칸(SL-1)
            CollectionAssert.AreEqual(new[] { Ids.Mutation.Gluttony }, cfg.Mutations);
            Assert.AreEqual(Ids.Block.MagicBolt, cfg.Loadout.Core);
            Assert.AreEqual(5, cfg.Loadout.Links.Count);
            CollectionAssert.AreEquivalent(p.Character(3).UnlockedArts, cfg.UnlockedArts);
            Assert.AreNotSame(p.Character(3).UnlockedArts, cfg.UnlockedArts);
            Assert.AreEqual(1, cfg.StartWave);
            Assert.IsFalse(cfg.PhantomFirst);
            Assert.IsTrue(cfg.RevengePending);
            Assert.IsFalse(cfg.CrowBundlePending);
            CollectionAssert.AreEqual(p.KnownAffinities, cfg.KnownAffinities);
            Assert.AreNotSame(p.KnownAffinities, cfg.KnownAffinities);
            Assert.IsFalse(cfg.PracticeInfiniteCrystal);

            Assert.AreEqual(runs + 1, p.RunsStarted);
            Assert.AreEqual(saves + 1, f.Store.SaveCount);
            Assert.AreEqual(3, p.LastCharacter);
        }

        [Test]
        public void S01_4_5__NewProfilePhantomFirstAndCrowPending()
        {
            var cfg = f.Wands.BuildRunConfig(1, f.Starter(1).Id, 1, 1).Value;
            Assert.IsTrue(cfg.PhantomFirst);          // phantom_met == false
            Assert.IsFalse(cfg.RevengePending);       // BeforeMeeting
            Assert.IsTrue(cfg.CrowBundlePending);     // crow_bundle_given == false
        }

        [Test]
        public void S07_BuildRunConfig__RevengeDoneNotPending()
        {
            f.P.Flags.PhantomMet = true;
            f.P.Flags.RevengeDoneAt = "2026-11-01T00:00:00+09:00";
            Assert.IsFalse(f.Wands.BuildRunConfig(1, f.Starter(1).Id, 1, 1).Value.RevengePending);
        }

        [Test]
        public void S07_BuildRunConfig__RunIdIsNewEachTime()
        {
            var a = f.Wands.BuildRunConfig(1, f.Starter(1).Id, 1, 1).Value;
            var b = f.Wands.BuildRunConfig(1, f.Starter(1).Id, 1, 1).Value;
            Assert.AreNotEqual(a.RunId, b.RunId);
        }

        [Test]
        public void S07_3_1__BuildRunConfigOtherCharactersWandRejected()
        {
            Assert.AreEqual(ErrorCode.InvalidArgument, f.Wands.BuildRunConfig(2, f.Starter(1).Id, 1, 1).Error);
            Assert.AreEqual(ErrorCode.NotFound, f.Wands.BuildRunConfig(1, 99999, 1, 1).Error);
        }

        // ------------------------------------------------------------ 08 5.4절 CanDepart

        [Test]
        public void S13_TV_031__CoreEmptyBlocksDepart()
        {
            var w = f.Starter(1);
            w.Loadout.Core = null;
            int runs = f.P.RunsStarted;
            int saves = f.Store.SaveCount;
            Assert.AreEqual(ErrorCode.CoreEmpty, f.Wands.BuildRunConfig(1, w.Id, 1, 1).Error);
            Assert.AreEqual(runs, f.P.RunsStarted);
            Assert.AreEqual(saves, f.Store.SaveCount);
        }

        [Test]
        public void S13_TV_032__ShopWandEmptyLoadoutBlocksDepart()
        {
            f.P.Currencies.Gold = 500;
            Assert.IsTrue(f.Shop.Buy(Ids.Item.WandBox, 1, 2).Ok);
            var bought = f.P.Wands[f.P.Wands.Count - 1];
            Assert.AreEqual(ErrorCode.CoreEmpty, f.Wands.BuildRunConfig(2, bought.Id, 1, 1).Error);
        }

        [Test]
        public void S13_5_4__OnExpeditionBlocksDepart()
        {
            var w = f.Starter(1);
            f.StartExpedition(w.Id);
            Assert.AreEqual(ErrorCode.WandLocked, f.Wands.BuildRunConfig(1, w.Id, 1, 1).Error);
        }

        [Test]
        public void S13_5_4__OkWhenCoreFilledAndNotLocked()
        {
            Assert.IsTrue(f.Wands.BuildRunConfig(1, f.Starter(1).Id, 1, 1).Ok);
        }

        // ------------------------------------------------------------ 시작 웨이브(01 4.6절)

        [Test]
        public void S01_4_6__StartWaveMustBeAllowedSkipPlusOne()
        {
            f.P.Character(1).BestWave = 15;
            f.Settlement.Skips = new List<int> { 0, 10 };
            var ok = f.Wands.BuildRunConfig(1, f.Starter(1).Id, 11, 1);
            Assert.IsTrue(ok.Ok);
            Assert.AreEqual(11, ok.Value.StartWave);
            Assert.AreEqual(15, f.Settlement.LastBest);
            Assert.AreEqual(ErrorCode.InvalidArgument, f.Wands.BuildRunConfig(1, f.Starter(1).Id, 21, 1).Error);
            Assert.AreEqual(ErrorCode.InvalidArgument, f.Wands.BuildRunConfig(1, f.Starter(1).Id, 16, 1).Error);
        }

        [Test]
        public void S01_4_6__WithoutSettlementOnlyWaveOne()
        {
            f.Ctx.Settlement = null;
            Assert.IsTrue(f.Wands.BuildRunConfig(1, f.Starter(1).Id, 1, 1).Ok);
            Assert.AreEqual(ErrorCode.InvalidArgument, f.Wands.BuildRunConfig(1, f.Starter(1).Id, 11, 1).Error);
        }

        // ------------------------------------------------------------ 컴파일 검사: 문맥은 ctx.Rules.Traits에서

        [Test]
        public void S00_5_2__CompileContextComesFromTraits()
        {
            var traits = new RecordingTraits();
            var spells = new RecordingSpells();
            f.Ctx.Rules.Traits = traits;
            f.Ctx.Rules.Spells = spells;
            f.AddLibrary(Ids.Block.Pierce, 1);
            var w = f.Starter(1);
            w.Stage = 6;
            w.SlotsExtra = 2;
            w.Mutations.Add(new MutationEntry { Id = Ids.Mutation.MirrorImage, Seq = 1 });
            var lo = w.Loadout.Clone(); lo.Links[1] = Ids.Block.Pierce;
            Assert.IsTrue(f.Wands.SetLoadout(w.Id, lo).Ok);

            var r = f.Wands.BuildRunConfig(1, w.Id, 1, 1);
            Assert.IsTrue(r.Ok, r.Error.ToString());
            Assert.AreEqual(1, traits.ComputeCalls);
            Assert.AreEqual(1, traits.ContextCalls);
            Assert.AreEqual(1, spells.Calls);
            Assert.AreEqual("char.1", traits.LastChar.Id);
            Assert.AreEqual(6, traits.LastStage);
            CollectionAssert.AreEqual(new[] { Ids.Mutation.MirrorImage }, traits.LastMutations);
            Assert.AreEqual(6, traits.LastLinkSlotCount);   // 4 + 2
            Assert.AreEqual(traits.Returned.LinkSlotCount, spells.LastCtx.LinkSlotCount);
            Assert.AreEqual(traits.Returned.Underline, spells.LastCtx.Underline);
            Assert.AreEqual(traits.Returned.DepthDecay, spells.LastCtx.DepthDecay);
            Assert.AreEqual(0.25f, spells.LastCtx.DepthDecay);   // 0.5(ComputeDerived) × 0.5(CompileContext)
            Assert.AreEqual(Ids.Block.MagicBolt, spells.LastCore);
            Assert.AreEqual(6, spells.LastLinks.Count);
            Assert.AreEqual(Ids.Block.Pierce, spells.LastLinks[1]);
        }

        [TestCase(SpellDiagCode.E_SLOT_COUNT_MISMATCH)]
        [TestCase(SpellDiagCode.E_CORE_NOT_PROJECTILE)]
        public void S03_4_6__CompileErrorIsInvalidLoadout(SpellDiagCode code)
        {
            var spells = new RecordingSpells();
            spells.Diagnose.Add(code);
            f.Ctx.Rules.Spells = spells;
            int runs = f.P.RunsStarted;
            Assert.AreEqual(ErrorCode.InvalidLoadout, f.Wands.BuildRunConfig(1, f.Starter(1).Id, 1, 1).Error);
            Assert.AreEqual(runs, f.P.RunsStarted);
            Assert.AreEqual(ErrorCode.InvalidLoadout, f.Wands.BuildPracticeConfig(1, f.Starter(1).Id, RunMode.Practice, null, 1).Error);
        }

        [Test]
        public void S03_4_6__WarningsDoNotBlock()
        {
            var spells = new RecordingSpells();
            spells.Diagnose.Add(SpellDiagCode.W_NO_EFFECT);
            spells.Diagnose.Add(SpellDiagCode.W_TRIGGER_DEPTH);
            f.Ctx.Rules.Spells = spells;
            Assert.IsTrue(f.Wands.BuildRunConfig(1, f.Starter(1).Id, 1, 1).Ok);
        }

        [Test]
        public void S00_5_2__NoRulesSkipsCompile()
        {
            f.Ctx.Rules = null;
            Assert.IsTrue(f.Wands.BuildRunConfig(1, f.Starter(1).Id, 1, 1).Ok);
            f.Ctx.Rules = SimModuleSet.Stubs();   // 빈 구현
            Assert.IsTrue(f.Wands.BuildRunConfig(1, f.Starter(1).Id, 1, 1).Ok);
        }

        // ------------------------------------------------------------ BuildPracticeConfig (계약 벡터)

        [Test]
        public void S00_D56__PracticeTenTimesLeavesProfileFileUntouched()
        {
            string dir = Path.Combine(Path.GetTempPath(), "solo-g13-" + Guid.NewGuid().ToString("N"));
            try
            {
                var db = f.Db;
                var clock = f.Clock;
                var store = new Wizard.Solo.Save.ProfileStore(dir, () => Wizard.Solo.Save.NewProfile.Create(db, clock), db.DataVersion);
                store.LoadOrCreate(out _, out _);
                f.Ctx.Profiles = store;
                var svc = new WandService(f.Ctx);
                int saved = 0;
                store.Saved += () => saved++;
                byte[] before = File.ReadAllBytes(store.FilePath);
                int runs = store.Current.RunsStarted;
                long wandId = store.Current.Wands.Find(w => w.Character == 1).Id;

                for (int i = 0; i < 10; i++)
                {
                    var mode = i % 2 == 0 ? RunMode.Practice : RunMode.Tutorial;
                    Assert.IsTrue(svc.BuildPracticeConfig(1, wandId, mode, null, (ulong)i).Ok);
                }
                Assert.AreEqual(0, saved);
                Assert.AreEqual(runs, store.Current.RunsStarted);
                CollectionAssert.AreEqual(before, File.ReadAllBytes(store.FilePath));
            }
            finally
            {
                try { if (Directory.Exists(dir)) Directory.Delete(dir, true); } catch (IOException) { }
            }
        }

        [Test]
        public void S08_8_5__PracticeConfigFields()
        {
            f.P.Flags.PhantomMet = true;   // Pursuing이어도 연습은 false
            f.P.KnownAffinities.Add(AffinityKey.Of("enemy.bat", Affinity.Resist));
            var w = f.Starter(2);
            var r = f.Wands.BuildPracticeConfig(2, w.Id, RunMode.Practice, null, 9);
            Assert.IsTrue(r.Ok);
            var cfg = r.Value;
            Assert.AreEqual(RunMode.Practice, cfg.Mode);
            Assert.AreEqual(string.Empty, cfg.RunId);
            Assert.AreEqual(1, cfg.StartWave);
            Assert.IsFalse(cfg.PhantomFirst);
            Assert.IsFalse(cfg.RevengePending);
            Assert.IsFalse(cfg.CrowBundlePending);
            Assert.IsFalse(cfg.PracticeInfiniteCrystal);
            CollectionAssert.AreEqual(f.P.KnownAffinities, cfg.KnownAffinities);
            Assert.AreEqual("char.2", cfg.CharId);
            Assert.AreEqual(Ids.Block.MagicBolt, cfg.Loadout.Core);
            Assert.AreEqual(4, cfg.Loadout.Links.Count);
            Assert.AreEqual(9UL, cfg.Seed);
        }

        [Test]
        public void S00_D59__TutorialInfiniteCrystalStepOne()
        {
            var cfg = f.Wands.BuildPracticeConfig(1, f.Starter(1).Id, RunMode.Tutorial, null, 1).Value;
            Assert.AreEqual(RunMode.Tutorial, cfg.Mode);
            Assert.IsTrue(cfg.PracticeInfiniteCrystal);
            Assert.AreEqual(1, cfg.TutorialStep);
        }

        [Test]
        public void S00_5_5__PracticeEndlessIsInvalidArgument()
        {
            Assert.AreEqual(ErrorCode.InvalidArgument, f.Wands.BuildPracticeConfig(1, f.Starter(1).Id, RunMode.Endless, null, 1).Error);
        }

        [Test]
        public void S00_D56__PracticeCoreEmptyWand()
        {
            var w = f.Starter(1);
            w.Loadout.Core = null;
            Assert.AreEqual(ErrorCode.CoreEmpty, f.Wands.BuildPracticeConfig(1, w.Id, RunMode.Practice, null, 1).Error);
            var over = new LoadoutSpec { Core = null, Links = new List<string> { null, null, null, null } };
            Assert.AreEqual(ErrorCode.CoreEmpty, f.Wands.BuildPracticeConfig(1, w.Id, RunMode.Practice, over, 1).Error);
        }

        [Test]
        public void S00_D17__OverrideWithBlocksNotInLibraryPasses()
        {
            // 야영에서 연 연습 = 지금 판의 조립(야생 블록 포함). 서고에 없는 블록이어도 통과한다.
            Assert.AreEqual(0, f.Wands.LibraryFree(Ids.Block.ChainLightning));
            var over = new LoadoutSpec
            {
                Core = Ids.Block.ChainLightning,
                Links = new List<string> { Ids.Block.Homing, Ids.Block.TripleHeat, null, Ids.Block.Corrosion }
            };
            var r = f.Wands.BuildPracticeConfig(1, f.Starter(1).Id, RunMode.Practice, over, 1);
            Assert.IsTrue(r.Ok, r.Error.ToString());
            Assert.AreEqual(Ids.Block.ChainLightning, r.Value.Loadout.Core);
            CollectionAssert.AreEqual(over.Links, r.Value.Loadout.Links);
            Assert.AreNotSame(over, r.Value.Loadout);
            Assert.AreEqual(0, f.P.Item(Ids.Block.ChainLightning).QtySocketed);   // 서고를 건드리지 않는다
        }

        [Test]
        public void S03_SL_4__OverrideStillChecksCoreAndSlotCount()
        {
            var badCore = new LoadoutSpec { Core = Ids.Block.Pierce, Links = new List<string> { null, null, null, null } };
            Assert.AreEqual(ErrorCode.InvalidLoadout, f.Wands.BuildPracticeConfig(1, f.Starter(1).Id, RunMode.Practice, badCore, 1).Error);
            var badCount = new LoadoutSpec { Core = Ids.Block.MagicBolt, Links = new List<string> { null, null, null } };
            Assert.AreEqual(ErrorCode.InvalidLoadout, f.Wands.BuildPracticeConfig(1, f.Starter(1).Id, RunMode.Practice, badCount, 1).Error);
        }
    }
}
