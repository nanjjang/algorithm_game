using Wizard.Core;

namespace Wizard.Sim.Spell
{
    // CONTRACTS 3.4
    public readonly struct ExecuteOptions
    {
        public readonly EntityId Owner;                   // 발사한 개체(플레이어 또는 허상)
        public readonly bool IsCrit, GlassCritBonus;
        public readonly int ShotId;                       // 발사 1회의 묶음(콤보 +3 상한, 결투 스침 묶음)
        public readonly SourceKind Source;
        public readonly int EnhanceStage;                 // 실행 순간의 실효 단계
        public readonly Fix64 CharacterMul;
        public readonly int ProjectileBudget;             // 비기 160 상한의 남은 몫. 제한 없으면 int.MaxValue
        public readonly EntityId AimTarget;               // 결투 조준 보정 대상. 없으면 None
    }
}
