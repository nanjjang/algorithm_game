using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Wizard.Solo.Sim;
using Wizard.Solo.UI;

namespace Wizard.Solo.Tests.PlayMode.Flow
{
    /// <summary>
    /// 파이널 어빌리티 15종(캐릭터 × 기본 공격 유형)을 실제 판(연습 방, 진짜 입력)에서 찍는다. 결정을 채우고 K를 빠르게 두 번.
    /// 스크린숏은 Logs/shots/flow/finals/. 진입 길은 AttackStyleShotsTests와 같다.
    /// </summary>
    public sealed class FinalAbilityShotsTests
    {
        FlowKit _k;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_k != null) { _k.Stop(); _k = null; }
            yield return null;
        }

        static readonly string[][] Styles =
        {
            new[] { "block.core.ember_mortar", "block.core.flame_jet", "block.core.bouncing_ember" },
            new[] { "block.core.chain_bolt", "block.core.thunder_orb", "block.core.sky_strike" },
            new[] { "block.core.page_flurry", "block.core.tome_throw", "block.core.rune_mine" },
            new[] { "block.core.frost_lance", "block.core.blizzard", "block.core.icicle_fall" },
            new[] { "block.core.shadow_wave", "block.core.shadow_scythe", "block.core.eye_seeker" },
        };

        IEnumerator Launch(int character, string style)
        {
            if (_k != null) { _k.Stop(); _k = null; yield return null; }
            _k = new FlowKit();
            _k.Start("finals");
            yield return null;
            var ctx = _k.Ctx;
            ctx.Profile.Flags.TutorialDummyDone = true;
            ctx.Profile.Flags.OfflineGuideSeen = true;
            ctx.Profiles.Save();
            _k.Screens.Replace(ScreenId.MainMenu);
            yield return _k.WaitTop(ScreenId.MainMenu);
            var r = ctx.Wands.BuildRunConfig(character, ctx.Profile.Character(character).EquippedWandId, 1, (ulong)(900 + character));
            Assert.IsTrue(r.Ok, "BuildRunConfig: " + r.Error);
            var cfg = r.Value;
            cfg.Mode = RunMode.Practice;
            cfg.AttackStyle = style;
            cfg.PracticeInfiniteCrystal = true;
            cfg.PracticeHeatFreeze = true;
            var battleArgs = ScreenArgs.From(ScreenId.MainMenu).With(ScreenArgKeys.RunConfig, cfg);
            _k.Screens.Push(ScreenId.Loading, ScreenArgs.ViaLoading(ScreenId.MainMenu, ScreenId.Battle, battleArgs));
            yield return _k.WaitTop(ScreenId.Battle, 20f);
            yield return _k.WaitUntil(() => _k.World != null && _k.World.Run.Phase == RunPhase.Practice, "연습 방");
            yield return _k.Seconds(0.6f);
        }

        void Stage()
        {
            var w = _k.World;
            var p = w.P0;
            var at = new Vector2(w.ArenaWidth * 0.5f - 3.5f, w.ArenaHeight * 0.5f);
            p.Pos = p.PrevPos = at;
            Vector2[] offs = { new Vector2(2.5f, 0f), new Vector2(3.2f, 1.2f), new Vector2(3.2f, -1.2f), new Vector2(5f, 0.5f), new Vector2(-2.5f, 1.5f), new Vector2(0.5f, -2.8f) };
            for (int i = 0; i < offs.Length; i++)
                w.Mods.Enemies.Spawn(w, new EnemySpawnSpec { DefId = Ids.Enemy.Dummy, Pos = at + offs[i], NoReward = true, Instant = true, HpOverride = 100000f });
            p.Crystals = p.Def.CrystalCap;
        }

        [UnityTest]
        public IEnumerator Finals15_Shots()
        {
            var missing = new List<string>();
            for (int c = 1; c <= 5; c++)
                for (int s = 0; s < 3; s++)
                {
                    string style = Styles[c - 1][s];
                    yield return Launch(c, style);
                    var k = _k;
                    Stage();
                    k.ReleaseAll();
                    k.SetKey(LogicalKey.Right, true);
                    yield return k.Frames(4);
                    k.SetKey(LogicalKey.Right, false);
                    yield return k.Frames(2);
                    k.World.P0.Crystals = k.World.P0.Def.CrystalCap;
                    int from = k.Events.Count;
                    // K 두 번 빠르게
                    k.SetKey(LogicalKey.Skill, true); yield return k.Frames(2); k.SetKey(LogicalKey.Skill, false); yield return k.Frames(2);
                    k.SetKey(LogicalKey.Skill, true);
                    yield return k.Frames(3);
                    k.SetKey(LogicalKey.Skill, false);
                    yield return k.Frames(4);
                    string tag = "c" + c + "-" + (char)('a' + s) + "-" + style.Substring("block.core.".Length);
                    bool fired = false;
                    float until = Time.realtimeSinceStartup + 1.5f;
                    while (!fired && Time.realtimeSinceStartup < until)
                    {
                        for (int i = 0; i < k.Events.Count; i++)
                            if (k.Events[i].Type == SimEventType.ArtFired && k.Events[i].Id != null && k.Events[i].Id.StartsWith("final.")) fired = true;
                        if (!fired) yield return null;
                    }
                    if (!fired) missing.Add(tag);
                    float[] at = { 0.12f, 0.3f, 0.35f, 0.5f };
                    for (int i = 0; i < at.Length; i++)
                    {
                        k.World.P0.InvulnUntilTick = k.World.Tick + 30;
                        yield return k.Seconds(at[i]);
                        yield return k.Shot(tag + "-" + (i + 1));
                    }
                    FlowKit.Log(tag + ": fired " + fired + ", stages " + k.Count(SimEventType.ArtStage) + ", damaged " + k.Count(SimEventType.EnemyDamaged));
                    Assert.IsEmpty(k.Errors, string.Join("\n", k.Errors));
                }
            Assert.IsEmpty(missing, "파이널이 안 나갔다: " + string.Join(", ", missing));
        }
    }
}
