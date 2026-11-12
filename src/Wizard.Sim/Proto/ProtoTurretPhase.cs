namespace Wizard.Sim.Proto
{
    // 포탑의 상태 기계(spec 05 4.4 의 술사에서 이동과 엄폐를 뺀 것): Idle ─(사거리 안 && 쿨다운 끝)→ Cast(24틱) → 발사 → Recovery(36틱) → Idle.
    // 상태와 해시에 들어가므로 번호는 바꾸지 않는다
    public enum ProtoTurretPhase : byte
    {
        Idle = 0,
        Cast = 1,
        Recovery = 2,
    }
}
