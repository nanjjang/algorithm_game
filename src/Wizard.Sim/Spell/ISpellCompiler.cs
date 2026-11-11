using System;
using Wizard.Content;

namespace Wizard.Sim.Spell
{
    // CONTRACTS 3.4. 컴파일은 순수 함수다(같은 입력이면 같은 Hash)
    public interface ISpellCompiler
    {
        CompiledSpell Compile(BlockId core, ReadOnlySpan<BlockId> links, in WandCombatProfile profile);
        SpellPreview BuildPreview(CompiledSpell spell);   // 조립 화면용(spec 02 14.4)
        string DebugPrint(CompiledSpell spell);           // 글자 출력(spec 02 14.5)
    }
}
