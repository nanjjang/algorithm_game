namespace Wizard.Solo.Meta.Records
{
    /// <summary>
    /// 지팡이별 강화 기록을 적는다(강화 화면의 기록 칸 — 시도·성공·유지·하락·대성공, 도전 단계별 표기 확률 대비 실제 성공률).
    /// 강화 서비스가 결과를 정한 **뒤** 부른다. 확률·비용·판정·난수에는 손대지 않는다(보여 주기만 하는 기록이다).
    /// 다섯 지팡이가 같은 규칙으로 적힌다.
    /// </summary>
    public static class EnhanceRecordBook
    {
        /// <summary>강화 한 번(Enhance)의 결과를 적는다. 변이 제시는 결정(ResolveMutation) 때 결과를 센다 — 시도와 표기 확률은 지금 센다.</summary>
        public static void NoteAttempt(WandData w, EnhanceResult r)
        {
            if (w == null || r == null) return;
            if (w.EnhanceStats == null) w.EnhanceStats = new WandEnhanceStats();
            WandEnhanceStats s = w.EnhanceStats;
            StageEnhanceStats st = s.Stage(r.StageBefore + 1);
            s.Attempts++;
            st.Attempts++;
            st.ShownPctSum += r.Breakdown.SuccessPct;
            switch (r.Outcome)
            {
                case EnhanceOutcome.GreatSuccess:
                    s.Successes++;
                    s.GreatSuccesses++;
                    st.Successes++;
                    break;
                case EnhanceOutcome.Success:
                    s.Successes++;
                    st.Successes++;
                    break;
                case EnhanceOutcome.FailDrop:
                    s.Drops++;
                    break;
                case EnhanceOutcome.FailCharmBlocked:
                case EnhanceOutcome.FailNoDrop:
                    s.Keeps++;
                    break;
                // MutationOffered: 결정 때 NoteResolve가 센다(받음 = 변이, 거절 = 그 실패 결과).
            }
        }

        /// <summary>변이 결정(ResolveMutation)의 결과를 적는다. 시도 수는 NoteAttempt가 이미 셌다.</summary>
        public static void NoteResolve(WandData w, EnhanceResult r)
        {
            if (w == null || r == null) return;
            if (w.EnhanceStats == null) w.EnhanceStats = new WandEnhanceStats();
            WandEnhanceStats s = w.EnhanceStats;
            switch (r.Outcome)
            {
                case EnhanceOutcome.MutationAccepted:
                    s.Mutations++;
                    break;
                case EnhanceOutcome.FailDrop:
                    s.Drops++;
                    break;
                case EnhanceOutcome.FailCharmBlocked:
                case EnhanceOutcome.FailNoDrop:
                    s.Keeps++;
                    break;
            }
        }

        /// <summary>그 도전 단계의 평균 표기 확률(%, 시도가 없으면 −1).</summary>
        public static double AverageShownPct(StageEnhanceStats st) =>
            st == null || st.Attempts <= 0 ? -1.0 : (double)st.ShownPctSum / st.Attempts;

        /// <summary>그 도전 단계의 실제 성공률(%, 시도가 없으면 −1).</summary>
        public static double ActualPct(StageEnhanceStats st) =>
            st == null || st.Attempts <= 0 ? -1.0 : 100.0 * st.Successes / st.Attempts;
    }
}
