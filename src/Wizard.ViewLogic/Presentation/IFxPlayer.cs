namespace Wizard.ViewLogic.Presentation
{
    // CONTRACTS 13. 지금은 NullFxPlayer. 실제 재생기는 에셋 반입 뒤(V-13)
    public interface IFxPlayer
    {
        void Play(in FxRequest request);
        void StopAll();
    }
}
