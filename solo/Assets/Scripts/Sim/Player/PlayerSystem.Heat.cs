using UnityEngine;

namespace Wizard.Solo.Sim.Player
{
    // 과열: 1단계 잠금 풀림, 6단계 J 발사, 11단계 냉각, 잠금 들어가기·깨기, 방열, 열 링. 02 요약서 6절, 00 문서 5.4절 (가).
    public sealed partial class PlayerSystem
    {
        public override void Stage1_Timers(SimWorld w)
        {
            int t = w.Tick;
            for (int s = 0; s < w.Players.Length; s++)
            {
                PlayerState p = w.Players[s];
                if (!p.Active) continue;
                Bind(w, p);
                // 10단계에 켠 피격 표시는 다음 틱 1단계까지 유효하다.
                p.HitThisTick = false;
                if (p.Locked && t >= p.UnlockTick)
                {
                    p.Locked = false;
                    p.HeatHu = 0;
                    w.Emit(SimEventType.HeatUnlocked, p.Slot, a: (int)HeatUnlockReason.Timeout);
                }
            }
        }

        /// <summary>발사 1회의 열(HU): 컴파일 결과의 열에 거울상이면 ×1.5(02 요약서 6.2절). 면제·흡수 전 값.</summary>
        public int ShotHeatHu(SimWorld w, PlayerState p)
        {
            if (p.Spell == null) return 0;
            int h = p.Spell.HeatPerShotHu;
            if (p.Derived.MirrorImage) h = Mathf.RoundToInt(h * Tuning(w).MirrorHeatMul);
            return h;
        }

        /// <summary>지금 조립의 임계 사격 가능 횟수(−1 = 무한, 0 = 불가). 02 요약서 6.6절.</summary>
        public int CritShotCount(SimWorld w, PlayerState p) => PlayerRules.CritShotCount(p.Derived.CritStartHu, ShotHeatHu(w, p));

        // 02 요약서 6.3절 TryFire.
        public override void Stage6_Fire(SimWorld w)
        {
            int t = w.Tick;
            PlayerTuning tune = Tuning(w);
            bool frozen = HeatFrozen(w);
            for (int s = 0; s < w.Players.Length; s++)
            {
                PlayerState p = w.Players[s];
                if (!p.Active) continue;
                bool want = p.Keys.IsDown(LogicalKey.Fire) || p.JPressedThisTick;
                if (!want) continue;
                // 공통 규칙 7: 발사 허용은 AttackAllowed 하나로(야영의 J는 모닥불·출발 표식 전용).
                if (!w.AttackAllowed(p)) continue;
                if (p.Locked)
                {
                    if (p.JPressedThisTick) w.Emit(SimEventType.FireDryWhileLocked, p.Slot);
                    continue;
                }
                if (t < p.FireReadyTick) continue;
                CompiledSpell spell = p.Spell;
                if (spell == null || !spell.IsValid) continue;

                // 여기까지 왔으면 발사가 실제로 나간다. 불안정 핵은 정확히 한 번 묻는다.
                int interval = spell.FireIntervalTicks;
                int shotId = p.NextShotId++;
                bool unstable = w.Mods.Traits.RollUnstableCore(w, p);
                p.LastFireTick = t;
                p.LastFireIntervalTicks = interval;
                p.FireReadyTick = t + interval;

                if (unstable)
                {
                    // 자폭(D-04·D-05): 탄이 나가지 않고 열도 오르지 않는다. 잠금 = t + 간격 + 2초(이미 잠겼으면 더 긴 쪽), 콤보 0.
                    // 끓어넘침 여부는 Traits.OnHeatLockEntered(fromUnstableCore = true)가 가른다.
                    ResetCombo(w, p, ComboResetReason.UnstableCore);
                    EnterLock(w, p, interval, tune.UnstableCoreLockTicks, true);
                    w.Mods.Traits.OnPlayerFired(w, p, shotId, interval);
                    continue;
                }

                int h = ShotHeatHu(w, p);
                bool exemptUsed = false;
                if (p.HeatExemptArmed)
                {
                    // 스침 면제가 먼저다(불가마 흡수량은 그대로 남는다).
                    h = 0;
                    p.HeatExemptArmed = false;
                    exemptUsed = true;
                    w.Emit(SimEventType.HeatExemptConsumed, p.Slot);
                }
                // 임계는 "발사 뒤 열" 기준.
                bool isCrit = p.HeatHu + h >= p.Derived.CritStartHu;
                if (isCrit && !exemptUsed && h > 0)
                {
                    int absorbed = w.Mods.Arts.AbsorbKilnHeat(w, p, h);
                    if (absorbed > 0)
                    {
                        if (absorbed > h) absorbed = h;
                        h -= absorbed;
                        w.Emit(SimEventType.KilnAbsorbed, p.Slot, a: absorbed);
                    }
                }
                // 연습 방 "열 고정"(D-57): 열이 변하지 않고 잠금에 들어가지 않는다.
                if (!frozen) p.HeatHu += h;
                bool willLock = !frozen && p.HeatHu > PlayerTuning.HeatMaxHu;
                // 100을 넘긴 발사는 임계로 나간다.
                if (willLock) isCrit = true;

                var req = new ExecuteRequest
                {
                    OwnerSlot = p.Slot,
                    Tree = spell,
                    Pos = p.Pos,
                    DirDegrees = Dir8Util.ToDegrees(p.Facing),
                    DamageMul = 1f,
                    HeatExempt = exemptUsed,
                    IsCrit = isCrit,
                    Source = ShotSource.Fire,
                    ShotId = shotId,
                    ProjectileBudget = 0,
                    CharacterMul = w.Mods.Traits.CharacterMul(w, p, p.Crystals),
                    HitEffect = ShotHitEffect.None,
                    NoSpread = false
                };
                w.Mods.Spells.Execute(w, req);
                w.Emit(SimEventType.ShotFired, p.Slot, a: shotId, b: isCrit ? 1 : 0, f: p.HeatHu / (float)SimConst.HuPerHeat, pos: p.Pos);

                w.Mods.Arts.OnPlayerFired(w, p, shotId);
                w.Mods.Traits.OnPlayerFired(w, p, shotId, interval);
                if (willLock) EnterLock(w, p, interval, p.Derived.LockTicks, false);
            }
        }

        /// <summary>냉각(11단계). 대기는 마지막 발사의 간격이 끝난 뒤부터(D-10). 잠금 중·열 고정 중에는 식지 않는다.</summary>
        void Cool(SimWorld w, PlayerState p)
        {
            if (p.Locked || HeatFrozen(w) || p.HeatHu <= 0) return;
            int t = w.Tick;
            if (p.LastFireTick == t) return;
            if (t < (long)p.LastFireTick + p.LastFireIntervalTicks + p.Derived.CoolDelayTicks) return;
            p.HeatHu = Mathf.Max(0, p.HeatHu - p.Derived.CoolHuPerTick);
        }

        /// <summary>
        /// 과열 잠금에 넣는다: UnlockTick = t + intervalTicks + lockTicks, 이미 잠겼으면 더 긴 쪽.
        /// 새로 잠길 때만 Traits.OnHeatLockEntered를 부른다. 연습 방 "열 고정" 중에는 잠기지 않는다.
        /// </summary>
        public override void EnterLock(SimWorld w, PlayerState p, int intervalTicks, int lockTicks, bool fromUnstableCore)
        {
            if (p == null || !p.Active || HeatFrozen(w)) return;
            int unlock = w.Tick + intervalTicks + lockTicks;
            if (p.Locked)
            {
                if (unlock > p.UnlockTick) p.UnlockTick = unlock;
                return;
            }
            p.Locked = true;
            p.LockStartTick = w.Tick;
            p.UnlockTick = unlock;
            w.Emit(SimEventType.HeatLockEntered, p.Slot, a: unlock - w.Tick);
            w.Mods.Traits.OnHeatLockEntered(w, p, fromUnstableCore);
        }

        /// <summary>잠금을 깬다(스침, 방열 폭발, 초기화). 열은 heatAfterHu가 된다. 잠겨 있지 않았으면 열만 맞춘다.</summary>
        public override void BreakLock(SimWorld w, PlayerState p, HeatUnlockReason reason, int heatAfterHu)
        {
            if (p == null) return;
            bool wasLocked = p.Locked;
            p.Locked = false;
            p.HeatHu = Mathf.Clamp(heatAfterHu, 0, PlayerTuning.HeatMaxHu);
            if (wasLocked) w.Emit(SimEventType.HeatUnlocked, p.Slot, a: (int)reason);
        }

        /// <summary>방열 폭발용: 열(0~100, 잠금 중이면 100)을 돌려주고 열 0·잠금 해제. 열 고정 중에는 값만 돌려준다.</summary>
        public override float ConsumeHeatForVent(SimWorld w, PlayerState p)
        {
            if (p == null) return 0f;
            if (p.Locked)
            {
                BreakLock(w, p, HeatUnlockReason.VentBlast, 0);
                return PlayerTuning.HeatScaleMax;
            }
            // 내림(02 요약서 6.5절).
            float heat = p.HeatHu / SimConst.HuPerHeat;
            if (!HeatFrozen(w)) p.HeatHu = 0;
            return heat;
        }

        /// <summary>열을 깎는다(탐식). 잠금 중·열 고정 중에는 아무 일도 없다.</summary>
        public override void ReduceHeat(SimWorld w, PlayerState p, int hu)
        {
            if (p == null || p.Locked || hu <= 0 || HeatFrozen(w)) return;
            p.HeatHu = Mathf.Max(0, p.HeatHu - hu);
        }

        HeatRingState Ring(PlayerState p, PlayerTuning tune) =>
            PlayerRules.Ring(p.Locked, p.HeatHu, p.Derived.CritStartHu, tune.RingImminentBelowCritHu, tune.RingBrinkFromHu);

        public override HeatRingState RingState(SimWorld w, PlayerState p)
        {
            if (p == null) return HeatRingState.Cold;
            return Ring(p, w != null ? Tuning(w) : Tuning((Wizard.Solo.Data.IContentDb)null));
        }
    }
}
