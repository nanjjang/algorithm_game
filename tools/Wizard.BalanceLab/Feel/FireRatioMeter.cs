using System.Collections.Generic;
using Wizard.BalanceLab.Core;
using Wizard.Content;
using Wizard.Core;
using Wizard.Sim;
using Wizard.Sim.Events;
using Wizard.Sim.Heat;
using Wizard.Sim.Input;

namespace Wizard.BalanceLab.Feel
{
    // 봇 입력으로 "임계 구간 플레이 ÷ 꾹 누르기" 화력 비를 잰다(GDD 16절 1단계, GDD 18절).
    //
    // 화력 = 발사한 탄의 기본 피해 × 임계 배율 × 탄 수(SpellCast 이벤트), 맞았는지와 무관. 적의 위치와 조준에 흔들리지 않는 값이다.
    // 비교는 완전한 주기만 담은 창으로 한다: 꾹 누르기는 HeatUnlocked 틱 사이, 임계 구간은 "쉬다가 다시 쏘기 시작한" 틱 사이.
    // 임계 구간 봇: 임계 n발(다음 발사가 100을 넘기기 직전까지) → 쉼(0.2초 대기 + 냉각) → 임계로 쏠 수 있는 마지막 틱에 다시 쏜다.
    // 처음 열이 0에서 차오르는 구간은 창 밖이다(주기가 아니다).
    //
    // 연습 세계에는 포탑이 있고, 포탑 탄을 스치면 열이 임계 시작까지 오르고 잠금이 풀린다(스침 보상, spec 01 8.4). 그러면 열 주기가
    // 흐트러져 화력 비가 아니라 스침을 재게 된다. 그래서 봇은 먼저 아래 벽까지 걸어가 포탑의 사거리 밖에 서고(SettleTicks),
    // 그 뒤에 쏜다. 스침 보상이 한 번이라도 있었으면 BotRun.GrazeEvents 가 0이 아니고 자체 점검이 잡는다.
    internal static class FireRatioMeter
    {
        public static BotRun Run(ContentDb db, BotKind kind, uint ticks, BlockId core, bool splitThree)
            => Run(db, kind, ticks, core, splitThree, FeelTargets.CritMulPermille);

        // critMulPermille 는 자체 점검이 일부러 틀린 값을 넣어 판정이 그것을 잡는지 볼 때만 바꾼다
        public static BotRun Run(ContentDb db, BotKind kind, uint ticks, BlockId core, bool splitThree, int critMulPermille)
        {
            CharacterDef character = db.Character(CharacterId.Baseline);
            BlockDef block = db.Block(core);
            int shotHeatHu = block.HeatHu;
            if (splitThree)
            {
                shotHeatHu = (int)((long)shotHeatHu * FeelTargets.SplitThreeHeatMulPermille / 1000);
            }
            Fix64 normalDamage = Fix64.MulPermille(block.BaseDamage, 1000);
            Fix64 critDamage = Fix64.MulPermille(block.BaseDamage, critMulPermille);

            var rig = new PracticeRig(db);
            if (core != BlockId.MagicBolt)
            {
                rig.Command(RunCommandOp.SetSlot, 0, (int)core);
            }
            if (splitThree)
            {
                rig.Command(RunCommandOp.SetSlot, 1, (int)BlockId.SplitThree);
            }

            var tracker = new HeatTracker(character, block);
            var cumDamage = new long[ticks + 1];
            var cumShots = new int[ticks + 1];
            var cumCrit = new int[ticks + 1];
            var boundaries = new List<uint>();
            bool jDown = false;
            bool firing = true;                                 // 임계 구간 봇의 상태: 쏘는 중 / 쉬는 중
            int logIndex = 0;
            int grazes = 0;
            uint ran = 0;

            for (uint t = 0; t < ticks; t++)
            {
                if (t < FeelTargets.SettleTicks)
                {
                    if (t == 0)
                    {
                        rig.Key(LogicalKey.Down, true);
                    }
                    else if (t == FeelTargets.WalkTicks)
                    {
                        rig.Key(LogicalKey.Down, false);
                    }
                }
                else
                {
                    bool want = true;
                    if (kind == BotKind.CritZone)
                    {
                        int heat = tracker.HeatAt(t);
                        if (tracker.Locked)
                        {
                            firing = false;                     // 잠겼다(예상 밖). 풀릴 때까지 쉰다
                        }
                        else if (firing && heat + shotHeatHu > HeatRules.MaxHeatHu)
                        {
                            firing = false;                     // 다음 발사가 100을 넘겨 잠글 것이다: 멈추고 식힌다
                        }
                        else if (!firing && heat - character.CoolHuPerTick < character.CritStartHu - shotHeatHu)
                        {
                            // 한 틱 더 식으면 다음 발사가 임계에 못 닿는다(발사 후 열 < 임계 시작): 지금이 임계로 쏠 수 있는 마지막 틱이다.
                            // 가장 오래 식힌 뒤 임계로 쏘는 것이 임계 구간 플레이의 한 주기다(GDD 18절)
                            firing = true;
                            boundaries.Add(t);
                        }
                        want = firing;
                    }
                    if (want != jDown)
                    {
                        rig.Key(LogicalKey.Fire, want);
                        jDown = want;
                    }
                }
                rig.Step();
                ran = t + 1;

                long damage = 0;
                int shots = 0;
                int crits = 0;
                List<SimEvent> events = rig.Log.Events;
                for (; logIndex < events.Count; logIndex++)
                {
                    SimEvent e = events[logIndex];
                    tracker.Observe(in e);
                    switch (e.Type)
                    {
                        case SimEventType.SpellCast:
                            // SpellCast: A = shotId, B = 낸 탄 수, C = 임계(0/1)
                            shots++;
                            damage += (e.C == 1 ? critDamage : normalDamage).Raw * e.B;
                            if (e.C == 1)
                            {
                                crits++;
                            }
                            break;
                        case SimEventType.HeatUnlocked:
                            if (kind == BotKind.HoldFire)
                            {
                                boundaries.Add(e.Tick);
                            }
                            break;
                        case SimEventType.GrazeAwarded:
                        case SimEventType.HeatExemptArmed:
                            grazes++;
                            break;
                        default:
                            break;
                    }
                }
                cumDamage[t + 1] = cumDamage[t] + damage;
                cumShots[t + 1] = cumShots[t] + shots;
                cumCrit[t + 1] = cumCrit[t] + crits;
                if (rig.World.IsOver)
                {
                    break;
                }
            }

            var run = new BotRun { Kind = kind, Ticks = ran, TotalShots = cumShots[ran], GrazeEvents = grazes };
            if (boundaries.Count >= 2)
            {
                uint start = boundaries[0];
                uint end = boundaries[boundaries.Count - 1];
                run.Cycles = boundaries.Count - 1;
                run.WindowStartTick = start;
                run.WindowEndTick = end;
                run.WindowDamageRaw = cumDamage[end] - cumDamage[start];
                run.WindowShots = cumShots[end] - cumShots[start];
                run.WindowCritShots = cumCrit[end] - cumCrit[start];
            }
            return run;
        }

        public static FireRatioReport Measure(ContentDb db, uint ticks, BlockId core, bool splitThree)
        {
            BotRun hold = Run(db, BotKind.HoldFire, ticks, core, splitThree);
            BotRun crit = Run(db, BotKind.CritZone, ticks, core, splitThree);
            return new FireRatioReport(hold, crit, core, splitThree);
        }
    }
}
