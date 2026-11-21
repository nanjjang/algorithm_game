namespace Wizard.Solo.Screens.Story
{
    public enum StoryFlashKind { None = 0, Full = 1, Border = 2 }

    /// <summary>번쩍 요청의 결과.</summary>
    public struct StoryFlash
    {
        public StoryFlashKind Kind;
        /// <summary>Full일 때 적용 불투명도(0~단계 상한).</summary>
        public float Opacity;
        public int StartTick;
    }

    /// <summary>
    /// 이 층의 전체 화면 번쩍 관리기(08 요약서 14.1절). 적용 불투명도 = min(요청, 단계 상한) × flash_scale.
    /// 단계 상한 Full 0.35 / Reduced 0.15 / Minimal = 테두리 강조. 어느 1초(60틱) 창에서도 2회 이하, 간격 24틱 이상 —
    /// 넘는 요청은 버리지 않고 테두리 강조(흰 선 8px, 9틱)로 바꾼다. 테두리 강조는 세지 않는다. 시간은 시뮬레이션 틱.
    /// </summary>
    public sealed class StoryFlashGate
    {
        readonly StoryTuning _t;
        readonly int[] _recent = new int[8];
        int _count;

        public StoryFlashGate(StoryTuning t) { _t = t ?? StoryTuning.Defaults(); }

        public void Reset() { _count = 0; }

        /// <param name="effectsLevel">0 전체 / 1 줄임 / 2 최소.</param>
        /// <param name="flashScale">설정 flash_scale(0~1).</param>
        public StoryFlash Request(float opacity, int tick, int effectsLevel, float flashScale)
        {
            var r = new StoryFlash { StartTick = tick };
            if (effectsLevel >= 2) { r.Kind = StoryFlashKind.Border; return r; }
            float cap = effectsLevel == 1 ? _t.FlashReducedCap : _t.FlashFullCap;
            if (!Allowed(tick)) { r.Kind = StoryFlashKind.Border; return r; }
            float scale = flashScale < 0f ? 0f : flashScale > 1f ? 1f : flashScale;
            float applied = (opacity < cap ? opacity : cap) * scale;
            if (applied <= 0f) { r.Kind = StoryFlashKind.None; return r; }
            Remember(tick);
            r.Kind = StoryFlashKind.Full;
            r.Opacity = applied;
            return r;
        }

        bool Allowed(int tick)
        {
            int inWindow = 0;
            for (int i = 0; i < _count; i++)
            {
                int d = tick - _recent[i];
                if (d < _t.FlashMinIntervalTicks) return false;
                if (d < _t.FlashWindowTicks) inWindow++;
            }
            return inWindow < _t.FlashMaxPerWindow;
        }

        void Remember(int tick)
        {
            if (_count == _recent.Length)
            {
                for (int i = 1; i < _recent.Length; i++) _recent[i - 1] = _recent[i];
                _count--;
            }
            _recent[_count++] = tick;
        }

        /// <summary>번쩍의 그 순간 불투명도: 켜짐 on틱 동안 올라가고 꺼짐 off틱에 걸쳐 내려간다(0.05초 안에 켜지고 0.10초 이상에 꺼짐).</summary>
        public static float AlphaAt(StoryFlash f, int tick, int onTicks, int offTicks)
        {
            if (f.Kind != StoryFlashKind.Full) return 0f;
            int e = tick - f.StartTick;
            if (e < 0) return 0f;
            if (onTicks < 1) onTicks = 1;
            if (e < onTicks) return f.Opacity * (e + 1) / onTicks;
            int off = e - onTicks;
            if (offTicks < 1 || off >= offTicks) return 0f;
            return f.Opacity * (1f - (float)off / offTicks);
        }

        /// <summary>테두리 강조가 보이는가(시작부터 border_ticks 동안).</summary>
        public static bool BorderAt(StoryFlash f, int tick, int borderTicks)
        {
            if (f.Kind != StoryFlashKind.Border) return false;
            int e = tick - f.StartTick;
            return e >= 0 && e < borderTicks;
        }
    }
}
