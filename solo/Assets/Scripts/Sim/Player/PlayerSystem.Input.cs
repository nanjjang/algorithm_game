using System.Collections.Generic;

namespace Wizard.Solo.Sim.Player
{
    // 2단계(입력 적용)와 3단계(대각 시야 유지 창 만료). 02 요약서 3.2절, 4.2~4.4절.
    public sealed partial class PlayerSystem
    {
        public override void Stage2_Input(SimWorld w)
        {
            PlayerTuning tune = Tuning(w);
            long endU = SimConst.TickStartU(w.Tick + 1);
            for (int s = 0; s < w.Players.Length; s++)
            {
                PlayerState p = w.Players[s];
                List<InputEvent> queue = w.InputQueues[s];
                Extra x = Bind(w, p);
                x.Events.Clear();

                // 이 틱의 것(TimeU < (t+1) × 256)만 꺼낸다. 늦게 들어온 지난 틱의 것도 이 틱에 처리한다.
                // 한 사람 한 틱의 상한을 넘는 것은 버린다(02 요약서 3.2절 [임시]).
                int keep = 0, taken = 0;
                for (int i = 0; i < queue.Count; i++)
                {
                    InputEvent e = queue[i];
                    if (e.TimeU < endU)
                    {
                        if (tune.MaxInputEventsPerTick <= 0 || taken < tune.MaxInputEventsPerTick) x.Events.Add(e);
                        taken++;
                    }
                    else queue[keep++] = e;
                }
                if (keep < queue.Count) queue.RemoveRange(keep, queue.Count - keep);

                p.JPressedThisTick = false;
                if (!p.Active) continue;

                // 공통 규칙 7: 여기서는 InputLocked만 본다. 잠긴 동안의 입력은 버리고 눌린 키를 전부 뗀 것으로 둔다
                // (되비침·쓰러짐이 끝난 뒤 뗀 키가 눌린 채 남지 않게).
                if (p.InputLocked)
                {
                    ReleaseAllForLock(w, p);
                    continue;
                }

                SortEvents(x.Events);
                for (int i = 0; i < x.Events.Count; i++) ApplyEvent(w, p, tune, x.Events[i]);
            }
        }

        /// <summary>(시각, 분류, 순번) 안정 정렬. 순번 = 큐에 들어온 차례.</summary>
        static void SortEvents(List<InputEvent> list)
        {
            for (int i = 1; i < list.Count; i++)
            {
                InputEvent e = list[i];
                int ec = PlayerRules.KeyClass(e.Key);
                int j = i - 1;
                while (j >= 0)
                {
                    InputEvent o = list[j];
                    bool after = o.TimeU > e.TimeU || (o.TimeU == e.TimeU && PlayerRules.KeyClass(o.Key) > ec);
                    if (!after) break;
                    list[j + 1] = o;
                    j--;
                }
                list[j + 1] = e;
            }
        }

        void ReleaseAllForLock(SimWorld w, PlayerState p)
        {
            for (int i = 0; i < p.Keys.Down.Length; i++) p.Keys.Down[i] = false;
            p.DiagPendingActive = false;
            p.Moving = false;
            if (p.InStance)
            {
                p.Stance = StanceState.None;
                w.Emit(SimEventType.StanceExited, p.Slot);
                w.Mods.Arts.OnCommandReset(w, p, CommandResetReason.StanceExit);
            }
        }

        void ApplyEvent(SimWorld w, PlayerState p, PlayerTuning tune, in InputEvent e)
        {
            int k = (int)e.Key;
            if (k < 0 || k >= SimConst.LogicalKeyCount) return;
            // 눌린 키의 누름, 안 눌린 키의 뗌은 무시.
            if (p.Keys.Down[k] == e.Down) return;

            Dir8 prevMove;
            bool hadPrev = PlayerRules.TryMoveDir(p.Keys, out prevMove);

            // 대각 유지 창이 이 이벤트보다 먼저 지났으면 먼저 만료시킨다(02 요약서 4.3절).
            if (p.Stance == StanceState.None && p.DiagPendingActive && e.TimeU > p.DiagPendingExpireU)
            {
                p.DiagPendingActive = false;
                if (hadPrev) SetFacing(w, p, prevMove);
            }

            p.Keys.Down[k] = e.Down;
            if (e.Down) p.Keys.LastPressU[k] = e.TimeU;

            switch (e.Key)
            {
                case LogicalKey.Stance:
                case LogicalKey.Reverse:
                    OnStanceKey(w, p, e);
                    break;
                case LogicalKey.Up:
                case LogicalKey.Left:
                case LogicalKey.Down:
                case LogicalKey.Right:
                    OnDirectionKey(w, p, tune, e, hadPrev, prevMove);
                    break;
                case LogicalKey.Fire:
                    // 같은 틱에 눌렀다 뗀 J도 이번 틱의 발사 조건을 채운다(02 요약서 3.2절 [임시]).
                    if (e.Down) p.JPressedThisTick = true;
                    break;
                case LogicalKey.Skill:
                    // 허용 여부(AttackLocked, 단계)는 Arts가 SimWorld.AttackAllowed로 본다. 여기서는 그대로 넘긴다.
                    if (e.Down) w.Mods.Arts.OnSkillPress(w, p, e.TimeU);
                    break;
                default:
                    // Pause는 실행기가 거른다. 들어와도 키 상태만 바뀐다.
                    break;
            }
        }

        void OnDirectionKey(SimWorld w, PlayerState p, PlayerTuning tune, in InputEvent e, bool hadPrev, Dir8 prevMove)
        {
            Dir8 cardinal = PlayerRules.CardinalOf(e.Key);
            if (e.Down) p.LastPressedCardinal = cardinal;

            if (p.InStance)
            {
                // 자세 중: 시야는 잠겨 있고, 누르는 순간만 상대 방향으로 판정기에 넘긴다.
                if (e.Down)
                    w.Mods.Arts.OnDirectionPress(w, p, Dir8Util.Relative(cardinal, p.LockedFacing), cardinal, e.TimeU);
                return;
            }

            Dir8 next;
            bool hasNext = PlayerRules.TryMoveDir(p.Keys, out next);
            if (!e.Down)
            {
                if (hadPrev && !Dir8Util.IsCardinal(prevMove) && hasNext && PlayerRules.IsAxisComponentOf(next, prevMove))
                {
                    // 대각에서 한 키를 뗐다: 창을 열고 시야는 대각으로 둔다(이동은 남은 키 방향으로 바로 바뀐다).
                    p.DiagPendingActive = true;
                    p.DiagPendingDir = prevMove;
                    p.DiagPendingExpireU = e.TimeU + tune.DiagonalHoldU;
                    return;
                }
                if (p.DiagPendingActive && !hasNext && e.TimeU <= p.DiagPendingExpireU)
                {
                    // 창 안에 나머지 키도 뗐다(정확히 창 끝 포함): 대각 시야 확정.
                    p.DiagPendingActive = false;
                    SetFacing(w, p, p.DiagPendingDir);
                    return;
                }
            }
            else p.DiagPendingActive = false;

            if (hasNext) SetFacing(w, p, next);
        }

        // 02 요약서 4.4절 표.
        void OnStanceKey(SimWorld w, PlayerState p, in InputEvent e)
        {
            bool stanceKey = e.Key == LogicalKey.Stance;
            switch (p.Stance)
            {
                case StanceState.None:
                    if (!e.Down) return;
                    Dir8 snap = PlayerRules.SnapCardinal(p.Facing, p.LastPressedCardinal, p.Keys);
                    p.LockedFacing = stanceKey ? snap : Dir8Util.Flip(snap);
                    p.Stance = stanceKey ? StanceState.Stance : StanceState.Reverse;
                    p.DiagPendingActive = false;
                    SetFacing(w, p, p.LockedFacing);
                    w.Emit(SimEventType.StanceEntered, p.Slot, a: (int)p.Stance);
                    w.Mods.Arts.OnCommandReset(w, p, CommandResetReason.StanceEnter);
                    break;

                case StanceState.Stance:
                    if (stanceKey)
                    {
                        if (!e.Down && !p.Keys.IsDown(LogicalKey.Reverse)) ExitStance(w, p);
                    }
                    else if (e.Down)
                    {
                        p.Stance = StanceState.Reverse;
                        p.LockedFacing = Dir8Util.Flip(p.LockedFacing);
                        SetFacing(w, p, p.LockedFacing);
                        w.Emit(SimEventType.ReverseFlipped, p.Slot);
                        w.Mods.Arts.OnCommandReset(w, p, CommandResetReason.Reverse);
                    }
                    break;

                case StanceState.Reverse:
                    // 반전 중 Stance 누름·뗌은 아무 일도 없다 [임시].
                    if (stanceKey || e.Down) return;
                    if (p.Keys.IsDown(LogicalKey.Stance))
                    {
                        // 반전을 떼고 자세로: 시야는 뒤집힌 채, 커맨드 초기화는 "반전" [임시].
                        p.Stance = StanceState.Stance;
                        w.Emit(SimEventType.StanceEntered, p.Slot, a: (int)p.Stance);
                        w.Mods.Arts.OnCommandReset(w, p, CommandResetReason.Reverse);
                    }
                    else ExitStance(w, p);
                    break;
            }
        }

        void ExitStance(SimWorld w, PlayerState p)
        {
            p.Stance = StanceState.None;
            Dir8 move;
            // 이동 키가 눌려 있으면 그 방향, 아니면 잠긴 시야 그대로.
            SetFacing(w, p, PlayerRules.TryMoveDir(p.Keys, out move) ? move : p.LockedFacing);
            w.Emit(SimEventType.StanceExited, p.Slot);
            w.Mods.Arts.OnCommandReset(w, p, CommandResetReason.StanceExit);
        }

        static void SetFacing(SimWorld w, PlayerState p, Dir8 dir)
        {
            if (p.Facing == dir) return;
            p.Facing = dir;
            w.Emit(SimEventType.FacingChanged, p.Slot, a: (int)dir);
        }

        public override void Stage3_ResolveFacing(SimWorld w)
        {
            long startU = SimConst.TickStartU(w.Tick);
            for (int s = 0; s < w.Players.Length; s++)
            {
                PlayerState p = w.Players[s];
                if (!p.Active || !p.DiagPendingActive) continue;
                if (p.Stance != StanceState.None) { p.DiagPendingActive = false; continue; }
                if (startU > p.DiagPendingExpireU)
                {
                    p.DiagPendingActive = false;
                    Dir8 move;
                    if (PlayerRules.TryMoveDir(p.Keys, out move)) SetFacing(w, p, move);
                }
            }
        }
    }
}
