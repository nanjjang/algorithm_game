namespace Wizard.Rules
{
    // CONTRACTS 4.1. 판 씨앗과 무관한 난수. 서버 = CSPRNG, 오프라인 = 기기 난수, 테스트 = 고정 열
    public interface IRandomSource
    {
        int NextInt(int exclusiveMax);
        ulong NextU64();
    }
}
