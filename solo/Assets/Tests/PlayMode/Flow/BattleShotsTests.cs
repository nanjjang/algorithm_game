using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Wizard.Solo.Sim;
using Wizard.Solo.UI;

namespace Wizard.Solo.Tests.PlayMode.Flow
{
    /// <summary>
    /// 전투 스크린숏: 다섯 캐릭터의 웨이브 1, 웨이브 10 보스(가마등) 처치, 웨이브 10 허상 첫 만남.
    /// 원정 준비를 거치지 않는 시험 전용 진입 길: 진짜 WandService가 만든 RunConfig를 Loading → Battle 화면으로 넘긴다(출발 버튼이 하는 일과 같다).
    /// 규칙은 바꾸지 않는다. 시간을 줄이려고 보스 체력만 깎을 때가 있고(처치는 진짜 탄), 그때는 주석에 적는다.
    /// </summary>
    public class BattleShotsTests
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

        /// <summary>새로 부팅하고 첫 실행·튜토리얼 표시를 끝낸 프로필로 판을 연다.</summary>
        IEnumerator Launch(string dir, int character, int startWave, bool phantomFirst, ulong seed)
        {
            if (_k != null) { _k.Stop(); yield return null; }
            _k = new FlowKit();
            _k.Start(dir);
            yield return null;
            var ctx = _k.Ctx;
            ctx.Profile.Flags.TutorialDummyDone = true;
            ctx.Profile.Flags.OfflineGuideSeen = true;
            ctx.Profiles.Save();
            _k.Screens.Replace(ScreenId.MainMenu);
            yield return _k.WaitTop(ScreenId.MainMenu);
            var r = ctx.Wands.BuildRunConfig(character, ctx.Profile.Character(character).EquippedWandId, 1, seed);
            Assert.IsTrue(r.Ok, "BuildRunConfig: " + r.Error);
            var cfg = r.Value;
            cfg.StartWave = startWave;
            cfg.PhantomFirst = phantomFirst;
            var battleArgs = ScreenArgs.From(ScreenId.MainMenu).With(ScreenArgKeys.RunConfig, cfg);
            _k.Screens.Push(ScreenId.Loading, ScreenArgs.ViaLoading(ScreenId.MainMenu, ScreenId.Battle, battleArgs));
            yield return _k.WaitTop(ScreenId.Battle, 20f);
            yield return _k.WaitUntil(() => _k.World != null && _k.World.Run.Phase != RunPhase.NotStarted, "판 시작");
        }

        [UnityTest]
        public IEnumerator FiveCharacters_Wave1()
        {
            for (int c = 1; c <= Ids.Char.Count; c++)
            {
                yield return Launch("characters", c, 1, false, (ulong)(40 + c));
                var k = _k;
                yield return k.WaitUntil(() => k.World.Run.Phase == RunPhase.WaveCombat, "웨이브 1 전투");
                yield return k.Fight(() => k.World.Tick >= k.World.Run.PhaseStartTick + 300, "웨이브 1 5초", 30f, god: true, useArts: true);
                Assert.AreEqual(Ids.Char.Of(c), k.World.P0.CharId);
                yield return k.Shot("char" + c + "-wave1");
                Assert.Greater(k.Count(SimEventType.ShotFired), 0, "char." + c + " 발사");
                Assert.AreEqual(0, k.Errors.Count, string.Join("\n", k.Errors));
            }
        }

        /// <summary>출발 야영(StartCamp)에서 출발 표식 위 J 1초로 떠난다(06 4.3절).</summary>
        IEnumerator DepartFromStartCamp()
        {
            var k = _k;
            Assert.AreEqual(RunPhase.StartCamp, k.World.Run.Phase, "시작 웨이브 10 → 출발 야영");
            yield return k.Seconds(0.3f);
            float end = Time.realtimeSinceStartup + 20f;
            while (k.World.Run.Phase == RunPhase.StartCamp)
            {
                Assert.Less(Time.realtimeSinceStartup, end, "출발 표식에서 J를 1초 누르면 떠나야 한다");
                if (k.Screens.Top == ScreenId.Camp)
                {
                    // 남은 비기 해금 차례가 있으면 출발 대신 야영 화면(비기 고르기)이 열린다(06 4.3절) → 고르고 [출발].
                    k.ReleaseAll();
                    var card = FullFlowTests.NamedWidget("PickCard0");
                    if (card != null) { k.Activate(card, "비기 해금 카드 0"); yield return k.Seconds(0.3f); continue; }
                    if (k.World.Run.CurrentArtOffer == null) { k.Click("str.camp.depart"); yield return k.Frames(3); continue; }
                    yield return null;
                    continue;
                }
                if (k.Screens.Top != ScreenId.Battle) { yield return null; continue; }
                var w = k.World;
                if ((Time.frameCount % 60) == 0)
                    FlowKit.Log("startcamp: pos " + w.P0.Pos + ", marker " + w.Run.DepartMarkerPos + " r " + w.Run.DepartMarkerRadius + ", sub " + w.Run.CampSub
                                + ", hold " + w.Run.DepartHoldStartTick + ", top " + k.Screens.Top + ", input " + k.Session.InputScreen + ", fire " + w.P0.Keys.IsDown(LogicalKey.Fire) + ", bot phys " + k.Bot.IsPhysicallyDown(LogicalKey.Fire) + " down " + k.Bot.IsDown(LogicalKey.Fire) + " rem " + k.Bot.Remaining);
                bool on = k.WalkTo(w.Run.DepartMarkerPos, w.Run.DepartMarkerRadius * 0.5f);
                k.SetKey(LogicalKey.Fire, on);
                yield return null;
            }
            k.ReleaseAll();
        }

        static EnemyState BossBody(SimWorld w) => w.Boss != null ? w.FindEnemy(w.Boss.EnemyId) : null;

        [UnityTest]
        public IEnumerator Wave10_KilnBoar_Kill()
        {
            yield return Launch("boss", 1, 10, false, 77UL);
            var k = _k;
            yield return k.Shot("boss-00-start-camp");
            yield return DepartFromStartCamp();
            yield return k.WaitUntil(() => k.World.Boss != null, "보스 등장");
            Assert.AreEqual(Ids.Boss.KilnBoar, k.World.Boss.Def.Id);
            yield return k.Seconds(0.6f);
            yield return k.Shot("boss-01-intro");
            yield return k.Fight(() => k.World.Run.Phase == RunPhase.WaveCombat && k.World.Tick >= k.World.Run.PhaseStartTick + 300, "보스 5초", 40f, god: true, useArts: true);
            yield return k.Shot("boss-02-fight");
            // 예고 구역이 보이는 순간을 한 장 더(돌진·불씨 등). 3초 안에 없으면 넘어간다.
            float until = Time.realtimeSinceStartup + 6f;
            while (k.World.Zones.Count == 0 && Time.realtimeSinceStartup < until)
            {
                k.World.P0.InvulnUntilTick = k.World.Tick + 10;
                k.CombatFrame(false);
                yield return null;
            }
            yield return k.Shot("boss-03-telegraph");
            // 시간을 줄이려고 체력만 깎는다. 처치는 진짜 탄이 한다.
            var body = BossBody(k.World);
            if (body != null) body.Hp = Mathf.Min(body.Hp, 120f);
            yield return k.Fight(() => k.World.Run.Phase == RunPhase.WaveClearing, "보스 처치", 60f, god: true, useArts: false);
            Assert.Greater(k.Count(SimEventType.BossKilled), 0);
            yield return k.Seconds(0.5f);
            yield return k.Shot("boss-04-killed");
            yield return k.WaitTop(ScreenId.LootPick, 20f);
            yield return k.Seconds(0.8f);
            yield return k.Shot("boss-05-loot");
            Assert.AreEqual(0, k.Errors.Count, string.Join("\n", k.Errors));
        }

        [UnityTest]
        public IEnumerator Wave10_PhantomFirstEncounter()
        {
            yield return Launch("phantom", 1, 10, true, 78UL);
            var k = _k;
            yield return DepartFromStartCamp();
            yield return k.WaitUntil(() => k.World.Boss != null, "허상 등장");
            Assert.AreEqual(Ids.Boss.MirrorPhantom, k.World.Boss.Def.Id);
            yield return k.Seconds(0.6f);
            yield return k.Shot("phantom-01-intro");
            yield return k.Fight(() => k.World.Run.Phase == RunPhase.WaveCombat && k.World.Tick >= k.World.Run.PhaseStartTick + 300, "허상 5초", 40f, god: true, useArts: true);
            yield return k.Shot("phantom-02-fight");
            // 시간을 줄이려고 체력을 바닥(50%) 바로 위로 깎는다. 바닥 닿음·되비침·쓰러짐은 진짜 탄과 규칙이 한다.
            var body = BossBody(k.World);
            if (body != null && body.HpFloor > 0f) body.Hp = body.HpFloor + 25f;
            yield return k.Fight(() => k.World.Run.Phase == RunPhase.PhantomFinale, "되비침", 60f, god: true, useArts: false);
            yield return k.Seconds(1.0f);
            yield return k.Shot("phantom-03-reflection");
            yield return k.WaitUntil(() => k.World.Run.Phase == RunPhase.PhantomDowned, "쓰러짐", 30f);
            yield return k.Seconds(1.0f);
            yield return k.Shot("phantom-04-downed");
            // 일어난 뒤: 클리어 → 전리품 → 새벽 야영(06 4.1절).
            yield return k.WaitUntil(() => k.Screens.Top == ScreenId.LootPick || k.World.InCamp, "일어난 뒤 전리품 또는 야영", 40f);
            yield return k.Seconds(0.8f);
            yield return k.Shot("phantom-05-after-rise");
            Assert.AreEqual(k.World.P0.MaxHp, k.World.P0.Hp, "일어나면 체력 가득");
            Assert.IsTrue(k.World.Run.PhantomFirstDone);
            Assert.AreNotEqual(RunPhase.RunEnded, k.World.Run.Phase, "첫 만남에서는 판이 끝나지 않는다");
            Assert.AreEqual(0, k.Errors.Count, string.Join("\n", k.Errors));
        }
    }
}
