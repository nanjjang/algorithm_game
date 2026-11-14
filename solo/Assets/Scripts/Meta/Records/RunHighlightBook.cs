namespace Wizard.Solo.Meta.Records
{
    /// <summary>판 끝 요약의 "이번 판 최고 순간" 중 캐릭터 개인 최고와 견주는 셋(한 방 피해, 스침 연속, 콤보).</summary>
    public struct RunHighlightBests
    {
        public bool Hit, GrazeChain, Combo;
        public bool Any => Hit || GrazeChain || Combo;
    }

    /// <summary>
    /// 판 끝 요약의 개인 최고(엔진 없음). 값은 표시 이벤트를 모은 통계에서 온다 — 점수·보상·도전 과제에는 쓰지 않는다(보여 주기만).
    /// Compare는 프로필을 바꾸지 않고(견줄 이전 기록이 0이면 새 기록으로 치지 않는다), Apply가 더 큰 값만 적는다(저장은 부르는 쪽).
    /// </summary>
    public static class RunHighlightBook
    {
        public static RunHighlightBests Compare(CharacterRecord rec, int maxHit, int maxGrazeChain, int maxCombo)
        {
            var b = new RunHighlightBests();
            if (rec == null) return b;
            // 견줄 기록이 있을 때만 "새 기록"(첫 판은 모든 값이 처음이라 딱지를 붙이지 않는다).
            b.Hit = rec.BestHit > 0 && maxHit > rec.BestHit;
            b.GrazeChain = rec.BestGrazeChain > 0 && maxGrazeChain > rec.BestGrazeChain;
            b.Combo = rec.BestCombo > 0 && maxCombo > rec.BestCombo;
            return b;
        }

        /// <summary>더 큰 값만 적는다. 바뀐 것이 있으면 true.</summary>
        public static bool Apply(CharacterRecord rec, int maxHit, int maxGrazeChain, int maxCombo)
        {
            if (rec == null) return false;
            bool changed = false;
            if (maxHit > rec.BestHit) { rec.BestHit = maxHit; changed = true; }
            if (maxGrazeChain > rec.BestGrazeChain) { rec.BestGrazeChain = maxGrazeChain; changed = true; }
            if (maxCombo > rec.BestCombo) { rec.BestCombo = maxCombo; changed = true; }
            return changed;
        }
    }
}
