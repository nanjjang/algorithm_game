namespace Wizard.Solo.Screens.Story
{
    /// <summary>복수 처치 연출의 한 순간(05 요약서 10.3절, 10 요약서 3.8절 E 15.2). 경과 = w.Tick − RevengeKill 이벤트의 틱.</summary>
    public struct RevengeFrame
    {
        public bool Visible;
        /// <summary>0~60: 보스 공통 느린 사망 + 거울 금이 가장자리까지.</summary>
        public bool SlowMotion;
        public float CrackT;
        /// <summary>60: 흰 번쩍 1회(관리기 경유)와 흔들림 대. 60~84: 거울이 은색 조각으로 흩어짐.</summary>
        public bool Shards;
        public float ShardsT;
        /// <summary>84~144: "금 간 거울 조각"이 떠올라 머리 옆에 붙음(144부터는 붙어 있다).</summary>
        public bool Ornament;
        public float OrnamentT;
        /// <summary>144~264: 칭호 띠 revenge.title_band + revenge.master_line.</summary>
        public bool TitleBand;
        public bool MasterLine;
        /// <summary>84틱부터: "J 넘기기" 안내(넘기기 판정은 Run이 한다 — 이 층은 J를 읽지 않는다).</summary>
        public bool SkipHint;
    }

    /// <summary>처음 쪽지(08 요약서 6.3·13.2절): 내려오고 240틱 뒤 올라간다.</summary>
    public struct NoteFrame
    {
        public bool Visible;
        /// <summary>0 = 위로 다 올라가 숨음 ~ 1 = 다 내려옴.</summary>
        public float Drop01;
        public bool Lifting;
    }

    /// <summary>되비침(05 10.2절): 만개 연출 세 번(24/54/84틱, 8방향)과 +114틱의 번쩍.</summary>
    public struct ReflectionFrame
    {
        public bool Visible;
        /// <summary>만개 i번째의 진행(0~1), 안 보이면 −1.</summary>
        public float Burst0, Burst1, Burst2;
        public float BurstAt(int i) => i == 0 ? Burst0 : i == 1 ? Burst1 : i == 2 ? Burst2 : -1f;
    }

    /// <summary>쓰러짐(10 3.8절): 무채색 전환(덮개로 대신), 2.60초 은색 가루, 3.20초부터 색이 돌아옴.</summary>
    public struct CollapseFrame
    {
        public bool Visible;
        public float OverlayAlpha;
        /// <summary>은색 가루의 진행(0~1), 없으면 −1.</summary>
        public float DissolveT;
    }

    /// <summary>
    /// 연출 시간표(순수 함수). 기준 틱에서의 경과만 보고 보일 것을 정한다. 시뮬레이션을 붙잡지 않는다:
    /// 복수 연출은 판이 WaveClearing을 벗어나면(넘겼거나 264틱이 찼거나) 바로 접힌다 — 길이의 주인은 Run이다(00 5.5절).
    /// </summary>
    public static class StoryTimeline
    {
        public static float Ratio(int elapsed, int start, int end)
        {
            if (end <= start) return elapsed >= start ? 1f : 0f;
            float t = (float)(elapsed - start) / (end - start);
            return t < 0f ? 0f : t > 1f ? 1f : t;
        }

        public static RevengeFrame Revenge(StoryTuning t, int elapsed, bool inWaveClearing)
        {
            var f = new RevengeFrame();
            if (t == null || !inWaveClearing || elapsed < 0 || elapsed >= t.RevengeEnd) return f;
            f.Visible = true;
            f.SlowMotion = elapsed < t.RevengeSlowEnd;
            f.CrackT = Ratio(elapsed, 0, t.RevengeSlowEnd);
            f.Shards = elapsed >= t.RevengeFlashTick && elapsed < t.RevengeOrnamentStart;
            f.ShardsT = Ratio(elapsed, t.RevengeFlashTick, t.RevengeOrnamentStart);
            f.Ornament = elapsed >= t.RevengeOrnamentStart;
            f.OrnamentT = Ratio(elapsed, t.RevengeOrnamentStart, t.RevengeOrnamentEnd);
            f.TitleBand = elapsed >= t.RevengeBandStart;
            f.MasterLine = f.TitleBand;
            f.SkipHint = elapsed >= t.RevengeSkipHint;
            return f;
        }

        /// <summary>복수 흰 번쩍을 청할 순간인가(지난 프레임 경과 → 이번 경과 사이에 60틱을 지났다).</summary>
        public static bool CrossedRevengeFlash(StoryTuning t, int prevElapsed, int elapsed) =>
            t != null && prevElapsed < t.RevengeFlashTick && elapsed >= t.RevengeFlashTick && elapsed < t.RevengeEnd;

        public static NoteFrame FirstNote(StoryTuning t, int elapsed)
        {
            var f = new NoteFrame();
            if (t == null || elapsed < 0) return f;
            int slide = t.FirstNoteSlideTicks > 0 ? t.FirstNoteSlideTicks : 1;
            if (elapsed < t.FirstNoteTicks)
            {
                f.Visible = true;
                f.Drop01 = Ratio(elapsed, 0, slide);
                return f;
            }
            if (elapsed < t.FirstNoteTicks + slide)
            {
                f.Visible = true;
                f.Lifting = true;
                f.Drop01 = 1f - Ratio(elapsed, t.FirstNoteTicks, t.FirstNoteTicks + slide);
            }
            return f;
        }

        public static ReflectionFrame Reflection(StoryTuning t, int elapsed)
        {
            var f = new ReflectionFrame { Burst0 = -1f, Burst1 = -1f, Burst2 = -1f };
            if (t == null || elapsed < 0) return f;
            f.Visible = true;
            var b = t.ReflectionBurstTicks;
            int life = t.ReflectionBurstLifeTicks > 0 ? t.ReflectionBurstLifeTicks : 1;
            for (int i = 0; b != null && i < b.Length && i < 3; i++)
            {
                if (elapsed < b[i] || elapsed >= b[i] + life) continue;
                float p = Ratio(elapsed, b[i], b[i] + life);
                if (i == 0) f.Burst0 = p; else if (i == 1) f.Burst1 = p; else f.Burst2 = p;
            }
            return f;
        }

        public static bool CrossedReflectionFlash(StoryTuning t, int prevElapsed, int elapsed) =>
            t != null && prevElapsed < t.ReflectionHpZeroTicks && elapsed >= t.ReflectionHpZeroTicks;

        public static CollapseFrame Collapse(StoryTuning t, int elapsed)
        {
            var f = new CollapseFrame { DissolveT = -1f };
            if (t == null || elapsed < 0 || elapsed >= t.CollapseTicks) return f;
            f.Visible = true;
            float a = t.CollapseOverlayAlpha;
            if (elapsed < t.CollapseFadeInTicks) a *= Ratio(elapsed + 1, 0, t.CollapseFadeInTicks);
            else if (elapsed >= t.CollapseColorReturnTick) a *= 1f - Ratio(elapsed, t.CollapseColorReturnTick, t.CollapseTicks);
            f.OverlayAlpha = a;
            if (elapsed >= t.CollapseDissolveTick && elapsed < t.CollapseDissolveTick + t.DissolveLifeTicks)
                f.DissolveT = Ratio(elapsed, t.CollapseDissolveTick, t.CollapseDissolveTick + t.DissolveLifeTicks);
            return f;
        }

        /// <summary>거울 금(PhantomMirrorCrack) 0.4초: 진행 0~1, 끝났으면 −1.</summary>
        public static float MirrorCrack(StoryTuning t, int elapsed)
        {
            if (t == null || elapsed < 0 || elapsed >= t.MirrorCrackTicks) return -1f;
            return Ratio(elapsed, 0, t.MirrorCrackTicks);
        }

        /// <summary>보스 등장 이름 띠(1.2초).</summary>
        public static bool IntroBand(StoryTuning t, int elapsed) => t != null && elapsed >= 0 && elapsed < t.IntroTicks;

        /// <summary>복수 꼬리표가 붙은 등장의 첫 만남 한마디(1.2초).</summary>
        public static bool IntroRevengeLine(StoryTuning t, int elapsed) => t != null && elapsed >= 0 && elapsed < t.IntroRevengeLineTicks;
    }
}
