using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Wizard.Solo.Sim;

namespace Wizard.Solo.Tests.Integration
{
    /// <summary>
    /// 협동 1단계(11-online.md 4절): 한 세계에 4명(슬롯 0~3)이 서서 웨이브를 끝낸다. 진짜 모듈·콘텐츠, 입력은 슬롯마다 봇.
    /// </summary>
    public class CoopIntegrationTests
    {
        IntegrationKit _k;

        [SetUp] public void SetUp() { _k = new IntegrationKit(); System.Array.Clear(_keys, 0, _keys.Length); }
        [TearDown] public void TearDown() { _k.Dispose(); }

        RunConfig Member(int character, ulong seed)
        {
            var wand = _k.Profile.Characters[character - 1].EquippedWandId;
            var r = _k.Ctx.Wands.BuildRunConfig(character, wand, 1, seed);
            Assert.IsTrue(r.Ok, "BuildRunConfig " + character + ": " + r.Error);
            return r.Value;
        }

        SimWorld StartParty(int size, ulong seed)
        {
            return _k.StartRun(1, seed, cfg =>
            {
                cfg.Party = new List<RunConfig>();
                for (int c = 2; c <= size; c++) cfg.Party.Add(Member(c, seed));
            });
        }

        // ------------------------------------------------------------ 슬롯별 봇

        readonly bool[,] _keys = new bool[SimConst.MaxPlayers, 5];

        void Key(int slot, LogicalKey key, bool down)
        {
            int k = key == LogicalKey.Fire ? 4 : (int)key;
            if (_keys[slot, k] == down) return;
            _keys[slot, k] = down;
            _k.W.EnqueueInput(slot, new InputEvent(key, down, SimConst.TickStartU(_k.W.Tick)));
        }

        void ReleaseAll(int slot)
        {
            Key(slot, LogicalKey.Fire, false);
            for (int i = 0; i < 4; i++) Key(slot, (LogicalKey)i, false);
        }

        void Bot(int slot)
        {
            var w = _k.W;
            var p = w.Players[slot];
            EnemyState best = null;
            float bd = float.MaxValue;
            foreach (var e in w.Enemies)
            {
                if (!e.Targetable) continue;
                float d = (e.Pos - p.Pos).sqrMagnitude;
                if (d < bd) { bd = d; best = e; }
            }
            Key(slot, LogicalKey.Fire, true);
            if (best == null) { for (int i = 0; i < 4; i++) Key(slot, (LogicalKey)i, false); return; }
            Vector2 v = best.Pos - p.Pos;
            bool h = Mathf.Abs(v.x) > Mathf.Abs(v.y);
            bool close = v.sqrMagnitude < 2.5f * 2.5f;
            bool blink = (w.Tick & 1) == 0;
            Key(slot, LogicalKey.Left, h && v.x < 0 && (!close || blink));
            Key(slot, LogicalKey.Right, h && v.x > 0 && (!close || blink));
            Key(slot, LogicalKey.Up, !h && v.y < 0 && (!close || blink));
            Key(slot, LogicalKey.Down, !h && v.y > 0 && (!close || blink));
        }

        /// <summary>until까지 판을 돈다. 전투는 슬롯마다 봇, 전리품·야영은 kit과 같은 방식(첫 카드, 첫 비기, 출발). god = 전원 무적.</summary>
        bool Play(int maxTicks, System.Func<SimWorld, bool> until, bool god = true)
        {
            var w = _k.W;
            for (int t = 0; t < maxTicks; t++)
            {
                if (until(w)) return true;
                var run = w.Run;
                if (run.Phase == RunPhase.RunEnded) return until(w);
                if (run.Phase == RunPhase.LootPick)
                {
                    // 사람마다 자기 제시를 고른다(협동 보상은 사람마다 따로).
                    for (int s = 0; s < w.Players.Length; s++)
                    {
                        ReleaseAll(s);
                        var r = run.Rewards[s];
                        var loot = r.Loot;
                        if (!w.Players[s].Active || r.LootDone || loot == null) continue;
                        if (loot.OverflowToDiscard > 0) w.Enqueue(new RunCommand(RunCommandKind.LootDiscard, 100, slot: s));
                        else
                        {
                            for (int i = 0; i < loot.Cards.Count && i < loot.PickCount; i++) w.Enqueue(new RunCommand(RunCommandKind.LootPick, i, slot: s));
                            w.Enqueue(new RunCommand(RunCommandKind.LootConfirm, slot: s));
                        }
                    }
                }
                else if (w.InCamp)
                {
                    for (int s = 0; s < w.Players.Length; s++)
                    {
                        ReleaseAll(s);
                        if (!w.Players[s].Active) continue;
                        var r = run.Rewards[s];
                        if (r.ArtOffer != null && r.ArtOffer.Candidates.Count > 0)
                            w.Enqueue(new RunCommand(RunCommandKind.ArtPick, 0, 0, r.ArtOffer.Candidates[0], s));
                        else if (!r.DepartReady) w.Enqueue(new RunCommand(RunCommandKind.CampDepart, slot: s));
                    }
                }
                else
                {
                    for (int s = 0; s < w.Players.Length; s++) if (w.Players[s].Active) Bot(s);
                }
                if (god) foreach (var p in w.Players) if (p.Active) p.InvulnUntilTick = w.Tick + 2;
                _k.Step();
            }
            return until(w);
        }

        // ------------------------------------------------------------ 테스트

        [Test]
        public void Coop_FourPlayers_StandUp_WithOwnCharacters()
        {
            var w = StartParty(4, 7UL);
            for (int s = 0; s < 4; s++)
            {
                var p = w.Players[s];
                Assert.IsTrue(p.Active, "슬롯 " + s + " Active");
                Assert.AreEqual("char." + (s + 1), p.CharId, "슬롯 " + s + " 캐릭터");
                Assert.Greater(p.MaxHp, 0, "슬롯 " + s + " 체력");
                Assert.IsFalse(string.IsNullOrEmpty(p.Loadout.Core.BlockId), "슬롯 " + s + " 코어 블록");
            }
            for (int a = 0; a < 4; a++)
                for (int b = a + 1; b < 4; b++)
                    Assert.AreNotEqual(w.Players[a].Pos, w.Players[b].Pos, "슬롯 " + a + "·" + b + " 시작 자리가 겹치면 안 된다");
        }

        [Test]
        public void Coop_SoloConfig_StillOnlySlot0()
        {
            var w = _k.StartRun(1, 7UL);
            Assert.IsTrue(w.Players[0].Active);
            for (int s = 1; s < 4; s++) Assert.IsFalse(w.Players[s].Active, "솔로는 슬롯 " + s + "이 서지 않는다");
        }

        [Test]
        public void Coop_FourPlayers_ClearWave1_ReachWave2()
        {
            StartParty(4, 5UL);
            Assert.IsTrue(Play(9000, w => w.Run.Phase == RunPhase.LootPick),
                "4인 웨이브 1 클리어 → 전리품(Phase = " + _k.W.Run.Phase + ", 적 " + _k.W.Enemies.Count + ")");
            TestContext.WriteLine("4인 웨이브 1 클리어 " + _k.W.Tick + "틱, 처치 " + _k.W.Run.TotalKills);
            Assert.IsTrue(Play(3000, w => w.Run.Phase == RunPhase.WaveCombat && w.Wave != null && w.Wave.Wave == 2),
                "야영 뒤 웨이브 2 전투(Phase = " + _k.W.Run.Phase + ")");
            foreach (var p in _k.W.Players) Assert.IsTrue(p.Active && !p.Downed, "슬롯 " + p.Slot + "이 웨이브 2에 서 있다");
        }

        [Test]
        public void Coop_RunEndsOnlyWhenEveryoneIsDown()
        {
            var w = StartParty(3, 9UL);
            Assert.IsTrue(Play(3000, x => x.Run.Phase == RunPhase.WaveCombat), "웨이브 1 전투");
            for (int s = 0; s < 2; s++)
            {
                w.Mods.Player.ForceDown(w, w.Players[s], default(PlayerDamageSource));
                w.Mods.Run.OnPlayerDowned(w, w.Players[s]);
                Assert.AreNotEqual(RunPhase.RunEnded, w.Run.Phase, "동료가 서 있으면 판이 이어진다(쓰러진 " + (s + 1) + "명)");
            }
            w.Mods.Player.ForceDown(w, w.Players[2], default(PlayerDamageSource));
            w.Mods.Run.OnPlayerDowned(w, w.Players[2]);
            Assert.AreEqual(RunPhase.RunEnded, w.Run.Phase, "셋 다 쓰러지면 판이 끝난다");
        }
    
        // ------------------------------------------------------------ 부활(11-online.md, DataSolo/Coop.json revive)

        void Down(SimWorld w, int slot)
        {
            w.Mods.Player.ForceDown(w, w.Players[slot], default(PlayerDamageSource));
            w.Mods.Run.OnPlayerDowned(w, w.Players[slot]);
        }

        [Test]
        public void Coop_StandNextToDownedAlly_Revives()
        {
            var w = StartParty(2, 13UL);
            Assert.IsTrue(Play(3000, x => x.Run.Phase == RunPhase.WaveCombat), "웨이브 1 전투");
            Down(w, 1);
            Assert.IsTrue(w.Players[1].Downed);
            // 슬롯 0을 쓰러진 동료 바로 옆에 둔다(입력 없이 서 있게).
            for (int s = 0; s < 2; s++) ReleaseAll(s);
            w.Players[0].Pos = w.Players[1].Pos + new Vector2(0.5f, 0f);
            int revivedAt = -1;
            for (int t = 0; t < 400 && revivedAt < 0; t++)
            {
                w.Players[0].InvulnUntilTick = w.Tick + 2;
                w.Players[0].Pos = w.Players[1].Pos + new Vector2(0.5f, 0f);
                _k.Step();
                if (!w.Players[1].Downed) revivedAt = t;
            }
            Assert.GreaterOrEqual(revivedAt, 170, "3초(180틱) 가까이 곁에 있어야 일어난다");
            Assert.LessOrEqual(revivedAt, 190);
            Assert.AreEqual(SimConst.HpPerCell, w.Players[1].Hp, "체력 1칸으로 일어난다");
            Assert.IsTrue(w.Players[1].Invulnerable(w.Tick), "일어난 뒤 잠깐 무적");
            Assert.GreaterOrEqual(_k.Count(SimEventType.PlayerRevived), 1);
        }

        [Test]
        public void Coop_ReviveProgressResets_WhenHelperLeaves()
        {
            var w = StartParty(2, 14UL);
            Assert.IsTrue(Play(3000, x => x.Run.Phase == RunPhase.WaveCombat));
            Down(w, 1);
            for (int s = 0; s < 2; s++) ReleaseAll(s);
            for (int t = 0; t < 100; t++) { w.Players[0].InvulnUntilTick = w.Tick + 2; w.Players[0].Pos = w.Players[1].Pos + new Vector2(0.5f, 0f); _k.Step(); }
            Assert.Greater(w.Players[1].ReviveTicks, 90);
            w.Players[0].Pos = w.Players[1].Pos + new Vector2(6f, 0f);
            w.Players[0].InvulnUntilTick = w.Tick + 2;
            _k.Step();
            Assert.AreEqual(0, w.Players[1].ReviveTicks, "곁을 떠나면 처음부터");
            Assert.IsTrue(w.Players[1].Downed);
        }

        [Test]
        public void Coop_WaveClear_RaisesDownedAlly()
        {
            var w = StartParty(2, 15UL);
            Assert.IsTrue(Play(3000, x => x.Run.Phase == RunPhase.WaveCombat));
            Down(w, 1);
            Assert.IsTrue(Play(9000, x => x.Run.Phase == RunPhase.LootPick), "남은 한 명이 웨이브를 깬다(Phase = " + w.Run.Phase + ")");
            Assert.IsFalse(w.Players[1].Downed, "웨이브를 깨면 쓰러진 동료가 일어난다");
            Assert.AreEqual(SimConst.HpPerCell, w.Players[1].Hp);
        }

        // ------------------------------------------------------------ 사람마다 보상

        /// <summary>전리품 단계까지 간다(웨이브 1을 깬다).</summary>
        SimWorld ToLoot(int size, ulong seed)
        {
            var w = StartParty(size, seed);
            Assert.IsTrue(Play(12000, x => x.Run.Phase == RunPhase.LootPick), "웨이브 1 클리어(Phase = " + w.Run.Phase + ")");
            for (int s = 0; s < w.Players.Length; s++) ReleaseAll(s);
            return w;
        }

        void ConfirmFirstCards(SimWorld w, int slot)
        {
            var loot = w.Run.Rewards[slot].Loot;
            for (int i = 0; i < loot.Cards.Count && i < loot.PickCount; i++) w.Enqueue(new RunCommand(RunCommandKind.LootPick, i, slot: slot));
            w.Enqueue(new RunCommand(RunCommandKind.LootConfirm, slot: slot));
            _k.Step();
            if (w.Run.Rewards[slot].Loot != null && w.Run.Rewards[slot].Loot.OverflowToDiscard > 0) Assert.Inconclusive("넘침은 이 시험의 대상이 아니다");
        }

        /// <summary>야영에서 모든 사람의 비기 차례를 고른다.</summary>
        void PickAllArts(SimWorld w)
        {
            for (int guard = 0; guard < 20; guard++)
            {
                bool any = false;
                for (int s = 0; s < w.Players.Length; s++)
                {
                    var r = w.Run.Rewards[s];
                    if (!w.Players[s].Active || r.ArtOffer == null) continue;
                    w.Enqueue(new RunCommand(RunCommandKind.ArtPick, 0, 0, r.ArtOffer.Candidates[0], s));
                    any = true;
                }
                if (!any) return;
                _k.Step();
            }
        }

        [Test]
        public void Coop_EachPlayerGetsOwnLootOffer()
        {
            var w = ToLoot(2, 21UL);
            Assert.IsNotNull(w.Run.Rewards[0].Loot, "슬롯 0 제시");
            Assert.IsNotNull(w.Run.Rewards[1].Loot, "슬롯 1 제시");
            Assert.AreNotSame(w.Run.Rewards[0].Loot, w.Run.Rewards[1].Loot);
            Assert.IsFalse(w.Run.Rewards[0].LootDone);
            Assert.IsFalse(w.Run.Rewards[1].LootDone);
        }

        [Test]
        public void Coop_OnePlayersPick_DoesNotTouchTheOther_AndWaits()
        {
            var w = ToLoot(2, 22UL);
            int stored0 = w.Run.Rewards[0].Storage.Count;
            ConfirmFirstCards(w, 1);
            Assert.AreEqual(RunPhase.LootPick, w.Run.Phase, "슬롯 0이 아직 고르는 중이면 전리품 단계에 머문다");
            Assert.IsTrue(w.Run.Rewards[1].LootDone);
            Assert.IsFalse(w.Run.Rewards[0].LootDone);
            Assert.AreEqual(stored0, w.Run.Rewards[0].Storage.Count, "슬롯 1이 받은 블록은 슬롯 0의 보관함에 가지 않는다");
            foreach (var c in w.Run.Rewards[0].Loot.Cards) Assert.IsFalse(c.Picked, "슬롯 0의 카드는 그대로");

            ConfirmFirstCards(w, 0);
            Assert.IsTrue(w.InCamp, "둘 다 고르면 야영(Phase = " + w.Run.Phase + ")");
        }

        [Test]
        public void Coop_ArtOffers_ArePerPlayer()
        {
            var w = ToLoot(2, 23UL);
            ConfirmFirstCards(w, 0);
            ConfirmFirstCards(w, 1);
            Assert.IsTrue(w.InCamp);
            var o0 = w.Run.Rewards[0].ArtOffer;
            var o1 = w.Run.Rewards[1].ArtOffer;
            if (o0 == null || o1 == null) Assert.Inconclusive("웨이브 1 해금 차례가 없다(콘텐츠)");
            w.Enqueue(new RunCommand(RunCommandKind.ArtPick, 0, 0, o0.Candidates[0], 0));
            _k.Step();
            Assert.AreSame(o1, w.Run.Rewards[1].ArtOffer, "슬롯 0의 고르기가 슬롯 1의 차례를 쓰지 않는다");
            Assert.IsTrue(w.Players[0].UnlockedArts.Contains(o0.Candidates[0]));
        }

        [Test]
        public void Coop_CampScreen_IsPerPlayer_AndDepartWaitsForEveryone()
        {
            var w = ToLoot(2, 24UL);
            ConfirmFirstCards(w, 0);
            ConfirmFirstCards(w, 1);
            PickAllArts(w);
            Assert.IsTrue(w.InCamp);

            w.Enqueue(new RunCommand(RunCommandKind.CampOpen, slot: 1));
            _k.Step();
            Assert.AreEqual(CampSubState.CampScreen, w.Run.Rewards[1].CampSub, "슬롯 1만 야영 화면");
            Assert.AreNotEqual(CampSubState.CampScreen, w.Run.Rewards[0].CampSub, "슬롯 0의 화면은 열리지 않는다");
            Assert.IsTrue(w.PlayerControlActiveFor(w.Players[0]), "동료가 화면을 열어도 나는 걷는다");
            Assert.IsFalse(w.PlayerControlActiveFor(w.Players[1]));

            w.Enqueue(new RunCommand(RunCommandKind.CampDepart, slot: 0));
            _k.Step();
            Assert.IsTrue(w.InCamp, "슬롯 1이 아직이면 떠나지 않는다");
            Assert.IsTrue(w.Run.Rewards[0].DepartReady);

            w.Enqueue(new RunCommand(RunCommandKind.CampDepart, slot: 1));
            _k.Step();
            Assert.IsFalse(w.InCamp, "모두 준비되면 떠난다(Phase = " + w.Run.Phase + ")");
            Assert.IsFalse(w.Run.Rewards[0].DepartReady, "출발하면 준비 표시를 지운다");
        }

        [Test]
        public void Coop_PlayerLeft_DuringLoot_DoesNotBlock_AndStaysDown()
        {
            var w = ToLoot(2, 25UL);
            ConfirmFirstCards(w, 0);
            Assert.AreEqual(RunPhase.LootPick, w.Run.Phase);
            w.Mods.Player.ForceDown(w, w.Players[1], default(PlayerDamageSource));
            w.Mods.Run.OnPlayerDowned(w, w.Players[1]);
            w.Mods.Run.OnPlayerLeft(w, 1);
            Assert.IsTrue(w.InCamp, "나간 사람을 기다리지 않는다(Phase = " + w.Run.Phase + ")");
            PickAllArts(w);
            w.Enqueue(new RunCommand(RunCommandKind.CampDepart, slot: 0));
            _k.Step();
            Assert.IsFalse(w.InCamp, "남은 사람만 준비되면 떠난다");
            Assert.IsTrue(Play(12000, x => x.Run.Phase == RunPhase.LootPick), "웨이브 2 클리어");
            Assert.IsTrue(w.Players[1].Downed, "나간 사람은 웨이브를 깨도 일어나지 않는다");
            Assert.IsNull(w.Run.Rewards[1].Loot, "나간 사람에게는 제시가 없다");
        }

        [Test]
        public void Coop_ServerBot_FinishesOwnRewards()
        {
            var w = ToLoot(2, 26UL);
            var cmds = new List<RunCommand>();
            for (int t = 0; t < 10 && !w.Run.Rewards[1].LootDone; t++)
            {
                cmds.Clear();
                Wizard.Solo.Net.CoopBotRewards.Decide(w, 1, cmds);
                foreach (var c in cmds) w.Enqueue(c);
                _k.Step();
            }
            Assert.IsTrue(w.Run.Rewards[1].LootDone, "봇이 자기 전리품을 끝낸다");
            Assert.AreEqual(RunPhase.LootPick, w.Run.Phase, "사람(슬롯 0)을 기다린다");
            ConfirmFirstCards(w, 0);
            Assert.IsTrue(w.InCamp);
            for (int t = 0; t < 20 && !w.Run.Rewards[1].DepartReady; t++)
            {
                cmds.Clear();
                Wizard.Solo.Net.CoopBotRewards.Decide(w, 1, cmds);
                foreach (var c in cmds) w.Enqueue(c);
                _k.Step();
            }
            Assert.IsTrue(w.Run.Rewards[1].DepartReady, "봇이 비기를 고르고 출발 준비를 한다");
            Assert.IsTrue(w.InCamp, "사람이 출발하기 전에는 떠나지 않는다");
        }
}
}
