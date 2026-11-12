using System;
using Wizard.Core;
using Wizard.Sim.Events;

namespace Wizard.Sim
{
    // CONTRACTS 3.3 ISimView 의 세계 쪽 구현. 세계 상태를 바꾸지 못한다.
    // 뼈대가 아는 것(틱, 플레이어 수, 슬롯 범위)은 직접 답하고, 나머지는 그 상태를 가진 모듈의 뷰 훅이 채운다(SimWorld.View.cs 의 Fill…, T-54).
    //
    // 캐시가 없다: 읽을 때마다 지금 상태에서 다시 만든다. Step 안에서 읽는 쪽(적 두뇌의 EnemyContext.World)이 있어도, 바깥에서 누가 먼저
    // 읽었는지에 따라 값이 달라지지 않는다. 목록 뷰가 돌려준 범위는 이 뷰의 버퍼를 가리키므로 같은 속성을 다시 읽거나 세계가 진행하면 무효다.
    // 읽는 쪽은 한 번 읽어 지역 변수에 두고 쓴다. 버퍼는 처음 읽을 때 만든다(읽지 않는 세계, 스냅숏의 그림자 세계는 버퍼를 갖지 않는다).
    internal sealed class SimWorldView : ISimView
    {
        private readonly SimWorld _world;
        private ViewBuffer<EnemyView>? _enemies;
        private ViewBuffer<ProjectileView>? _projectiles;
        private ViewBuffer<TelegraphView>? _telegraphs;
        private ViewBuffer<HazardView>? _hazards;

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
            PlayerViewDraft draft = default;
            _world.FillPlayerView(slot.Value, ref draft);
            return draft.ToView();
        }

        public ReadOnlySpan<EnemyView> Enemies
        {
            get
            {
                ViewBuffer<EnemyView> b = _enemies ??= new ViewBuffer<EnemyView>();
                b.Clear();
                _world.FillEnemyViews(b);
                return b.AsSpan();
            }
        }

        public ReadOnlySpan<ProjectileView> Projectiles
        {
            get
            {
                ViewBuffer<ProjectileView> b = _projectiles ??= new ViewBuffer<ProjectileView>();
                b.Clear();
                _world.FillProjectileViews(b);
                return b.AsSpan();
            }
        }

        public ReadOnlySpan<TelegraphView> Telegraphs
        {
            get
            {
                ViewBuffer<TelegraphView> b = _telegraphs ??= new ViewBuffer<TelegraphView>();
                b.Clear();
                _world.FillTelegraphViews(b);
                return b.AsSpan();
            }
        }

        public ReadOnlySpan<HazardView> Hazards
        {
            get
            {
                ViewBuffer<HazardView> b = _hazards ??= new ViewBuffer<HazardView>();
                b.Clear();
                _world.FillHazardViews(b);
                return b.AsSpan();
            }
        }

        public WaveView Wave
        {
            get
            {
                WaveView view = default;
                _world.FillWaveView(ref view);
                return view;
            }
        }

        public CampView Camp
        {
            get
            {
                CampView view = default;
                _world.FillCampView(ref view);
                return view;
            }
        }

        public DuelView Duel
        {
            get
            {
                DuelView view = default;
                _world.FillDuelView(ref view);
                return view;
            }
        }
    }
}
