using System;
using Wizard.Core;
using Wizard.Sim.Events;

namespace Wizard.Sim
{
    // CONTRACTS 3.3 ISimView 의 세계 쪽 구현. 세계 상태를 바꾸지 못한다.
    // 뼈대가 아는 것(틱, 플레이어 수, 슬롯 범위)만 답한다. 플레이어·적·투사체·예고·지대·웨이브·야영·결투의 값은
    // 그 상태를 가진 모듈이 채울 자리인데, 뷰를 채우는 훅이 계약(CONTRACTS 3.2 훅 표)에 없어 지금은 기본값과 빈 범위다
    // (결과 보고의 계약 변경 요청).
    internal sealed class SimWorldView : ISimView
    {
        private readonly SimWorld _world;

        public SimWorldView(SimWorld world)
        {
            _world = world;
        }

        public uint Tick => _world.Tick;

        public int PlayerCount => _world.PlayerCount;

        public PlayerView Player(PlayerSlot slot)
        {
            if (!_world.IsValidSlot(slot))
            {
                throw new ArgumentOutOfRangeException(nameof(slot));
            }
            return default;
        }

        public ReadOnlySpan<EnemyView> Enemies => ReadOnlySpan<EnemyView>.Empty;

        public ReadOnlySpan<ProjectileView> Projectiles => ReadOnlySpan<ProjectileView>.Empty;

        public ReadOnlySpan<TelegraphView> Telegraphs => ReadOnlySpan<TelegraphView>.Empty;

        public ReadOnlySpan<HazardView> Hazards => ReadOnlySpan<HazardView>.Empty;

        public WaveView Wave => default;

        public CampView Camp => default;

        public DuelView Duel => default;
    }
}
