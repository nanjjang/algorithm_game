using System.Collections.Generic;
using UnityEngine;

namespace Wizard.Solo.Sim.Player
{
    // 콤보(10 요약서 1절)와 마력 결정(10 요약서 2절), 피격 시 결정 손실(02 요약서 5.3절).
    public sealed partial class PlayerSystem
    {
        // ------------------------------------------------------------ 콤보

        /// <summary>플레이어 쪽 피해가 적을 맞혔다. J 발사의 탄만 콤보에 넣는다(비기·사본·패시브·지대 제외).</summary>
        public override void OnPlayerHitEnemy(SimWorld w, in HitInfo hit)
        {
            if (hit.Origin != DamageOrigin.Projectile || hit.ShotSource != ShotSource.Fire) return;
            if (hit.AttackerSlot < 0 || hit.AttackerSlot >= w.Players.Length) return;
            PlayerState p = w.Players[hit.AttackerSlot];
            if (!p.Active) return;
            PlayerTuning tune = Tuning(w);

            // 상한에 걸린 적중도 끊김 타이머는 다시 채운다 [임시].
            p.ComboIdleTicks = 0;
            int gained;
            p.ShotComboGain.TryGetValue(hit.ShotId, out gained);
            // 적중 +1, 임계 적중 +2. 발사 1회로 오르는 콤보는 최대 +3.
            int inc = Mathf.Min(hit.IsCrit ? tune.ComboCritGain : 1, tune.ComboPerShotCap - gained);
            if (inc <= 0) return;
            p.ShotComboGain[hit.ShotId] = gained + inc;
            AddCombo(w, p, inc);
        }

        /// <summary>콤보를 올린다. combo_per_crystal의 배수를 "지날 때"마다 결정 1개(상한이면 콤보만 오른다).</summary>
        public override void AddCombo(SimWorld w, PlayerState p, int amount)
        {
            if (p == null || !p.Active || amount <= 0) return;
            int old = p.Combo;
            p.Combo += amount;
            int per = p.Def != null ? p.Def.ComboPerCrystal : 0;
            if (per > 0)
            {
                int crossed = p.Combo / per - old / per;
                for (int i = 0; i < crossed; i++) GainCrystal(w, p, CrystalSource.Combo);
            }
            w.Emit(SimEventType.ComboChanged, p.Slot, a: p.Combo, b: amount);
        }

        public override void ResetCombo(SimWorld w, PlayerState p, ComboResetReason reason)
        {
            if (p == null) return;
            int before = p.Combo;
            p.Combo = 0;
            p.ComboIdleTicks = 0;
            p.ShotComboGain.Clear();
            if (before > 0) w.Emit(SimEventType.ComboReset, p.Slot, a: (int)reason, b: before);
        }

        /// <summary>틱 끝: 2초 동안 못 맞히면 0. 과열 잠금 중에는 타이머가 멈춘다.</summary>
        void TickComboIdle(SimWorld w, PlayerState p, PlayerTuning tune)
        {
            if (p.Locked || p.Combo == 0) return;
            if (++p.ComboIdleTicks >= tune.ComboIdleTicks) ResetCombo(w, p, ComboResetReason.Timeout);
        }

        /// <summary>발사별 +3 상한의 기록은 그 발사의 탄이 모두 사라지면 지운다 [임시].</summary>
        void PruneShotComboGain(SimWorld w, PlayerState p, Extra x)
        {
            if (p.ShotComboGain.Count == 0) return;
            List<int> gone = x.Scratch;
            gone.Clear();
            foreach (KeyValuePair<int, int> kv in p.ShotComboGain)
            {
                bool live = false;
                for (int i = 0; i < w.Projectiles.Count; i++)
                {
                    ProjectileEntity pr = w.Projectiles[i];
                    if (!pr.Dead && pr.OwnerSlot == p.Slot && pr.ShotId == kv.Key) { live = true; break; }
                }
                if (!live) gone.Add(kv.Key);
            }
            for (int i = 0; i < gone.Count; i++) p.ShotComboGain.Remove(gone[i]);
            gone.Clear();
        }

        // ------------------------------------------------------------ 결정

        static int CrystalCap(PlayerState p) => p.Def != null ? p.Def.CrystalCap : 0;

        int MaxDebt(PlayerState p, PlayerTuning tune) => p.Derived.MaxDebt > 0 ? p.Derived.MaxDebt : tune.MaxDebt;

        /// <summary>연습 방 "결정 무한"이 켜져 있나.</summary>
        static bool InfiniteCrystal(SimWorld w) => w != null && w.Run != null && w.Run.PracticeInfiniteCrystal;

        /// <summary>결정 1개 얻기. 빚이 있으면 빚부터 갚는다. 얻었으면(갚았으면) true, 상한이면 false.</summary>
        public override bool GainCrystal(SimWorld w, PlayerState p, CrystalSource source)
        {
            if (p == null || !p.Active) return false;
            Bind(w, p);
            if (p.Debt > 0)
            {
                p.Debt -= 1;
                w.Emit(SimEventType.DebtRepaid, p.Slot, a: p.Debt);
                return true;
            }
            int cap = CrystalCap(p);
            if (p.Crystals >= cap)
            {
                w.Emit(SimEventType.CrystalGainBlocked, p.Slot);
                return false;
            }
            p.Crystals += 1;
            w.Emit(SimEventType.CrystalGained, p.Slot, a: p.Crystals, b: (int)source);
            if (p.Crystals == cap) w.Emit(SimEventType.CrystalsFull, p.Slot);
            return true;
        }

        /// <summary>결정 값을 치를 수 있나. 모자라면 외상(지팡이 특성)이 되는지 본다: 빚 + 모자란 수 ≤ 빚 상한.</summary>
        public override bool CanAfford(SimWorld w, PlayerState p, int cost, bool allowCredit)
        {
            if (p == null) return false;
            if (InfiniteCrystal(w)) return true;
            if (p.Crystals >= cost) return true;
            if (!allowCredit || !p.Derived.HasCredit) return false;
            int maxDebt = p.Derived.MaxDebt;
            if (maxDebt <= 0 && w != null) maxDebt = Tuning(w).MaxDebt;
            return p.Debt + (cost - p.Crystals) <= maxDebt;
        }

        /// <summary>결정을 쓴다(CanAfford가 참일 때만 부른다). 돌려주는 값 = 새로 진 빚. 결정 무한이면 줄지 않는다.</summary>
        public override int SpendCrystals(SimWorld w, PlayerState p, int cost, bool allowCredit)
        {
            if (p == null || cost <= 0) return 0;
            Bind(w, p);
            if (w.Run != null && w.Run.PracticeInfiniteCrystal)
            {
                w.Mods.Run.OnCrystalsSpent(w, p, cost);
                return 0;
            }
            int use = Mathf.Min(p.Crystals, cost);
            p.Crystals -= use;
            int shortBy = cost - use;
            int newDebt = 0;
            if (shortBy > 0 && allowCredit && p.Derived.HasCredit)
            {
                int room = MaxDebt(p, Tuning(w)) - p.Debt;
                newDebt = Mathf.Clamp(shortBy, 0, Mathf.Max(0, room));
                if (newDebt > 0)
                {
                    p.Debt += newDebt;
                    w.Emit(SimEventType.DebtAdded, p.Slot, a: p.Debt);
                }
            }
            if (use > 0) w.Emit(SimEventType.CrystalSpent, p.Slot, a: use);
            if (use + newDebt > 0) w.Mods.Run.OnCrystalsSpent(w, p, use + newDebt);
            return newDebt;
        }

        /// <summary>결정을 전부 쓴다(만개). 돌려주는 값 = 쓴 개수. 결정 무한이면 가진 수를 돌려주고 줄지 않는다.</summary>
        public override int SpendAllCrystals(SimWorld w, PlayerState p)
        {
            if (p == null) return 0;
            Bind(w, p);
            int n = p.Crystals;
            if (n > 0) w.Mods.Run.OnCrystalsSpent(w, p, n);
            if (w.Run != null && w.Run.PracticeInfiniteCrystal) return n;
            p.Crystals = 0;
            if (n > 0) w.Emit(SimEventType.CrystalSpent, p.Slot, a: n);
            return n;
        }

        /// <summary>
        /// 피격 시 결정 손실(02 요약서 5.3절): 빚이 있으면 결정 파괴 대신 빚 +1(상한까지),
        /// 아니면 min(crystal_loss_on_hit, 가진 수)개가 깨진다. 손실 0(얼린 결정)이면 지켜졌다는 이벤트만.
        /// </summary>
        void LoseCrystalsOnHit(SimWorld w, PlayerState p, PlayerTuning tune)
        {
            if (p.Debt > 0)
            {
                int max = MaxDebt(p, tune);
                if (p.Debt < max)
                {
                    p.Debt += 1;
                    w.Emit(SimEventType.DebtAdded, p.Slot, a: p.Debt);
                }
                return;
            }
            if (p.Crystals <= 0) return;
            int loss = p.Def != null ? p.Def.CrystalLossOnHit : 1;
            if (p.Derived.KeepCrystal) loss = 0;
            if (loss <= 0)
            {
                w.Emit(SimEventType.CrystalProtected, p.Slot);
                return;
            }
            int lost = Mathf.Min(loss, p.Crystals);
            p.Crystals -= lost;
            w.Emit(SimEventType.CrystalLost, p.Slot, a: lost);
        }
    }
}
