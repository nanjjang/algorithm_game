using System.Collections.Generic;
using UnityEngine;

namespace Wizard.Solo.Sim.Player
{
    // 스쳐 피하기: 9단계 판정과 보상. 02 요약서 7절, 00 문서 5.4절 (마), 10 요약서 8절 Q-13.
    public sealed partial class PlayerSystem
    {
        public override void Stage9_GrazeAndPickups(SimWorld w)
        {
            int t = w.Tick;
            for (int s = 0; s < w.Players.Length; s++)
            {
                PlayerState p = w.Players[s];
                if (!p.Active || p.Def == null) continue;
                // 이번 틱에 피격 후보가 올라왔으면 이 플레이어의 이번 틱 스침은 전부 버린다(HitThisTick이 아니라 HasPendingHit).
                if (p.HasPendingHit) continue;
                // 쓰러진 뒤에는 인정하지 않는다(Q-13).
                if (p.Downed || p.Hp <= 0) continue;
                bool invuln = p.Invulnerable(t);
                // 무적 중에는 인정하지 않는다. 잔상 걸음의 창만 1회 허용.
                bool window = t < p.ArtGrazeWindowUntilTick && !p.ArtGrazeWindowUsed;
                if (invuln && !window) continue;

                Extra x = Bind(w, p);
                List<GrazeCandidate> list = x.Graze;
                list.Clear();
                w.Mods.Threats.CollectGraze(w, p, list);
                if (list.Count == 0) continue;
                SortByThreatId(list);

                float hitR = p.Def.HitRadius, grazeR = p.Def.GrazeRadius;
                for (int i = 0; i < list.Count; i++)
                {
                    GrazeCandidate c = list[i];
                    // 같은 시전에서는 1회만.
                    if (p.GrazedCasts.Contains(c.CastId)) continue;
                    // 감지 원과는 겹치고 피격 원과는 안 겹친다: hit_radius < 겉면 거리 ≤ graze_radius (구역은 D-09).
                    if (!(c.SurfaceDistance > hitR && c.SurfaceDistance <= grazeR)) continue;
                    if (invuln)
                    {
                        if (p.ArtGrazeWindowUsed) break;
                        p.ArtGrazeWindowUsed = true;
                    }
                    AwardGraze(w, p, c.CastId, c.Kind, c.Pos);
                }
                list.Clear();
            }
        }

        /// <summary>위협 id 오름차순(안정).</summary>
        static void SortByThreatId(List<GrazeCandidate> list)
        {
            for (int i = 1; i < list.Count; i++)
            {
                GrazeCandidate c = list[i];
                int j = i - 1;
                while (j >= 0 && list[j].ThreatId > c.ThreatId)
                {
                    list[j + 1] = list[j];
                    j--;
                }
                list[j + 1] = c;
            }
        }

        /// <summary>
        /// 스침 보상(02 요약서 7.2절): 잠금 해제 또는 열을 임계 시작까지 올림, 다음 발사 1회 열 면제, 가속, 콤보 +2,
        /// 그리고 Traits.OnGrazeAwarded(피뢰침) → Run.OnGraze(점수용 횟수). 밖에서 직접 인정시킬 때(벼락 질주)도 쓴다.
        /// </summary>
        public override void AwardGraze(SimWorld w, PlayerState p, int castId, GrazeKind kind, Vector2 threatPos)
        {
            if (p == null || !p.Active || p.Downed || p.Hp <= 0) return;
            Extra x = Bind(w, p);
            PlayerTuning tune = Tuning(w);
            int t = w.Tick;

            p.GrazedCasts.Add(castId);
            x.CastLastLiveTick[castId] = t;

            int crit = p.Derived.CritStartHu;
            if (p.Locked)
            {
                // 즉시 해제 + 열 = 임계 시작.
                BreakLock(w, p, HeatUnlockReason.Graze, crit);
                w.Emit(SimEventType.LockBrokenByGraze, p.Slot);
            }
            else if (p.HeatHu < crit && !HeatFrozen(w)) p.HeatHu = crit;   // 낮을 때만 올린다

            // 면제는 겹치지 않는다(참/거짓).
            if (!p.HeatExemptArmed)
            {
                p.HeatExemptArmed = true;
                w.Emit(SimEventType.HeatExemptArmed, p.Slot);
            }

            // 가속: 시간만 갱신, 중첩 없음.
            int buffTicks = p.Derived.GrazeBuffTicks > 0 ? p.Derived.GrazeBuffTicks
                : (p.Def != null ? SimConst.Ticks(p.Def.GrazeBuffSeconds) : 0);
            p.GrazeBuffUntilTick = t + buffTicks;
            w.Emit(SimEventType.GrazeBuffStarted, p.Slot, a: buffTicks);

            // 콤보 +2(발사당 +3 상한과 무관). 끊김 타이머도 다시 채운다 [임시].
            p.ComboIdleTicks = 0;
            AddCombo(w, p, tune.GrazeComboGain);

            w.Emit(SimEventType.GrazeAwarded, p.Slot, a: castId, b: (int)kind, pos: threatPos);
            w.Mods.Traits.OnGrazeAwarded(w, p, castId);
            w.Mods.Run.OnGraze(w, p);
        }

        /// <summary>
        /// 인정한 시전의 기억을 지운다(02 요약서 7.1절 [임시]: 그 시전의 위협이 다 사라지면). 한 시전의 다음 탄이 늦게 나올 수 있어
        /// 위협이 마지막으로 보인 뒤 graze_cast_forget_seconds만큼 더 기억한다.
        /// </summary>
        void PruneGrazedCasts(SimWorld w, PlayerState p, Extra x, PlayerTuning tune)
        {
            if (p.GrazedCasts.Count == 0)
            {
                if (x.CastLastLiveTick.Count > 0) x.CastLastLiveTick.Clear();
                return;
            }
            int t = w.Tick;
            for (int i = 0; i < w.EnemyShots.Count; i++)
            {
                EnemyShotEntity shot = w.EnemyShots[i];
                if (!shot.Dead && p.GrazedCasts.Contains(shot.CastId)) x.CastLastLiveTick[shot.CastId] = t;
            }
            for (int i = 0; i < w.Zones.Count; i++)
            {
                ZoneEntity z = w.Zones[i];
                if (!z.Cancelled && !z.Detonated && p.GrazedCasts.Contains(z.CastId)) x.CastLastLiveTick[z.CastId] = t;
            }
            for (int i = 0; i < w.Enemies.Count; i++)
            {
                EnemyState e = w.Enemies[i];
                if (e.Alive && e.DashActive && p.GrazedCasts.Contains(e.DashCastId)) x.CastLastLiveTick[e.DashCastId] = t;
            }

            List<int> gone = x.Scratch;
            gone.Clear();
            foreach (int cast in p.GrazedCasts)
            {
                int last;
                if (!x.CastLastLiveTick.TryGetValue(cast, out last)) { gone.Add(cast); continue; }
                if (t - last > tune.GrazeCastForgetTicks) gone.Add(cast);
            }
            for (int i = 0; i < gone.Count; i++)
            {
                p.GrazedCasts.Remove(gone[i]);
                x.CastLastLiveTick.Remove(gone[i]);
            }
            gone.Clear();
        }
    }
}
