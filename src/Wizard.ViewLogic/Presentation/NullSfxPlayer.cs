namespace Wizard.ViewLogic.Presentation
{
    // CONTRACTS 13, T-29. 에셋이 없는 지금의 빈 구현
    public sealed class NullSfxPlayer : ISfxPlayer
    {
        public int PlayCount { get; private set; }

        public void Play(string sfxId, int pitchStep = 0, float volumeDb = 0f) => PlayCount++;

        public void SetBus(SfxBus bus, float volume)
        {
        }
    }
}
