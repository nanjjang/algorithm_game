namespace Wizard.ViewLogic.Presentation
{
    // CONTRACTS 13, T-29. 에셋이 없는 지금의 빈 구현
    public sealed class NullFxPlayer : IFxPlayer
    {
        public int PlayCount { get; private set; }   // 디버그 표시와 테스트가 읽는다

        public void Play(in FxRequest request) => PlayCount++;

        public void StopAll()
        {
        }
    }
}
