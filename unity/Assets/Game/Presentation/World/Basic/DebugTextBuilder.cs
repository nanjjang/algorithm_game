#nullable enable
using System.Globalization;
using System.Text;
using Wizard.Core;
using Wizard.Sim.Events;
using Wizard.Sim.Heat;
using Wizard.ViewLogic.Convert;

namespace Wizard.Unity.Presentation.World.Basic
{
    // 디버그 글자: 틱, 한 프레임에 돈 틱 수, 도형 수, 플레이어마다 열·링·잠금·자세, 마지막 섬 간격(밀리초, GDD 16 1단계).
    // 값은 ISimView·표시 이벤트(GapReadout)·보간기에서만 읽는다. 틱이나 섬 간격 보고가 바뀔 때만 글자를 다시 만든다.
    // [임시] 화면 글자의 이름 표(M6-TXT-01)가 아직 없어 숫자와 ASCII 약자만 쓴다(OPEN-QUESTIONS). 섬 간격 막대 S-1401 은 M5 연습장이다.
    public sealed class DebugTextBuilder
    {
        private readonly StringBuilder _sb = new StringBuilder();
        private uint _tick;
        private int _gapVersion = -1;
        private bool _built;

        public string Text { get; private set; } = string.Empty;

        // 글자를 새로 만들었으면 참
        public bool Update(ISimView view, ShapeInterpolator shapes, GapReadout gaps)
        {
            if (_built && view.Tick == _tick && gaps.Version == _gapVersion)
            {
                return false;
            }
            _built = true;
            _tick = view.Tick;
            _gapVersion = gaps.Version;
            CultureInfo inv = CultureInfo.InvariantCulture;
            _sb.Clear();
            _sb.Append("tick ").Append(view.Tick.ToString(inv)).Append("  step ").Append(shapes.TickSpan.ToString(inv));
            _sb.Append("  shapes ").Append(shapes.Current.PlayerCount.ToString(inv)).Append('+').Append(shapes.Current.EntityCount.ToString(inv));
            if (shapes.Current.DroppedCount > 0)
            {
                _sb.Append("  dropped ").Append(shapes.Current.DroppedCount.ToString(inv));
            }
            for (int slot = 0; slot < view.PlayerCount; slot++)
            {
                PlayerView p = view.Player(new PlayerSlot((byte)slot));
                if (!PlayerShapeSampler.IsPresent(in p))
                {
                    continue;                                 // 시작 상태가 서기 전(첫 틱 전)
                }
                string tag = "\nP" + (slot + 1).ToString(inv);
                _sb.Append(tag).Append(" heat ").Append((p.HeatHu / HeatRules.HuPerHeat).ToString(inv))
                   .Append('/').Append((p.CritStartHu / HeatRules.HuPerHeat).ToString(inv))
                   .Append(' ').Append(p.HeatRing.ToString());
                if (p.HeatRing == HeatRingState.Locked)
                {
                    _sb.Append(" lock ").Append(p.LockRemainTicks.ToString(inv)).Append('/').Append(p.LockTotalTicks.ToString(inv));
                }
                if (p.ExemptArmed)
                {
                    _sb.Append(" exempt");
                }
                _sb.Append("  ").Append(p.Stance.ToString()).Append(' ').Append(p.Facing.ToString())
                   .Append("  hp ").Append(p.HpPoints.ToString(inv)).Append('/').Append(p.MaxHpPoints.ToString(inv));
                _sb.Append(tag).Append(" gap ");
                if (gaps.TryGet(slot, out GapReport g))
                {
                    if (g.HasGap)
                    {
                        _sb.Append(g.RoundedMilliseconds.ToString(inv)).Append(" ms");
                    }
                    else
                    {
                        _sb.Append('-');
                    }
                    if (g.Decided)
                    {
                        _sb.Append("  art ").Append(g.Art.ToString());
                        if (g.Just)
                        {
                            _sb.Append(" just");
                        }
                    }
                    _sb.Append("  @").Append(g.Tick.ToString(inv));
                }
                else
                {
                    _sb.Append('-');
                }
            }
            Text = _sb.ToString();
            return true;
        }
    }
}
