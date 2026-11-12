using Wizard.Core;
using Wizard.Sim.Arts;
using Wizard.Sim.Events;
using Wizard.Sim.Input;

namespace Wizard.Sim.Player
{
    // spec 01 5.3(시야), 5.4(대각 시야 유지 창), 5.5(자세와 반전 자세)의 상태 기계. 2단계(이벤트마다)와 3단계(창 만료)에서 불린다.
    // 할당이 없다. 표시 이벤트(100번대)는 상태가 실제로 바뀐 순간에 한 번씩만 낸다:
    //   StanceEntered(A = 새 자세) — 자세 밖 → 자세·반전 자세
    //   ReverseFlipped(A = 새 자세, B = 잠긴 시야) — 자세 ↔ 반전 자세
    //   StanceExited — 자세·반전 자세 → 자세 밖
    //   FacingChanged(A = 새 시야) — 시야 값이 바뀔 때마다
    //   CommandReset(A = CommandResetReason) — 자세 진입·해제·반전, 일시정지 누름(spec 01 4.4, GDD 8)
    internal static class PlayerMotion
    {
        // 입력 이벤트 하나(spec 01 3.8의 2단계: KeyState → 자세 → 시야). 결과는 이어서 불리는 판정기가 읽는다
        public static void ApplyInput(ref PlayerState p, in InputEvent e, PlayerSlot slot, uint tick, ISimEventSink sink,
                                      out PlayerInputResult result)
        {
            result = default;
            result.Key = e.Key;
            result.Down = e.Down;
            result.Time = e.Time;

            // "이 이벤트 직전의 이동 방향"(spec 01 5.4)은 KeyState 를 바꾸기 전에 읽는다
            bool hadPrev = PlayerRules.TryMoveDir(in p.Keys, out Dir8 prevDir);
            if (!p.Keys.Apply(in e))
            {
                return;                                  // 눌린 키의 누름, 안 눌린 키의 뗌: 무시(spec 01 4.3, T-I07·T-I08)
            }
            result.Accepted = true;

            // 창이 이미 지났으면 이 이벤트보다 먼저 만료한다(spec 01 5.4 첫 줄, T-F15). 창은 자세 밖에서만 열려 있다.
            // 자세 키 이벤트보다도 먼저다: 자세 진입의 SnapCardinal 이 만료된 대각을 읽지 않게 한다
            if (p.Stance == StanceMode.Normal && p.Pending.Active && e.Time > p.Pending.ExpireTime)
            {
                p.Pending.Active = false;
                if (hadPrev)
                {
                    SetFacing(ref p, prevDir, slot, tick, sink);
                }
            }

            bool hasNew = PlayerRules.TryMoveDir(in p.Keys, out Dir8 newDir);
            switch (e.Key)
            {
                case LogicalKey.Stance:
                case LogicalKey.Reverse:
                    OnStanceKey(ref p, in e, hasNew, newDir, slot, tick, sink, ref result);
                    break;
                case LogicalKey.Up:
                case LogicalKey.Left:
                case LogicalKey.Down:
                case LogicalKey.Right:
                    OnDirectionKey(ref p, in e, hadPrev, prevDir, hasNew, newDir, slot, tick, sink, ref result);
                    break;
                case LogicalKey.Pause:
                    if (e.Down)
                    {
                        Reset(CommandResetReason.Pause, slot, tick, sink, ref result);
                    }
                    break;
                default:
                    break;                               // Fire·Skill 은 과열·판정기의 것
            }
        }

        // 3단계: 대각 유지 창 만료(spec 01 5.4 ResolvePending). 이 틱의 시작 시각이 창 끝을 넘었으면 지금 이동 방향으로
        public static void ResolvePending(ref PlayerState p, PlayerSlot slot, uint tick, ISimEventSink sink)
        {
            if (p.Stance != StanceMode.Normal || !p.Pending.Active)
            {
                return;
            }
            if (SimTime.InputTime(tick, 0) <= p.Pending.ExpireTime)
            {
                return;
            }
            p.Pending.Active = false;
            if (PlayerRules.TryMoveDir(in p.Keys, out Dir8 now))
            {
                SetFacing(ref p, now, slot, tick, sink);
            }
        }

        private static void OnDirectionKey(ref PlayerState p, in InputEvent e, bool hadPrev, Dir8 prevDir, bool hasNew, Dir8 newDir,
                                           PlayerSlot slot, uint tick, ISimEventSink sink, ref PlayerInputResult result)
        {
            Dir8 keyDir = PlayerRules.KeyDir(e.Key);
            if (e.Down)
            {
                p.LastPressedCardinal = keyDir;          // spec 01 5.3: 방향 누름마다
            }

            if (p.Stance != StanceMode.Normal)
            {
                // 자세 중: 시야는 잠겨 있다. 누름만 판정기에 상대 방향으로 넘긴다(spec 01 4.4 "누르는 순간만", 5.5)
                if (e.Down)
                {
                    result.HasDirection = true;
                    result.Direction = PlayerRules.Relative(p.StanceLockedFacing, keyDir);
                }
                return;
            }

            // 자세 밖(spec 01 5.4의 OnKeyEvent, 그 밖은 5.3)
            if (!e.Down)
            {
                if (hadPrev && PlayerRules.IsDiagonal(prevDir) && hasNew && PlayerRules.IsAxisComponent(prevDir, newDir))
                {
                    // 대각에서 한 키를 뗐다: 시야는 대각 그대로 두고 창을 연다. 이동은 남은 키 방향으로 이미 바뀌었다
                    p.Pending.Active = true;
                    p.Pending.Diag = prevDir;
                    p.Pending.ExpireTime = e.Time + PlayerRules.DiagHoldWindowUnits;
                    return;
                }
                if (p.Pending.Active && !hasNew && e.Time <= p.Pending.ExpireTime)
                {
                    // 두 번째 뗌이 창 안(경계 포함): 대각 유지 확정
                    p.Pending.Active = false;
                    SetFacing(ref p, p.Pending.Diag, slot, tick, sink);
                    return;
                }
            }
            else
            {
                p.Pending.Active = false;                // 새 방향 입력이 오면 창 취소, 정상 규칙으로
            }

            if (hasNew)
            {
                SetFacing(ref p, newDir, slot, tick, sink);
            }
        }

        // spec 01 5.5 전이표. 표에 없는 조합(자세 밖에서 자세 키 뗌 등)은 키 상태만 바뀌고 자세·시야는 그대로다
        private static void OnStanceKey(ref PlayerState p, in InputEvent e, bool hasNew, Dir8 newDir,
                                        PlayerSlot slot, uint tick, ISimEventSink sink, ref PlayerInputResult result)
        {
            bool isStanceKey = e.Key == LogicalKey.Stance;
            switch (p.Stance)
            {
                case StanceMode.Normal:
                    if (e.Down)
                    {
                        Dir8 snapped = PlayerRules.SnapCardinal(p.Facing, p.LastPressedCardinal, in p.Keys);
                        Enter(ref p, isStanceKey ? StanceMode.Stance : StanceMode.Reverse,
                              isStanceKey ? snapped : PlayerRules.Flip(snapped), slot, tick, sink, ref result);
                    }
                    break;

                case StanceMode.Stance:
                    if (!isStanceKey && e.Down)
                    {
                        // Shift 를 누른 채 Space: 잠긴 시야만 180도
                        Switch(ref p, StanceMode.Reverse, PlayerRules.Flip(p.StanceLockedFacing), slot, tick, sink, ref result);
                    }
                    else if (isStanceKey && !e.Down && !p.Keys.IsDown(LogicalKey.Reverse))
                    {
                        Exit(ref p, hasNew, newDir, slot, tick, sink, ref result);
                    }
                    break;

                case StanceMode.Reverse:
                    if (!isStanceKey && !e.Down)
                    {
                        if (p.Keys.IsDown(LogicalKey.Stance))
                        {
                            // Shift 를 누른 채 Space 를 뗌: 뒤집힌 방향으로 잠긴 채 자세 [임시, U5]
                            Switch(ref p, StanceMode.Stance, p.StanceLockedFacing, slot, tick, sink, ref result);
                        }
                        else
                        {
                            Exit(ref p, hasNew, newDir, slot, tick, sink, ref result);
                        }
                    }
                    // 반전 자세 중 Shift 누름·뗌: 변화 없음 [임시, U7]
                    break;
            }
        }

        private static void Enter(ref PlayerState p, StanceMode mode, Dir8 locked, PlayerSlot slot, uint tick, ISimEventSink sink,
                                  ref PlayerInputResult result)
        {
            p.Pending.Active = false;                    // 자세에 들어가면 창은 취소된다(spec 01 5.4)
            p.Stance = mode;
            p.StanceLockedFacing = locked;
            Emit(sink, SimEventType.StanceEntered, slot, tick, (int)mode, 0);
            SetFacing(ref p, locked, slot, tick, sink);
            result.StanceChanged = true;
            result.InStance = true;
            Reset(CommandResetReason.StanceEnter, slot, tick, sink, ref result);
        }

        private static void Switch(ref PlayerState p, StanceMode mode, Dir8 locked, PlayerSlot slot, uint tick, ISimEventSink sink,
                                   ref PlayerInputResult result)
        {
            p.Stance = mode;
            p.StanceLockedFacing = locked;
            Emit(sink, SimEventType.ReverseFlipped, slot, tick, (int)mode, (int)locked);
            SetFacing(ref p, locked, slot, tick, sink);
            result.InStance = true;
            Reset(CommandResetReason.Reverse, slot, tick, sink, ref result);
        }

        // 자세에서 나온다. 이동 키가 눌려 있으면 그 방향으로 바로, 아니면 잠긴 시야 그대로(spec 01 5.5, U6).
        // 창은 비활성에서 시작한다
        private static void Exit(ref PlayerState p, bool hasMove, Dir8 moveDir, PlayerSlot slot, uint tick, ISimEventSink sink,
                                 ref PlayerInputResult result)
        {
            Dir8 locked = p.StanceLockedFacing;
            p.Stance = StanceMode.Normal;
            p.Pending.Active = false;
            Emit(sink, SimEventType.StanceExited, slot, tick, 0, 0);
            SetFacing(ref p, hasMove ? moveDir : locked, slot, tick, sink);
            result.StanceChanged = true;
            result.InStance = false;
            Reset(CommandResetReason.StanceExit, slot, tick, sink, ref result);
        }

        private static void Reset(CommandResetReason reason, PlayerSlot slot, uint tick, ISimEventSink sink, ref PlayerInputResult result)
        {
            result.HasReset = true;
            result.ResetReason = reason;
            Emit(sink, SimEventType.CommandReset, slot, tick, (int)reason, 0);
        }

        private static void SetFacing(ref PlayerState p, Dir8 facing, PlayerSlot slot, uint tick, ISimEventSink sink)
        {
            if (p.Facing == facing)
            {
                return;
            }
            p.Facing = facing;
            Emit(sink, SimEventType.FacingChanged, slot, tick, (int)facing, 0);
        }

        private static void Emit(ISimEventSink sink, SimEventType type, PlayerSlot slot, uint tick, int a, int b)
        {
            var ev = new SimEvent { Type = type, Slot = slot, Tick = tick, A = a, B = b };
            sink.Emit(in ev);
        }
    }
}
