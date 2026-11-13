namespace Wizard.ViewLogic.Presentation
{
    // 연속 스침 음높이의 표시 데이터(event_bindings.json 의 graze_chain. spec 01 17.2 chainIndex 0~5, CONTRACTS 0.3 T-50)
    public readonly struct GrazeChainSettings
    {
        public GrazeChainSettings(int windowTicks, int maxStep)
        {
            WindowTicks = windowTicks;
            MaxStep = maxStep;
        }

        // 같은 슬롯의 앞 스침과 틱 차가 이 값 이하이면 한 단 올린다
        public int WindowTicks { get; }

        // 단의 위 끝(0부터 이 값까지)
        public int MaxStep { get; }
    }
}
