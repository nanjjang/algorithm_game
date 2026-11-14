using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Wizard.Solo.Sim;

namespace Wizard.Solo.Tests.Player
{
    /// <summary>체력·피격·무적·쓰러짐과 계약(QueueHit, ForceDown, Rise). 02 요약서 5절, 00 문서 5.4절 (라).</summary>
    public class PlayerHitTests
    {
        const int Hu = 120;

        // T-P01: 틱 100에 피격 → 틱 159에 또 닿음 → 무시(무적). T-P02: 틱 160에 닿음 → 피격.
        [Test]
        public void S01_T_P01_P02__InvulnSixtyTicksBoundary()
        {
            var k = Kit.Create();
            var results = new List<bool>();
            k.HitOnTick(100, 1, 1, results);
            k.HitOnTick(159, 2, 1, results);
            k.HitOnTick(160, 3, 1, results);
            k.StepTo(101);
            Assert.AreEqual(500, k.P.Hp);
            Assert.AreEqual(160, k.P.InvulnUntilTick);
            k.StepTo(160);
            Assert.AreEqual(500, k.P.Hp, "틱 159의 공격은 무적에 막힌다");
            k.Step();
            Assert.AreEqual(400, k.P.Hp, "틱 160의 공격은 들어간다");
            CollectionAssert.AreEqual(new[] { true, false, true }, results);
            Assert.AreEqual(2, k.Count(SimEventType.PlayerHit));
            Assert.AreEqual(2, k.Traits.PlayerHitCalls);
            Assert.AreEqual(2, k.Run.HitCalls);
        }

        // 무적이 끝나는 틱(159)의 끝에 InvulnEnded.
        [Test]
        public void S01_2_5__InvulnEndedEventAtLastInvulnTick()
        {
            var k = Kit.Create();
            k.HitOnTick(100, 1, 1);
            k.StepTo(159);
            Assert.AreEqual(0, k.Count(SimEventType.InvulnEnded));
            k.Step();
            Assert.AreEqual(1, k.Count(SimEventType.InvulnEnded));
            Assert.IsFalse(k.P.Invulnerable(160));
        }

        // T-P05: 같은 틱에 공격 id 7(1칸), id 4(2칸) → id 4만 들어간다(2칸). 밀린 후보도 QueueHit은 true(D-63).
        [TestCase(true)]
        [TestCase(false)]
        public void S01_T_P05__SameTickSmallestAttackIdWins(bool sevenFirst)
        {
            var k = Kit.Create();
            var results = new List<bool>();
            if (sevenFirst) { k.HitOnTick(3, 7, 1, results); k.HitOnTick(3, 4, 2, results); }
            else { k.HitOnTick(3, 4, 2, results); k.HitOnTick(3, 7, 1, results); }
            k.StepTo(4);
            Assert.AreEqual(600 - 200, k.P.Hp);
            CollectionAssert.AreEqual(new[] { true, true }, results);
            Assert.AreEqual(1, k.Count(SimEventType.PlayerHit));
            SimEvent e;
            Assert.IsTrue(k.TryLast(SimEventType.PlayerHit, out e));
            Assert.AreEqual(200, e.A);
            Assert.AreEqual(400, e.B);
        }

        // 계약: QueueHit은 무적·쓰러짐·피해 판정이 꺼진 단계(!PlayerDamageActive)에서 false.
        [Test]
        public void Contract__QueueHitReturnsFalseWhenInvulnerable()
        {
            var k = Kit.Create();
            k.Sys.GrantInvuln(k.W, k.P, 12, false);
            var req = new PlayerHitRequest { AttackId = 1, DamageCells = 1 };
            Assert.IsFalse(k.Sys.QueueHit(k.W, k.P, req));
            Assert.IsFalse(k.P.HasPendingHit);
        }

        [TestCase(RunPhase.WaveCombat, true)]
        [TestCase(RunPhase.Practice, true)]
        [TestCase(RunPhase.WavePrelude, false)]
        [TestCase(RunPhase.BossIntro, false)]
        [TestCase(RunPhase.WaveClearing, false)]
        [TestCase(RunPhase.PhantomFinale, false)]
        [TestCase(RunPhase.Camp, false)]
        [TestCase(RunPhase.RunEnded, false)]
        public void Contract__QueueHitFollowsPlayerDamageActive(RunPhase phase, bool accepted)
        {
            var k = Kit.Create();
            k.W.Run.Phase = phase;
            var req = new PlayerHitRequest { AttackId = 1, DamageCells = 1 };
            Assert.AreEqual(accepted, k.Sys.QueueHit(k.W, k.P, req));
            Assert.AreEqual(accepted, k.P.HasPendingHit);
            k.Step();
            Assert.AreEqual(accepted ? 500 : 600, k.P.Hp);
        }

        [Test]
        public void Contract__QueueHitReturnsFalseWhenDowned()
        {
            var k = Kit.Create();
            k.P.Downed = true;
            Assert.IsFalse(k.Sys.QueueHit(k.W, k.P, new PlayerHitRequest { AttackId = 1, DamageCells = 1 }));
        }

        // 계약: HitThisTick은 10단계에 켜지고 다음 틱 1단계에 꺼진다.
        [Test]
        public void Contract__HitThisTickClearedAtNextTickStage1()
        {
            var k = Kit.Create();
            k.HitOnTick(2, 1, 1);
            k.StepTo(2);
            Assert.IsFalse(k.P.HitThisTick);
            k.Step();
            Assert.IsTrue(k.P.HitThisTick, "그 틱이 끝난 뒤에도(표시가 읽을 때까지) 켜져 있다");
            Assert.IsFalse(k.P.HasPendingHit);
            k.Step();
            Assert.IsFalse(k.P.HitThisTick);
        }

        // 5.2절: 피격 → 콤보 0(이유 Hit).
        [Test]
        public void S01_6_2__HitResetsCombo()
        {
            var k = Kit.Create();
            k.P.Combo = 9;
            k.HitOnTick(0, 1, 1);
            k.Step();
            Assert.AreEqual(0, k.P.Combo);
            SimEvent e;
            Assert.IsTrue(k.TryLast(SimEventType.ComboReset, out e));
            Assert.AreEqual((int)ComboResetReason.Hit, e.A);
            Assert.AreEqual(9, e.B);
        }

        // 5.4절: 체력 0 → 쓰러짐. PlayerDowned 이벤트, Run.OnPlayerDowned 한 번. 그 뒤로는 후보를 받지 않는다.
        [Test]
        public void S01_6_3__HpZeroDownsAndCallsRunOnce()
        {
            var k = Kit.Create("char.2");   // 체력 4칸
            k.Run.EndRunOnDowned = true;
            k.HitOnTick(0, 1, 3);
            k.HitOnTick(60, 2, 3);
            k.StepTo(1);
            Assert.AreEqual(100, k.P.Hp);
            Assert.IsFalse(k.P.Downed);
            Assert.AreEqual(1, k.P.HpCells);
            k.StepTo(61);
            Assert.AreEqual(0, k.P.Hp);
            Assert.IsTrue(k.P.Downed);
            Assert.AreEqual(1, k.Count(SimEventType.PlayerDowned));
            Assert.AreEqual(1, k.Run.DownedCalls);
            Assert.AreEqual(RunPhase.RunEnded, k.W.Run.Phase);
            Assert.IsNull(k.W.NearestPlayer(Vector2.zero));
            k.W.Run.Phase = RunPhase.WaveCombat;
            Assert.IsFalse(k.Sys.QueueHit(k.W, k.P, new PlayerHitRequest { AttackId = 9, DamageCells = 1 }));
        }

        // 5.2절·8.3절(제자 5): 빚이 있으면 받는 피해 +1칸(3칸 상한 뒤 → 최대 4칸. 체력 5칸이라 가득 찬 체력에서 한 방에 안 죽는다).
        // 5.3절: 빚이 있을 때 맞으면 결정 파괴 대신 빚 +1(최대 2).
        [Test]
        public void S04_7_4__DebtAddsOneCellAndGrowsOnHit()
        {
            var k = Kit.Create("char.5", kit => { kit.Traits.Credit = true; kit.Traits.MaxDebt = 2; });
            k.P.Debt = 1;
            k.HitOnTick(0, 1, 3);
            k.Step();
            Assert.AreEqual(500 - 400, k.P.Hp);
            Assert.IsFalse(k.P.Downed);
            Assert.AreEqual(2, k.P.Debt);
            Assert.AreEqual(0, k.P.Crystals);

            var b = Kit.Create("char.5", kit => { kit.Traits.Credit = true; kit.Traits.MaxDebt = 2; });
            b.P.Debt = 2;
            b.HitOnTick(0, 1, 1);
            b.Step();
            Assert.AreEqual(500 - 200, b.P.Hp);
            Assert.AreEqual(2, b.P.Debt, "빚은 2를 넘지 않는다");
        }

        // 5.3절 결정 손실: 그 밖 −min(1, 결정) / 제자 4 그대로(CrystalProtected) / 제자 5 −min(2, 결정), 1개면 0개·빚은 생기지 않음.
        [TestCase(null, 3, 2)]
        [TestCase("char.1", 1, 0)]
        [TestCase("char.2", 0, 0)]
        [TestCase("char.4", 3, 3)]
        [TestCase("char.5", 3, 1)]
        [TestCase("char.5", 1, 0)]
        [TestCase("char.5", 0, 0)]
        public void S01_10_4__CrystalLossOnHit(string charId, int before, int after)
        {
            var k = Kit.Create(charId);
            k.P.Crystals = before;
            k.HitOnTick(0, 1, 1);
            k.Step();
            Assert.AreEqual(after, k.P.Crystals);
            Assert.AreEqual(0, k.P.Debt);
            bool protectedCase = charId == "char.4" && before > 0;
            Assert.AreEqual(protectedCase ? 1 : 0, k.Count(SimEventType.CrystalProtected));
            Assert.AreEqual(before > after ? 1 : 0, k.Count(SimEventType.CrystalLost));
            if (before > after)
            {
                SimEvent e;
                Assert.IsTrue(k.TryLast(SimEventType.CrystalLost, out e));
                Assert.AreEqual(before - after, e.A);
            }
        }

        // 5.3절 불변식(제자 5): 빚 > 0 ⇒ 결정 == 0. 얻기·쓰기·피격을 섞어 돌려도 깨지지 않는다.
        [Test]
        public void S01_10_4__DebtImpliesNoCrystalsInvariant()
        {
            var k = Kit.Create("char.5", kit => { kit.Traits.Credit = true; kit.Traits.MaxDebt = 2; });
            var rng = new Pcg32(20240607UL);
            for (int i = 0; i < 2000; i++)
            {
                int op = rng.Range(0, 5);
                if (op == 0) k.Sys.GainCrystal(k.W, k.P, CrystalSource.Combo);
                else if (op == 1) k.Sys.GainCrystal(k.W, k.P, CrystalSource.Starfall);
                else if (op == 2)
                {
                    int cost = rng.Range(1, 4);
                    if (k.Sys.CanAfford(k.W, k.P, cost, true)) k.Sys.SpendCrystals(k.W, k.P, cost, true);
                }
                else if (op == 3) k.Sys.SpendAllCrystals(k.W, k.P);
                else
                {
                    k.P.Hp = k.P.MaxHp;
                    k.P.InvulnUntilTick = 0;
                    k.Sys.QueueHit(k.W, k.P, new PlayerHitRequest { AttackId = 1, DamageCells = 1 });
                    k.Sys.Stage10_ResolveHits(k.W);
                }
                Assert.IsTrue(k.P.Debt >= 0 && k.P.Debt <= 2, "step " + i);
                Assert.IsTrue(k.P.Crystals >= 0 && k.P.Crystals <= 7, "step " + i);
                if (k.P.Debt > 0) Assert.AreEqual(0, k.P.Crystals, "step " + i);
            }
        }

        // 계약(D-61): ForceDown — 무적 중에도, 피해 판정이 꺼진 단계에서도 쓰러진다. Run.OnPlayerDowned는 부르지 않는다.
        [Test]
        public void Contract__ForceDownIgnoresInvulnAndDoesNotCallRun()
        {
            var k = Kit.Create();
            k.W.Run.Phase = RunPhase.PhantomFinale;
            k.Sys.GrantInvuln(k.W, k.P, 600, false);
            k.P.Combo = 5;
            k.P.Crystals = 2;
            k.P.PendingHit = new PlayerHitRequest { AttackId = 3, DamageCells = 1 };
            k.P.HasPendingHit = true;
            k.Sys.ForceDown(k.W, k.P, PlayerDamageSource.Scripted);
            Assert.AreEqual(0, k.P.Hp);
            Assert.IsTrue(k.P.Downed);
            Assert.IsFalse(k.P.HasPendingHit);
            Assert.AreEqual(0, k.P.Combo);
            Assert.AreEqual(2, k.P.Crystals, "결정은 남는다(일어난 뒤 환전)");
            Assert.AreEqual(1, k.Count(SimEventType.PlayerHit));
            Assert.AreEqual(1, k.Count(SimEventType.PlayerDowned));
            Assert.AreEqual(0, k.Run.DownedCalls);
            SimEvent e;
            Assert.IsTrue(k.TryLast(SimEventType.PlayerHit, out e));
            Assert.AreEqual(600, e.A);
            Assert.AreEqual(0, e.B);
            k.Step(3);
            Assert.AreEqual(0, k.Run.DownedCalls);
            Assert.AreEqual(1, k.Count(SimEventType.PlayerDowned));
        }

        // 5.4절 일어남: 체력 가득, 열 0, 잠금 풀림, 콤보 0, 빚 0, 무적 0, Downed = false. 결정은 남는다. PlayerRose.
        [Test]
        public void S01_6_3__Rise()
        {
            var k = Kit.Create("char.5", kit => { kit.Traits.Credit = true; kit.Traits.MaxDebt = 2; });
            k.P.Debt = 2;
            k.P.Combo = 4;
            k.P.HeatHu = 104 * Hu;
            k.P.Locked = true;
            k.P.UnlockTick = 900;
            k.Sys.ForceDown(k.W, k.P, PlayerDamageSource.Scripted);
            k.P.InvulnUntilTick = 500;
            k.Sys.Rise(k.W, k.P);
            Assert.AreEqual(500, k.P.Hp);
            Assert.IsFalse(k.P.Downed);
            Assert.AreEqual(0, k.P.HeatHu);
            Assert.IsFalse(k.P.Locked);
            Assert.AreEqual(0, k.P.Combo);
            Assert.AreEqual(0, k.P.Debt);
            Assert.AreEqual(0, k.P.InvulnUntilTick);
            Assert.AreEqual(1, k.Count(SimEventType.PlayerRose));
            Assert.IsNotNull(k.W.NearestPlayer(Vector2.zero));

            var c = Kit.Create();
            c.P.Crystals = 3;
            c.Sys.ForceDown(c.W, c.P, PlayerDamageSource.Scripted);
            c.Sys.Rise(c.W, c.P);
            Assert.AreEqual(3, c.P.Crystals);
        }

        // 01 요약서 8절·D-03: 웨이브 끝 — 콤보 0, 열 0·잠금 해제, 면제·가속·불가마 흡수 해제. 빚은 남는다.
        [Test]
        public void S01_10_6_D03__ResetForWaveEnd()
        {
            var k = Kit.Create("char.5", kit => { kit.Traits.Credit = true; kit.Traits.MaxDebt = 2; });
            k.P.Combo = 11;
            k.P.HeatHu = 104 * Hu;
            k.P.Locked = true;
            k.P.UnlockTick = 900;
            k.P.HeatExemptArmed = true;
            k.P.GrazeBuffUntilTick = 80;
            k.P.KilnAbsorbLeftHu = 700;
            k.P.Debt = 1;
            k.Sys.ResetForWaveEnd(k.W, k.P);
            Assert.AreEqual(0, k.P.Combo);
            Assert.AreEqual(0, k.P.HeatHu);
            Assert.IsFalse(k.P.Locked);
            Assert.IsFalse(k.P.HeatExemptArmed);
            Assert.AreEqual(0, k.P.GrazeBuffUntilTick);
            Assert.AreEqual(0, k.P.KilnAbsorbLeftHu);
            Assert.AreEqual(1, k.P.Debt);
            SimEvent e;
            Assert.IsTrue(k.TryLast(SimEventType.ComboReset, out e));
            Assert.AreEqual((int)ComboResetReason.WaveEnd, e.A);
            Assert.IsTrue(k.TryLast(SimEventType.HeatUnlocked, out e));
            Assert.AreEqual((int)HeatUnlockReason.Reset, e.A);

            var c = Kit.Create();
            c.P.Crystals = 4;
            c.Sys.ResetForWaveEnd(c.W, c.P);
            Assert.AreEqual(4, c.P.Crystals, "결정 환전은 Run이 한다");
        }

        // 5.2절: 비기 무적은 InvulnUntil = max(InvulnUntil, t + n).
        [Test]
        public void S01_6_2__GrantInvulnTakesMax()
        {
            var k = Kit.Create();
            k.StepTo(10);
            k.Sys.GrantInvuln(k.W, k.P, 12, false);
            Assert.AreEqual(22, k.P.InvulnUntilTick);
            k.Sys.GrantInvuln(k.W, k.P, 5, false);
            Assert.AreEqual(22, k.P.InvulnUntilTick);
            k.Sys.GrantInvuln(k.W, k.P, 30, false);
            Assert.AreEqual(40, k.P.InvulnUntilTick);
            Assert.AreEqual(0, k.P.ArtGrazeWindowUntilTick);
            k.P.ArtGrazeWindowUsed = true;
            k.Sys.GrantInvuln(k.W, k.P, 9, true);
            Assert.AreEqual(40, k.P.InvulnUntilTick);
            Assert.AreEqual(19, k.P.ArtGrazeWindowUntilTick);
            Assert.IsFalse(k.P.ArtGrazeWindowUsed);
            // 피격 무적도 더 긴 쪽을 지킨다.
            k.P.InvulnUntilTick = 0;
            k.Sys.QueueHit(k.W, k.P, new PlayerHitRequest { AttackId = 1, DamageCells = 1 });
            k.P.InvulnUntilTick = 500;
            k.Sys.Stage10_ResolveHits(k.W);
            Assert.AreEqual(500, k.P.InvulnUntilTick);
        }

        [Test]
        public void Heal_CapsAtMaxAndEmitsCells()
        {
            var k = Kit.Create();
            k.P.Hp = 250;
            k.Sys.Heal(k.W, k.P, 2);
            Assert.AreEqual(450, k.P.Hp);
            SimEvent e;
            Assert.IsTrue(k.TryLast(SimEventType.PlayerHealed, out e));
            Assert.AreEqual(2, e.A);
            k.Sys.Heal(k.W, k.P, 9);
            Assert.AreEqual(600, k.P.Hp);
            k.W.Events.Clear();
            k.Sys.Heal(k.W, k.P, 1);
            Assert.AreEqual(0, k.Count(SimEventType.PlayerHealed));
        }
    }
}
