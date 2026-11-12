namespace Wizard.ViewLogic.Presentation.Limits
{
    // effects.md 9.3 "지팡이 문서가 채우는 몫"의 열. 번호는 더하기만 한다
    public enum WandParticleKind : byte
    {
        Idle = 0,     // 대기 입자(동시), 분류 5, 수명 2초
        Muzzle = 1,   // 발사 1회의 총구 입자, 분류 4, 수명 0.2초
        Shed = 2,     // 탄 1발이 흘리는 입자(동시), 분류 4, 수명 0.3초
        Hit = 3,      // 적중 1회, 분류 2, 수명 0.4초
    }
}
