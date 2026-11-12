namespace Wizard.BalanceLab.Feel
{
    // 축 하나의 판정. NoData = 판정할 시도가 없다(통과도 실패도 아니다)
    internal enum Verdict
    {
        Pass = 1,
        Fail = 2,
        NoData = 3,
    }

    internal static class VerdictNames
    {
        public static string Of(Verdict v)
        {
            switch (v)
            {
                case Verdict.Pass:
                    return "pass";
                case Verdict.Fail:
                    return "fail";
                default:
                    return "no_data";
            }
        }

        // 하나라도 실패면 fail, 아니면 하나라도 자료 없음이면 inconclusive, 아니면 pass
        public static string Overall(params Verdict[] axes)
        {
            bool noData = false;
            foreach (Verdict v in axes)
            {
                if (v == Verdict.Fail)
                {
                    return "fail";
                }
                if (v == Verdict.NoData)
                {
                    noData = true;
                }
            }
            return noData ? "inconclusive" : "pass";
        }
    }
}
