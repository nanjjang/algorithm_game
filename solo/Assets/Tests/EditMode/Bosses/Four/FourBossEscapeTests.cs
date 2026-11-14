using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Wizard.Solo.Sim;
using Wizard.Solo.Sim.Bosses;
using Wizard.Solo.Tests.Bosses.Common;

namespace Wizard.Solo.Tests.Bosses.Four
{
    /// <summary>
    /// 네 보스의 예고 패턴이 전부 R5를 단독으로 통과하는지(05 요약서 2절·2.2절) 등록된 진짜 Threats 구현의 CheckEscape로 본다.
    /// 구역은 진짜 두뇌의 BuildZones(없으면 뼈대의 기본 배치)가 짠 그대로다. Player·Enemies는 가짜.
    /// </summary>
    public class FourBossEscapeTests
    {
        sealed class World
        {
            public SimWorld W;
            public BossSystem Sys;
            public ThreatModule Threats;
            public BossContext Ctx => BossSystem.Context(W);
        }

        static World Make(string bossId, int wave, Vector2 playerPos)
        {
            var mods = SimModuleSet.Stubs();
            var real = SimModuleSet.CreateDefault();
            var world = new World { Sys = new BossSystem(), Threats = real.Threats };
            mods.Bosses = world.Sys;
            mods.Threats = world.Threats;
            mods.Player = new FakePlayer();
            mods.Enemies = new FakeEnemies();
            world.W = new SimWorld(TestContent.Db, mods, 7);
            var p = world.W.Players[0];
            p.Active = true;
            p.CharId = Ids.Char.C1;
            p.Def = TestContent.Db.Character(Ids.Char.C1);
            p.MaxHp = p.Def.MaxHpCells * SimConst.HpPerCell;
            p.Hp = p.MaxHp;
            p.Pos = playerPos;
            world.W.Run.Phase = RunPhase.BossIntro;
            world.W.Wave.Wave = wave;
            world.Sys.SpawnBoss(world.W, new WavePlan { Wave = wave, BossId = bossId });
            int guard = 0;
            while (world.W.Boss.State == BossFightState.Intro && guard++ < 1000) world.W.Step();
            world.W.Run.Phase = RunPhase.WaveCombat;
            return world;
        }

        // 계약 확인: 이 테스트는 빈 구현이 아닌 진짜 Threats로 돈다(빈 구현의 CheckEscape는 늘 참이라 뜻이 없다).
        [Test]
        public void S05_R5__UsesRegisteredThreatModule()
        {
            var threats = SimModuleSet.CreateDefault().Threats;
            Assert.AreNotEqual(typeof(ThreatModule), threats.GetType(), "Threats 구현이 등록돼 있지 않다");
        }

        // 05 요약서 2.2절 "단독 공격 전부 통과": 다른 예고가 없는 전장에서 각 패턴의 모든 예고 step이 R5를 통과한다.
        // 플레이어 자리는 가운데·벽 가·구석으로 바꿔 본다.
        [TestCase(Ids.Boss.SporeQueen, 30)]
        [TestCase(Ids.Boss.StormOwl, 40)]
        [TestCase(Ids.Boss.FrostGoat, 50)]
        [TestCase(Ids.Boss.NightSerpent, 60)]
        public void S05_R5__EveryTelegraphPassesAlone(string bossId, int wave)
        {
            var spots = new[] { new Vector2(12f, 10f), new Vector2(12f, 7f), new Vector2(1f, 7f), new Vector2(22.5f, 1f), new Vector2(6f, 12.5f) };
            int checkedSteps = 0;
            foreach (var spot in spots)
            {
                var world = Make(bossId, wave, spot);
                Assert.AreNotEqual(typeof(ThreatModule), world.Threats.GetType(), "Threats 구현이 등록돼 있지 않다");
                var c = world.Ctx;
                var def = c.Def;
                for (int phase = 0; phase < def.Phases.Count; phase++)
                {
                    c.Boss.Phase = phase;
                    c.Patterns = BossPatternData.Resolve(def, phase, c.Boss.Cycle);
                    foreach (string key in def.Phases[phase].Patterns)
                    {
                        var pattern = c.Pattern(key);
                        Assert.IsNotNull(pattern, key);
                        c.Run = new BossPatternRun { Pattern = pattern, StartTick = world.W.Tick };
                        c.Brain.OnPatternStart(c, pattern);
                        var target = world.W.Players[0];
                        for (int s = 0; s < pattern.Steps.Count; s++)
                        {
                            var step = pattern.Steps[s];
                            if (!step.HasTelegraph) continue;
                            var inst = new BossStepInstance();
                            var zones = new List<ZoneSpec>();
                            if (!c.Brain.BuildZones(c, step, inst, target, zones)) c.DefaultZones(step, inst, target, zones);
                            Assert.Greater(zones.Count, 0, bossId + " " + key + " step " + s);
                            Assert.IsTrue(world.Threats.CheckEscape(world.W, zones, c.Body.Id),
                                bossId + " " + key + " step " + s + " 단계 " + (phase + 1) + " 플레이어 " + spot + " 가 R5를 통과하지 못했다");
                            checkedSteps++;
                        }
                        c.Brain.OnPatternEnd(c, pattern, true);
                        c.Run = null;
                    }
                }
            }
            Assert.Greater(checkedSteps, 0);
        }

        // 05 요약서 2.2절 벡터(S5 TV-R5): 서리밭 칸에서 뿔 휘두르기는 60틱이면 실패, 72틱이면 통과 → 흰뿔은 서리밭 위 플레이어에게 72틱으로 예고한다.
        [Test]
        public void S05_TV_R5__HornSwingOnFrostNeeds72Ticks()
        {
            var world = Make(Ids.Boss.FrostGoat, 50, new Vector2(12f, 7f));
            var c = world.Ctx;
            var w = world.W;
            var target = w.Players[0];
            // 플레이어가 부채꼴의 가장 불리한 자리(축 위, 꼭짓점에서 R/(1 + sin 45°) ≈ 2.93칸)에 서고, 둘레가 서리밭이다.
            c.Body.Pos = target.Pos - new Vector2(0f, 2.93f);
            world.Threats.SpawnHazard(w, new HazardSpec
            {
                Kind = HazardKind.FrostField, OwnerEnemyId = c.Body.Id, Shape = ZoneShape.Circle, Pos = target.Pos, Radius = 6f,
                DurationTicks = 480, SlowOnly = true
            });
            var pattern = c.Pattern(FrostGoatBrain.PatternSwing);
            c.Run = new BossPatternRun { Pattern = pattern, StartTick = w.Tick };
            var zones = new List<ZoneSpec>();
            Assert.IsTrue(c.Brain.BuildZones(c, pattern.Steps[0], new BossStepInstance(), target, zones));
            Assert.AreEqual(1, zones.Count);
            Assert.AreEqual(72, zones[0].TelegraphTicks);
            Assert.IsTrue(world.Threats.CheckEscape(w, zones, c.Body.Id), "72틱이면 통과");
            var shortZone = zones[0];
            shortZone.TelegraphTicks = 60;
            Assert.IsFalse(world.Threats.CheckEscape(w, new List<ZoneSpec> { shortZone }, c.Body.Id), "60틱이면 실패");
            c.Run = null;
        }
    }
}
