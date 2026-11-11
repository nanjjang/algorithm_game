using System;
using Wizard.Core;

namespace Wizard.Sim.Events
{
    // CONTRACTS 3.3. 표시 계층이 매 프레임 읽는다. 코어 상태를 바꾸지 못한다
    public interface ISimView
    {
        uint Tick { get; }
        int PlayerCount { get; }
        PlayerView Player(PlayerSlot slot);
        ReadOnlySpan<EnemyView> Enemies { get; }
        ReadOnlySpan<ProjectileView> Projectiles { get; }
        ReadOnlySpan<TelegraphView> Telegraphs { get; }   // 시작 틱과 길이를 준다. 표시는 이것으로만 채움을 계산한다
        ReadOnlySpan<HazardView> Hazards { get; }
        WaveView Wave { get; }
        CampView Camp { get; }
        DuelView Duel { get; }
    }
}
