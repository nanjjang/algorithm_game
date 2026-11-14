using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Wizard.Solo.Meta;
using Wizard.Solo.Screens.Enhance;
using Wizard.Solo.Screens.Prep;
using Wizard.Solo.Screens.Settings;
using Wizard.Solo.Screens.Start;
using Wizard.Solo.Sim;
using Wizard.Solo.UI;

namespace Wizard.Solo.Tests.PlayMode.Flow
{
    /// <summary>
    /// 처음부터 끝까지(08 요약서 2절, S13 0.4 "처음 실행 → 판 → 정산 → 강화 → 다시 실행"): 깨끗한 프로필에서 첫 실행 안내 → 타이틀 →
    /// 메인 메뉴 → 원정 준비 → 튜토리얼 → 웨이브 1 → 전리품 → 야영 → 웨이브 2 → 포기 → 정산 → 강화 → 메뉴 화면들.
    /// 단계마다 Logs/shots/flow/main/에 스크린숏을 남긴다. 오류 로그·예외는 테스트 러너가 실패로 잡는다.
    /// </summary>
    public class FullFlowTests
    {
        FlowKit _k;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _k = new FlowKit();
            _k.Start("main");
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_k != null)
            {
                FlowKit.Log("shots: " + string.Join(", ", _k.Shots));
                FlowKit.Log("warnings(" + _k.Warnings.Count + "): " + string.Join(" | ", _k.Warnings));
                _k.Stop();
            }
            yield return null;
        }

        internal static UiWidget NamedWidget(string goName) => Named(goName);

        static UiWidget Named(string goName)
        {
            foreach (var w in Object.FindObjectsByType<UiWidget>(FindObjectsSortMode.None))
                if (w != null && w.isActiveAndEnabled && w.name == goName) return w;
            return null;
        }

        [UnityTest]
        public IEnumerator FirstRun_To_Enhance_And_MenuScreens()
        {
            var k = _k;
            Assert.IsTrue(k.Boot.FirstRun, "깨끗한 데이터 폴더 → 첫 실행");

            // ---- 첫 실행 안내 → 타이틀 → 오프라인 안내 → 메인 메뉴
            yield return k.WaitTop(ScreenId.FirstRunPrompt);
            yield return k.Seconds(0.5f);
            yield return k.Shot("01-first-run-prompt");
            k.Key(MenuKey.Confirm);
            yield return k.WaitTop(ScreenId.Title);
            yield return k.Seconds(0.5f);
            yield return k.Shot("02-title");
            k.Key(MenuKey.Confirm);
            yield return k.WaitTop(ScreenId.OfflineGuide);
            yield return k.Seconds(0.3f);
            yield return k.Shot("03-offline-guide");
            k.Click(StartNameKeys.CommonOk);
            yield return k.WaitTop(ScreenId.MainMenu);
            Assert.IsTrue(k.Ctx.Profile.Flags.OfflineGuideSeen, "오프라인 안내는 한 번 보면 표시가 남는다");
            yield return k.Seconds(0.5f);
            yield return k.Shot("04-main-menu");

            // ---- 원정 준비: 캐릭터, 지팡이, 출발 조립, 출발
            k.Activate(FlowKit.Field<UiButton>(k.TopScreen, "_expedition"), "원정 카드");
            yield return k.WaitTop(ScreenId.ExpeditionPrep);
            k.Activate(Named("Card2"), "캐릭터 2");
            yield return k.Frames(3);
            k.Activate(Named("Card1"), "캐릭터 1");
            yield return k.Seconds(0.3f);
            yield return k.Shot("05-expedition-prep");
            k.Click(PrepKeys.ChangeLoadout);
            yield return k.WaitTop(ScreenId.Camp);
            yield return k.Seconds(0.3f);
            yield return k.Shot("06-prep-loadout-edit");
            k.Key(MenuKey.Back);
            yield return k.WaitTop(ScreenId.ExpeditionPrep);
            k.Click(PrepKeys.Depart);

            // ---- 튜토리얼(첫 판 전 허수아비 방, 08 6.4절)
            yield return k.WaitTop(ScreenId.Tutorial);
            yield return k.WaitUntil(() => k.World != null && k.World.Run.Phase == RunPhase.Practice, "튜토리얼 방");
            yield return k.Seconds(0.5f);
            yield return k.Shot("07-tutorial-step1");
            yield return Tutorial(k);

            // ---- 진짜 판(원정): 웨이브 1
            yield return k.WaitTop(ScreenId.Battle, 20f);
            yield return k.WaitUntil(() => k.World != null && k.World.Run.Config.Mode == RunMode.Endless && k.World.Run.Phase == RunPhase.WaveCombat, "웨이브 1 전투");
            Assert.IsTrue(k.Ctx.Profile.Flags.TutorialDummyDone, "튜토리얼을 마치면 표시가 남는다");
            k.Events.Clear();
            yield return k.Fight(() => k.World.Tick >= k.World.Run.PhaseStartTick + 240, "웨이브 1 4초", 30f, god: true, useArts: true);
            yield return k.Shot("08-wave1-combat");
            yield return k.Fight(() => k.Screens.Top == ScreenId.LootPick, "웨이브 1 클리어", 120f, god: true, useArts: true);
            FlowKit.Log("wave1: shots " + k.Count(SimEventType.ShotFired) + ", kills " + k.Count(SimEventType.EnemyKilled)
                        + ", arts " + k.Count(SimEventType.ArtFired) + "/" + k.Count(SimEventType.ArtFizzled) + ", locks " + k.Count(SimEventType.HeatLockEntered)
                        + ", drops " + k.Count(SimEventType.DropSpawned) + "/" + k.Count(SimEventType.DropPicked));
            Assert.Greater(k.Count(SimEventType.ShotFired), 0, "J 발사");
            Assert.Greater(k.Count(SimEventType.EnemyKilled), 0, "적 처치");
            Assert.Greater(k.Count(SimEventType.ArtFired) + k.Count(SimEventType.ArtFizzled), 0, "K 비기(발동 또는 불발)");
            Assert.Greater(k.Count(SimEventType.WaveCleared), 0, "웨이브 클리어");

            // ---- 전리품 고르기
            yield return k.Seconds(1.0f);
            yield return k.Shot("09-loot-pick");
            var loot = k.World.Run.Loot;
            Assert.IsNotNull(loot);
            var cards = FlowKit.Field<List<UiButton>>(k.TopScreen, "_cardButtons");
            for (int i = 0; i < loot.PickCount && i < cards.Count; i++) { k.Activate(cards[i], "전리품 카드 " + i); yield return k.Frames(2); }
            yield return k.Shot("10-loot-picked");
            k.Click("str.loot_pick.confirm");
            yield return k.WaitUntil(() => k.World.Run.Phase == RunPhase.Camp, "전리품 뒤 야영");
            yield return k.WaitTop(ScreenId.Battle);
            Assert.Greater(k.World.Run.Storage.Count, 0, "고른 블록이 보관함에 들어간다");

            // ---- 야영: 전장 모닥불 옆에서 J → 야영 화면
            yield return k.Seconds(0.5f);
            yield return k.Shot("11-camp-field");
            yield return CampFireOpen(k);
            yield return k.Seconds(0.3f);
            yield return k.Shot("12-camp-screen");
            // 웨이브 1을 넘기면 비기 해금 차례가 온다(04 11.3절): 야영 화면이 비기 탭의 고르기를 먼저 띄운다 → 첫 카드를 고른다.
            if (Named("PickCard0") != null)
            {
                k.Activate(Named("PickCard0"), "비기 해금 카드 0");
                yield return k.WaitUntil(() => k.World.Run.CurrentArtOffer == null, "비기 해금 고르기 끝");
                yield return k.Seconds(0.3f);
                yield return k.Shot("12b-camp-art-picked");
            }
            k.Click("str.camp.tab.assembly");
            yield return k.Frames(3);
            var camp = k.TopScreen;
            var refs = FlowKit.Field<Dictionary<UiButton, int>>(camp, "_cellRefs");
            UiButton storage0 = null;
            foreach (var kv in refs) if (kv.Value == 100) storage0 = kv.Key;
            int linksBefore = Filled(k.World.P0.Loadout);
            k.Activate(storage0, "보관함 첫 칸(J = 끼우기)");
            yield return k.WaitUntil(() => Filled(k.World.P0.Loadout) > linksBefore, "블록이 지팡이 칸에 들어간다");
            yield return k.Seconds(0.3f);
            yield return k.Shot("13-camp-inserted");
            int hpBefore = k.World.P0.Hp;
            if (k.World.P0.Hp < k.World.P0.MaxHp && k.World.Run.Purse >= k.World.Mods.Run.HealCost(k.World)) k.Click("str.camp.heal");
            else FlowKit.Log("heal skipped: hp " + k.World.P0.Hp + "/" + k.World.P0.MaxHp + ", purse " + k.World.Run.Purse);
            yield return k.Frames(3);
            k.Click("str.camp.depart");

            // ---- 웨이브 2 → 포기
            yield return k.WaitUntil(() => k.World.Run.Phase == RunPhase.WaveCombat && k.World.Wave.Wave == 2, "웨이브 2 전투");
            yield return k.WaitTop(ScreenId.Battle);
            yield return k.Fight(() => k.World.Tick >= k.World.Run.PhaseStartTick + 300, "웨이브 2 5초", 30f, god: false, useArts: false);
            yield return k.Shot("14-wave2-combat");
            k.Key(MenuKey.Back);
            yield return k.WaitTop(ScreenId.Pause);
            yield return k.Seconds(0.3f);
            yield return k.Shot("15-pause");
            k.Click("str.pause.give_up");
            yield return k.WaitTop(ScreenId.ConfirmDialog);
            yield return k.Shot("16-give-up-confirm");
            k.Click(UiNameKeys.CommonYes);

            // ---- 정산 1·2·3쪽 → 메인 메뉴
            yield return k.WaitTop(ScreenId.Settlement, 20f);
            yield return k.Seconds(1.0f);
            yield return k.Shot("17-settlement-1");
            k.Click("str.common.next");
            yield return k.Seconds(0.5f);
            yield return k.Shot("18-settlement-2");
            k.Click("str.common.next");
            yield return k.Seconds(1.0f);
            yield return k.Shot("19-settlement-3");
            Assert.AreEqual(1, k.Ctx.Profile.RunHistory.Count, "정산하면 판 목록에 한 줄");
            k.Click(Wizard.Solo.Screens.Settlement.SettlementNameKeys.ToMenu);
            yield return k.WaitTop(ScreenId.MainMenu);
            Assert.IsFalse(k.Session.HasRun, "정산 뒤 판이 닫힌다");

            // ---- 강화: 지팡이 고르기 → 강화 몇 번 → 결과
            yield return k.Seconds(0.3f);
            yield return k.Shot("20-main-menu-after-run");
            // 한 판(웨이브 1)의 보상으로는 강화 비용이 모자란다(골드·하급 강화석) → 시험용으로 프로필에 넣고 저장한다(규칙은 그대로).
            FlowKit.Log("after run: gold " + k.Ctx.Profile.Currencies.Gold + ", stone_low " + k.Ctx.Profile.Items[Ids.Item.StoneLow].Qty);
            k.Ctx.Profile.Currencies.Gold += 20000;
            k.Ctx.Profile.Items[Ids.Item.StoneLow].Qty += 10;
            k.Ctx.Profiles.Save();
            yield return k.Frames(2);
            k.Click(StartNameKeys.MainMenuEnhance);
            yield return k.WaitUntil(() => k.Screens.Top == ScreenId.WandPicker || k.Screens.Top == ScreenId.EnhanceTutorial, "지팡이 고르기");
            if (k.Screens.Top == ScreenId.EnhanceTutorial)
            {
                yield return k.Seconds(0.3f);
                yield return k.Shot("21-enhance-tutorial");
                k.Key(MenuKey.Confirm);
                yield return k.WaitTop(ScreenId.WandPicker);
            }
            yield return k.Seconds(0.3f);
            yield return k.Shot("22-wand-picker");
            k.Key(MenuKey.Confirm);
            yield return k.WaitTop(ScreenId.Enhance);
            yield return k.Seconds(1.0f);
            yield return k.Shot("23-enhance");
            var p = k.Ctx.Profile;
            long wandId = p.Character(1).EquippedWandId;
            int stoneLow = p.Items[Ids.Item.StoneLow].Qty;
            FlowKit.Log("enhance resources: gold " + p.Currencies.Gold + ", stone_low " + stoneLow);
            int stageBefore = p.Wand(wandId).Stage;
            int tries = 0;
            for (int n = 0; n < 3; n++)
            {
                var b = k.Ctx.Enhance.Preview(wandId, new string[0], false);
                if (b.Blocked != ErrorCode.None) { FlowKit.Log("enhance blocked: " + b.Blocked); break; }
                k.Click(EnhanceNameKeys.Enhance);
                tries++;
                var es = (EnhanceScreen)k.TopScreen;
                yield return k.Seconds(0.6f);
                if (n == 0) yield return k.Shot("24-enhance-ritual");
                yield return EnhanceToIdle(k, es, n == 0);
            }
            FlowKit.Log("enhance tries " + tries + ", stage " + stageBefore + " → " + p.Wand(wandId).Stage);
            Assert.Greater(tries, 0, "강화를 한 번 이상 해야 한다(재료 부족이면 정산 보상을 확인)");
            yield return k.Shot("26-enhance-after");
            // 강화 화면의 K는 제물 하나 빼기다. 나가기는 Esc(08 10.1절: Esc → 들어온 곳).
            k.Key(MenuKey.Back);
            yield return k.WaitUntil(() => k.Screens.Top == ScreenId.MainMenu || k.Screens.Top == ScreenId.WandPicker, "강화 화면 닫기");
            if (k.Screens.Top == ScreenId.WandPicker) { k.Key(MenuKey.Back); yield return k.WaitTop(ScreenId.MainMenu); }

            // ---- 메뉴 화면들 열고 닫기
            yield return OpenClose(k, StartNameKeys.MainMenuAssemblyLibrary, ScreenId.Camp, "27-library");
            yield return OpenClose(k, StartNameKeys.MainMenuRecords, ScreenId.Records, "28-records");
            yield return OpenClose(k, StartNameKeys.MainMenuShop, ScreenId.Shop, "29-shop");
            // 설정 → 조작 분류 → 키 재설정
            k.Click(StartNameKeys.MainMenuSettings);
            yield return k.WaitTop(ScreenId.Settings);
            yield return k.Seconds(0.3f);
            yield return k.Shot("30-settings");
            for (int i = 0; i < 6 && !k.HasButton(SettingsKeys.OptKeyRebind); i++) { k.Key(MenuKey.NextTab); yield return k.Frames(3); }
            yield return k.Shot("31-settings-controls");
            k.Click(SettingsKeys.OptKeyRebind);
            yield return k.WaitTop(ScreenId.KeyRebind);
            yield return k.Seconds(0.3f);
            yield return k.Shot("32-key-rebind");
            k.Key(MenuKey.Back);
            yield return k.WaitTop(ScreenId.Settings);
            k.Key(MenuKey.Back);
            yield return k.WaitTop(ScreenId.MainMenu);

            FlowKit.Log("errors: " + k.Errors.Count);
            Assert.AreEqual(0, k.Errors.Count, string.Join("\n", k.Errors));
        }

        static int Filled(RunLoadout lo)
        {
            int n = lo.Core.IsEmpty ? 0 : 1;
            foreach (var s in lo.Links) if (!s.IsEmpty) n++;
            return n;
        }

        static IEnumerator OpenClose(FlowKit k, string itemKey, ScreenId id, string shot)
        {
            k.Click(itemKey);
            yield return k.WaitTop(id);
            yield return k.Seconds(0.4f);
            yield return k.Shot(shot);
            k.Key(MenuKey.Back);
            yield return k.WaitTop(ScreenId.MainMenu);
        }

        /// <summary>전장의 모닥불로 걸어가 J(누르는 순간)로 야영 화면을 연다.</summary>
        static IEnumerator CampFireOpen(FlowKit k)
        {
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
        }

        /// <summary>강화 의식을 결과 카드까지 보고 닫는다. 변이 제시가 오면 받는다.</summary>
        static IEnumerator EnhanceToIdle(FlowKit k, EnhanceScreen es, bool shot)
        {
            float end = Time.realtimeSinceStartup + 45f;
            bool shotTaken = false;
            while (es.Model.Phase != EnhancePhase.Idle)
            {
                Assert.Less(Time.realtimeSinceStartup, end, "강화 연출이 끝나야 한다(지금 " + es.Model.Phase + ")");
                if (es.Model.Phase == EnhancePhase.ResultCard || es.Model.Phase == EnhancePhase.ShortResult)
                {
                    if (shot && !shotTaken) { shotTaken = true; yield return k.Seconds(0.3f); yield return k.Shot("25-enhance-result"); }
                    // 결과 카드: J = 다시 강화(08 10.7절), K = 닫기.
                    k.Key(MenuKey.Cancel);
                    yield return k.Seconds(0.2f);
                    continue;
                }
                if (es.Model.Phase == EnhancePhase.MutationChoice) { k.Click(EnhanceNameKeys.MutationAccept); yield return k.Seconds(0.2f); continue; }
                yield return null;
            }
        }

        /// <summary>튜토리얼 다섯 단계를 실제 입력으로 마친다(08 6.4절 완료 조건).</summary>
        static IEnumerator Tutorial(FlowKit k)
        {
            float end = Time.realtimeSinceStartup + 90f;
            int lastStep = -1;
            int phaseTick = 0;
            bool campShot = false;
            while (k.Screens.Top == ScreenId.Tutorial || k.Screens.Top == ScreenId.Camp || k.Screens.Top == ScreenId.ConfirmDialog)
            {
                Assert.Less(Time.realtimeSinceStartup, end, "튜토리얼이 끝나야 한다(단계 " + lastStep + ")");
                var w = k.World;
                if (w == null || w.Run == null || w.Run.Config.Mode != RunMode.Tutorial) { yield return null; continue; }
                int step = w.Run.TutorialStep;
                if (step != lastStep)
                {
                    FlowKit.Log("tutorial step " + step);
                    k.ReleaseAll();
                    lastStep = step;
                    phaseTick = w.Tick;
                    yield return k.Frames(2);
                    if (step >= 2 && step <= 5) yield return k.Shot("07-tutorial-step" + step);
                    continue;
                }
                int t = w.Tick - phaseTick;
                switch (step)
                {
                    case 1: // 1칸 이상 이동 + 발사 1회
                        k.SetKey(LogicalKey.Right, t < 40);
                        k.SetKey(LogicalKey.Fire, t >= 45 && t < 50);
                        break;
                    case 2: // 자세(Shift)를 누른 채 1칸 이동
                        k.SetKey(LogicalKey.Stance, true);
                        k.SetKey(LogicalKey.Left, t > 4 && t < 60);
                        break;
                    case 3: // 임계 사격 1회: 쏘다 보면 열이 임계에 든다
                        k.SetKey(LogicalKey.Fire, true);
                        break;
                    case 4: // 반동 사격: 오른쪽을 보고 → 자세 → 뒤(왼쪽) → K
                    {
                        int c = t % 60;
                        k.SetKey(LogicalKey.Right, c < 4);
                        k.SetKey(LogicalKey.Stance, c >= 6 && c < 30);
                        k.SetKey(LogicalKey.Left, c >= 10 && c < 13);
                        k.SetKey(LogicalKey.Skill, c >= 14 && c < 17);
                        break;
                    }
                    case 5: // 블록 끼우기: [조립 열기](J) → 야영(튜토리얼 모드) → 보관함 칸 J
                        if (k.Screens.Top == ScreenId.Tutorial && t > 30 && (t % 30) == 0) k.Key(MenuKey.Confirm);
                        if (k.Screens.Top == ScreenId.Camp)
                        {
                            if (!campShot) { campShot = true; yield return k.Seconds(0.3f); yield return k.Shot("07-tutorial-step5-camp"); }
                            var refs = FlowKit.Field<Dictionary<UiButton, int>>(k.TopScreen, "_cellRefs");
                            foreach (var kv in refs) if (kv.Value == 100) { k.Activate(kv.Key, "튜토리얼 보관함 칸"); break; }
                            yield return k.Seconds(0.5f);
                        }
                        break;
                }
                yield return null;
            }
            k.ReleaseAll();
            FlowKit.Log("tutorial done, top " + k.Screens.Top);
        }
    }
}
