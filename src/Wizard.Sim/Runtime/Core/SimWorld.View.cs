using Wizard.Sim.Events;

namespace Wizard.Sim
{
    // CONTRACTS 3.3 "뷰 훅"(T-54). ISimView 의 세계 쪽 구현(SimWorldView)이 읽을 때 부르는 문이다. 뷰 훅을 부르는 순서는 여기 한 곳뿐이고
    // CONTRACTS 3.3 의 뷰 훅 표와 같다(HookTableTests 가 대조한다). 훅을 더하거나 순서를 바꾸는 것은 계약 변경이다.
    // 문과 훅은 세계 상태를 바꾸지 않는다. 그래서 읽는 쪽이 있든 없든(클라이언트, 서버, 검증 워커) 같은 입력의 해시가 같다.
    public sealed partial class SimWorld
    {
        // PlayerView 한 명(index = 슬롯). 모듈이 자기 칸만 채운다. 채우지 않은 칸은 기본값이다
        internal void FillPlayerView(int index, ref PlayerViewDraft draft)
        {
            View_Player_Player(index, ref draft);
            View_Player_Heat(index, ref draft);
            View_Player_Graze(index, ref draft);
            View_Player_Combo(index, ref draft);
            View_Player_Arts(index, ref draft);
            View_Player_Characters(index, ref draft);
        }

        internal void FillEnemyViews(ViewBuffer<EnemyView> into)
        {
            View_Enemies_Enemies(into);
            View_Enemies_Bosses(into);
        }

        // 플레이어 탄(Spell) → 적 탄(Enemies) → 보스의 탄(Bosses)
        internal void FillProjectileViews(ViewBuffer<ProjectileView> into)
        {
            View_Projectiles_Spell(into);
            View_Projectiles_Enemies(into);
            View_Projectiles_Bosses(into);
        }

        internal void FillTelegraphViews(ViewBuffer<TelegraphView> into)
        {
            View_Telegraphs_Telegraph(into);
        }

        internal void FillHazardViews(ViewBuffer<HazardView> into)
        {
            View_Hazards_Enemies(into);
            View_Hazards_Bosses(into);
        }

        internal void FillWaveView(ref WaveView view)
        {
            View_Wave_Waves(ref view);
        }

        internal void FillCampView(ref CampView view)
        {
            View_Camp_Camp(ref view);
        }

        internal void FillDuelView(ref DuelView view)
        {
            View_Duel_Duel(ref view);
        }
    }
}
