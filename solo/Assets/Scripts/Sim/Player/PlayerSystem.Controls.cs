using System;
using System.Collections.Generic;
using UnityEngine;

namespace Wizard.Solo.Sim.Player
{
    // 입력 적용, 대각 시야 유지, 이동. 02 요약서 3.2절, 4.2~4.6절.
    public sealed partial class PlayerSystem
    {
        public override void Stage2_Input(SimWorld w)
        {
            PlayerTuning tune = Tuning(w);
            long endU = SimConst.TickStartU(w.Tick + 1);
            for (int slot = 0; slot < w.Players.Length; slot++)
            {
                PlayerState p = w.Players[slot];
                List<InputEvent> events = Bind(w, p).Events;
                events.Clear();

                // 미래 틱의 입력은 순서를 유지해 큐에 남긴다. 처리 상한을 넘긴 현재 입력은 버린다.
                List<InputEvent> queue = w.InputQueues[slot];
                int remaining = 0;
                int limit = tune.MaxInputEventsPerTick;
                for (int i = 0; i < queue.Count; i++)
                {
                    InputEvent input = queue[i];
                    if (input.TimeU >= endU)
                        queue[remaining++] = input;
                    else if (limit <= 0 || events.Count < limit)
                        events.Add(input);
                }
                if (remaining < queue.Count)
                    queue.RemoveRange(remaining, queue.Count - remaining);

                p.JPressedThisTick = false;
                if (!p.Active) continue;

                // 공통 규칙 7: 여기서는 InputLocked만 본다. 잠긴 동안의 입력은 버리고 눌린 키를 전부 뗀 것으로 둔다
                // (되비침·쓰러짐이 끝난 뒤 뗀 키가 눌린 채 남지 않게).
                if (p.InputLocked)
                {
                    ReleaseAllForLock(w, p);
                    continue;
                }

                SortEvents(events);
                for (int i = 0; i < events.Count; i++)
                    ApplyEvent(w, p, tune, events[i]);
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
            Array.Clear(p.Keys.Down, 0, p.Keys.Down.Length);
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

        /// <summary>지금 이동 속력(칸/초): 자세면 자세 속도로 교체, 스침 가속 ×, 환경 감속 ×(02 요약서 4.5절).</summary>
        public float MoveSpeed(SimWorld w, PlayerState p)
        {
            if (p.Def == null) return 0f;
            float speed = p.InStance ? p.Def.StanceSpeed : p.Def.WalkSpeed;
            // 가속은 겹치지 않는다(시간만 갱신).
            if (w.Tick < p.GrazeBuffUntilTick) speed *= p.Def.GrazeBuffSpeed;
            return speed * p.EnvSpeedMul;
        }

        public override void Stage4_Move(SimWorld w)
        {
            for (int s = 0; s < w.Players.Length; s++)
            {
                PlayerState p = w.Players[s];
                if (!p.Active) continue;
                bool control = w.PlayerControlActiveFor(p);
                p.PrevPos = p.Pos;
                Dir8 dir;
                if (!control || p.Downed || p.InputLocked || p.Def == null || !PlayerRules.TryMoveDir(p.Keys, out dir))
                {
                    p.Moving = false;
                    continue;
                }
                p.Moving = true;
                p.MoveDir = dir;

                // 대각도 단위 벡터라 속력이 같다.
                Vector2 delta = Dir8Util.ToSimVector(dir) * (MoveSpeed(w, p) * SimConst.TickSeconds);
                Vector2 target = p.Pos + delta;
                ObstacleEntity hit;
                if (w.Mods.Threats.SegmentHitsObstacle(w, p.Pos, target, false, out hit))
                {
                    // 장애물: 축별로 따로 가 본다(미끄러짐). 둘 다 막히면 제자리.
                    Vector2 alongX = new Vector2(p.Pos.x + delta.x, p.Pos.y);
                    Vector2 alongY = new Vector2(p.Pos.x, p.Pos.y + delta.y);
                    if (delta.x != 0f && !w.Mods.Threats.SegmentHitsObstacle(w, p.Pos, alongX, false, out hit)) target = alongX;
                    else if (delta.y != 0f && !w.Mods.Threats.SegmentHitsObstacle(w, p.Pos, alongY, false, out hit)) target = alongY;
                    else target = p.Pos;
                }
                // 벽: 몸 반지름만큼 안쪽으로 축별로 자른다(벽 따라 미끄러짐).
                p.Pos = w.ClampToArena(target, p.Def.BodyRadius);
            }
        }

        /// <summary>
        /// 비기의 순간 이동. 벽 앞에서 멈추고, stopAtEnemies면 살아 있는 적의 몸 앞에서, stopAtObstacles면 장애물 앞에서 멈춘다.
        /// 도착 위치를 돌려준다. 이벤트(PlayerMovedByArt)는 부른 쪽(Arts)이 낸다.
        /// </summary>
        public override Vector2 MoveByArt(SimWorld w, PlayerState p, Vector2 delta, bool stopAtEnemies, bool stopAtObstacles)
        {
            if (p == null || !p.Active) return p != null ? p.Pos : Vector2.zero;
            float body = p.Def != null ? p.Def.BodyRadius : 0f;
            Vector2 from = p.Pos;
            float t = 1f;

            // 벽: 몸 반지름을 뺀 안쪽 상자를 벗어나는 지점까지.
            t = Mathf.Min(t, WallLimit(from.x, delta.x, body, w.ArenaWidth - body));
            t = Mathf.Min(t, WallLimit(from.y, delta.y, body, w.ArenaHeight - body));

            if (stopAtObstacles)
            {
                ObstacleEntity hit;
                if (w.Mods.Threats.SegmentHitsObstacle(w, from, from + delta * t, false, out hit))
                {
                    float th;
                    if (hit != null && (PlayerRules.SegmentEntersCircle(from, delta, hit.Pos, hit.Radius + body, out th)
                                        || PlayerRules.SegmentEntersCircle(from, delta, hit.Pos, hit.Radius, out th)))
                        t = Mathf.Min(t, th);
                    else t = 0f;
                }
            }

            if (stopAtEnemies)
            {
                for (int i = 0; i < w.Enemies.Count; i++)
                {
                    EnemyState e = w.Enemies[i];
                    // 죽어 가는 적은 없는 것으로 본다(공통 규칙 2). 땅속의 적은 몸이 없다.
                    if (!e.Alive || e.Burrowed) continue;
                    float te;
                    if (PlayerRules.SegmentEntersCircle(from, delta, e.Pos, e.BodyRadius + body, out te)) t = Mathf.Min(t, te);
                }
            }

            if (t < 0f) t = 0f;
            p.Pos = w.ClampToArena(from + delta * t, body);
            return p.Pos;
        }

        /// <summary>한 축에서 [lo, hi]를 벗어나지 않는 이동 비율(0~1).</summary>
        static float WallLimit(float pos, float d, float lo, float hi)
        {
            if (d > 0f) return pos >= hi ? 0f : Mathf.Min(1f, (hi - pos) / d);
            if (d < 0f) return pos <= lo ? 0f : Mathf.Min(1f, (lo - pos) / d);
            return 1f;
        }
    }
}
