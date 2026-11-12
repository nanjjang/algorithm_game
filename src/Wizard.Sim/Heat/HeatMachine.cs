using System;
using Wizard.Core;

namespace Wizard.Sim.Heat
{
    // spec 01 7.3~7.6의 상태 기계. 플레이어 한 명의 HeatState 위에서 도는 순수 연산이고, 표시 이벤트는 세계(SimWorld.Heat.cs)가
    // 결과를 보고 낸다. 세계는 이것을 CONTRACTS 3.2의 단계 순서대로 부른다: 1단계 ProcessUnlock, 6단계 TryFire, 11단계 Cool.
    // 할당하지 않는다. 시각은 틱 번호(uint)이고 비교는 long 으로 넓혀서 한다(틱 + 간격 + 대기의 넘침을 피한다).
    internal static class HeatMachine
    {
        private const int PermilleOne = 1000;    // 천분율 1.0(단위 환산)

        // spec 01 7.5 ProcessUnlock(1단계): 잠금 시간이 끝났으면 풀고 열 0
        public static bool ProcessUnlock(ref HeatState s, uint t)
        {
            if (!s.Locked || t < s.UnlockTick)
            {
                return false;
            }
            s.Locked = false;
            s.HeatHu = 0;
            return true;
        }

        // spec 01 7.3 TryFire(6단계). held = J 가 눌려 있다, pressed = 이번 틱에 J 를 눌렀다(같은 틱에 뗐어도).
        // inKiln = 발사 틱에 플레이어가 불가마 지대 안에 있다(세계가 위치로 판정한다).
        // selfBlowupAllowed = SimConfig.UnstableCoreSelfBlowup(결투는 거짓 → 자폭 없음, 난수도 뽑지 않는다).
        // 불안정 핵이 있으면 발사마다(잠금으로 이어지는 발사 포함) Mutation 흐름에서 한 번 뽑는다(7.6.4)
        public static void TryFire(ref HeatState s, uint t, bool held, bool pressed, bool fireEnabled, bool inKiln,
                                   bool selfBlowupAllowed, ref Pcg32 mutationRng, ref int lastShotId, out HeatFire fire)
        {
            fire = default;
            fire.Tick = t;
            if (!held && !pressed)
            {
                return;
            }
            if (s.Locked)
            {
                if (pressed)
                {
                    fire.Kind = HeatFireKind.DryWhileLocked;     // "J를 누르면 지팡이가 헛돈다"(eff 10)
                }
                return;
            }
            if (s.HasFired && t < s.FireReadyTick)
            {
                return;                                          // 간격 중. 눌렀다 뗀 J 는 보관하지 않는다 [임시]
            }
            if (!fireEnabled || !s.Shot.Armed)
            {
                return;                                          // 광장, 주문 없음
            }

            int h = s.Shot.HeatHu;
            bool exemptUsed = false;
            if (s.ExemptArmed)
            {
                h = 0;
                s.ExemptArmed = false;
                exemptUsed = true;                               // 8.4: 면제가 불가마보다 먼저(7.6.2)
            }
            int critStart = s.CritStartHu;
            bool isCrit = s.HeatHu + h >= critStart;
            int absorbed = 0;
            if (isCrit && !exemptUsed && inKiln && s.KilnAbsorbLeftHu > 0)
            {
                absorbed = s.KilnAbsorbLeftHu < h ? s.KilnAbsorbLeftHu : h;
                s.KilnAbsorbLeftHu -= absorbed;
                h -= absorbed;
            }
            s.HeatHu = unchecked(s.HeatHu + h);
            bool willLock = HeatRules.LocksAfter(s.HeatHu);
            if (willLock)
            {
                isCrit = true;                                   // "100을 넘긴 발사는 임계로 나가고"
            }
            bool blowup = s.Mods.UnstableChancePermille > 0 && selfBlowupAllowed
                          && mutationRng.Chance(s.Mods.UnstableChancePermille, PermilleOne);

            lastShotId = unchecked(lastShotId + 1);
            int interval = s.Shot.IntervalTicks;
            s.HasFired = true;
            s.LastFireTick = t;
            s.LastFireIntervalTicks = interval;
            s.FireReadyTick = unchecked(t + (uint)interval);
            if (willLock)
            {
                EnterLock(ref s, t, interval, s.LockTicks + s.Mods.ExtraLockTicks);
            }
            if (blowup)
            {
                UnstableLock(ref s, t, interval);
            }

            fire.Kind = HeatFireKind.Fired;
            fire.ShotId = lastShotId;
            fire.Core = s.Shot.Core;
            fire.IsCrit = isCrit;
            fire.ExemptUsed = exemptUsed;
            fire.KilnAbsorbedHu = absorbed;
            fire.HeatAddedHu = h;
            fire.HeatAfterHu = s.HeatHu;
            fire.LockEntered = willLock;
            fire.UnstableBlowup = blowup;
        }

        // spec 01 7.5 EnterLock: 잠금은 그 발사의 간격이 끝난 뒤부터 센다([해석] 15절 C8)
        public static void EnterLock(ref HeatState s, uint t, int intervalTicks, int lockTicks)
        {
            s.Locked = true;
            s.LockStartTick = t;
            s.LockTotalTicks = intervalTicks + lockTicks;
            s.UnlockTick = unchecked(t + (uint)s.LockTotalTicks);
        }

        // spec 01 7.6.4 UnstableCoreBlowup 의 잠금: 간격 + 자폭 잠금. 이미 잠겨 있으면 더 긴 쪽 [임시]
        private static void UnstableLock(ref HeatState s, uint t, int intervalTicks)
        {
            int total = intervalTicks + s.Mods.UnstableLockTicks;
            uint unlock = unchecked(t + (uint)total);
            if (!s.Locked || unlock > s.UnlockTick)
            {
                s.Locked = true;
                s.LockStartTick = t;
                s.LockTotalTicks = total;
                s.UnlockTick = unlock;
            }
        }

        // spec 01 7.4 Cool(11단계, 틱 끝): 잠금 중에는 식지 않는다. 대기는 마지막 발사의 간격이 끝난 뒤부터 센다([해석] 15절 C8)
        public static void Cool(ref HeatState s, uint t)
        {
            if (s.Locked)
            {
                return;
            }
            if (s.HasFired)
            {
                if (s.LastFireTick == t)
                {
                    return;                                      // 이번 틱에 쐈다
                }
                if ((long)t < (long)s.LastFireTick + s.LastFireIntervalTicks + s.CoolDelayTicks)
                {
                    return;
                }
            }
            int cool = HeatRules.CoolPerTickHu(s.CoolHuPerTick, s.Mods.CoolPermille);
            s.HeatHu = s.HeatHu > cool ? s.HeatHu - cool : 0;
        }

        // spec 01 7.5 BreakLock(스침, 방열 폭발): 잠금을 풀고 열을 정한다. 잠겨 있지 않았으면 거짓(아무것도 안 함)
        public static bool BreakLock(ref HeatState s, int heatAfterHu)
        {
            if (!s.Locked)
            {
                return false;
            }
            s.Locked = false;
            s.HeatHu = heatAfterHu;
            return true;
        }

        // spec 01 8.4 의 열 부분: 잠금이면 풀고 열 = 임계 시작, 아니면 열이 임계 시작보다 낮을 때만 올린다. 그리고 열 면제 대기(겹치지 않는다).
        // 돌려주는 값: 잠금을 풀었는가. armedNow = 이번에 새로 면제가 걸렸는가
        public static bool ApplyGraze(ref HeatState s, out bool armedNow)
        {
            int c = s.CritStartHu;
            bool broke = BreakLock(ref s, c);
            if (!broke && s.HeatHu < c)
            {
                s.HeatHu = c;
            }
            armedNow = !s.ExemptArmed;
            s.ExemptArmed = true;
            return broke;
        }

        // spec 01 7.6.7: 피해 계산용 열을 돌려주고 열 0. 잠금 중이면 열 100으로 계산하고 잠금을 푼다
        public static int ConsumeForVentBlast(ref HeatState s, out bool brokeLock)
        {
            int heat = HeatRules.VentBlastHeat(s.HeatHu, s.Locked);
            brokeLock = BreakLock(ref s, 0);
            s.HeatHu = 0;
            return heat;
        }

        // spec 01 7.6.5 OnKill(탐식): 처치당 KillHeatHu, 최근 60틱 창의 합이 KillWindowCapHu 면 더 깎지 않는다. 잠금 중에는 깎지 않는다 [임시].
        // window·windowTicks 는 이 플레이어의 60칸 고리(색인 = 틱 % 60). 돌려주는 값: 깎은 양
        public static int OnKill(ref HeatState s, uint t, Span<int> windowHu, Span<uint> windowTicks)
        {
            if (s.Mods.KillHeatHu <= 0 || s.Locked)
            {
                return 0;
            }
            int room = s.Mods.KillWindowCapHu - WindowSum(windowHu, windowTicks, t);
            int d = s.Mods.KillHeatHu;
            if (room < d)
            {
                d = room;
            }
            if (s.HeatHu < d)
            {
                d = s.HeatHu;
            }
            if (d <= 0)
            {
                return 0;
            }
            s.HeatHu -= d;
            WindowAdd(windowHu, windowTicks, t, d);
            return d;
        }

        // 최근 60틱(t − 59 ~ t)의 합. 창의 길이는 "초당"(spec 01 7.6.5)이라 틱 속도와 같다
        public static int WindowSum(ReadOnlySpan<int> windowHu, ReadOnlySpan<uint> windowTicks, uint t)
        {
            int sum = 0;
            for (int k = 0; k < windowHu.Length; k++)
            {
                if (windowHu[k] > 0 && windowTicks[k] <= t && (long)t - windowTicks[k] < windowHu.Length)
                {
                    sum += windowHu[k];
                }
            }
            return sum;
        }

        private static void WindowAdd(Span<int> windowHu, Span<uint> windowTicks, uint t, int amount)
        {
            int k = (int)(t % (uint)SimVersion.TickRate);
            if (windowTicks[k] != t)
            {
                windowHu[k] = 0;
                windowTicks[k] = t;
            }
            windowHu[k] += amount;
        }
    }
}
