using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Wizard.Solo.Data;
using Wizard.Solo.Sim;

namespace Wizard.Solo.Tests.Spells
{
    /// <summary>
    /// 기본 공격 유형(Docs/12-attack-styles.md) 시험 도구: 진짜 모듈 전부(SimModuleSet.CreateDefault) + 연습 방(허수아비)에서
    /// 유형 하나를 쏘아 DPS·사건을 잰다. 봇은 상태만 보고 입력을 정한다(결정론: 같은 씨앗 = 같은 값).
    /// 손잡이를 켠 콘텐츠(Tuned — 게임 부팅과 같은 값)와 명세 콘텐츠(TestContent.Db)를 둘 다 쓸 수 있다.
    /// </summary>
    public static class AttackStyleKit
    {
        static IContentDb _tuned;

        /// <summary>재미 조정 손잡이(FunPass)를 켠 콘텐츠 — 냉각 대기 0.08초, 고유 비기 강화·튜토리얼 A 유형 켬.</summary>
        public static IContentDb Tuned
        {
            get
            {
                if (_tuned == null)
                    _tuned = ContentDb.LoadFromDirectory(TestContent.DataDir, TestContent.SoloExtraDir, msg => { }, applyTuning: true);
                return _tuned;
            }
        }

        public static readonly string[][] Styles =
        {
            new[] { "block.core.ember_mortar", "block.core.flame_jet", "block.core.bouncing_ember" },
            new[] { "block.core.chain_bolt", "block.core.thunder_orb", "block.core.sky_strike" },
            new[] { "block.core.page_flurry", "block.core.tome_throw", "block.core.rune_mine" },
            new[] { "block.core.frost_lance", "block.core.blizzard", "block.core.icicle_fall" },
            new[] { "block.core.shadow_wave", "block.core.shadow_scythe", "block.core.eye_seeker" },
        };

        /// <summary>J 정책: Hold = 꾹 누르기, Tap = 임계 끊어 쏘기(다음 발이 잠그면 손을 뗀다 — FunPassKit와 같다).</summary>
        public enum Policy { Hold, Tap }

        public struct Result
        {
            public float Dps;
            public int Shots, Crits, Locks;
            public long Damage;
            /// <summary>오른 콤보의 합(ComboChanged B) — 결정이 차는 속도 비교용.</summary>
            public int ComboGain;
        }

        /// <summary>연습 방 판 설정(출발 조립 = 코어 마력탄 + 빈 연결 칸, 고른 유형). style = null이면 마력탄 그대로.</summary>
        public static RunConfig PracticeConfig(IContentDb db, int character, string style, RunMode mode = RunMode.Practice, ulong seed = 7UL,
            List<string> links = null)
        {
            var def = db.Character(Ids.Char.Of(character));
            int l = def != null ? def.BaseLinkSlots : 4;
            var lo = new LoadoutSpec { Core = Ids.Block.MagicBolt };
            for (int i = 0; i < l; i++) lo.Links.Add(links != null && i < links.Count ? links[i] : null);
            return new RunConfig
            {
                Mode = mode,
                Seed = seed,
                CharId = Ids.Char.Of(character),
                WandId = 1,
                WandStage = 0,
                LinkSlotCount = l,
                Loadout = lo,
                AttackStyle = style
            };
        }

        public static SimWorld Start(IContentDb db, RunConfig cfg)
        {
            var w = new SimWorld(db, SimModuleSet.CreateDefault(), cfg.Seed);
            w.StartRun(cfg);
            return w;
        }

        public static EnemyState Dummy(SimWorld w)
        {
            for (int i = 0; i < w.Enemies.Count; i++)
                if (w.Enemies[i].DefId == Ids.Enemy.Dummy && !w.Enemies[i].Dead) return w.Enemies[i];
            return null;
        }

        /// <summary>죽지 않는 허수아비 하나를 더 세운다(무리 측정용).</summary>
        public static EnemyState AddDummy(SimWorld w, Vector2 pos)
        {
            var e = w.Mods.Enemies.Spawn(w, new EnemySpawnSpec { DefId = Ids.Enemy.Dummy, Pos = pos, NoReward = true, Instant = true, HpOverride = 1e7f });
            if (e != null) { e.MaxHp = 1e7f; e.Hp = 1e7f; e.HpFloor = 1f; }
            return e;
        }

        /// <summary>
        /// 허수아비 방에서 유형 하나를 ticks틱 쏜 DPS. 플레이어는 허수아비 정면(시야 S) distance칸에 서고 움직이지 않는다.
        /// cluster = 허수아비를 셋으로(정면 하나 + 뒤 좌우 둘, 0.8칸·0.7칸 — "3마리 무리"). 피해 = EnemyDamaged(A, 반올림)의 합.
        /// </summary>
        public static Result Measure(IContentDb db, int character, string style, Policy policy, float distance, bool cluster,
            int ticks = 6000, ulong seed = 7UL)
        {
            var w = Start(db, PracticeConfig(db, character, style, RunMode.Practice, seed));
            var p = w.P0;
            var d = Dummy(w);
            if (d == null) throw new InvalidOperationException("no dummy");
            p.Pos = d.Pos - Dir8Util.ToSimVector(Dir8.S) * distance;
            p.PrevPos = p.Pos;
            p.Facing = Dir8.S;
            if (cluster)
            {
                AddDummy(w, d.Pos + new Vector2(-0.8f, 0.7f));
                AddDummy(w, d.Pos + new Vector2(0.8f, 0.7f));
            }
            w.Events.Clear();
            var res = new Result();
            bool down = false;
            const int maxHu = 100 * SimConst.HuPerHeat;
            for (int t = 0; t < ticks; t++)
            {
                int h = p.Spell != null ? p.Spell.HeatPerShotHu : 0;
                bool want = policy == Policy.Hold || (!p.Locked && p.HeatHu + h <= maxHu);
                if (want != down)
                {
                    down = want;
                    w.EnqueueInput(0, new InputEvent(LogicalKey.Fire, down, SimConst.TickStartU(w.Tick)));
                }
                p.InvulnUntilTick = w.Tick + 2;
                w.Step();
                for (int i = 0; i < w.Events.Count; i++)
                {
                    var e = w.Events[i];
                    if (e.Type == SimEventType.EnemyDamaged && e.Slot == 0) res.Damage += e.A;
                    else if (e.Type == SimEventType.ShotFired && e.Slot == 0) { res.Shots++; if (e.B == 1) res.Crits++; }
                    else if (e.Type == SimEventType.HeatLockEntered && e.Slot == 0) res.Locks++;
                    else if (e.Type == SimEventType.ComboChanged && e.Slot == 0 && e.B > 0) res.ComboGain += e.B;
                }
                w.Events.Clear();
            }
            res.Dps = res.Damage * (float)SimConst.TicksPerSecond / ticks;
            return res;
        }

        /// <summary>그 유형을 재기 알맞은 거리(근접은 붙어서, 나머지는 허수아비 방의 기본 3칸).</summary>
        public static float DistanceFor(string style)
        {
            switch (style)
            {
                case "block.core.shadow_scythe": return 1.6f;
                case "block.core.flame_jet": return 1.8f;
                case "block.core.blizzard": return 1.6f;
                default: return 3f;
            }
        }

        /// <summary>세계 상태 지문(결정론 비교): 틱, 적(위치·체력·상태), 내 탄(유형 상태 전부), 플레이어(위치·열·콤보·결정).</summary>
        public static string Digest(SimWorld w)
        {
            var sb = new StringBuilder();
            sb.Append(w.Tick).Append('|');
            var p = w.P0;
            sb.Append(F(p.Pos.x)).Append(',').Append(F(p.Pos.y)).Append(',').Append(p.HeatHu).Append(',').Append(p.Combo).Append(',').Append(p.Crystals).Append('|');
            for (int i = 0; i < w.Enemies.Count; i++)
            {
                var e = w.Enemies[i];
                sb.Append(e.Id).Append(':').Append(F(e.Pos.x)).Append(',').Append(F(e.Pos.y)).Append(',').Append(F(e.Hp))
                  .Append(',').Append(e.BurnStacks).Append(',').Append(e.BurnNextTick).Append(',').Append(e.ChillStacks)
                  .Append(',').Append(e.FrozenUntilTick).Append(';');
            }
            sb.Append('|');
            for (int i = 0; i < w.Projectiles.Count; i++)
            {
                var r = w.Projectiles[i];
                sb.Append(r.Id).Append(':').Append(r.Block != null ? r.Block.Id : "-").Append(',').Append(F(r.Pos.x)).Append(',').Append(F(r.Pos.y))
                  .Append(',').Append(r.StylePhase).Append(',').Append(r.TimerTick).Append(',').Append(r.StepIndex).Append(',').Append(r.StyleHits)
                  .Append(',').Append(F(r.DamageMul)).Append(',').Append(r.BouncesLeft).Append(',').Append(r.PierceLeft).Append(',').Append(r.RicochetsLeft)
                  .Append(',').Append(r.Generation).Append(';');
            }
            return sb.ToString();
        }

        static string F(float v) => v.ToString("R", System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>한 유형의 측정 한 벌: 꾹·끊어(과녁 하나), 꾹(3마리 무리).</summary>
        public struct Row
        {
            public string Style;
            public float Hold, Tap, Cluster;
            /// <summary>꾹 누르기의 초당 콤보.</summary>
            public float ComboPerSecond;
        }

        public static Row MeasureRow(IContentDb db, int character, string style, int ticks)
        {
            float dist = DistanceFor(style);
            var hold = Measure(db, character, style, Policy.Hold, dist, false, ticks);
            return new Row
            {
                Style = style ?? Ids.Block.MagicBolt,
                Hold = hold.Dps,
                ComboPerSecond = hold.ComboGain * (float)SimConst.TicksPerSecond / ticks,
                Tap = Measure(db, character, style, Policy.Tap, dist, false, ticks).Dps,
                Cluster = Measure(db, character, style, Policy.Hold, dist, true, ticks).Dps
            };
        }

        /// <summary>균형 표(손잡이 켬, 6,000틱). 마력탄 대비 비율: 꾹·끊어 ±12%, 무리 ≤ 1.8, 끊어/꾹 ≥ 1.25가 목표(Docs/12 5절).</summary>
        public static string BalanceTable()
        {
            var db = Tuned;
            const int ticks = 6000;
            var sb = new StringBuilder();
            sb.AppendLine("| 캐릭터 | 유형 | 꾹 DPS | 끊어 DPS | 무리(3) 꾹 DPS | 꾹/마력탄 | 끊어/마력탄 | 무리/마력탄 무리 | 끊어/꾹 | 콤보/초(꾹) |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|");
            for (int c = 1; c <= 5; c++)
            {
                var bolt = MeasureRow(db, c, null, ticks);
                sb.AppendLine(Line(c, bolt, bolt));
                for (int k = 0; k < 3; k++) sb.AppendLine(Line(c, MeasureRow(db, c, Styles[c - 1][k], ticks), bolt));
            }
            return sb.ToString();
        }

        /// <summary>
        /// 수치 찾기 도구(시험이 아니라 AttackStyles.json 값을 정할 때 쓴 것 — Docs/12 5절). 끊어/꾹 비율은 열만으로 정해지고(피해에 선형),
        /// 피해는 두 DPS를 같이 비례로 옮긴다 → 열을 훑어 마력탄과 같은 비율이 되는 값을 고르고, 피해 배율 = √(꾹 비 × 끊어 비)의 역수.
        /// 콘텐츠의 블록 정의를 그 자리에서 바꿔 재고 끝나면 되돌린다.
        /// </summary>
        public static string TuneHeat()
        {
            var db = Tuned;
            const int ticks = 6000;
            var sb = new StringBuilder();
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            for (int c = 1; c <= 5; c++)
            {
                var bolt = MeasureRow(db, c, null, ticks);
                float target = bolt.Tap / bolt.Hold;
                for (int k = 0; k < 3; k++)
                {
                    string id = Styles[c - 1][k];
                    var b = db.Block(id);
                    float h0 = b.Heat, d0 = b.BaseDamage;
                    float bestH = h0, bestErr = float.MaxValue, bestHold = 0f, bestTap = 0f;
                    bool found = false;
                    // 처음 값에 가까운 쪽부터(열을 덜 바꾸는 쪽) 훑고, 비율이 목표의 ±12% 안이면 거기서 멈춘다(피해 배율로 두 DPS를 ±6% 안에 둔다).
                    for (int i = 0; i <= 40 && !found; i++)
                    {
                        int step = (i + 1) / 2 * ((i & 1) == 1 ? 1 : -1);
                        float h = (float)Math.Round(h0 * (1f + step * 0.05f), 1);
                        if (h <= 0f) continue;
                        b.Heat = h;
                        float dist = DistanceFor(id);
                        float hold = Measure(db, c, id, Policy.Hold, dist, false, ticks).Dps;
                        float tap = Measure(db, c, id, Policy.Tap, dist, false, ticks).Dps;
                        float err = Math.Abs(tap / hold - target);
                        if (err < bestErr) { bestErr = err; bestH = h; bestHold = hold; bestTap = tap; }
                        if (err <= target * 0.12f && tap / hold >= 1.27f) found = true;
                    }
                    b.Heat = h0;
                    float scale = (float)Math.Sqrt((bolt.Hold / bestHold) * (bolt.Tap / bestTap));
                    sb.AppendLine(string.Format(inv, "{0} char.{1} heat {2} -> {3}  dmg {4} -> {5:0.0}  (ratio {6:0.00} target {7:0.00}, hold {8:0.00} tap {9:0.00} after scale)",
                        id, c, h0, bestH, d0, d0 * scale, bestTap / bestHold, target, bestHold * scale / bolt.Hold, bestTap * scale / bolt.Tap));
                    b.BaseDamage = d0;
                }
            }
            return sb.ToString();
        }

        static string Line(int c, Row r, Row bolt)
        {
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            return string.Format(inv, "| char.{0} | {1} | {2:0.0} | {3:0.0} | {4:0.0} | {5:0.00} | {6:0.00} | {7:0.00} | {8:0.00} | {9:0.0} |",
                c, r.Style.Replace("block.", ""), r.Hold, r.Tap, r.Cluster, r.Hold / bolt.Hold, r.Tap / bolt.Tap, r.Cluster / bolt.Cluster,
                r.Hold > 0f ? r.Tap / r.Hold : 0f, r.ComboPerSecond);
        }

        /// <summary>한 세계를 정해진 입력(J 꾹, 0.5초마다 방향 바꾸기)으로 ticks틱 돌린 지문들(매 60틱).</summary>
        public static List<string> RunScripted(IContentDb db, int character, string style, int ticks, ulong seed, bool cluster)
        {
            var w = Start(db, PracticeConfig(db, character, style, RunMode.Practice, seed));
            var d = Dummy(w);
            w.P0.Pos = d.Pos - Dir8Util.ToSimVector(Dir8.S) * DistanceFor(style);
            if (cluster)
            {
                AddDummy(w, d.Pos + new Vector2(-0.8f, 0.7f));
                AddDummy(w, d.Pos + new Vector2(0.8f, 0.7f));
            }
            var marks = new List<string>();
            w.EnqueueInput(0, new InputEvent(LogicalKey.Fire, true, SimConst.TickStartU(w.Tick)));
            for (int t = 0; t < ticks; t++)
            {
                w.P0.InvulnUntilTick = w.Tick + 2;
                w.Step();
                w.Events.Clear();
                if (t % 60 == 59) marks.Add(Digest(w));
            }
            return marks;
        }
    }
}
