using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Wizard.Solo.Screens.Prep;
using Wizard.Solo.Screens.Start;
using Wizard.Solo.Sim;
using Wizard.Solo.Sim.Run;
using Wizard.Solo.UI;

namespace Wizard.Solo.Tests.PlayMode.Flow
{
    /// <summary>
    /// 사람 QA 대용: 튜토리얼을 건너뛰고 실제 판을 무적 없이 웨이브 1~MaxWaves(쓰러질 때까지) 하며 1초마다 화면을 찍는다.
    /// 전리품·야영·출발을 실제 화면으로 지나간다. 스크린숏은 Logs/shots/qa/, 콘솔 경고·오류는 로그에 모은다.
    /// 판정은 느슨하다(예외·오류 0, 화면 흐름이 막히지 않음) — 목적은 사람이 볼 스크린숏을 남기는 것.
    /// </summary>
    public sealed class QaPlaythroughTests
    {
        FlowKit _k;
        /// <summary>웨이브 몇까지 하는가(그 전에 쓰러지면 쓰러짐 연출을 찍고 끝낸다).</summary>
        const int MaxWaves = 6;

        [UnitySetUp]
        public IEnumerator SetUp() { _k = new FlowKit(); _k.Start("qa"); yield return null; }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_k != null)
            {
                foreach (var w in _k.Warnings) FlowKit.Log("WARN " + w);
                foreach (var e in _k.Errors) FlowKit.Log("ERROR " + e);
                _k.Stop();
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator Qa_Waves1To3_Screenshots()
        {
            var k = _k;
            yield return k.WaitTop(ScreenId.FirstRunPrompt);
            k.Key(MenuKey.Confirm);
            yield return k.WaitTop(ScreenId.Title);
            yield return k.Seconds(1f);
            yield return k.Shot("000-title");
            k.Key(MenuKey.Confirm);
            yield return k.WaitTop(ScreenId.OfflineGuide);
            k.Click(StartNameKeys.CommonOk);
            yield return k.WaitTop(ScreenId.MainMenu);
            yield return k.Seconds(1f);
            yield return k.Shot("001-main-menu");
            k.Activate(FlowKit.Field<UiButton>(k.TopScreen, "_expedition"), "원정 카드");
            yield return k.WaitTop(ScreenId.ExpeditionPrep);
            yield return k.Seconds(1f);
            yield return k.Shot("002-prep");
            k.Click(PrepKeys.Depart);

            // 튜토리얼: 첫 두 단계를 찍고 Esc로 건너뛴다(튜토리얼 자체는 FullFlowTests가 끝까지 간다).
            yield return k.WaitTop(ScreenId.Tutorial);
            yield return k.Seconds(1.5f);
            yield return k.Shot("003-tutorial-start");
            k.Key(MenuKey.Back);
            yield return k.Seconds(0.5f);
            yield return k.Shot("004-tutorial-skip");
            if (k.TopScreen != null && k.Screens.Top != ScreenId.Battle) { k.Key(MenuKey.Confirm); yield return k.Seconds(0.3f); }

            int shot = 10;
            for (int wave = 1; wave <= MaxWaves; wave++)
            {
                yield return k.WaitUntil(() => k.World != null && k.World.Run != null && k.World.Run.Phase == RunPhase.WaveCombat
                                              && k.Screens.Top == ScreenId.Battle, "웨이브 " + wave + " 전투", 40f);
                float next = 0f, t0 = Time.realtimeSinceStartup;
                while (k.Screens.Top == ScreenId.Battle && k.World.Run.Phase == RunPhase.WaveCombat && k.World.P0.Hp > 0)
                {
                    KiteFrame(k);
                    float t = Time.realtimeSinceStartup - t0;
                    if (t >= next) { k.ReleaseAll(); yield return k.Shot((shot++).ToString("000") + "-w" + wave + "-t" + Mathf.RoundToInt(t)); next += 1f; }
                    if (t > 150f) Assert.Fail("웨이브 " + wave + " 150초 안에 끝나지 않음 (적 " + k.World.Enemies.Count + ")");
                    yield return null;
                }
                k.ReleaseAll();
                FlowKit.Log("qa wave " + wave + " hit sources: " + HitSources(k));
                FlowKit.Log("qa wave " + wave + ": " + (Time.realtimeSinceStartup - t0).ToString("0.0") + "s, hp " + k.World.P0.Hp
                            + ", kills " + k.Count(SimEventType.EnemyKilled) + ", grazes " + k.Count(SimEventType.GrazeAwarded)
                            + ", arts " + k.Count(SimEventType.ArtFired) + "/" + k.Count(SimEventType.ArtFizzled)
                            + ", hits " + k.Count(SimEventType.PlayerHit) + ", locks " + k.Count(SimEventType.HeatLockEntered));
                if (k.World.P0.Hp <= 0)
                {
                    // 쓰러짐 연출(그림 정지 → 슬로모션 → 덮개·비네트·줌 → 사망 글)을 시간대별로 찍는다(정산까지 2초).
                    float d0 = Time.realtimeSinceStartup;
                    float[] at = { 0.1f, 0.4f, 0.8f, 1.2f, 1.7f };
                    for (int i = 0; i < at.Length; i++)
                    {
                        while (Time.realtimeSinceStartup - d0 < at[i]) yield return null;
                        yield return k.Shot((shot++).ToString("000") + "-dead-t" + Mathf.RoundToInt(at[i] * 10f).ToString("00"));
                    }
                    FlowKit.Log("qa dead: wave " + wave + ", top " + k.Screens.Top);
                    yield break;
                }

                // 전리품
                yield return k.WaitTop(ScreenId.LootPick, 20f);
                yield return k.Seconds(1.2f);
                yield return k.Shot((shot++).ToString("000") + "-w" + wave + "-loot");
                var loot = k.World.Run.Loot;
                var cards = FlowKit.Field<List<UiButton>>(k.TopScreen, "_cardButtons");
                for (int i = 0; i < loot.PickCount && i < cards.Count; i++) { k.Activate(cards[i], "카드 " + i); yield return k.Seconds(0.4f); }
                yield return k.Shot((shot++).ToString("000") + "-w" + wave + "-loot-picked");
                k.Click("str.loot_pick.confirm");
                yield return k.WaitUntil(() => k.World.Run.Phase == RunPhase.Camp, "야영", 20f);
                yield return k.WaitTop(ScreenId.Battle, 20f);
                yield return k.Seconds(1f);
                yield return k.Shot((shot++).ToString("000") + "-w" + wave + "-camp-field");
                // 출발: 출발 표식 위에서 J 1초(야영 화면은 열지 않는다 — 비기 해금은 야영 화면에서 먼저 고르게 한다)
                yield return QaCamp(k, shot); shot += 3;
            }
            Assert.IsEmpty(k.Errors, string.Join("\n", k.Errors));
        }


        /// <summary>지금까지의 피격을 때린 적 종(·피해 종류)별로 센다(EnemySpawned의 E → 종 id).</summary>
        static string HitSources(FlowKit k)
        {
            var species = new Dictionary<int, string>();
            var counts = new SortedDictionary<string, int>();
            foreach (var e in k.Events)
            {
                if (e.Type == SimEventType.EnemySpawned) species[e.EntityId] = e.Id;
                else if (e.Type == SimEventType.PlayerHit)
                {
                    string sp;
                    if (!species.TryGetValue(e.EntityId, out sp)) sp = "?";
                    string key = sp + "/" + (PlayerDamageSource)e.F;
                    int n; counts.TryGetValue(key, out n); counts[key] = n + 1;
                }
            }
            var parts = new List<string>();
            foreach (var kv in counts) parts.Add(kv.Key + "×" + kv.Value);
            return string.Join(" ", parts);
        }

        int _kiteFrame;
        /// <summary>사람처럼: 가까운 적을 향해 시야를 맞춘 뒤 자세(Shift)로 시야를 잠그고 J를 누른 채 뒷걸음·옆걸음. 결정이 있으면 가끔 K.</summary>
        void KiteFrame(FlowKit k)
        {
            var w = k.World; var p = w.P0; var e = k.NearestEnemy();
            _kiteFrame++;
            if (e == null) { k.ReleaseAll(); k.DropFrame(); return; }
            Vector2 d = e.Pos - p.Pos; float dist = d.magnitude;
            bool horiz = Mathf.Abs(d.x) > Mathf.Abs(d.y);
            int phase = _kiteFrame % 24;
            if (phase < 3)
            {
                // 시야 맞추기: 자세를 풀고 적 쪽 방향을 짧게 누른다.
                k.ReleaseAll();
                k.SetKey(horiz ? (d.x < 0 ? LogicalKey.Left : LogicalKey.Right) : (d.y < 0 ? LogicalKey.Up : LogicalKey.Down), true);
                return;
            }
            k.SetKey(LogicalKey.Stance, true);
            k.SetKey(LogicalKey.Fire, true);
            if (p.Crystals > 0 && phase == 12) { k.SetKey(LogicalKey.Skill, true); return; }
            k.SetKey(LogicalKey.Skill, false);
            Vector2 move = dist < 4f ? -d : (dist > 6.5f ? d : new Vector2(-d.y, d.x));
            k.SetKey(LogicalKey.Left, move.x < -0.3f * move.magnitude);
            k.SetKey(LogicalKey.Right, move.x > 0.3f * move.magnitude);
            k.SetKey(LogicalKey.Up, move.y < -0.3f * move.magnitude);
            k.SetKey(LogicalKey.Down, move.y > 0.3f * move.magnitude);
        }

        IEnumerator QaCamp(FlowKit k, int shot)
        {
            // 모닥불 옆에서 J → 야영 화면 → (비기 고르기) → 출발
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
            yield return k.Seconds(1f);
            yield return k.Shot(shot.ToString("000") + "-camp-screen");
            var pick = GameObject.Find("PickCard0");
            if (pick != null) { k.Activate(pick.GetComponent<UiButton>(), "비기 카드"); yield return k.Seconds(0.6f); }
            yield return k.Shot((shot + 1).ToString("000") + "-camp-screen-after");
            if (GameObject.Find("PickCard0") == null) k.Click("str.camp.depart");
            yield return k.Seconds(1.5f);
            yield return k.Shot((shot + 2).ToString("000") + "-depart");
        }
    }
}
