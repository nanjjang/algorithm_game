using Wizard.Core;

namespace Wizard.Sim.Spell
{
    // CONTRACTS 3.4. GDD 4절의 여섯 인자 + 옵션. J 발사, 비기, 사본 마법진, 허상의 복사가 모두 이것을 부른다
    public interface ISpellExecutor
    {
        ExecuteResult Execute(SpellHandle tree, FixVec2 position, Angle direction, Fix64 damageMul,
        bool heatExempt, Faction faction, in ExecuteOptions options);
    }
}
