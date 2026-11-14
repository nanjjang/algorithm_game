using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using Wizard.Solo.Data;
using Wizard.Solo.Sim;
using Wizard.Solo.Tests;

namespace Wizard.Solo.Tests.Integration
{
    /// <summary>
    /// 재미 조정(Docs/11-fun-pass.md)을 재는 도구. 명세 값(TestContent.Db, 손잡이 꺼짐)과 조정 값(TunedDb, DataSolo/FunPass.json 켬)을
    /// 같은 봇으로 돌려 비교한다. 봇은 결정론적이다(입력은 상태만 보고 정한다) → 같은 씨앗이면 같은 수치.
    /// </summary>
    internal static class FunPassKit
    {
        static IContentDb _tuned;

        /// <summary>재미 조정 손잡이를 켠 콘텐츠(게임 부팅과 같은 값).</summary>
        public static IContentDb TunedDb
        {
            get
            {
                if (_tuned == null)
                    _tuned = ContentDb.LoadFromDirectory(TestContent.DataDir, TestContent.SoloExtraDir, msg => { }, applyTuning: true);
                return _tuned;
            }
        }

        public static void Log(string line)
        {
            try { TestContext.WriteLine(line); }
            catch (Exception) { Console.WriteLine(line); }
        }

        // ------------------------------------------------------------ 과열 정책별 DPS (허수아비 방)

        /// <summary>J 정책. Hold = 꾹 누르기, Tap = 잠기지 않는 한 쏜다(한 발씩 끊어 쏘기), Burst = 임계 구간을 몰아 쏘고 임계 아래까지 식힌다.</summary>
        public enum HeatPolicy { Hold, Tap, Burst }

        public struct PolicyResult
        {
            public float Dps;
            public int Shots, Crits, Locks;
        }

        /// <summary>허수아비 방에서 정책대로 ticks틱 쏜 DPS(허수아비가 받은 피해 합 ÷ 시간). 시작 열 0.</summary>
        public static PolicyResult MeasurePolicy(IContentDb db, int character, HeatPolicy policy, int ticks = 6000)
        {
            using (var k = new IntegrationKit(db))
            {
                var wand = k.Profile.Characters[character - 1].EquippedWandId;
                var r = k.Ctx.Wands.BuildPracticeConfig(character, wand, RunMode.Practice, null, 7UL);
                Assert.IsTrue(r.Ok, "BuildPracticeConfig " + r.Error);
                k.Begin(r.Value);
                Assert.AreEqual(RunPhase.Practice, k.W.Run.Phase);
                var p = k.P;
                int start = k.AllEvents.Count;
                bool cooling = false;
                const int maxHu = 100 * SimConst.HuPerHeat;
                for (int t = 0; t < ticks; t++)
                {
                    int h = p.Spell != null ? p.Spell.HeatPerShotHu : 0;
                    bool down;
                    switch (policy)
                    {
                        case HeatPolicy.Hold: down = true; break;
                        case HeatPolicy.Tap: down = !p.Locked && p.HeatHu + h <= maxHu; break;
                        default:
                            if (p.Locked) { down = false; break; }
                            if (cooling && p.HeatHu < p.Derived.CritStartHu) cooling = false;
                            if (!cooling && p.HeatHu + h > maxHu) cooling = true;
                            down = !cooling;
                            break;
                    }
                    k.SetKey(LogicalKey.Fire, down);
                    k.Step(god: true);
                }
                var res = new PolicyResult();
                long dmg = 0;
                for (int i = start; i < k.AllEvents.Count; i++)
                {
                    var e = k.AllEvents[i];
                    if (e.Type == SimEventType.EnemyDamaged && e.Slot == 0) dmg += e.A;
                    else if (e.Type == SimEventType.ShotFired && e.Slot == 0) { res.Shots++; if (e.B == 1) res.Crits++; }
                    else if (e.Type == SimEventType.HeatLockEntered && e.Slot == 0) res.Locks++;
                }
                res.Dps = dmg * (float)SimConst.TicksPerSecond / ticks;
                return res;
            }
        }

        // ------------------------------------------------------------ 판 한 벌 (웨이브 곡선·성장 곡선)

        public sealed class WaveRow
        {
            public int Wave;
            public bool Boss;
            public int ClearTicks;
            public int Kills;
            public int Spawned;
            public int Gold;
            public int Score;
            public int FastScore;
            public int Grazes;
            public int CrystalsGained;
            public int ArtsFired;
            public float EnemyHp;
            public float HoldDps;
            public int MaxAlive;
            public string Species;
            public string Whims;
        }

        /// <summary>
        /// 판 한 벌을 봇으로 돈다(무적). 전투 = 가장 가까운 적에게 다가가며 J 꾹, 결정이 있으면 K(가장 단순한 비기 커맨드는 쓰지 않고 K만 —
        /// 판정기가 정한 기본 비기). 전리품 = 앞 카드 고르기, 야영 = 비기 해금 첫 후보 + 보관함 블록을 탐욕으로 조립(허수아비 DPS가 오르면 끼운다).
        /// toWave까지 클리어하거나 maxTicks가 지나면 멈춘다.
        /// </summary>
        public static List<WaveRow> PlayRun(IContentDb db, int character, ulong seed, int toWave, HeatPolicy policy = HeatPolicy.Hold,
            int maxTicks = 60 * 60 * 90)
        {
            var rows = new List<WaveRow>();
            using (var k = new IntegrationKit(db))
            {
                k.StartRun(character, seed);
                var w = k.W;
                WaveRow cur = null;
                int seenEnemyId = 0, campWave = -1, eventCursor = 0;
                var species = new SortedDictionary<string, int>();
                for (int t = 0; t < maxTicks; t++)
                {
                    var run = w.Run;
                    if (run.Phase == RunPhase.RunEnded) break;
                    if (run.LastClearedWave >= toWave && run.Phase != RunPhase.WaveCombat && run.Phase != RunPhase.WaveClearing) break;

                    if ((run.Phase == RunPhase.WavePrelude || run.Phase == RunPhase.WaveCombat || run.Phase == RunPhase.BossIntro)
                        && w.Wave != null && (cur == null || cur.Wave != w.Wave.Wave))
                    {
                        cur = new WaveRow { Wave = w.Wave.Wave, Boss = w.Wave.Plan != null && w.Wave.Plan.BossId != null, HoldDps = HoldDps(w) };
                        cur.Whims = w.Wave.Plan != null ? string.Join(",", w.Wave.Plan.Whims).Replace("modifier.", "") : "";
                        if (cur.Boss) cur.Whims = w.Wave.Plan.BossId.Replace("boss.", "");
                        rows.Add(cur);
                        species.Clear();
                    }

                    switch (run.Phase)
                    {
                        case RunPhase.LootPick:
                            DriveLoot(w);
                            k.ReleaseAll();
                            break;
                        case RunPhase.StartCamp:
                        case RunPhase.Camp:
                            k.ReleaseAll();
                            if (campWave != run.Wave) { campWave = run.Wave; Assemble(k); }
                            if (run.CurrentArtOffer != null && run.CurrentArtOffer.Candidates.Count > 0)
                                w.Enqueue(new RunCommand(RunCommandKind.ArtPick, 0, 0, run.CurrentArtOffer.Candidates[0], 0));
                            else w.Enqueue(new RunCommand(RunCommandKind.CampDepart));
                            break;
                        default:
                            // 끊어 쏘기: 다음 발이 잠그면 손을 뗀다(누름 이벤트가 섞이지 않게 처음부터 정해 넘긴다).
                            int h = k.P.Spell != null ? k.P.Spell.HeatPerShotHu : 0;
                            k.CombatBot(policy != HeatPolicy.Tap || (!k.P.Locked && k.P.HeatHu + h <= 100 * SimConst.HuPerHeat));
                            // 결정이 있으면 K(가장 쉬운 비기: 자세 없이 K = 판정기 기본). 0.5초마다 한 번만 누른다.
                            bool kDown = k.P.Crystals > 0 && w.AttackAllowed(k.P) && (w.Tick % 30) == 0;
                            w.EnqueueInput(0, new InputEvent(LogicalKey.Skill, kDown, SimConst.TickStartU(w.Tick)));
                            break;
                    }
                    // 무적(InvulnUntilTick)이면 스침이 인정되지 않는다 → 맞기는 하되(콤보 0·결정 손실) 체력만 매 틱 채운다.
                    k.P.Hp = k.P.MaxHp;
                    k.Step(god: false);

                    // 기록
                    for (int i = 0; i < w.Enemies.Count; i++)
                    {
                        var e = w.Enemies[i];
                        if (e.Id <= seenEnemyId) continue;
                        seenEnemyId = e.Id;
                        if (cur == null || e.SummonerId != 0) continue;
                        cur.Spawned++;
                        cur.EnemyHp += e.MaxHp;
                        int n; species.TryGetValue(e.DefId ?? "?", out n); species[e.DefId ?? "?"] = n + 1;
                    }
                    if (cur != null && run.Phase == RunPhase.WaveCombat) cur.MaxAlive = Math.Max(cur.MaxAlive, w.AliveEnemies());
                    for (; eventCursor < k.AllEvents.Count; eventCursor++)
                    {
                        var ev = k.AllEvents[eventCursor];
                        if (cur == null) continue;
                        if (ev.Type == SimEventType.GrazeAwarded) cur.Grazes++;
                        else if (ev.Type == SimEventType.CrystalGained) cur.CrystalsGained++;
                        else if (ev.Type == SimEventType.ArtFired) cur.ArtsFired++;
                    }
                    if (cur != null && run.WaveResults.Count > 0)
                    {
                        var last = run.WaveResults[run.WaveResults.Count - 1];
                        if (last.Wave == cur.Wave && cur.ClearTicks == 0)
                        {
                            cur.ClearTicks = last.Stats.ElapsedTicks;
                            cur.Kills = last.Stats.KillCount;
                            cur.Gold = last.GoldEarned;
                            cur.Score = last.Score.Total;
                            cur.FastScore = last.Score.Fast;
                            var sb = new StringBuilder();
                            foreach (var kv in species) sb.Append(kv.Key.Replace("enemy.", "")).Append('×').Append(kv.Value).Append(' ');
                            cur.Species = sb.ToString().Trim();
                        }
                    }
                }
            }
            return rows;
        }

        static void DriveLoot(SimWorld w)
        {
            var loot = w.Run.Loot;
            if (loot == null) return;
            if (loot.OverflowToDiscard > 0) { w.Enqueue(new RunCommand(RunCommandKind.LootDiscard, 100)); return; }
            // 높은 등급 카드부터(평균적인 사람: 반짝이는 카드를 고른다). 같으면 앞 카드.
            var order = new List<int>();
            for (int i = 0; i < loot.Cards.Count; i++) order.Add(i);
            order.Sort((a, b) =>
            {
                int ra = RarityRank(loot.Cards[a].Rarity), rb = RarityRank(loot.Cards[b].Rarity);
                return ra != rb ? rb.CompareTo(ra) : a.CompareTo(b);
            });
            for (int i = 0; i < order.Count && i < loot.PickCount; i++) w.Enqueue(new RunCommand(RunCommandKind.LootPick, order[i]));
            w.Enqueue(new RunCommand(RunCommandKind.LootConfirm));
        }

        static int RarityRank(string r) => r == Ids.Rarity.Hero ? 2 : r == Ids.Rarity.Rare ? 1 : 0;

        /// <summary>지금 조립의 허수아비 DPS(꾹 누르기 1주기, 단일 표적).</summary>
        public static float HoldDps(SimWorld w)
        {
            var p = w.P0;
            if (p == null || p.Spell == null) return 0f;
            return w.Mods.Spells.ComputeStats(w.Content, p.Spell, p.Def, p.Derived).DummyDps;
        }

        static float DpsWith(SimWorld w, string[] links)
        {
            var p = w.P0;
            var lo = p.Loadout;
            var ctx = w.Mods.Traits.CompileContext(p.Derived, p.Wand.LinkSlotCount);
            ctx.SlotStars = lo.SlotStars();
            string core = lo.Core.IsEmpty ? null : lo.Core.BlockId;
            var spell = w.Mods.Spells.Compile(w.Content, core, links, ctx);
            return w.Mods.Spells.ComputeStats(w.Content, spell, p.Def, p.Derived).DummyDps;
        }

        /// <summary>탐욕 조립: 보관함 블록 하나를 연결 칸 하나에 끼워 허수아비 DPS가 0.1% 넘게 오르는 가장 좋은 수를 되풀이한다.</summary>
        static void Assemble(IntegrationKit k)
        {
            var w = k.W;
            for (int guard = 0; guard < 16; guard++)
            {
                var p = w.P0;
                var lo = p.Loadout;
                if (lo == null || lo.Links == null) return;
                int n = lo.Links.Length;
                var ids = new string[n];
                for (int i = 0; i < n; i++) ids[i] = lo.Links[i].IsEmpty ? null : lo.Links[i].BlockId;
                float best = DpsWith(w, ids) * 1.001f + 1e-3f;
                int bestStorage = -1, bestSlot = -1;
                for (int s = 0; s < w.Run.Storage.Count; s++)
                    for (int slot = 0; slot < n; slot++)
                    {
                        var trial = (string[])ids.Clone();
                        trial[slot] = w.Run.Storage[s].BlockId;
                        float d = DpsWith(w, trial);
                        if (d > best) { best = d; bestStorage = s; bestSlot = slot; }
                    }
                if (bestStorage < 0) return;
                w.Enqueue(new RunCommand(RunCommandKind.LoadoutMove, 100 + bestStorage, 1 + bestSlot, null, 0));
                k.Step(god: true);
            }
        }

        public static string Table(List<WaveRow> rows)
        {
            var sb = new StringBuilder();
            sb.AppendLine("wave | boss | clear s | kills | spawned | maxAlive | gold | gold/min | score | fast | grazes | crystals | arts | enemyHP | holdDPS | HP/DPS s | whims/boss | species");
            for (int i = 0; i < rows.Count; i++)
            {
                var r = rows[i];
                float sec = r.ClearTicks / 60f;
                sb.AppendFormat("{0,3} | {1} | {2,6:0.0} | {3,4} | {4,4} | {5,3} | {6,5} | {7,6:0} | {8,6} | {9,5} | {10,3} | {11,3} | {12,3} | {13,8:0} | {14,6:0.0} | {15,6:0.0} | {17} | {16}",
                    r.Wave, r.Boss ? "B" : " ", sec, r.Kills, r.Spawned, r.MaxAlive, r.Gold, sec > 0 ? r.Gold * 60f / sec : 0f, r.Score, r.FastScore,
                    r.Grazes, r.CrystalsGained, r.ArtsFired, r.EnemyHp, r.HoldDps, r.HoldDps > 0 ? r.EnemyHp / r.HoldDps : 0f, r.Species, r.Whims);
                sb.AppendLine();
            }
            return sb.ToString();
        }
    }
}
