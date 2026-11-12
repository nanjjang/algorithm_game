namespace Wizard.ViewLogic.Common.Dialogs
{
    // Loading 을 열 때 주는 값: 한 줄 도움말(이름 표 열쇠)
    public sealed class LoadingRequest
    {
        public LoadingRequest(string hintKey)
        {
            HintKey = hintKey;
        }

        public string HintKey { get; }
    }
}
