using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Wizard.Solo.Sim;

namespace Wizard.Solo.Tests.Integration
{
    /// <summary>결투 규칙(RunFlow.Duel.cs, docs/spec/10-duel.md 3절): 라운드·피해·순간 상한·승패·기권.</summary>
    public class DuelTests
    {
        IntegrationKit _k;
        readonly bool[,] _keys = new bool[2, 5];

        [SetUp] public void SetUp() { _k = new IntegrationKit(); System.Array.Clear(_keys, 0, _keys.Length); }
        [TearDown] public void TearDown() { _k.Dispose(); }

        RunConfig Member(int character)
        {
            var wand = _k.Profile.Characters[character - 1].EquippedWandId;
            var r = _k.Ctx.Wands.BuildRunConfig(character, wand, 1, 1UL);
            Assert.IsTrue(r.Ok);
            return r.Value;
        }

        SimWorld StartDuel()
        {
            var a = Member(1);
            a.Mode = RunMode.Duel;
            a.Party = new List<RunConfig> { Member(2) };
            return _k.Begin(a);
        }

        void Key(int slot, LogicalKey key, bool down)
        {
            int k = key == LogicalKey.Fire ? 4 : (int)key;
            if (_keys[slot, k] == down) return;
            _keys[slot, k] = down;
            _k.W.EnqueueInput(slot, new InputEvent(key, down, SimConst.TickStartU(_k.W.Tick)));
        }

        /// <summary>slot이 상대 쪽으로 걸어가며 J. 너무 가까우면 멈춰 쏜다.</summary>
        void Shoot(int slot)
        {
            var w = _k.W;
            var me = w.Players[slot];
            var them = w.Players[slot == 0 ? 1 : 0];
            Vector2 v = them.Pos - me.Pos;
            bool close = v.magnitude < 4f;
            Key(slot, LogicalKey.Fire, true);
            bool h = Mathf.Abs(v.x) >= Mathf.Abs(v.y);
            bool blink = (w.Tick & 1) == 0;
            Key(slot, LogicalKey.Right, h && v.x > 0 && (!close || blink));
            Key(slot, LogicalKey.Left, h && v.x < 0 && (!close || blink));
            Key(slot, LogicalKey.Down, !h && v.y > 0 && (!close || blink));
            Key(slot, LogicalKey.Up, !h && v.y < 0 && (!close || blink));
        }

        [Test]
        public void Duel_Start_TwoPlayersFaceEachOther_Countdown()
        {
            var w = StartDuel();
            Assert.AreEqual(RunPhase.DuelCountdown, w.Run.Phase);
            Assert.IsNotNull(w.Run.Duel);
            Assert.AreEqual(1, w.Run.Duel.Round);
            Assert.AreEqual(new Vector2(6f, 7f), w.Players[0].Pos);
            Assert.AreEqual(new Vector2(18f, 7f), w.Players[1].Pos);
            Assert.AreEqual(0, w.Enemies.Count, "결투에는 적이 없다");
            Key(0, LogicalKey.Right, true);
            _k.Step(60);
            Assert.AreEqual(new Vector2(6f, 7f), w.Players[0].Pos, "카운트다운 동안 움직이지 못한다");
            _k.Step(130);
            Assert.AreEqual(RunPhase.DuelFight, w.Run.Phase);
        }

        [Test]
        public void Duel_ShotsHurtOpponent_KnockoutWinsRound()
        {
            var w = StartDuel();
            _k.Step(185);
            int hp1 = w.Players[1].Hp;
            int t = 0;
            int maxShots = 0; float minDist = 99f; string style = w.Players[0].Loadout.Core.BlockId;
            for (; t < 6000 && w.Run.Phase == RunPhase.DuelFight; t++)
            {
                Shoot(0); _k.Step();
                maxShots = Mathf.Max(maxShots, w.Projectiles.Count);
                foreach (var pr in w.Projectiles) minDist = Mathf.Min(minDist, (pr.Pos - w.Players[1].Pos).magnitude);
            }
            TestContext.WriteLine("core " + style + " maxShots " + maxShots + " minDist " + minDist + " p0 " + w.Players[0].Pos + " p1 " + w.Players[1].Pos + " fired " + _k.Count(SimEventType.ProjectileSpawned));
            Assert.Less(w.Players[1].Hp, hp1, "슬롯 0의 탄이 슬롯 1을 맞힌다");
            Assert.AreEqual(RunPhase.DuelRoundEnd, w.Run.Phase, "라운드가 끝난다(t = " + t + ")");
            Assert.AreEqual(1, w.Run.Duel.Wins[0], "슬롯 0이 이겼다");
            Assert.GreaterOrEqual(_k.Count(SimEventType.DuelRoundEnded), 1);
            TestContext.WriteLine("녹아웃까지 " + t + "틱, 준 피해 " + w.Run.Duel.DamageDealt[0]);
        }

        [Test]
        public void Duel_BurstCap_LimitsDamageInHalfSecond()
        {
            var w = StartDuel();
            _k.Step(185);
            var q = w.Players[1];
            int max = q.MaxHp;
            int startHp = q.Hp;
            // 30틱 창 안에서 받는 피해의 합은 최대 체력의 30%를 넘지 않는다.
            int worst = 0;
            var hist = new List<int>();
            for (int t = 0; t < 1200 && w.Run.Phase == RunPhase.DuelFight; t++)
            {
                int before = q.Hp;
                Shoot(0);
                _k.Step();
                hist.Add(before - q.Hp);
                int sum = 0;
                for (int k = hist.Count - 1; k >= 0 && k >= hist.Count - 30; k--) sum += hist[k];
                if (sum > worst) worst = sum;
            }
            Assert.Greater(startHp - q.Hp, 0);
            Assert.LessOrEqual(worst, max * 30 / 100, "0.5초 창의 피해 ≤ 최대 체력 30%");
        }

        [Test]
        public void Duel_TwoRoundWins_EndsMatch()
        {
            var w = StartDuel();
            for (int t = 0; t < 40000 && w.Run.Phase != RunPhase.RunEnded; t++)
            {
                if (w.Run.Phase == RunPhase.DuelFight) Shoot(0);
                else { Key(0, LogicalKey.Fire, false); for (int i = 0; i < 4; i++) Key(0, (LogicalKey)i, false); }
                _k.Step();
            }
            Assert.AreEqual(RunPhase.RunEnded, w.Run.Phase);
            Assert.IsTrue(w.Run.Duel.MatchOver);
            Assert.AreEqual(0, w.Run.Duel.MatchWinner);
            Assert.AreEqual(2, w.Run.Duel.Wins[0]);
            Assert.AreEqual(1, _k.Count(SimEventType.DuelMatchEnded));
        }

        [Test]
        public void Duel_Timeout_HigherHpRatioWins()
        {
            var w = StartDuel();
            _k.Step(185);
            w.Players[1].Hp -= 100; // 슬롯 1이 덜 남았다
            _k.Step(SimConst.Ticks(90f) + 5);
            Assert.AreEqual(RunPhase.DuelRoundEnd, w.Run.Phase);
            Assert.AreEqual(0, w.Run.Duel.LastRoundWinner);
            Assert.AreEqual(DuelRoundEndKind.Timeout, w.Run.Duel.LastRoundEnd);
        }

        [Test]
        public void Duel_Timeout_Tie_GoesToOvertime()
        {
            var w = StartDuel();
            _k.Step(185);
            // 체력 비율 같음(둘 다 가득), 준 피해 0 → 연장
            _k.Step(SimConst.Ticks(90f) + 5);
            Assert.AreEqual(RunPhase.DuelFight, w.Run.Phase);
            Assert.IsTrue(w.Run.Duel.Overtime);
        }

        [Test]
        public void Duel_Forfeit_OpponentWinsMatch()
        {
            var w = StartDuel();
            _k.Step(200);
            w.Mods.Run.ForfeitDuel(w, 1);
            Assert.IsTrue(w.Run.Duel.MatchOver);
            Assert.AreEqual(0, w.Run.Duel.MatchWinner);
            _k.Step(SimConst.Ticks(3f));
            Assert.AreEqual(RunPhase.RunEnded, w.Run.Phase);
        }
    }
}
