namespace Wizard.Unity.Input
{
    // 단조 시계(초). FixedTickDriver 의 "now" 와 RawKeyEvent.TimeSeconds 는 같은 시간 축이어야 한다(ARCHITECTURE 6.1, CONTRACTS 6.5:
    // SimClock 의 epoch 로 키 시각을 양자화한다). Time.time·Time.deltaTime 처럼 Time.timeScale 을 따르는 시계를 쓰지 않는다(spec 01 D12).
    public interface IMonotonicClock
    {
        double NowSeconds { get; }
    }
}
