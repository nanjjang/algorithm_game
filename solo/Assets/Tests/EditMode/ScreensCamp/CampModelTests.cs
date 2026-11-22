using System.Collections.Generic;
using NUnit.Framework;
using Wizard.Solo.Meta;
using Wizard.Solo.Screens.Camp;
using Wizard.Solo.Sim;
using Wizard.Solo.UI;

namespace Wizard.Solo.Tests.ScreensCamp
{
    /// <summary>야영 화면(모드 넷)의 규칙과 계약 벡터(08 요약서 8.2절, 03 요약서 13절, 06 요약서 4.3절).</summary>
    public class CampModelTests
    {
        static readonly CampMode[] AllModes = { CampMode.Camp, CampMode.Library, CampMode.LoadoutEdit, CampMode.Tutorial };

        [Test]
        public void S08_TV_068__NoEnhanceTabOrButtonInAnyMode()
        {
            foreach (var mode in AllModes)
            {
                var l = CampModeLayout.For(mode);
                foreach (var t in l.Tabs)
                {
                    string key = CampModeLayout.TabKey(t);
                    StringAssert.DoesNotContain("enhance", key, mode + " 모드에 강화 탭");
                    Assert.IsTrue(key == "str.camp.tab.assembly" || key == "str.camp.tab.arts" || key == "str.camp.tab.library");
                }
            }
            // 강화 화면은 이 묶음에서 열 수 없다: 등록한 화면에 강화 묶음이 없다.
            var m = new CampFakeScreens();
            ScreenRegistry.RegisterAll(m, new GameContext());
            Assert.IsTrue(m.Registered.Contains(ScreenId.Camp));
        }

        [Test]
        public void S00_G21_C01__ModeTabsAndTitleKeys()
        {
            var camp = CampModeLayout.For(CampMode.Camp);
            CollectionAssert.AreEqual(new[] { CampTab.Assembly, CampTab.Arts }, camp.Tabs);
            Assert.AreEqual("str.camp.title", camp.TitleKey);
            Assert.IsTrue(camp.ShowHeal && camp.ShowDepart && camp.ShowPractice && camp.ShowNextWave && camp.UsesWorld && camp.CloseByCommand);
            Assert.IsFalse(camp.QeSwitchesCharacter, "야영은 Q/E = 탭");

            var lib = CampModeLayout.For(CampMode.Library);
            CollectionAssert.AreEqual(new[] { CampTab.Assembly, CampTab.Library }, lib.Tabs);
            Assert.AreEqual("str.camp.title.library", lib.TitleKey);
            Assert.IsTrue(lib.QeSwitchesCharacter && lib.ShowWandPicker && lib.ShowPractice);
            Assert.IsFalse(lib.UsesWorld || lib.ShowHeal || lib.ShowDepart || lib.ShowNextWave);

            var edit = CampModeLayout.For(CampMode.LoadoutEdit);
            CollectionAssert.AreEqual(new[] { CampTab.Assembly }, edit.Tabs);
            Assert.AreEqual("str.camp.title.loadout_edit", edit.TitleKey);
            Assert.IsFalse(edit.ShowPractice || edit.ShowWandPicker || edit.UsesWorld);

            var tut = CampModeLayout.For(CampMode.Tutorial);
            CollectionAssert.AreEqual(new[] { CampTab.Assembly }, tut.Tabs);
            Assert.IsTrue(tut.UsesWorld);
            Assert.IsFalse(tut.ShowHeal || tut.ShowDepart || tut.ShowPractice || tut.ShowNextWave || tut.CloseByCommand, "튜토리얼: 회복·출발·비기·조립 시험·다음 웨이브 없음");

            foreach (var mode in AllModes)
                foreach (var t in CampModeLayout.For(mode).Tabs)
                    Assert.IsTrue(TestContent.Names.Has(CampModeLayout.TabKey(t)));
            Assert.AreEqual("조립과 서고", TestContent.Names.Get("str.camp.title.library"));
            Assert.AreEqual("조립 바꾸기", TestContent.Names.Get("str.camp.title.loadout_edit"));
        }

        [Test]
        public void S08_TV_065__HealCostWave10_80Then120()
        {
            var w = CampTestKit.World(SimModuleSet.CreateDefault());
            w.Run.LastClearedWave = 10;
            w.Run.Purse = 1000;
            Assert.AreEqual(80, CampRules.Heal(w, w.P0).Cost);
            w.Run.HealsThisCamp = 1;
            Assert.AreEqual(120, CampRules.Heal(w, w.P0).Cost);
        }

        [Test]
        public void S08_TV_066__HealCostWave23_145Then217()
        {
            var w = CampTestKit.World(SimModuleSet.CreateDefault());
            w.Run.LastClearedWave = 23;
            w.Run.Purse = 1000;
            Assert.AreEqual(145, CampRules.Heal(w, w.P0).Cost);
            w.Run.HealsThisCamp = 1;
            Assert.AreEqual(217, CampRules.Heal(w, w.P0).Cost);
        }

        [Test]
        public void S08_8_2__HealButtonPurseOnlyAndFullHpOff()
        {
            var mods = SimModuleSet.Stubs();
            mods.Run = new CampFixedHealRun { Cost = 80 };
            var w = CampTestKit.World(mods);
            w.Run.Purse = 79;
            var s = CampRules.Heal(w, w.P0);
            Assert.IsFalse(s.Enabled);
            Assert.AreEqual("error.not_enough_gold", s.DisabledReasonKey);
            w.Run.Purse = 80;
            Assert.IsTrue(CampRules.Heal(w, w.P0).Enabled);
            w.P0.Hp = w.P0.MaxHp;
            Assert.IsFalse(CampRules.Heal(w, w.P0).Enabled, "체력 가득이면 꺼짐");
        }

        [Test]
        public void S08_TV_067__DawnCampShowsFreeHealLine()
        {
            var mods = SimModuleSet.Stubs();
            mods.Run = new CampFixedHealRun { Cost = 0 };
            var w = CampTestKit.World(mods);
            w.Run.DawnCamp = true;
            Assert.IsTrue(CampRules.Heal(w, w.P0).DawnLine);
            Assert.IsTrue(TestContent.Names.Has("str.camp.heal_dawn"));
        }

        [Test]
        public void S08_TV_069__CritShotsZeroWarns()
        {
            Assert.IsTrue(CampRules.CritZeroWarning(new SpellStats { CritShots = 0 }));
            Assert.IsFalse(CampRules.CritZeroWarning(new SpellStats { CritShots = 4 }));
            Assert.IsFalse(CampRules.CritZeroWarning(new SpellStats { CritShots = -1 }), "−1 = 무한(열 0)");
            Assert.AreEqual("임계 사격을 한 번도 할 수 없다", TestContent.Names.Get("str.camp.warn.crit_zero"));
        }

        [Test]
        public void S08_TV_070__ChangedHeatLights30Ticks()
        {
            var h = new StatHighlight(30);
            Assert.IsFalse(h.Set("heat", "8.0"), "처음 값은 밝히지 않는다");
            Assert.IsTrue(h.Set("heat", "11.0"));
            for (int i = 0; i < 29; i++) h.StepTick();
            Assert.IsTrue(h.IsLit("heat"), "29틱 뒤에도 밝다");
            h.StepTick();
            Assert.IsFalse(h.IsLit("heat"), "30틱(0.5초)에 끝");
            Assert.IsFalse(h.Set("heat", "11.0"), "같은 값은 다시 밝히지 않는다");
        }

        [Test]
        public void S08_TV_071__BeforeMeetingWave9CampShowsPhantomNameOnly()
        {
            var plan = new WavePlan { Wave = 10, Tod = Ids.Tod.Boss, BossId = Ids.Boss.MirrorPhantom, PhantomFirst = true };
            var v = CampRules.NextWave(TestContent.Db, plan, new HashSet<string>());
            Assert.AreEqual(Ids.Boss.MirrorPhantom, v.BossId);
            Assert.AreEqual("스승의 허상", TestContent.Names.Get(v.BossId));
            Assert.AreEqual(0, v.Whims.Count);
        }

        [Test]
        public void S06_TV_197__BagPreviewCleared14Is4SlotsHero2()
        {
            var b = CampRules.BagPreview(null, TestContent.Db, 14);
            Assert.AreEqual(4, b.Slots);
            Assert.AreEqual(2, b.HeroCap);
            Assert.AreEqual("지금 판을 끝내면 가져가는 귀환 가방 4칸 · 영웅 2개까지",
                TestContent.Names.Format("str.camp.bag_preview", "slots", b.Slots, "hero", b.HeroCap));
            var st = new CampFakeSettlement { Limits = new BagLimits { Slots = 9, HeroCap = 2 } };
            var b2 = CampRules.BagPreview(st, TestContent.Db, 14);
            Assert.AreEqual(14, st.LastCleared, "정산 서비스가 있으면 그 값(BagLimitsFor)");
            Assert.AreEqual(9, b2.Slots);
        }

        [Test]
        public void S06_TV_198__NextWave15GuaranteesLow1Mid1()
        {
            var into = new List<KeyValuePair<string, int>>();
            CampRules.GuaranteedMaterials(TestContent.Db, 15, false, into);
            CollectionAssert.AreEquivalent(new[] { new KeyValuePair<string, int>(Ids.Item.StoneLow, 1), new KeyValuePair<string, int>(Ids.Item.StoneMid, 1) }, into);
        }

        [Test]
        public void S06_TV_199__NextWave30GuaranteesMid1High1()
        {
            var into = new List<KeyValuePair<string, int>>();
            CampRules.GuaranteedMaterials(TestContent.Db, 30, false, into);
            CollectionAssert.AreEquivalent(new[] { new KeyValuePair<string, int>(Ids.Item.StoneMid, 1), new KeyValuePair<string, int>(Ids.Item.StoneHigh, 1) }, into);
        }

        [Test]
        public void S00_G21_C02__NextWaveGlyphsOnlyForKnownAffinities()
        {
            // 약점·저항이 있는 종을 데이터에서 고른다.
            string sp = null; string weak = null, resist = null;
            foreach (var e in TestContent.Db.Enemies)
                if (!string.IsNullOrEmpty(e.Weak) && !string.IsNullOrEmpty(e.Resist)) { sp = e.Id; weak = e.Weak; resist = e.Resist; break; }
            Assert.IsNotNull(sp, "약점·저항이 둘 다 있는 종이 데이터에 있어야 한다");
            var plan = new WavePlan { Wave = 4 };
            plan.PreviewSpecies.Add(sp);
            var none = CampRules.NextWave(TestContent.Db, plan, new HashSet<string>());
            Assert.IsNull(none.Species[0].KnownWeak, "모르는 칸은 비운다(D-54)");
            Assert.IsNull(none.Species[0].KnownResist);
            var known = new HashSet<string> { AffinityKey.Of(sp, Affinity.Weak) };
            var some = CampRules.NextWave(TestContent.Db, plan, known);
            Assert.AreEqual(weak, some.Species[0].KnownWeak);
            Assert.IsNull(some.Species[0].KnownResist, "저항은 아직 모른다");
            known.Add(AffinityKey.Of(sp, Affinity.Resist));
            Assert.AreEqual(resist, CampRules.NextWave(TestContent.Db, plan, known).Species[0].KnownResist);
        }

        [Test]
        public void S00_G21_C03__LibraryBlockRemovedGoesToBenchRef300_ThenReinserted()
        {
            var w = CampTestKit.World();
            var a = new WorldAssembly(w, w.P0);
            // 코어의 서고 블록(WildUid 0)을 뺀다 → 명령 대상 −1(시뮬레이션이 대기 칸으로 보낸다).
            Assert.IsTrue(a.Move(SlotRefs.Core, SlotRefs.None));
            Assert.AreEqual(1, w.Commands.Count);
            Assert.AreEqual(RunCommandKind.LoadoutMove, w.Commands[0].Kind);
            Assert.AreEqual(0, w.Commands[0].A);
            Assert.AreEqual(-1, w.Commands[0].B);
            Assert.AreEqual(0, w.Commands[0].Slot);
            // 시뮬레이션이 옮긴 뒤의 상태: 대기 칸 0번째 = SlotRef 300으로 그려진다.
            w.P0.Loadout.Core = default;
            w.Run.LibraryBench.Add(new WildBlock { Uid = 5, BlockId = Ids.Block.MagicBolt, FromLibrary = true });
            Assert.AreEqual(1, a.BenchCount);
            Assert.AreEqual(Ids.Block.MagicBolt, a.At(300).BlockId);
            Assert.IsFalse(a.At(300).Wild);
            Assert.AreEqual(SlotRefs.Core, a.AutoInsertTarget(Ids.Block.MagicBolt), "투사체는 빈 코어 먼저");
            Assert.IsTrue(a.Move(300, SlotRefs.Core));
            Assert.AreEqual(300, w.Commands[1].A);
            Assert.AreEqual(0, w.Commands[1].B);
            // 보관함 i = 100 + i.
            w.Run.Storage.Add(new WildBlock { Uid = 9, BlockId = Ids.Block.Frost, Rarity = Ids.Rarity.Common });
            Assert.AreEqual(Ids.Block.Frost, a.At(100).BlockId);
            Assert.IsFalse(a.CanMove(100, SlotRefs.Core), "코어에는 투사체만");
            Assert.IsTrue(a.CanMove(100, 1));
            Assert.IsFalse(a.CanMove(100, 300), "보관함 ↔ 대기 칸은 못 옮긴다");
            Assert.IsTrue(a.RemoveAll());
            Assert.AreEqual(RunCommandKind.LoadoutRemoveAll, w.Commands[2].Kind);
        }

        [Test]
        public void S00_G21_C04__WildBlockCannotLeaveWhenStorageFull()
        {
            var w = CampTestKit.World();
            w.P0.Loadout.Links[0] = new SlotItem { BlockId = Ids.Block.Frost, WildUid = 3 };
            for (int i = 0; i < TestContent.Db.Drops.StorageCap; i++) w.Run.Storage.Add(new WildBlock { Uid = 10 + i, BlockId = Ids.Block.Pierce });
            var a = new WorldAssembly(w, w.P0);
            Assert.IsFalse(a.CanMove(1, SlotRefs.None));
            Assert.IsTrue(a.CanMove(1, SlotRefs.Storage(0)), "가득이어도 보관함 칸과는 서로 바꿀 수 있다");
        }

        [Test]
        public void S08_TV_083__LibraryCellsOwnedOnly_ZeroStateAndEquippedSeparate()
        {
            var profile = CampTestKit.NewProfile();
            profile.Items[Ids.Block.MagicBolt] = new ItemStack { Qty = 1, QtySocketed = 1 };
            profile.Items[Ids.Block.Frost] = new ItemStack { Qty = 0 };
            var wands = new CampFakeWands();
            var lib = new LibraryAssembly(TestContent.Db, wands, profile, 11);
            var cells = lib.VisibleCells();
            Assert.AreEqual(2, cells.Count, "가진 적 없는 블록 칸은 없다");
            var bolt = cells.Find(c => c.BlockId == Ids.Block.MagicBolt);
            var frost = cells.Find(c => c.BlockId == Ids.Block.Frost);
            Assert.AreEqual(1, bolt.Socketed, "마력탄은 끼움으로 따로 센다");
            Assert.IsFalse(bolt.ZeroState);
            Assert.IsTrue(frost.ZeroState, "서리는 0개 상태");
            Assert.IsFalse(cells.Exists(c => c.BlockId == Ids.Block.Fireball));
        }

        [Test]
        public void S00_G21_C05__LoadoutEditEditsOnlyArgWand()
        {
            var profile = CampTestKit.NewProfile();
            profile.Items[Ids.Block.Frost] = new ItemStack { Qty = 2 };
            var wands = new CampFakeWands();
            wands.Free[Ids.Block.Frost] = 2;
            var lib = new LibraryAssembly(TestContent.Db, wands, profile, 13);
            Assert.AreEqual(13, lib.WandId);
            Assert.AreEqual(2, lib.Available(Ids.Block.Frost));
            int k = lib.VisibleCells().FindIndex(c => c.BlockId == Ids.Block.Frost);
            Assert.IsTrue(lib.Move(SlotRefs.Library(k), SlotRefs.Link(0)));
            CollectionAssert.AreEqual(new long[] { 13 }, wands.SetLoadoutWands, "SetLoadout은 인자의 지팡이 하나에만");
            Assert.AreEqual(Ids.Block.Frost, lib.At(SlotRefs.Link(0)).BlockId);
            Assert.AreEqual(Ids.Block.Frost, wands.LastSetLoadout.Links[0]);
            Assert.AreEqual(Ids.Block.MagicBolt, wands.LastSetLoadout.Core);
            // 저장 뒤 서비스의 안 끼운 수가 하나 줄면(2 → 1) 더 끼울 수 있는 수도 1.
            wands.Free[Ids.Block.Frost] = 1;
            Assert.AreEqual(1, lib.Available(Ids.Block.Frost), "끼운 만큼 더 끼울 수 있는 수가 준다");
            lib.Move(SlotRefs.Link(0), SlotRefs.None);
            wands.Free[Ids.Block.Frost] = 2;
            Assert.AreEqual(2, lib.Available(Ids.Block.Frost), "빼면 서고로 돌아간다");
        }

        [Test]
        public void S00_G21_C06__SetLoadoutErrorRevertsAndShowsKey()
        {
            var profile = CampTestKit.NewProfile();
            var wands = new CampFakeWands { SetLoadoutError = ErrorCode.LastProjectile };
            var lib = new LibraryAssembly(TestContent.Db, wands, profile, 11);
            Assert.IsFalse(lib.Move(SlotRefs.Core, SlotRefs.None));
            Assert.AreEqual("error.last_projectile", lib.LastErrorKey);
            Assert.AreEqual(Ids.Block.MagicBolt, lib.At(SlotRefs.Core).BlockId, "거절되면 편집본을 되돌린다");
            Assert.AreEqual("마지막 투사체 블록은 올릴 수 없다.", TestContent.Names.Get(lib.LastErrorKey));
            Assert.AreEqual("error.core_empty", CampFormat.ErrorKey(ErrorCode.CoreEmpty));
            Assert.AreEqual("error.invalid_loadout", CampFormat.ErrorKey(ErrorCode.InvalidLoadout));
        }

        [Test]
        public void S00_G21_C07__LibraryQeSwitchesCharacterAndStartsAtEquipped()
        {
            Assert.IsTrue(CampModeLayout.For(CampMode.Library).QeSwitchesCharacter);
            var profile = CampTestKit.NewProfile();
            var lib = new LibraryAssembly(TestContent.Db, new CampFakeWands(), profile, profile.Character(3).EquippedWandId);
            Assert.AreEqual(13, lib.WandId, "처음 = 든 지팡이");
            Assert.AreEqual(TestContent.Db.Character(Ids.Char.C3).BaseLinkSlots, lib.BaseLinkCount, "제자 3은 연결 5칸");
        }

        [Test]
        public void S00_G21_C08__HeaderWaveIsTheWaveJustPassed()
        {
            var run = new RunState { LastClearedWave = 9 };
            Assert.AreEqual(9, CampRules.HeaderWave(run));
            run.NextPlan = new WavePlan { Wave = 11 };
            Assert.AreEqual(10, CampRules.HeaderWave(run));
            Assert.AreEqual("야영 · 1일째 웨이브 10 다음", TestContent.Names.Format("str.camp.header", "day", DayCycle.Day(10), "wave", 10));
        }

        [Test]
        public void S04_11_3__DepartNeedsPickWhenTurnsRemain()
        {
            var run = new RunState();
            Assert.IsFalse(CampRules.ArtPickPending(run));
            run.PendingArtTurns.Enqueue(3);
            Assert.IsTrue(CampRules.ArtPickPending(run));
            var r2 = new RunState { CurrentArtOffer = new ArtOffer { TurnWave = 2 } };
            Assert.IsFalse(CampRules.ArtPickPending(r2), "후보가 없는 차례는 보여 주지 않는다(U-21)");
            r2.CurrentArtOffer.Candidates.Add(Ids.Art.Fan);
            Assert.IsTrue(CampRules.ArtPickPending(r2));
        }

        [Test]
        public void S00_G21_C09__RegistersFourCampScreens()
        {
            var m = new CampFakeScreens();
            ScreenRegistry.RegisterAll(m, new GameContext());
            foreach (var id in new[] { ScreenId.LootPick, ScreenId.Camp, ScreenId.ArtTrie, ScreenId.DummyPractice })
                Assert.AreEqual(1, m.Registered.FindAll(x => x == id).Count, id + " 하나만 등록");
        }
    }
}
