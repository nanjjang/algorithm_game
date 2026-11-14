namespace Wizard.Solo.Sim
{
    /// <summary>점수 규칙 등록(00 문서 2.1절).</summary>
    public static partial class SimModuleFactory
    {
        static partial void CreateScore(ref ScoreRules m) { m = new Score.ScoreCalculator(); }
    }
}
