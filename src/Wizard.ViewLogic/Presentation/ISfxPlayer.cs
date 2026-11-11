namespace Wizard.ViewLogic.Presentation
{
    // CONTRACTS 13. 지금은 NullSfxPlayer. 실제 재생기는 에셋 반입 뒤(V-14)
    public interface ISfxPlayer
    {
        void Play(string sfxId, int pitchStep = 0, float volumeDb = 0f);
        void SetBus(SfxBus bus, float volume);
    }
}
