namespace Wizard.Sim.Heat
{
    // HeatFire.Kind
    internal enum HeatFireKind : byte
    {
        None = 0,                          // J 를 누르지 않았거나 쏠 수 없는 때(간격 중, 광장, 주문 없음)
        DryWhileLocked = 1,                // 잠금 중에 J 를 이번 틱에 눌렀다(FireDryWhileLocked)
        Fired = 2,
    }
}
