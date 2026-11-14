using UnityEngine;

namespace Wizard.Solo.Sim.Player
{
    // 체력·피격·무적·쓰러짐. 02 요약서 5절, 00 문서 5.4절 (라), D-61·D-63.
    public sealed partial class PlayerSystem
    {
        /// <summary>
        /// 피격 후보를 올린다. false = 버렸다(무적, 쓰러짐, 피해 판정이 꺼진 단계). true = 후보로 올라갔다
        /// (같은 틱의 더 작은 AttackId에 밀려 실제 피해가 안 들어가도 true — 부른 쪽은 쿨다운을 소비한다. D-63).
        /// </summary>
        public override bool QueueHit(SimWorld w, PlayerState p, in PlayerHitRequest hit)
        {
            if (p == null || !p.Active) return false;
            if (!w.PlayerDamageActive) return false;
            if (p.Downed || p.Hp <= 0) return false;
            if (p.Invulnerable(w.Tick)) return false;
            // 같은 틱에 여럿이면 AttackId가 가장 작은 하나만 남긴다(02 요약서 5.2절 [임시]).
            if (!p.HasPendingHit || hit.AttackId < p.PendingHit.AttackId) p.PendingHit = hit;
            p.HasPendingHit = true;
            return true;
        }

        public override void Stage10_ResolveHits(SimWorld w)
        {
            PlayerTuning tune = Tuning(w);
            for (int s = 0; s < w.Players.Length; s++)
            {
                PlayerState p = w.Players[s];
                if (!p.HasPendingHit) continue;
                p.HasPendingHit = false;
                if (!p.Active || p.Downed || p.Hp <= 0) continue;
                PlayerHitRequest hit = p.PendingHit;

                // 칸 수는 이미 3칸 상한이 걸려 온다. 빚이 있으면 그 뒤에 +1칸(최대 4칸).
                int cells = hit.DamageCells;
                if (p.Debt > 0) cells += tune.DebtExtraDamageCells;
                int dmg = cells * SimConst.HpPerCell;
                p.Hp = Mathf.Max(0, p.Hp - dmg);
                p.InvulnUntilTick = Mathf.Max(p.InvulnUntilTick, w.Tick + InvulnTicks(p));
                ResetCombo(w, p, ComboResetReason.Hit);
                LoseCrystalsOnHit(w, p, tune);
                p.HitThisTick = true;
                w.Emit(SimEventType.PlayerHit, p.Slot, entityId: hit.SourceEnemyId, a: dmg, b: p.Hp, pos: hit.FromPos);

                w.Mods.Traits.OnPlayerHit(w, p);
                w.Mods.Run.OnPlayerHit(w, p);
                if (p.Hp == 0)
                {
                    p.Downed = true;
                    w.Emit(SimEventType.PlayerDowned, p.Slot);
                    w.Mods.Run.OnPlayerDowned(w, p);
                }
            }
        }

        static int InvulnTicks(PlayerState p)
        {
            if (p.Derived.InvulnTicks > 0) return p.Derived.InvulnTicks;
            return p.Def != null ? SimConst.Ticks(p.Def.InvulnSeconds) : 0;
        }

        /// <summary>
        /// 각본 피해로 바로 쓰러뜨린다(첫 만남 되비침). 무적·가동 여부·대기 중인 후보를 무시한다.
        /// 결정은 남기고(일어난 뒤 평소처럼 환전), Traits·Run의 피격 훅과 Run.OnPlayerDowned는 부르지 않는다.
        /// </summary>
        public override void ForceDown(SimWorld w, PlayerState p, PlayerDamageSource source)
        {
            if (p == null || !p.Active) return;
            p.HasPendingHit = false;
            int dmg = p.Hp;
            p.Hp = 0;
            p.Downed = true;
            p.Moving = false;
            ResetCombo(w, p, ComboResetReason.Hit);
            p.HitThisTick = true;
            w.Emit(SimEventType.PlayerHit, p.Slot, a: dmg, b: 0, pos: p.Pos);
            w.Emit(SimEventType.PlayerDowned, p.Slot);
        }

        /// <summary>무적을 늘린다(InvulnUntil = max). grazeWindow면 그동안 스침 1회만 허용한다(잔상 걸음).</summary>
        public override void GrantInvuln(SimWorld w, PlayerState p, int ticks, bool grazeWindow)
        {
            if (p == null || ticks <= 0) return;
            int until = w.Tick + ticks;
            if (until > p.InvulnUntilTick) p.InvulnUntilTick = until;
            if (grazeWindow)
            {
                p.ArtGrazeWindowUntilTick = until;
                p.ArtGrazeWindowUsed = false;
            }
        }

        public override void Heal(SimWorld w, PlayerState p, int cells)
        {
            if (p == null || !p.Active || p.Downed || cells <= 0) return;
            int before = p.Hp;
            p.Hp = Mathf.Min(p.MaxHp, p.Hp + cells * SimConst.HpPerCell);
            int healed = p.Hp - before;
            if (healed > 0)
                w.Emit(SimEventType.PlayerHealed, p.Slot, a: (healed + SimConst.HpPerCell - 1) / SimConst.HpPerCell);
        }

        /// <summary>첫 만남 일어남: 체력 가득, 열 0, 잠금 해제, 콤보 0, 빚 0, 무적 0, Downed = false. 결정은 남긴다.</summary>
        public override void Rise(SimWorld w, PlayerState p)
        {
            if (p == null || !p.Active) return;
            p.Hp = p.MaxHp;
            p.Downed = false;
            p.HasPendingHit = false;
            p.HitThisTick = false;
            p.InvulnUntilTick = 0;
            p.ArtGrazeWindowUntilTick = 0;
            BreakLock(w, p, HeatUnlockReason.Reset, 0);
            ResetCombo(w, p, ComboResetReason.Rise);
            if (p.Debt != 0)
            {
                p.Debt = 0;
                w.Emit(SimEventType.DebtRepaid, p.Slot, a: 0);
            }
            w.Emit(SimEventType.PlayerRose, p.Slot);
        }

        /// <summary>웨이브 끝 초기화(D-03): 콤보 0, 열 0·잠금 해제, 면제·가속·불가마 흡수 해제. 빚은 남는다. 결정 환전은 Run이 한다.</summary>
        public override void ResetForWaveEnd(SimWorld w, PlayerState p)
        {
            if (p == null || !p.Active) return;
            ResetCombo(w, p, ComboResetReason.WaveEnd);
            BreakLock(w, p, HeatUnlockReason.Reset, 0);
            p.HeatExemptArmed = false;
            if (w.Tick < p.GrazeBuffUntilTick) w.Emit(SimEventType.GrazeBuffEnded, p.Slot);
            p.GrazeBuffUntilTick = 0;
            p.KilnAbsorbLeftHu = 0;
            p.HasPendingHit = false;
        }
    }
}
