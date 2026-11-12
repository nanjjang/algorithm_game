namespace Wizard.BalanceLab.Feel
{
    // 사람 기록 분석의 매개변수. 기본값은 FeelTargets 의 것이고 명령줄로 바꿀 수 있다(바꾼 값은 JSON 에 그대로 적힌다)
    internal sealed class FeelOptions
    {
        public long PracticeMinutes = FeelTargets.PracticeMinutes;
        public long AttemptWindowMs = FeelTargets.AttemptWindowMsDefault;
    }
}
