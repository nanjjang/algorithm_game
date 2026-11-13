using System;
using Wizard.Content;

namespace Wizard.Sim.Spell.Compile
{
    // CONTRACTS 3.4 ISpellCompiler 의 구현(DECISIONS-TECH T-57). 세계 없이 쓴다: 조립 화면은 이것만으로 미리보기를 만든다.
    // 순수 함수다(같은 입력이면 같은 결과, 같은 Hash). 상태가 없고 ContentDb 와 그 안의 주문 규칙(ContentDb.SpellRules)만 읽는다.
    // 세계 안의 컴파일(SpellTable)도 같은 SpellCompilerCore 를 부른다.
    public sealed class SpellCompiler : ISpellCompiler
    {
        private readonly ContentDb _db;
        private readonly CompileRules _rules;

        // 팩에 주문 규칙이 없으면(blocks 절의 꼬리가 없다) ContentException
        public SpellCompiler(ContentDb db)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _rules = CompileRules.FromContent(db);
        }

        // 트리 실행 1회의 투사체 상한(spec 02 SF-2). 미리보기와 글자 출력의 "/64"
        public int TreeProjectileCap => _rules.TreeProjectileCap;

        // 규칙상의 "안 됨"(빈 코어, 칸 수 불일치, 모르는 블록)은 예외가 아니라 진단이다(IsValid == false).
        // 정규 바이트에 담기지 않는 문맥(연결 칸 255개 초과, PowBase 가 감쇠 표가 아님)은 부른 코드의 결함이라 SimInvariantException
        public CompiledSpell Compile(BlockId core, ReadOnlySpan<BlockId> links, in WandCombatProfile profile) =>
            SpellCompilerCore.Compile(_db, CompileSource.From(core, links, in profile), _rules);

        public SpellPreview BuildPreview(CompiledSpell spell) =>
            SpellPreviewBuilder.Build(spell ?? throw new ArgumentNullException(nameof(spell)));

        public string DebugPrint(CompiledSpell spell) =>
            SpellDebugText.Print(spell ?? throw new ArgumentNullException(nameof(spell)), _rules.TreeProjectileCap);
    }
}
