using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Wizard.Solo.Meta;
using Wizard.Solo.Screens.Enhance;
using Wizard.Solo.UI;

namespace Wizard.Solo.Tests.ScreensEnhance
{
    /// <summary>
    /// 강화 묶음 화면의 계약 벡터: 첫 안내 문(D-67, TV-152·153), 인자 형(00 8.1절), Esc의 ReturnTo, OnExit의 Release("ritual"),
    /// 떠나보내기(TV-146·148·149·150), 화면을 실제로 조립해 돌리는 연기 시험.
    /// </summary>
    public sealed class EnhanceScreenContractTests
    {
        readonly List<GameObject> _roots = new List<GameObject>();

        [SetUp]
        public void SetUp() { _roots.Clear(); }

        [TearDown]
        public void TearDown()
        {
            foreach (var g in _roots) if (g != null) Object.DestroyImmediate(g);
            _roots.Clear();
        }

        RectTransform NewRoot()
        {
            var go = new GameObject("G23Root", typeof(RectTransform));
            _roots.Add(go);
            return (RectTransform)go.transform;
        }

        T Build<T>(G23Kit kit) where T : IScreen, new()
        {
            var s = new T();
            s.Build(kit.Ctx, NewRoot());
            return s;
        }

        // ------------------------------------------------------------------ 첫 안내 (D-67)

        [Test]
        public void S08_TV_152__TutorialOpensWhenBundleGivenAndNotShown()
        {
            var p = new ProfileData();
            p.Flags.CrowBundleGiven = true;
            Assert.IsTrue(EnhanceTutorialGate.ShouldOpen(p));
            var store = new G23ProfileStore { Current = p };
            var model = new EnhanceTutorialModel(4);
            Assert.IsTrue(model.Close(p, store));
            Assert.IsTrue(p.Flags.EnhanceTutorialShown);
            Assert.AreEqual(1, store.SaveCount);
            Assert.IsFalse(EnhanceTutorialGate.ShouldOpen(p), "두 번째 진입에는 없다");
            Assert.IsFalse(model.Close(p, store));
            Assert.IsFalse(EnhanceTutorialGate.MarkShown(p, store));
            Assert.AreEqual(1, store.SaveCount, "저장은 한 번만");
        }

        [Test]
        public void S08_TV_153__TutorialDeferredBeforeBundle()
        {
            var p = new ProfileData();
            Assert.IsFalse(EnhanceTutorialGate.ShouldOpen(p));
        }

        [Test]
        public void S00_D_67__TutorialGateOnWandPickerEnterFromMainMenu()
        {
            var kit = new G23Kit();
            kit.Profile.Flags.CrowBundleGiven = true;
            var picker = Build<WandPickerScreen>(kit);
            picker.OnEnter(ScreenArgs.From(ScreenId.MainMenu));
            Assert.AreEqual(1, kit.Screens.Count("push " + ScreenId.EnhanceTutorial), "정산을 거치지 않고 메인 메뉴에서 와도 뜬다");
            var a = kit.Screens.PushedArgs[kit.Screens.PushedArgs.Count - 1];
            Assert.IsFalse(a.Values.ContainsKey(ScreenArgKeys.NextScreen), "NextScreen 없이 — 닫으면 원래 화면");
        }

        [Test]
        public void S00_D_67__TutorialGateOnEnhanceEnter()
        {
            var kit = new G23Kit();
            kit.Profile.Flags.CrowBundleGiven = true;
            var s = Build<EnhanceScreen>(kit);
            s.OnEnter(ScreenArgs.From(ScreenId.WandPicker).With(ScreenArgKeys.WandId, 1L));
            Assert.AreEqual(1, kit.Screens.Count("push " + ScreenId.EnhanceTutorial));
        }

        [Test]
        public void S00_D_67__NoTutorialOnceShown()
        {
            var kit = new G23Kit();
            kit.Profile.Flags.CrowBundleGiven = true;
            kit.Profile.Flags.EnhanceTutorialShown = true;
            var picker = Build<WandPickerScreen>(kit);
            picker.OnEnter(ScreenArgs.From(ScreenId.MainMenu));
            Assert.AreEqual(0, kit.Screens.Count("push " + ScreenId.EnhanceTutorial));
        }

        [Test]
        public void S08_10_11__TutorialScreenFourPagesBodyHiddenCloseOnce()
        {
            var kit = new G23Kit();
            kit.Profile.Flags.CrowBundleGiven = true;
            var s = Build<EnhanceTutorialScreen>(kit);
            s.OnEnter(ScreenArgs.From(ScreenId.WandPicker));
            Assert.AreEqual(4, s.Model.Pages);
            Assert.AreEqual("1 / 4", s.Model.PageLabel(kit.Ctx.Names));
            Assert.IsNull(s.Model.Body(kit.Ctx.Names), "본문 값이 없으면 줄을 숨긴다(N-05)");
            var withBody = new G23Names(kit.Ctx.Names).Set("str.enhance_tutorial.page.1.body", "b");
            Assert.AreEqual("b", s.Model.Body(withBody));
            Assert.IsTrue(s.OnBack());
            Assert.IsTrue(kit.Profile.Flags.EnhanceTutorialShown);
            Assert.AreEqual(1, kit.Store.SaveCount);
            Assert.AreEqual(1, kit.Screens.Count("close " + ScreenId.EnhanceTutorial), "Pop — 밑 화면이 그대로 있다");
        }

        // ------------------------------------------------------------------ 인자와 Esc

        [Test]
        public void S00_8_1__ArgumentTypes()
        {
            var picker = ScreenArgs.From(ScreenId.Settlement).With(ScreenArgKeys.Character, 2).With(ScreenArgKeys.ReturnTo, ScreenId.MainMenu);
            var a = EnhanceNav.EnhanceArgs(7L, picker);
            Assert.IsInstanceOf<long>(a.Values[ScreenArgKeys.WandId]);
            Assert.AreEqual(7L, a.Get(ScreenArgKeys.WandId, 0L));
            Assert.IsInstanceOf<ScreenId>(a.Values[ScreenArgKeys.ReturnTo]);
            Assert.AreEqual(ScreenId.MainMenu, a.Get(ScreenArgKeys.ReturnTo, ScreenId.None));
            var r = EnhanceNav.ReleaseArgs(new[] { 3L, 4L });
            Assert.IsInstanceOf<List<long>>(r.Values[ScreenArgKeys.WandIds]);
            CollectionAssert.AreEqual(new[] { 3L, 4L }, r.Get<List<long>>(ScreenArgKeys.WandIds));
            var m = EnhanceNav.MutationRemoveArgs(5L, Ids.Mutation.Gluttony);
            Assert.IsInstanceOf<long>(m.Values[ScreenArgKeys.WandId]);
            Assert.IsInstanceOf<string>(m.Values[ScreenArgKeys.MutationId]);
            Assert.IsInstanceOf<long>(EnhanceNav.EngraveArgs(5L).Values[ScreenArgKeys.WandId]);
        }

        [Test]
        public void S08_10_1__EnhanceFromMainMenuPickerReturnsToMainMenu()
        {
            var a = EnhanceNav.EnhanceArgs(1L, ScreenArgs.From(ScreenId.MainMenu));
            Assert.AreEqual(ScreenId.MainMenu, EnhanceNav.ReturnTo(a));
        }

        [Test]
        public void S08_10_1__EscFollowsReturnTo()
        {
            var screens = new G23Screens();
            Assert.IsTrue(EnhanceNav.Back(screens, ScreenArgs.From(ScreenId.WandPicker).With(ScreenArgKeys.ReturnTo, ScreenId.MainMenu)));
            Assert.AreEqual(ScreenId.MainMenu, screens.LastPopTo);
            Assert.IsTrue(EnhanceNav.Back(screens, ScreenArgs.From(ScreenId.WandPicker)));
            Assert.AreEqual("pop", screens.Calls[screens.Calls.Count - 1]);
        }

        [Test]
        public void S00_D_34__FromSettlementEscGoesToMainMenu()
        {
            Assert.AreEqual(ScreenId.MainMenu, EnhanceNav.ReturnTo(ScreenArgs.From(ScreenId.Settlement)));
        }

        [Test]
        public void S08_10_1__EnhanceScreenEscUsesReturnTo()
        {
            var kit = new G23Kit();
            var s = Build<EnhanceScreen>(kit);
            s.OnEnter(ScreenArgs.From(ScreenId.WandPicker).With(ScreenArgKeys.WandId, 1L).With(ScreenArgKeys.ReturnTo, ScreenId.MainMenu));
            Assert.IsTrue(s.OnBack());
            Assert.AreEqual(ScreenId.MainMenu, kit.Screens.LastPopTo);
        }

        [Test]
        public void S08_10_1__WandPickerEscWithoutReturnToPops()
        {
            var kit = new G23Kit();
            var s = Build<WandPickerScreen>(kit);
            s.OnEnter(ScreenArgs.From(ScreenId.MainMenu));
            Assert.IsTrue(s.OnBack());
            Assert.AreEqual("pop", kit.Screens.Calls[kit.Screens.Calls.Count - 1]);
        }

        [Test]
        public void S00_10__OnExitReleasesRitualOnce()
        {
            var kit = new G23Kit();
            var s = Build<EnhanceScreen>(kit);
            s.OnEnter(ScreenArgs.From(ScreenId.WandPicker).With(ScreenArgKeys.WandId, 1L));
            s.OnExit();
            CollectionAssert.AreEqual(new[] { "ritual" }, kit.Art.Released);
            Assert.DoesNotThrow(() => EnhanceScreen.ReleaseRitualArt(new GameContext()), "ctx.Art가 null이면 건너뛴다");
        }

        // ------------------------------------------------------------------ 강화 화면 연기 시험

        [Test]
        public void S08_Contract__EnhanceScreenAttemptUsesServiceResultAndPlaysSounds()
        {
            var kit = new G23Kit();
            kit.Profile.Currencies.Gold = 100000;
            kit.Profile.Item(Ids.Item.StoneLow).Qty = 3;
            var s = Build<EnhanceScreen>(kit);
            s.OnEnter(ScreenArgs.From(ScreenId.WandPicker).With(ScreenArgKeys.WandId, 1L));
            Assert.IsTrue(kit.Sound.Played.Contains(EnhanceSoundIds.Open));
            var r = G23Results.Make(EnhanceOutcome.Success, 0, 1, 0, 0, 100, 5000);
            kit.Enhance.Results.Enqueue(r);
            Assert.IsTrue(s.PressEnhance());
            Assert.AreEqual(1, kit.Enhance.EnhanceCalls, "결과는 누른 순간 한 번 받는다");
            Assert.AreSame(r, s.Model.Result);
            Assert.AreEqual(RitualStyle.Full, s.Model.Style);
            for (int i = 0; i < 2000 && s.Model.Phase != EnhancePhase.ResultCard; i++) s.TickOnce();
            Assert.AreEqual(EnhancePhase.ResultCard, s.Model.Phase);
            Assert.IsTrue(kit.Sound.Played.Contains(EnhanceSoundIds.Press));
            Assert.IsTrue(kit.Sound.Played.Contains(EnhanceSoundIds.ChargeBegin));
            Assert.IsTrue(kit.Sound.Played.Contains("ui.enhance.success"));
            Assert.IsTrue(kit.Sound.Played.Contains(EnhanceSoundIds.ResultCard));
            Assert.IsFalse(kit.Sound.Played.Contains(EnhanceSoundIds.ChargeProgress), "+1 도전은 심장 소리 없음(TV-100)");
            foreach (var b in kit.Sound.Buses) Assert.IsTrue(b == SoundBus.Ui || b == SoundBus.Wand);
            s.OnExit();
        }

        [Test]
        public void S08_Contract__EnhanceRitualFastSettingGivesShortForm()
        {
            var kit = new G23Kit();
            kit.Profile.Currencies.Gold = 100000;
            SettingsOptions.Set(kit.SettingsStore.Current, OptionKeys.EnhanceRitual, OptionValues.Fast);
            var s = Build<EnhanceScreen>(kit);
            s.OnEnter(ScreenArgs.From(ScreenId.WandPicker).With(ScreenArgKeys.WandId, 1L));
            kit.Enhance.Results.Enqueue(G23Results.Make(EnhanceOutcome.Success, 0, 1, 0, 0, 100));
            Assert.IsTrue(s.PressEnhance());
            Assert.AreEqual(RitualStyle.Fast, s.Model.Style);
            int n = 0;
            while (s.Model.Phase != EnhancePhase.ResultCard && n < 500) { s.TickOnce(); n++; }
            Assert.AreEqual(48, n);
            CollectionAssert.Contains(s.Model.History, EnhancePhase.ShortResult);
            s.OnExit();
        }

        [Test]
        public void S07_TV_M_07__PendingWandOpensMutationCardFirst()
        {
            var kit = new G23Kit();
            var w = kit.Wand(1);
            w.Stage = 6;
            w.FloorPeak = 6;
            w.RecordPeak = 6;
            w.PendingOffer = new MutationOffer { AttemptId = 4, Candidate = Ids.Mutation.MirrorImage, FallbackOutcome = "FailDrop", FallbackStageAfter = 5, FallbackAuraAfter = 1 };
            var s = Build<EnhanceScreen>(kit);
            s.OnEnter(ScreenArgs.From(ScreenId.WandPicker).With(ScreenArgKeys.WandId, 1L));
            Assert.AreEqual(EnhancePhase.MutationChoice, s.Model.Phase);
            var decision = G23Results.Make(EnhanceOutcome.FailDrop, 6, 5, 0, 1);
            kit.Enhance.Decisions.Enqueue(decision);
            s.ChooseMutation(false);
            Assert.AreEqual(1, kit.Enhance.ResolveCalls);
            Assert.AreEqual(EnhancePhase.MutationRejectReplay, s.Model.Phase);
            s.OnExit();
        }

        [Test]
        public void S08_10_4__EnhanceScreenProbabilityFromPreview()
        {
            var kit = new G23Kit();
            var w = kit.Wand(1);
            w.Stage = 7; w.FloorPeak = 8; w.RecordPeak = 8; w.Aura = 2;
            var s = Build<EnhanceScreen>(kit);
            s.OnEnter(ScreenArgs.From(ScreenId.WandPicker).With(ScreenArgKeys.WandId, 1L));
            Assert.AreEqual(25 + 30, s.Probability.TotalPct, "제물 없이 기본 + 기운");
            Assert.AreEqual(EnhanceHint.DropTo, s.Probability.Hint);
            Assert.IsFalse(s.Probability.CanAfford, "골드 0");
            s.OnExit();
        }

        // ------------------------------------------------------------------ 떠나보내기

        static WandListEntry Entry(int stage, int mutations = 0, int ext = 0)
        {
            var w = new WandData { Id = 100 + stage * 10 + mutations + ext, Character = 1, Stage = stage, SlotsExtra = ext };
            for (int i = 0; i < mutations; i++) w.Mutations.Add(new MutationEntry { Id = Ids.Mutation.All[i] });
            return new WandListEntry { Wand = w };
        }

        [Test]
        public void S08_TV_146__LastWandCannotBeReleased()
        {
            Assert.AreEqual(ReleaseBlock.LastWand, WandPickerModel.CanRelease(Entry(3), 1));
            Assert.AreEqual(ReleaseBlock.None, WandPickerModel.CanRelease(Entry(3), 2));
            Assert.AreEqual("마지막 한 자루", TestContent.Names.Get(WandPickerModel.ReleaseReasonKey(ReleaseBlock.LastWand)));
            var pending = Entry(3);
            pending.Wand.PendingOffer = new MutationOffer();
            Assert.AreEqual(ReleaseBlock.MutationPending, WandPickerModel.CanRelease(pending, 2));
        }

        [Test]
        public void S08_TV_148__PlusZeroRefundsZero()
        {
            var econ = TestContent.Db.Economy;
            Assert.AreEqual(0, WandPickerModel.RefundOf(econ, new WandData { Stage = 0 }));
            Assert.Greater(WandPickerModel.RefundOf(econ, new WandData { Stage = 5 }), 0);
        }

        [Test]
        public void S08_TV_149__Plus7NeedsExactName()
        {
            var t = EnhanceScreenTuning.From(TestContent.Db);
            var names = TestContent.Names;
            var w7 = new List<WandData> { new WandData { Character = 1, Stage = 7 } };
            Assert.IsTrue(WandReleaseRules.NeedsName(t, w7));
            Assert.IsFalse(WandReleaseRules.CanConfirm(t, w7, "", names), "이름 입력 전 꺼짐");
            Assert.IsTrue(WandReleaseRules.CanConfirm(t, w7, "  " + names.Get("wand.1") + " ", names), "앞뒤 공백 제거 뒤 정확히 같으면 켜짐");
            Assert.IsFalse(WandReleaseRules.CanConfirm(t, w7, names.Get("wand.2"), names));
            var w6 = new List<WandData> { new WandData { Character = 1, Stage = 6 } };
            Assert.IsTrue(WandReleaseRules.CanConfirm(t, w6, null, names));
        }

        [Test]
        public void S08_TV_150__MultiCheckable()
        {
            var m = new WandPickerModel(EnhanceScreenTuning.From(TestContent.Db));
            Assert.IsTrue(m.Checkable(Entry(3), 5));
            Assert.IsFalse(m.Checkable(Entry(4), 5));
            Assert.IsFalse(m.Checkable(Entry(2, 0, 1), 5));
            Assert.IsFalse(m.Checkable(Entry(1, 1, 0), 5));
        }

        [Test]
        public void S08_10_2__MultiCheckBlocksLastInHand()
        {
            var m = new WandPickerModel(EnhanceScreenTuning.From(TestContent.Db));
            var a = Entry(0); a.Wand.Id = 1;
            var b = Entry(1); b.Wand.Id = 2;
            m.Rows.Add(a); m.Rows.Add(b);
            Assert.IsTrue(m.ToggleCheck(a, 2));
            Assert.IsFalse(m.ToggleCheck(b, 2), "고른 묶음이 손의 지팡이를 모두 포함하면 마지막 체크를 막는다");
        }

        [Test]
        public void S08_10_2__FiltersAnd()
        {
            var w = new WandData { Stage = 0 };
            Assert.IsTrue(WandPickerModel.Passes(w, WandFilter.PlusZeroOnly));
            Assert.IsFalse(WandPickerModel.Passes(w, WandFilter.PlusZeroOnly | WandFilter.HasMutation));
            Assert.IsFalse(WandPickerModel.Passes(w, WandFilter.TradeLocked), "솔로: 거래 잠금은 늘 거짓");
            w.SlotsExtra = 1;
            Assert.IsTrue(WandPickerModel.Passes(w, WandFilter.HasExtSlot));
        }

        [Test]
        public void S08_10_2__PickerRowOpensEnhanceWithLongWandId()
        {
            var kit = new G23Kit();
            var s = Build<WandPickerScreen>(kit);
            s.OnEnter(ScreenArgs.From(ScreenId.MainMenu).With(ScreenArgKeys.Character, 2));
            Assert.AreEqual(2, s.Model.Character);
            Assert.AreEqual(1, s.Model.Rows.Count);
            Assert.AreEqual(2L, s.Model.Rows[0].Wand.Id);
        }
    }
}
