namespace Wizard.Sim.Proto
{
    // 시제품의 적 종류(GDD 16 1단계: 슬라임 무한 생성, 3갈래 탄 포탑 1개). 상태와 해시에 들어가므로 번호는 바꾸지 않는다
    public enum ProtoEnemyKind : byte
    {
        None = 0,
        Slime = 1,
        Turret = 2,
    }
}
