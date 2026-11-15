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
    /// 기본 공격 유형 15종 × (발사 순간, 적중·폭발 순간)과 고유 비기 강화 5종 × (솟음, 절정)을 실제 판(연습 방, 진짜 입력)에서 찍는다.
    /// 스크린숏은 Logs/shots/flow/styles/. 사람이 열어 본다(캐릭터마다 확실히 다른가, 과해서 가독성이 무너지지 않는가, 예고 원이 적 예고와 헷갈리지 않는가).
    /// 진입 길은 BattleShotsTests와 같다(진짜 WandService가 만든 RunConfig → Loading → Battle). 판 설정에 기본 공격과 연습 방 옵션(결정 무한·열 고정)을
    /// 싣는다 — 규칙은 바꾸지 않는다. 장면 꾸미기로 허수아비를 몇 더 세우고(연습 방의 허수아비와 같은 종) 내 캐릭터 자리를 옮긴다.
    /// 판정은 느슨하다: 그 유형의 표시 이벤트가 실제로 나왔고 오류 로그가 없다.
    /// </summary>
    public sealed class AttackStyleShotsTests
    {
        FlowKit _k;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_k != null)
            {
                FlowKit.Log("styles shots: " + string.Join(", ", _k.Shots));
                foreach (var w in _k.Warnings) if (w.Contains("readability")) FlowKit.Log("WARN " + w);
                _k.Stop();
                _k = null;
            }
            yield return null;
        }

        sealed class Plan
        {
            public string Block;
            public Func<SimEvent, bool> Launch, Impact;
            public float LaunchDelay, ImpactDelay;
        }

        static Func<SimEvent, bool> Ev(SimEventType t) => e => e.Type == t;
        static Func<SimEvent, bool> Blast(StyleBlastKind k) => e => e.Type == SimEventType.StyleBlast && e.B == (int)k;
        static Func<SimEvent, bool> Spawned(string blk) => e => e.Type == SimEventType.ProjectileSpawned && e.Id == blk;

        static Plan P(string blk, Func<SimEvent, bool> launch, float ld, Func<SimEvent, bool> impact, float id) =>
            new Plan { Block = blk, Launch = launch, LaunchDelay = ld, Impact = impact, ImpactDelay = id };

        static readonly Plan[] Plans =
        {
            P("block.core.ember_mortar", Ev(SimEventType.StyleLobLaunched), 0.18f, Blast(StyleBlastKind.MortarLand), 0.05f),
            P("block.core.flame_jet", Ev(SimEventType.StyleConeStarted), 0.3f, Ev(SimEventType.EnemyDamaged), 0.45f),
            P("block.core.bouncing_ember", Spawned("block.core.bouncing_ember"), 0.08f, Ev(SimEventType.StyleRicochet), 0.02f),
            P("block.core.chain_bolt", Ev(SimEventType.ShotFired), 0.0f, e => e.Type == SimEventType.ChainLightningArc && e.A >= 1, 0.02f),
            P("block.core.thunder_orb", Spawned("block.core.thunder_orb"), 0.3f, Ev(SimEventType.StyleZap), 0.02f),
            P("block.core.sky_strike", Ev(SimEventType.StyleStrikeWarned), 0.12f, Blast(StyleBlastKind.SkyStrike), 0.04f),
            P("block.core.page_flurry", Spawned("block.core.page_flurry"), 0.12f, Ev(SimEventType.EnemyDamaged), 0.02f),
            P("block.core.tome_throw", Spawned("block.core.tome_throw"), 0.22f, Ev(SimEventType.EnemyDamaged), 0.03f),
            P("block.core.rune_mine", Ev(SimEventType.StyleGroundSpawned), 0.3f, Blast(StyleBlastKind.RuneBurst), 0.04f),
            P("block.core.frost_lance", Spawned("block.core.frost_lance"), 0.2f, Ev(SimEventType.StyleShatter), 0.03f),
            P("block.core.blizzard", Ev(SimEventType.StyleConeStarted), 0.3f, Ev(SimEventType.EnemyDamaged), 0.45f),
            P("block.core.icicle_fall", Ev(SimEventType.StyleStrikeWarned), 0.15f, Blast(StyleBlastKind.IcicleDrop), 0.1f),
            P("block.core.shadow_wave", Ev(SimEventType.StyleConeStarted), 0.08f, Ev(SimEventType.EnemyDamaged), 0.06f),
            P("block.core.shadow_scythe", Ev(SimEventType.StyleConeStarted), 0.04f, Ev(SimEventType.EnemyDamaged), 0.03f),
            P("block.core.eye_seeker", Spawned("block.core.eye_seeker"), 0.25f, Ev(SimEventType.StyleSplit), 0.03f),
        };

        /// <summary>캐릭터의 고유 비기: 커맨드(상대 방향 글자)와 (솟음, 절정)을 고르는 단계 이벤트.</summary>
        static readonly string[] ArtIds = { Ids.Art.Kiln, Ids.Art.BoltDash, Ids.Art.CopyCircle, Ids.Art.IceScreen, Ids.Art.ShadowHand };
        static readonly string[] ArtCommands = { "BB", "LF", "FL", "BL", "LR" };

        static Func<SimEvent, bool> Stage(string art, ArtStageKind kind, int b) =>
            e => e.Type == SimEventType.ArtStage && e.Id == art && e.A == (int)kind && (b < 0 || e.B == b);

        static readonly Func<SimEvent, bool>[][] ArtMoments =
        {
            new[] { Stage(Ids.Art.Kiln, ArtStageKind.Rise, 0), Stage(Ids.Art.Kiln, ArtStageKind.Rise, 2) },
            new[] { Stage(Ids.Art.BoltDash, ArtStageKind.Warn, 0), Stage(Ids.Art.BoltDash, ArtStageKind.Land, 4) },
            new[] { Stage(Ids.Art.CopyCircle, ArtStageKind.Pulse, 0), Stage(Ids.Art.CopyCircle, ArtStageKind.Pulse, 2) },
            new[] { Stage(Ids.Art.IceScreen, ArtStageKind.Rise, 0), Stage(Ids.Art.IceScreen, ArtStageKind.Rise, 3) },
            new[] { Stage(Ids.Art.ShadowHand, ArtStageKind.Grip, 0), Stage(Ids.Art.ShadowHand, ArtStageKind.Crush, 0) },
        };
        static readonly float[][] ArtDelays = { new[] { 0.04f, 0.12f }, new[] { 0.06f, 0.03f }, new[] { 0.3f, 0.15f }, new[] { 0.06f, 0.14f }, new[] { 0.18f, 0.05f } };
        static readonly string[][] ArtShotNames = { new[] { "1-slam", "2-pillars" }, new[] { "1-warn", "2-strike" }, new[] { "1-open", "2-vortex" },
            new[] { "1-slam", "2-spikes" }, new[] { "1-grip", "2-crush" } };

        /// <summary>새로 부팅하고 연습 방 판을 그 기본 공격으로 연다.</summary>
        IEnumerator Launch(int character, string style)
        {
            if (_k != null) { _k.Stop(); _k = null; yield return null; }
            _k = new FlowKit();
            _k.Start("styles");
            yield return null;
            var ctx = _k.Ctx;
            ctx.Profile.Flags.TutorialDummyDone = true;
            ctx.Profile.Flags.OfflineGuideSeen = true;
            ctx.Profiles.Save();
            _k.Screens.Replace(ScreenId.MainMenu);
            yield return _k.WaitTop(ScreenId.MainMenu);
            var r = ctx.Wands.BuildRunConfig(character, ctx.Profile.Character(character).EquippedWandId, 1, (ulong)(700 + character));
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

        /// <summary>장면: 내 캐릭터를 왼쪽에 두고 앞(오른쪽)에 허수아비 무리를 세운다. 가까이 쏘는 유형은 가깝게, 추적탄은 약한 슬라임도.</summary>
        void Stage(string style)
        {
            var w = _k.World;
            var p = w.P0;
            var at = new Vector2(w.ArenaWidth * 0.5f - 4.5f, w.ArenaHeight * 0.5f);
            p.Pos = p.PrevPos = at;
            var b = w.Content.Block(style);
            bool near = b != null && b.Style != null && b.Style.Kind == Wizard.Solo.Data.AttackBehavior.Cone && b.Style.Attached;
            float d = near ? 1.7f : 3.4f;
            Vector2[] offs = { new Vector2(d, 0f), new Vector2(d + 0.6f, 0.9f), new Vector2(d + 0.6f, -0.9f), new Vector2(d + 2.2f, 0.4f), new Vector2(d + 2.4f, -0.6f) };
            for (int i = 0; i < offs.Length; i++)
                w.Mods.Enemies.Spawn(w, new EnemySpawnSpec { DefId = Ids.Enemy.Dummy, Pos = at + offs[i], NoReward = true, Instant = true, HpOverride = 100000f });
            if (style == "block.core.eye_seeker")
                for (int i = 0; i < 4; i++)
                    w.Mods.Enemies.Spawn(w, new EnemySpawnSpec { DefId = Ids.Enemy.SlimeMoss, Pos = at + new Vector2(2.4f + i * 0.5f, -1.8f + i * 1.1f), NoReward = true, Instant = true, HpOverride = 1f });
        }

        IEnumerator FaceRight()
        {
            var k = _k;
            k.ReleaseAll();
            k.SetKey(LogicalKey.Right, true);
            yield return k.Frames(4);
            k.SetKey(LogicalKey.Right, false);
            yield return k.Frames(2);
        }

        IEnumerator WaitEvent(Func<SimEvent, bool> match, int from, float timeout, Action<int> found)
        {
            var k = _k;
            float end = Time.realtimeSinceStartup + timeout;
            while (Time.realtimeSinceStartup < end)
            {
                if (k.World != null) k.World.P0.InvulnUntilTick = Mathf.Max(k.World.P0.InvulnUntilTick, k.World.Tick + 10);
                for (int i = from; i < k.Events.Count; i++)
                    if (match(k.Events[i])) { found(i); yield break; }
                yield return null;
            }
            found(-1);
        }

        [UnityTest]
        public IEnumerator Styles15_LaunchAndImpact_Arts5_RiseAndPeak()
        {
            var missing = new List<string>();
            for (int c = 1; c <= Ids.Char.Count; c++)
            {
                for (int s = 0; s < 3; s++)
                {
                    var plan = Plans[(c - 1) * 3 + s];
                    yield return Launch(c, plan.Block);
                    var k = _k;
                    Assert.AreEqual(plan.Block, k.World.P0.Loadout.Core.BlockId, "판 시작 규칙: 마력탄 자리가 고른 유형의 핵");
                    Stage(plan.Block);
                    yield return FaceRight();
                    string tag = "c" + c + "-" + (char)('a' + s) + "-" + plan.Block.Substring("block.core.".Length);
                    k.SetKey(LogicalKey.Stance, true);
                    yield return k.Frames(2);
                    int from = k.Events.Count;
                    k.SetKey(LogicalKey.Fire, true);
                    int li = -1, ii = -1;
                    yield return WaitEvent(plan.Launch, from, 6f, i => li = i);
                    if (li < 0) { missing.Add(tag + " launch"); }
                    else
                    {
                        yield return k.Seconds(plan.LaunchDelay);
                        yield return k.Shot(tag + "-1-launch");
                    }
                    yield return WaitEvent(plan.Impact, Mathf.Max(from, li), 8f, i => ii = i);
                    if (ii < 0 && plan.Block == "block.core.eye_seeker")
                        yield return WaitEvent(Ev(SimEventType.EnemyDamaged), from, 4f, i => ii = i);
                    if (ii < 0) missing.Add(tag + " impact");
                    else
                    {
                        yield return k.Seconds(plan.ImpactDelay);
                        yield return k.Shot(tag + "-2-hit");
                    }
                    // 더 쏘아 쌓인 모습(가독성: 계속 쏠 때 화면이 얼마나 덮이는가)
                    yield return k.Seconds(1.2f);
                    yield return k.Shot(tag + "-3-sustained");
                    k.ReleaseAll();
                    FlowKit.Log(tag + ": launch " + li + ", impact " + ii + ", events " + k.Events.Count + ", fx " + StyleFxSummary());

                    if (s == 0)
                    {
                        // 고유 비기 강화: 자세 + 커맨드 + K
                        yield return k.Seconds(0.4f);
                        yield return FaceRight();
                        yield return CastArt(c, missing);
                    }
                    Assert.IsEmpty(k.Errors, string.Join("\n", k.Errors));
                }
            }
            if (missing.Count > 0) FlowKit.Log("styles missing moments: " + string.Join(", ", missing));
            Assert.IsEmpty(missing, "찍지 못한 순간: " + string.Join(", ", missing));
        }

        /// <summary>원정 준비의 기본 공격 카드(실제 그림 아이콘 + 미리보기 애니)와 조립 판의 핵, 도감의 "기본 공격" 분류.</summary>
        [UnityTest]
        public IEnumerator Prep_StyleCards_Preview_And_Codex()
        {
            _k = new FlowKit();
            _k.Start("styles");
            yield return null;
            var k = _k;
            var ctx = k.Ctx;
            ctx.Profile.Flags.TutorialDummyDone = true;
            ctx.Profile.Flags.OfflineGuideSeen = true;
            ctx.Profiles.Save();
            k.Screens.Replace(ScreenId.MainMenu);
            yield return k.WaitTop(ScreenId.MainMenu);
            k.Activate(FlowKit.Field<UiButton>(k.TopScreen, "_expedition"), "원정 카드");
            yield return k.WaitTop(ScreenId.ExpeditionPrep);
            for (int c = 1; c <= Ids.Char.Count; c++)
            {
                if (c > 1) k.Key(MenuKey.NextTab);
                yield return k.Seconds(0.5f);
                var prep = k.TopScreen as Wizard.Solo.Screens.Prep.ExpeditionPrepScreen;
                Assert.IsNotNull(prep);
                for (int i = 0; i < 3; i++)
                {
                    var pv = prep.StylePreviewAt(i);
                    Assert.IsNotNull(pv, "미리보기 " + i);
                    Assert.IsNotNull(pv.CurrentId, "char." + c + " 카드 " + i + " 미리보기 유형");
                }
                yield return k.Shot("prep-c" + c + "-a");
                // 미리보기는 반복 애니 — 시간을 두고 한 번 더(다른 장면)
                yield return k.Seconds(0.35f);
                yield return k.Shot("prep-c" + c + "-b");
                // 셋째 카드를 고르면 조립 판의 핵이 바뀐다
                var cards = FlowKit.Field<List<UiButton>>(prep, "_styleCards");
                k.Activate(cards[2], "기본 공격 셋째");
                yield return k.Seconds(0.4f);
                yield return k.Shot("prep-c" + c + "-picked-c");
                k.Activate(cards[0], "기본 공격 기본");
                yield return k.Seconds(0.2f);
            }
            k.Screens.Push(ScreenId.Codex, ScreenArgs.From(ScreenId.ExpeditionPrep));
            yield return k.WaitTop(ScreenId.Codex);
            var codex = k.TopScreen as Wizard.Solo.Screens.Hints.CodexScreen;
            Assert.IsNotNull(codex);
            codex.Show(System.Array.IndexOf(Wizard.Solo.Screens.Hints.CodexModel.Sections, Wizard.Solo.Screens.Hints.CodexModel.Styles));
            yield return k.Seconds(0.5f);
            yield return k.Shot("codex-styles");
            Assert.IsEmpty(k.Errors, string.Join(" | ", k.Errors));
        }

        IEnumerator CastArt(int c, List<string> missing)
        {
            var k = _k;
            var w = k.World;
            string art = ArtIds[c - 1];
            string tag = "c" + c + "-art-" + art.Substring("art.".Length);
            k.SetKey(LogicalKey.Stance, true);
            yield return k.Frames(3);
            int from = k.Events.Count;
            string cmd = ArtCommands[c - 1];
            for (int i = 0; i < cmd.Length; i++)
            {
                var key = KeyFor(w.P0.Facing, cmd[i]);
                k.SetKey(key, true);
                yield return k.Frames(3);
                k.SetKey(key, false);
                yield return k.Frames(3);
            }
            k.SetKey(LogicalKey.Skill, true);
            yield return k.Frames(3);
            k.SetKey(LogicalKey.Skill, false);
            var moments = ArtMoments[c - 1];
            int last = from;
            for (int m = 0; m < moments.Length; m++)
            {
                int at = -1;
                yield return WaitEvent(moments[m], last, 5f, i => at = i);
                if (at < 0) { missing.Add(tag + " " + ArtShotNames[c - 1][m]); continue; }
                last = at;
                yield return k.Seconds(ArtDelays[c - 1][m]);
                yield return k.Shot(tag + "-" + ArtShotNames[c - 1][m]);
            }
            FlowKit.Log(tag + ": art fired " + k.Count(SimEventType.ArtFired) + ", stages " + k.Count(SimEventType.ArtStage) + ", fizzles " + k.Count(SimEventType.ArtFizzled)
                        + ", fx " + StyleFxSummary());
            yield return k.Seconds(0.6f);
            k.ReleaseAll();
        }

        /// <summary>유형 그림 층이 그린 것(최근 이름 몇 개)과 떠 있는 조각 수 — 로그로 남긴다.</summary>
        string StyleFxSummary()
        {
            var runner = _k.Session as Wizard.Solo.App.GameRunner;
            var layers = runner != null && runner.Host != null ? runner.Host.Layers : null;
            if (layers == null) return "?";
            foreach (var l in layers)
            {
                var fx = l as Wizard.Solo.View.Effects.StyleFxLayer;
                if (fx == null) continue;
                var seen = new HashSet<string>(fx.Drawn);
                return "bits " + fx.LiveBits + ", grounds " + fx.GroundViews + ", style events " + fx.StyleEvents + ", stages " + fx.StageEvents
                       + ", drawn {" + string.Join(" ", seen) + "}";
            }
            return "no StyleFxLayer";
        }

        /// <summary>상대 방향 글자(F·B·L·R, 지금 시야 기준, 왼쪽 = 반시계 90도) → 물리 방향 키.</summary>
        static LogicalKey KeyFor(Dir8 facing, char rel)
        {
            Dir8 d = rel == 'F' ? facing : rel == 'B' ? Dir8Util.Flip(facing) : rel == 'L' ? Dir8Util.Rotate(facing, 2) : Dir8Util.Rotate(facing, -2);
            switch (d)
            {
                case Dir8.N: return LogicalKey.Up;
                case Dir8.S: return LogicalKey.Down;
                case Dir8.W: return LogicalKey.Left;
                default: return LogicalKey.Right;
            }
        }
    }
}
