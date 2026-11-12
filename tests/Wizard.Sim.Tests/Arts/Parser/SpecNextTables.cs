namespace Wizard.Sim.Tests.Arts.Parser
{
    // spec 03 5.6의 DFA 전이 표(Next[노드][F, B, L, R]). 노드는 루트에서의 경로 글자(빈 글자 = 루트). 명세 표 그대로
    internal static class SpecNextTables
    {
        public static readonly string[][] Char1 =
        {
            new[] { "", "F", "B", "L", "R" },
            new[] { "B", "BF", "BB", "L", "R" },
            new[] { "F", "FF", "FB", "L", "R" },
            new[] { "L", "F", "B", "LL", "R" },
            new[] { "R", "F", "B", "L", "RR" },
            new[] { "BB", "BF", "BB", "L", "R" },
            new[] { "BF", "FF", "FB", "L", "R" },
            new[] { "FB", "BF", "FBB", "L", "R" },
            new[] { "FF", "FF", "FB", "L", "R" },
            new[] { "LL", "F", "B", "LL", "R" },
            new[] { "RR", "F", "B", "L", "RR" },
            new[] { "FBB", "FBBF", "BB", "L", "R" },
            new[] { "FBBF", "FF", "FB", "L", "R" },
        };

        public static readonly string[][] Char2 =
        {
            new[] { "", "F", "B", "L", "R" },
            new[] { "B", "BF", "B", "L", "R" },
            new[] { "F", "FF", "FB", "L", "R" },
            new[] { "L", "LF", "B", "LL", "R" },
            new[] { "R", "RF", "B", "L", "RR" },
            new[] { "BF", "FF", "FB", "L", "R" },
            new[] { "FB", "BF", "FBB", "L", "R" },
            new[] { "FF", "FF", "FB", "L", "R" },
            new[] { "LF", "FF", "FB", "L", "R" },
            new[] { "LL", "LF", "B", "LL", "R" },
            new[] { "RF", "FF", "FB", "L", "R" },
            new[] { "RR", "RF", "B", "L", "RR" },
            new[] { "FBB", "FBBF", "B", "L", "R" },
            new[] { "FBBF", "FF", "FB", "L", "R" },
        };

        public static readonly string[][] Char3 =
        {
            new[] { "", "F", "B", "L", "R" },
            new[] { "B", "BF", "B", "L", "R" },
            new[] { "F", "FF", "FB", "FL", "FR" },
            new[] { "L", "F", "B", "LL", "R" },
            new[] { "R", "F", "B", "L", "RR" },
            new[] { "BF", "FF", "FB", "FL", "FR" },
            new[] { "FB", "BF", "FBB", "L", "R" },
            new[] { "FF", "FF", "FB", "FL", "FR" },
            new[] { "FL", "F", "B", "LL", "R" },
            new[] { "FR", "F", "B", "L", "RR" },
            new[] { "LL", "F", "B", "LL", "R" },
            new[] { "RR", "F", "B", "L", "RR" },
            new[] { "FBB", "FBBF", "B", "L", "R" },
            new[] { "FBBF", "FF", "FB", "FL", "FR" },
        };

        public static readonly string[][] Char4 =
        {
            new[] { "", "F", "B", "L", "R" },
            new[] { "B", "BF", "B", "BL", "BR" },
            new[] { "F", "FF", "FB", "L", "R" },
            new[] { "L", "F", "B", "LL", "R" },
            new[] { "R", "F", "B", "L", "RR" },
            new[] { "BF", "FF", "FB", "L", "R" },
            new[] { "BL", "F", "B", "LL", "R" },
            new[] { "BR", "F", "B", "L", "RR" },
            new[] { "FB", "BF", "FBB", "BL", "BR" },
            new[] { "FF", "FF", "FB", "L", "R" },
            new[] { "LL", "F", "B", "LL", "R" },
            new[] { "RR", "F", "B", "L", "RR" },
            new[] { "FBB", "FBBF", "B", "BL", "BR" },
            new[] { "FBBF", "FF", "FB", "L", "R" },
        };

        public static readonly string[][] Char5 =
        {
            new[] { "", "F", "B", "L", "R" },
            new[] { "B", "BF", "B", "L", "R" },
            new[] { "F", "FF", "FB", "L", "R" },
            new[] { "L", "F", "B", "LL", "LR" },
            new[] { "R", "F", "B", "RL", "RR" },
            new[] { "BF", "FF", "FB", "L", "R" },
            new[] { "FB", "BF", "FBB", "L", "R" },
            new[] { "FF", "FF", "FB", "L", "R" },
            new[] { "LL", "F", "B", "LL", "LR" },
            new[] { "LR", "F", "B", "RL", "RR" },
            new[] { "RL", "F", "B", "LL", "LR" },
            new[] { "RR", "F", "B", "RL", "RR" },
            new[] { "FBB", "FBBF", "B", "L", "R" },
            new[] { "FBBF", "FF", "FB", "L", "R" },
        };
    }
}
