using Wizard.Content;
using Wizard.Core;
using Wizard.Sim.Events;
using Wizard.Sim.Proto;
using Wizard.Sim.Spell;

namespace Wizard.Sim
{
    // 시제품의 적·탄을 ISimView 로 내주는 뷰 훅(CONTRACTS 3.3 "뷰 훅", T-54). 표시 계층(회색 도형)은 이 폴더를 모르고 ISimView 만 읽는다.
    //   View_Enemies_Enemies     — 슬라임(종 = 이끼 슬라임)과 포탑(종 = 갓버섯 술사: 그 3갈래 탄을 제자리에서 쏘는 것, spec 05 4.4).
    //                              포탑이 시전 중이면 공격 준비 칸에 시전의 시작 틱과 길이를 준다(예고의 채움은 이 둘로만 센다)
    //   View_Projectiles_Spell   — 플레이어 탄(마력탄, 화염구). 묶음 = J 발사의 ShotId
    //   View_Projectiles_Enemies — 포탑의 탄. 묶음 = 시전 묶음 CastId(spec 01 8.2)
    // 풀은 id 오름차순이라 그대로 Add 한다. 상태를 바꾸지 않는다(시작 상태가 서기 전에는 아무것도 내지 않는다).
    // 이 폴더와 같이 M3-CB-02 에서 사라지고, 그때는 진짜 Spell·Enemies 모듈이 같은 훅을 구현한다.
    public sealed partial class SimWorld
    {
        partial void View_Enemies_Enemies(ViewBuffer<EnemyView> into)
        {
            if (!ProtoOn || !_protoReady)
            {
                return;
            }
            for (int i = 0; i < _protoEnemyCount; i++)
            {
                ref readonly ProtoEnemy e = ref _protoEnemies[i];
                bool slime = e.Kind == ProtoEnemyKind.Slime;
                bool casting = !slime && e.Phase == ProtoTurretPhase.Cast;
                into.Add(new EnemyView(
                    e.Id,
                    slime ? EnemyTypeId.SlimeMoss : EnemyTypeId.ShamanCap,
                    e.Pos,
                    Fix64.FromRatio(slime ? ProtoTuning.SlimeBodyMilli : ProtoTuning.TurretBodyMilli, ProtoTuning.MilliPerCell),
                    e.Hp,
                    e.MaxHp,
                    casting ? unchecked(e.PhaseEndTick - (uint)ProtoTuning.TurretCastTicks) : 0U,
                    casting ? ProtoTuning.TurretCastTicks : 0));
            }
        }

        partial void View_Projectiles_Spell(ViewBuffer<ProjectileView> into)
        {
            if (!ProtoOn || !_protoReady)
            {
                return;
            }
            for (int i = 0; i < _protoBulletCount; i++)
            {
                ref readonly ProtoBullet b = ref _protoBullets[i];
                into.Add(new ProjectileView(b.Id, Faction.Player, b.Owner, b.Block, b.Element, b.Pos, b.Dir, b.Radius, b.ShotId));
            }
        }

        partial void View_Projectiles_Enemies(ViewBuffer<ProjectileView> into)
        {
            if (!ProtoOn || !_protoReady)
            {
                return;
            }
            Fix64 radius = Fix64.FromRatio(ProtoTuning.TurretShotRadiusMilli, ProtoTuning.MilliPerCell);
            for (int i = 0; i < _protoShotCount; i++)
            {
                ref readonly ProtoEnemyShot s = ref _protoShots[i];
                into.Add(new ProjectileView(s.Id, Faction.Enemy, PlayerSlot.None, BlockId.None, Element.None, s.Pos, s.Dir, radius, s.CastId));
            }
        }
    }
}
