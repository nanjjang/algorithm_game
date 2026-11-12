using System;
using System.Collections.Generic;
using Wizard.BalanceLab.Core;
using Wizard.Content;
using Wizard.Sim;
using Wizard.Sim.Arts;
using Wizard.Sim.Events;
using Wizard.Sim.Input;

namespace Wizard.BalanceLab.Feel
{
    // 세계가 낸 표시 이벤트(방출 순서 그대로)와 사람의 키 기록에서 손맛 세 축의 시도와 결과를 센다.
    // 이벤트의 뜻(spec 03 21절, 시제품의 비기 쪽 훅 파일):
    //   CmdDirInput(A = 상대 방향 CmdDir)          자세 중 방향이 판정기에 들어감
    //   PracticeGapReport(A = 간격 단위 또는 −1, V = 간격(long), B = 판정한 기술, C = 판정함 0/1)   K 를 누를 때마다 한 번
    //   ArtKBuffered                                재사용 대기 끝에 나갈 K 를 보관(바로 뒤의 GapReport 가 그 K)
    //   ArtCast / ArtFizzle*(A = 기술)              5단계의 발동. 그 틱에 판정한 K 들의 것이 먼저, 보관된 K 의 것이 마지막
    //   StanceEntered / ReverseFlipped / StanceExited / CommandReset   자세 상태와 커맨드 초기화
    //
    // 정의(결과 보고의 열린 질문 [임시]):
    //   두 번 누르는 커맨드 = 앞, 앞, K(꿰뚫는 창 ⑥). 시도 = 자세 중 K 를 누를 때 직전(마지막 K 이후)의 방향 입력 둘이 앞, 앞이고
    //     첫째 앞이 K 보다 AttemptWindow 안에 있는 것. 성공 = 나간 기술이 꿰뚫는 창.
    //   섬 = 위 시도 가운데 연습 시간(PracticeMinutes) 뒤의 것. 성공 = 꿰뚫는 창이 나갔고 마지막 방향에서 K 까지가 0.07초(1,075단위) 이하.
    //     (시제품에는 섬 기술이 없고 간격만 잰다. 섬 연습 때 사람은 언제나 섬을 노린다는 전제다)
    //   무시된 K = 재사용 대기 중 보관 창 밖의 K. 판정기는 커서와 방향 기록을 그대로 두므로 같은 앞, 앞으로 다시 누른 K 는 같은 시도다:
    //     무시된 K 가 두 번 누르기 시도였고 다시 누른 K 도 같은 앞, 앞(첫째 앞의 틱이 같다)이면 무시된 쪽은 세지 않고 다시 누른 쪽이 시도다.
    //     다시 누르지 않았으면 무시된 K 가 실패한 시도로 남는다. 다시 누른 K 는 시도이므로 오발로 세지 않는다.
    //   자세 중 오발 = 자세 중 이동하다(방향 키가 눌려 있거나 마지막 방향 입력이 0.3초 안) 누른 K 가운데, 앞, 앞, K 시도가 아닌 것에서
    //     기술이 나갔을 때 마력 방출(K 혼자)이 아닌 기술이 나간 비율. 측정 규칙: 연습 중 "뒤, K"(반동 사격)는 일부러 쓰지 않는다.
    internal static class FeelAnalyzer
    {
        private const int MaxDirHistory = 8;
        private const int MovementKeys = 4;                    // LogicalKey 0..3 = 위, 왼쪽, 아래, 오른쪽

        private sealed class Press
        {
            public uint Tick;
            public long GapUnits;
            public bool InStance;
            public bool Moving;
            public bool TwoPress;
            public int FirstGapTicks;
            public ArtId Outcome;
            public bool Resolved;
            public bool Ignored;
            public uint FirstDirTick;          // 두 번 누르기 시도일 때 첫째 앞의 틱(같은 시도인지 가르는 열쇠)
            public bool Superseded;            // 무시된 K 였고 같은 방향 기록으로 다시 누른 K 가 그 시도를 이어받음(어느 축에도 세지 않는다)
        }

        public static FeelResult Analyze(IReadOnlyList<SimEvent> events, IReadOnlyList<PlayerInput> keys, uint endTick, FeelOptions options)
        {
            var result = new FeelResult
            {
                Ticks = endTick,
                PracticeTicks = options.PracticeMinutes * 60L * SimVersion.TickRate,
            };
            long windowTicks = IntMath.MsToTicks(options.AttemptWindowMs);

            var presses = new List<Press>();
            var dirs = new List<(CmdDir Dir, uint Tick)>();
            var pendingBuffered = new Queue<Press>();
            // 재사용 대기에 무시된 K. 판정기는 무시된 K 에 커서와 방향 기록을 그대로 두므로(spec 03 10절, CommandParser U-02·U-04)
            // 같은 방향 기록으로 다시 누른 K 는 같은 시도다. 방향 기록이 비워지면(초기화, 발동, 보관) 이 목록도 비운다
            var ignoredPending = new List<Press>();
            int decidedCastsPending = 0;
            bool inStance = false;
            bool bufferedFlag = false;
            int heldMask = 0;
            int keyCursor = 0;

            foreach (SimEvent e in events)
            {
                switch (e.Type)
                {
                    case SimEventType.StanceEntered:
                        inStance = true;
                        dirs.Clear();
                        ignoredPending.Clear();
                        break;
                    case SimEventType.StanceExited:
                        inStance = false;
                        dirs.Clear();
                        ignoredPending.Clear();
                        break;
                    case SimEventType.ReverseFlipped:
                    case SimEventType.CommandReset:
                        dirs.Clear();
                        ignoredPending.Clear();
                        break;
                    case SimEventType.CmdDirInput:
                        if (dirs.Count == MaxDirHistory)
                        {
                            dirs.RemoveAt(0);
                        }
                        dirs.Add(((CmdDir)e.A, e.Tick));
                        break;
                    case SimEventType.ArtKBuffered:
                        bufferedFlag = true;
                        break;
                    case SimEventType.PracticeGapReport:
                    {
                        // 이 틱까지의 키로 방향 키 상태를 맞춘다
                        while (keyCursor < keys.Count && keys[keyCursor].Event.Tick <= e.Tick)
                        {
                            InputEvent k = keys[keyCursor++].Event;
                            if ((int)k.Key < MovementKeys)
                            {
                                heldMask = k.Down ? heldMask | (1 << (int)k.Key) : heldMask & ~(1 << (int)k.Key);
                            }
                        }
                        var p = new Press { Tick = e.Tick, GapUnits = e.V, InStance = inStance };
                        p.Moving = heldMask != 0 || (e.V >= 0 && e.V <= FeelTargets.GapDefaultUnits);
                        if (inStance && dirs.Count >= 2 && dirs[dirs.Count - 1].Dir == CmdDir.Forward && dirs[dirs.Count - 2].Dir == CmdDir.Forward
                            && e.Tick - dirs[dirs.Count - 2].Tick <= windowTicks)
                        {
                            p.TwoPress = true;
                            p.FirstGapTicks = (int)(dirs[dirs.Count - 1].Tick - dirs[dirs.Count - 2].Tick);
                            p.FirstDirTick = dirs[dirs.Count - 2].Tick;
                            // 앞서 무시된 K 와 같은 앞, 앞(첫째 앞의 틱이 같다)이면 같은 시도의 다시 누르기다: 앞의 것은 세지 않는다
                            foreach (Press earlier in ignoredPending)
                            {
                                if (earlier.TwoPress && earlier.FirstDirTick == p.FirstDirTick)
                                {
                                    earlier.Superseded = true;
                                }
                            }
                        }
                        if (e.C == 1)
                        {
                            p.Outcome = (ArtId)e.B;
                            p.Resolved = true;
                            decidedCastsPending++;
                        }
                        else if (bufferedFlag)
                        {
                            pendingBuffered.Enqueue(p);              // 대기 끝의 발동에서 정해진다
                        }
                        else
                        {
                            p.Resolved = true;
                            p.Ignored = true;                        // 재사용 대기 중이고 보관 창 밖
                        }
                        bufferedFlag = false;
                        if (p.Ignored)
                        {
                            // 판정기는 무시된 K 에 방향 기록을 그대로 둔다. 비우지 않아야 다시 누른 K 가 같은 앞, 앞으로 판정된다
                            ignoredPending.Add(p);
                        }
                        else
                        {
                            dirs.Clear();                            // 발동·불발·보관된 K 는 판정기가 커서와 기록을 비운다
                            ignoredPending.Clear();
                        }
                        presses.Add(p);
                        break;
                    }
                    case SimEventType.ArtCast:
                    case SimEventType.ArtFizzleNoCrystal:
                    case SimEventType.ArtFizzleLate:
                        if (decidedCastsPending > 0)
                        {
                            decidedCastsPending--;
                        }
                        else if (pendingBuffered.Count > 0)
                        {
                            Press held = pendingBuffered.Dequeue();
                            held.Outcome = (ArtId)e.A;
                            held.Resolved = true;
                        }
                        break;
                    default:
                        break;
                }
            }

            result.KPresses = presses.Count;
            foreach (Press p in presses)
            {
                if (p.Superseded)
                {
                    continue;
                }
                if (p.TwoPress)
                {
                    var a = new AttemptRecord
                    {
                        Tick = p.Tick,
                        GapUnits = p.GapUnits,
                        FirstGapTicks = p.FirstGapTicks,
                        Outcome = p.Outcome,
                        Ignored = p.Ignored,
                    };
                    result.Attempts.Add(a);
                    if (p.Tick >= result.PracticeTicks)
                    {
                        result.JustAttempts++;
                        if (a.Success && a.GapUnits >= 0 && a.GapUnits <= FeelTargets.JustWindowUnits)
                        {
                            result.JustSuccesses++;
                        }
                    }
                    continue;
                }
                if (!p.InStance || !p.Moving)
                {
                    continue;
                }
                if (p.Outcome == ArtId.None)
                {
                    result.IgnoredOrUnresolvedMoving++;
                    continue;
                }
                result.MisfireCasts++;
                if (p.Outcome == ArtId.Burst)
                {
                    continue;
                }
                result.Misfires++;
                if (p.Outcome == ArtId.Recoil)
                {
                    result.MisfireRecoil++;
                }
                else if (p.Outcome == ArtId.Spear)
                {
                    result.MisfireSpear++;
                }
                else
                {
                    result.MisfireOther++;
                }
            }
            return result;
        }
    }
}
