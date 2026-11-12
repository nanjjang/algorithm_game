namespace Wizard.ViewLogic.Presentation.Limits
{
    // effects.md 9.3 "넘쳤을 때의 처리". 번호는 더하기만 한다
    public enum ParticleRecurrence : byte
    {
        OneShot = 0,     // 사건 하나에 한 번: 몫이 차면 그 분류의 가장 오래된 것부터 0.1초에 흐려지며 자리를 내준다. 새 사건은 항상 보인다
        Recurring = 1,   // 계속 새로 나오는 것: 몫이 차면 새로 만들지 않는다. 이미 나온 것은 제 수명대로 사라진다
    }
}
