using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Wizard.Solo.Sim;
using Wizard.Solo.UI;

namespace Wizard.Solo.Tests.PlayMode.Flow
{
    /// <summary>
    /// 전리품 개편 스크린숏(Logs/shots/flow/loot): 웨이브 1 클리어 → 전리품(설명·맞물림·미리보기·건너뛰기) → 별 겹치기 → 전장 HUD 회로 ★ → 야영 화면 회로 ★.
    /// 시험 전용 진입 길은 BattleShotsTests와 같다(진짜 WandService의 RunConfig → Loading → Battle). 별 겹치기를 확실히 보이려고
    /// 첫 카드만 출발 조립의 코어와 같은 블록으로 바꾼다(제시 상태를 시험이 고친다 — 규칙은 그대로 Run이 적용한다).
    /// </summary>
    public class LootCampShotsTests
    {
        FlowKit _k;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_k != null)
            {
                FlowKit.Log("shots: " + string.Join(", ", _k.Shots));
                _k.Stop();
                _k = null;
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator Loot_Star_Camp()
        {
            _k = new FlowKit();
            _k.Start("loot");
            yield return null;
            var k = _k;
            var ctx = k.Ctx;
            ctx.Profile.Flags.TutorialDummyDone = true;
            ctx.Profile.Flags.OfflineGuideSeen = true;
            ctx.Profiles.Save();
            k.Screens.Replace(ScreenId.MainMenu);
            yield return k.WaitTop(ScreenId.MainMenu);
            var r = ctx.Wands.BuildRunConfig(1, ctx.Profile.Character(1).EquippedWandId, 1, 2024UL);
            Assert.IsTrue(r.Ok, "BuildRunConfig: " + r.Error);
            var battleArgs = ScreenArgs.From(ScreenId.MainMenu).With(ScreenArgKeys.RunConfig, r.Value);
            k.Screens.Push(ScreenId.Loading, ScreenArgs.ViaLoading(ScreenId.MainMenu, ScreenId.Battle, battleArgs));
            yield return k.WaitTop(ScreenId.Battle, 20f);
            yield return k.WaitUntil(() => k.World != null && k.World.Run.Phase == RunPhase.WaveCombat, "웨이브 1 전투");
            yield return k.Fight(() => k.Screens.Top == ScreenId.LootPick, "웨이브 1 클리어", 120f, god: true, useArts: true);
            yield return k.Seconds(1.2f);
            yield return k.Shot("09-loot-pick");

            var loot = k.World.Run.Loot;
            Assert.IsNotNull(loot);
            for (int i = 0; i < loot.Cards.Count; i++)
                for (int j = i + 1; j < loot.Cards.Count; j++)
                    Assert.AreNotEqual(loot.Cards[i].BlockId, loot.Cards[j].BlockId, "한 제시 안 중복 없음");

            // 별 겹치기를 보이려고 첫 카드를 코어 블록으로(StarTarget도 Run이 정하는 값과 같게 1).
            string coreId = k.World.P0.Loadout.Core.BlockId;
            loot.Cards[0].BlockId = coreId;
            loot.Cards[0].Rarity = k.Ctx.Content.Block(coreId).Rarity;
            loot.Cards[0].StarTarget = k.World.P0.Loadout.Core.Stars + 1;
            k.World.Run.Purse += 1;   // 화면 서명을 바꿔 다시 그리게 한다
            yield return k.Seconds(1.0f);
            var cards = FlowKit.Field<System.Collections.Generic.List<UiButton>>(k.TopScreen, "_cardButtons");
            for (int i = 0; i < loot.PickCount && i < cards.Count; i++) { k.Activate(cards[i], "전리품 카드 " + i); yield return k.Frames(2); }
            yield return k.Shot("10-loot-picked-star");
            k.Click("str.loot_pick.confirm");
            yield return k.WaitUntil(() => k.World.Run.Phase == RunPhase.Camp, "전리품 뒤 야영");
            Assert.AreEqual(1, k.World.P0.Loadout.Core.Stars, "코어가 ★1로 자란다");
            yield return k.WaitTop(ScreenId.Battle);
            yield return k.Seconds(0.6f);
            yield return k.Shot("11-camp-field-hud-star");

            float end = Time.realtimeSinceStartup + 20f;
            while (k.Screens.Top != ScreenId.Camp)
            {
                Assert.Less(Time.realtimeSinceStartup, end, "모닥불 옆 J로 야영 화면이 열려야 한다");
                var w = k.World;
                bool near = k.WalkTo(w.Run.CampFirePos, w.Run.CampInteractRadius * 0.6f);
                if (near) k.SetKey(LogicalKey.Fire, (Time.frameCount & 7) < 4);
                yield return null;
            }
            k.ReleaseAll();
            yield return k.Seconds(0.4f);
            yield return k.Shot("12-camp-screen");
            Assert.AreEqual(0, k.Errors.Count, string.Join("\n", k.Errors));
        }
    }
}
