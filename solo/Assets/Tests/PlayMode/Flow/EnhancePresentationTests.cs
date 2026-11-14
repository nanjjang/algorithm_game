using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Wizard.Solo.Meta;
using Wizard.Solo.Meta.Enhance;
using Wizard.Solo.Screens.Enhance;
using Wizard.Solo.UI;

namespace Wizard.Solo.Tests.PlayMode.Flow
{
    public sealed class EnhancePresentationTests
    {
        sealed class FixedRoll : IEnhanceRandom
        {
            readonly int _roll;

            public FixedRoll(int roll) { _roll = roll; }

            public int Next(int maxExclusive) => _roll;
        }

        FlowKit _kit;

        [UnityTearDown]
        public IEnumerator StopGame()
        {
            _kit?.Stop();
            yield return null;
        }

        [UnityTest]
        public IEnumerator GreatSuccess_PlaysTwoStageReveals_ThenClearsOnCard()
        {
            var screen = OpenForge(10, 0, 0);
            var chance = FlowKit.Field<UiLabel>(screen, "_bigProb");
            Assert.AreEqual(_kit.Ctx.Names.Format(EnhanceNameKeys.FormatPercent, "n", screen.Probability.TotalPct), chance.FullText);
            AssertNoTruncatedLabels(screen, "forge");
            // 갈무리는 화면이 한 번 그려진 뒤에(바로 찍으면 첫 프레임 전의 빈 화면이 찍힌다).
            yield return _kit.Seconds(0.5f);
            yield return _kit.Shot("01-forge");
            _kit.Click(EnhanceNameKeys.Enhance);

            yield return _kit.WaitUntil(() => Visible("ChargeLight"), "charge sparks");
            yield return _kit.Seconds(0.45f);
            yield return _kit.Shot("02-charge");

            yield return _kit.WaitUntil(() => Visible("ImpactLight"), "great success impact");
            yield return _kit.Seconds(0.25f);
            yield return _kit.Shot("03-great-success");
            // 두 번 내려찍기(+11 → 0.25초 뒤 +12)는 실시간 프레임 사이에 지나갈 수 있어 화면이 남긴 찍기 기록으로 차례를 본다.
            yield return _kit.WaitUntil(() => screen.StageReveals.Contains(11), "first great success stage reveal");
            yield return _kit.WaitUntil(() => StageInHeader(screen, 12), "second great success stage reveal");
            CollectionAssert.AreEqual(new[] { 11, 12 }, screen.StageReveals, "great success reveals +11 then +12");
            yield return _kit.WaitUntil(() => screen.Model.Phase == EnhancePhase.ResultCard, "great success result card");

            Assert.AreEqual(EnhanceOutcome.GreatSuccess, screen.Model.Shown.Outcome);
            Assert.AreEqual(12, screen.Wand.Stage);
            Assert.IsFalse(Visible("ChargeLight"));
            Assert.IsFalse(Visible("ImpactLight"));
            AssertNoTruncatedLabels(screen, "great success result card");
            yield return _kit.Shot("04-great-success-result");
            // 결과 카드 아래 줄: [흔적 공개] [닫기] [다시 시도]. 닫으면 대기로 간다(08 10.5절).
            Assert.IsNotNull(VisibleButton("ResultCard", EnhanceNameKeys.Retry), "result card retry button");
            // 마우스만으로도 닫을 수 있어야 한다(사용자 결정 2026-11-15): [닫기] 단추로 닫는다. K도 같은 동작이다.
            _kit.Activate(VisibleButton("ResultCard", EnhanceNameKeys.CommonClose), "result close");
            Assert.AreEqual(EnhancePhase.Idle, screen.Model.Phase);
        }

        [UnityTest]
        public IEnumerator MaximumStage_HasOwnReveal_AndCanBeSkippedAfterLock()
        {
            var screen = OpenForge(12, 0, 0);
            _kit.Click(EnhanceNameKeys.Enhance);
            yield return _kit.WaitUntil(() => screen.Model.Phase == EnhancePhase.Plus13, "maximum stage ritual");
            yield return _kit.WaitUntil(() => StageInHeader(screen, 13) && screen.Model.PhaseTick >= 67
                && Visible("ImpactLight"), "maximum stage column and stage reveal");
            yield return _kit.Shot("05-maximum-stage");
            yield return _kit.WaitUntil(() => screen.Model.PhaseTick >= 132, "maximum stage skip unlock");
            _kit.Key(MenuKey.Confirm);
            Assert.AreEqual(EnhancePhase.ResultCard, screen.Model.Phase);
            Assert.AreEqual(13, screen.Wand.Stage);
            Assert.IsFalse(Visible("ImpactLight"));
            Assert.IsFalse(Visible("ChargeLight"));
        }

        [UnityTest]
        public IEnumerator FastGreatSuccess_StillRevealsBothSteps()
        {
            var screen = OpenForge(4, 0, 0, fast: true);
            _kit.Click(EnhanceNameKeys.Enhance);
            yield return _kit.WaitUntil(() => screen.Model.Phase == EnhancePhase.ShortResult, "fast result");
            Assert.IsTrue(StageInHeader(screen, 5));
            Assert.IsFalse(Visible("ChargeLight"));
            yield return _kit.WaitUntil(() => StageInHeader(screen, 6), "second fast result stage");
            CollectionAssert.AreEqual(new[] { 5, 6 }, screen.StageReveals, "fast great success reveals +5 then +6");
            yield return _kit.WaitUntil(() => screen.Model.Phase == EnhancePhase.ResultCard, "fast result card");
            Assert.AreEqual(6, screen.Wand.Stage);
        }

        [UnityTest]
        public IEnumerator MinimalEffects_ShowsProtectedResultAndConsumesCharm()
        {
            var screen = OpenForge(7, 9999, 2);
            _kit.Activate(FlowKit.Field<UiButton>(screen, "_charmSlot"), "equip ward");
            _kit.Click(EnhanceNameKeys.Enhance);

            Assert.AreEqual(EnhancePhase.ResultCard, screen.Model.Phase);
            Assert.AreEqual(EnhanceOutcome.FailCharmBlocked, screen.Model.Shown.Outcome);
            Assert.AreEqual(7, screen.Wand.Stage);
            Assert.AreEqual(0, _kit.Ctx.Profile.Items[Ids.Item.Charm].Qty);
            yield return null;
            Assert.IsFalse(Visible("ImpactLight"));
            Assert.IsFalse(Visible("ChargeLight"));
            yield return _kit.Shot("06-ward-result-minimal");
        }

        EnhanceScreen OpenForge(int stage, int roll, int effectsLevel, bool fast = false)
        {
            _kit = new FlowKit();
            _kit.Start("enhancement-presentation");
            var ctx = _kit.Ctx;
            ctx.Enhance = new EnhanceService(ctx, new FixedRoll(roll));
            SettingsOptions.SetEffectsLevel(ctx.Settings.Current, effectsLevel);
            SettingsOptions.Set(ctx.Settings.Current, OptionKeys.EnhanceRitual, fast ? OptionValues.Fast : OptionValues.Full);
            var wand = ctx.Profile.Wand(ctx.Profile.Character(1).EquippedWandId);
            wand.Stage = wand.RecordPeak = wand.FloorPeak = stage;
            ctx.Profile.Currencies.Gold = 500000;
            foreach (string item in new[] { Ids.Item.StoneLow, Ids.Item.StoneMid, Ids.Item.StoneHigh })
                ctx.Profile.Items[item] = new ItemStack { Qty = 50 };
            ctx.Profile.Items[Ids.Item.Charm] = new ItemStack { Qty = 1 };
            _kit.Screens.Push(ScreenId.Enhance, ScreenArgs.From(ScreenId.MainMenu).With(ScreenArgKeys.WandId, wand.Id));
            return (EnhanceScreen)_kit.TopScreen;
        }

        bool StageInHeader(EnhanceScreen screen, int stage)
        {
            var label = FlowKit.Field<UiLabel>(screen, "_stageDigits");
            return label.Text.text == EnhanceNameKeys.Stage(_kit.Ctx.Names, stage);
        }

        /// <summary>강화 화면 안에서 켜져 있는 한 줄 글자가 "…"로 잘리지 않았나(08 1.4절).</summary>
        static void AssertNoTruncatedLabels(EnhanceScreen screen, string where)
        {
            var content = FlowKit.Field<UiLabel>(screen, "_bigProb").transform.parent.parent;
            var cut = new System.Collections.Generic.List<string>();
            foreach (var label in content.GetComponentsInChildren<UiLabel>(false))
                if (label.Truncated) cut.Add(label.FullText);
            Assert.IsEmpty(cut, where + ": truncated labels: " + string.Join(" | ", cut));
        }

        UiButton VisibleButton(string root, string nameKey)
        {
            foreach (var button in GameObject.Find(root).GetComponentsInChildren<UiButton>())
                if (button.LabelText != null && button.LabelText.text == _kit.T(nameKey)) return button;
            return null;
        }

        static bool Visible(string name) => GameObject.Find(name) != null;
    }
}
