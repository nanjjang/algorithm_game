namespace Wizard.TestKit.Shared.Fuzz
{
    // 성질 검사. 지키면 null, 어기면 이유를 돌려준다. 던진 예외도 어긴 것으로 본다.
    public delegate string? FuzzProperty<T>(T input);
}
