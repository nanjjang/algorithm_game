using System.Collections.Generic;
using Wizard.Content;
using Wizard.Sim;
using Wizard.Sim.Events;

namespace Wizard.BalanceLab.Feel
{
    // 봇이 열을 아는 방법. 표시 계층의 뷰(ISimView.Player)는 아직 채워지지 않으므로(SimWorldView 의 열린 계약) 공개된 이벤트만 쓴다:
    //   ShotFired.V = 쏜 뒤의 열(HU), HeatLockEntered.A = 풀리는 틱, HeatUnlocked(열 0).
    // 그 사이의 냉각은 spec 01 7.4 의 식으로 예측한다: 마지막 발사 틱 s 의 뒤 간격 + 대기가 지난 틱의 끝부터 틱당 CoolHuPerTick 씩 준다.
    // 예측이 세계와 어긋나면 봇의 주기가 달라져 FeelSelfCheck 의 주기 검사가 잡는다.
    internal sealed class HeatTracker
    {
        private readonly int _intervalTicks;
        private readonly int _coolDelayTicks;
        private readonly int _coolHuPerTick;
        private long _lastShotTick = -1;
        private int _heatAfterHu;

        public HeatTracker(CharacterDef character, BlockDef core)
        {
            _intervalTicks = core.IntervalTicks;
            _coolDelayTicks = character.CoolDelayTicks;
            _coolHuPerTick = character.CoolHuPerTick;
        }

        public bool Locked { get; private set; }

        public void Observe(in SimEvent e)
        {
            switch (e.Type)
            {
                case SimEventType.ShotFired:
                    _lastShotTick = e.Tick;
                    _heatAfterHu = (int)e.V;
                    break;
                case SimEventType.HeatLockEntered:
                    Locked = true;
                    break;
                case SimEventType.HeatUnlocked:
                    Locked = false;
                    _heatAfterHu = 0;
                    break;
                default:
                    break;
            }
        }

        // 틱 t 의 Step 을 하기 전의 열(HU)
        public int HeatAt(uint t)
        {
            if (_lastShotTick < 0)
            {
                return 0;
            }
            long coolStart = _lastShotTick + _intervalTicks + _coolDelayTicks;
            long coolTicks = t > coolStart ? t - coolStart : 0;
            long heat = _heatAfterHu - coolTicks * _coolHuPerTick;
            return heat > 0 ? (int)heat : 0;
        }
    }
}
