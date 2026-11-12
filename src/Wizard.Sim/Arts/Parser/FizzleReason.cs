namespace Wizard.Sim.Arts
{
    // spec 03 7.4. 불발의 이유. 번호는 기록·해시에 들어가므로 바꾸지 않는다
    public enum FizzleReason : byte
    {
        None = 0,
        NoCrystal = 1,          // 결정 부족(외상·부모 대체까지 본 뒤. spec 03 7.3)
        FullBloomLate = 2,      // 만개 노드의 0.18초 재검사에서 떨어짐(spec 03 7.2)
    }
}
