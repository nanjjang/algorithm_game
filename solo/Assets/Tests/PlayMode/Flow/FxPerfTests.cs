using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Wizard.Solo.Sim;
using Wizard.Solo.UI;

namespace Wizard.Solo.Tests.PlayMode.Flow
{
    /// <summary>
    /// 연출 최대 + 적 80마리 프레임 시간(목표 16.6ms). 판을 진짜 길로 열고(BattleShotsTests와 같은 시험 전용 진입), 웨이브 1 전투 중에
    /// 적을 80마리로 채워 두고(죽으면 바로 다시 채운다 — 처치·적중 연출이 끊이지 않는다) 봇이 비기까지 쓰며 싸운다.
    /// 프레임 시간(Time.unscaledDeltaTime)의 평균·95번째·최대를 Logs/shots/perf/perf.txt와 로그에 남긴다. 스크린숏 두 장(난장판, 화면 밖 표식).
    /// 에디터 안 측정이라 플레이어 빌드보다 느리다 — 실패 기준은 넉넉히(평균 50ms) 두고 값은 보고로 본다.
    /// </summary>
    public class FxPerfTests
    {
        FlowKit _k;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_k != null) { _k.Stop(); _k = null; }
            yield return null;
        }

        static readonly string[] Species = { Ids.Enemy.SlimeMoss, Ids.Enemy.BugLantern, Ids.Enemy.WolfBramble, Ids.Enemy.BatDusk, Ids.Enemy.HogNeedle };

        int Fill(SimWorld w, int target, int salt)
        {
            int added = 0;
            var p = w.P0;
            for (int i = w.Enemies.Count; i < target; i++)
            {
                float a = (i * 137.5f + salt * 11f) * Mathf.Deg2Rad;
                float r = 2.5f + (i % 7) * 1.1f;
                var pos = new Vector2(Mathf.Clamp(p.Pos.x + Mathf.Cos(a) * r, 1f, w.ArenaWidth - 1f), Mathf.Clamp(p.Pos.y + Mathf.Sin(a) * r, 1f, w.ArenaHeight - 1f));
                var e = w.Mods.Enemies.Spawn(w, new EnemySpawnSpec { DefId = Species[i % Species.Length], Pos = pos, Instant = true, NoReward = true });
                if (e != null) added++;
            }
            return added;
        }

        [UnityTest]
        public IEnumerator Enemies80_MaxFx_FrameTime()
        {
            _k = new FlowKit();
            _k.Start("perf");
            yield return null;
            var ctx = _k.Ctx;
            ctx.Profile.Flags.TutorialDummyDone = true;
            ctx.Profile.Flags.OfflineGuideSeen = true;
            ctx.Profiles.Save();
            _k.Screens.Replace(ScreenId.MainMenu);
            yield return _k.WaitTop(ScreenId.MainMenu);
            var r = ctx.Wands.BuildRunConfig(1, ctx.Profile.Character(1).EquippedWandId, 1, 91UL);
            Assert.IsTrue(r.Ok, "BuildRunConfig: " + r.Error);
            var battleArgs = ScreenArgs.From(ScreenId.MainMenu).With(ScreenArgKeys.RunConfig, r.Value);
            _k.Screens.Push(ScreenId.Loading, ScreenArgs.ViaLoading(ScreenId.MainMenu, ScreenId.Battle, battleArgs));
            yield return _k.WaitTop(ScreenId.Battle, 20f);
            var k = _k;
            yield return k.WaitUntil(() => k.World != null && k.World.Run.Phase == RunPhase.WaveCombat, "웨이브 1 전투", 30f);

            // 몸 풀기(그림·풀 데우기)
            Fill(k.World, 80, 0);
            float warm = Time.realtimeSinceStartup + 2f;
            while (Time.realtimeSinceStartup < warm)
            {
                k.World.P0.InvulnUntilTick = k.World.Tick + 10;
                Fill(k.World, 80, k.World.Tick);
                k.CombatFrame(true);
                yield return null;
            }

            var frames = new List<float>(1024);
            int minEnemies = int.MaxValue, maxEnemies = 0, kills0 = k.Count(SimEventType.EnemyKilled), hits0 = k.Count(SimEventType.EnemyDamaged);
            float end = Time.realtimeSinceStartup + 6f;
            bool shot = false;
            while (Time.realtimeSinceStartup < end)
            {
                var w = k.World;
                if (w == null || w.Run.Phase != RunPhase.WaveCombat) break;
                w.P0.InvulnUntilTick = w.Tick + 10;
                Fill(w, 80, w.Tick);
                minEnemies = Mathf.Min(minEnemies, w.Enemies.Count);
                maxEnemies = Mathf.Max(maxEnemies, w.Enemies.Count);
                k.CombatFrame(true);
                yield return null;
                frames.Add(Time.unscaledDeltaTime * 1000f);
                if (!shot && frames.Count == 180) { shot = true; yield return k.Shot("perf-80-enemies"); }
            }
            Assert.Greater(frames.Count, 60, "측정 프레임");
            frames.Sort();
            float sum = 0f;
            for (int i = 0; i < frames.Count; i++) sum += frames[i];
            float avg = sum / frames.Count, p95 = frames[Mathf.Min(frames.Count - 1, Mathf.FloorToInt(frames.Count * 0.95f))], max = frames[frames.Count - 1];
            int over = 0;
            for (int i = 0; i < frames.Count; i++) if (frames[i] > 16.7f) over++;
            string line = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "perf: frames {0}, avg {1:F2} ms, p95 {2:F2} ms, max {3:F2} ms, over16.7 {4} ({5:P1}), enemies {6}~{7}, kills {8}, hits {9}, screen {10}x{11}",
                frames.Count, avg, p95, max, over, over / (float)frames.Count, minEnemies, maxEnemies,
                k.Count(SimEventType.EnemyKilled) - kills0, k.Count(SimEventType.EnemyDamaged) - hits0, Screen.width, Screen.height);
            FlowKit.Log(line);
            Directory.CreateDirectory(k.ShotDir);
            File.WriteAllText(Path.Combine(k.ShotDir, "perf.txt"), line + "\n");

            // 화면 밖 표식: 플레이어를 전장 한쪽 끝으로 옮기고 반대편에 적을 둔다
            var w2 = k.World;
            if (w2 != null && w2.Run.Phase == RunPhase.WaveCombat)
            {
                for (int i = w2.Enemies.Count - 1; i >= 0; i--) w2.Enemies[i].Hp = 0f;
                yield return k.Seconds(0.4f);
                w2.P0.Pos = w2.P0.PrevPos = new Vector2(2.5f, w2.ArenaHeight * 0.5f);
                for (int i = 0; i < 6; i++)
                    w2.Mods.Enemies.Spawn(w2, new EnemySpawnSpec { DefId = Species[i % Species.Length], Pos = new Vector2(w2.ArenaWidth - 1.5f, 2f + i * 1.8f), NoReward = true });
                float until = Time.realtimeSinceStartup + 1.2f;
                bool notice = false;
                while (Time.realtimeSinceStartup < until)
                {
                    w2.P0.InvulnUntilTick = w2.Tick + 10;
                    k.ReleaseAll();
                    yield return null;
                    if (!notice && Time.realtimeSinceStartup > until - 1.0f) { notice = true; yield return k.Shot("perf-offscreen-notice"); }
                }
                yield return k.Shot("perf-offscreen-markers");
            }
            Assert.AreEqual(0, k.Errors.Count, string.Join("\n", k.Errors));
            Assert.Less(avg, 50f, line);
        }
    }
}
