using Wizard.Content;
using Wizard.Core;
using Wizard.Sim.Spell;
using Wizard.Sim.Spell.Compile;
using Wizard.Sim.Tests.Runtime;
using Xunit;
using static Wizard.Sim.Tests.Spell.Compile.CompileKit;

namespace Wizard.Sim.Tests.Spell.Compile
{
    // CONTRACTS 3.4 ISpellCompiler 의 공개 구현(SpellCompiler, DECISIONS-TECH T-57): 세계 없이 팩만으로 컴파일·미리보기·글자 출력.
    // 컴파일 규칙(64, 5, 1.5)은 팩의 blocks 절 꼬리에서 읽는다.
    public sealed class SpellCompilerTests
    {
        private static WandCombatProfile DalguProfile(int ext = 0, byte mutations = 0) =>
            new WandCombatProfile(CharacterId.Char1, 4, ext, PowBase.Decay085, false, new MutationSet(mutations));

        [Fact]
        public void SpecRules_AreInThePack()
        {
            CompileRules r = TestRules;
            Assert.Equal(TreeCap, r.TreeProjectileCap);              // spec 02 SF-2
            Assert.Equal(UnderlineMaxCutHu, r.UnderlineMaxCutHu);    // spec 02 HT-5
            Assert.Equal(PermilleMirror, r.MirrorHeatMulPermille);   // spec 02 HT-7
        }

        [Fact]
        public void PublicCompiler_IsTheSamePureFunctionAsTheCore()
        {
            ISpellCompiler compiler = new SpellCompiler(Db);
            BlockId[] links = L(Split3, Trig, Fireball, __);
            CompiledSpell a = compiler.Compile(Bolt, links, DalguProfile());
            CompiledSpell b = compiler.Compile(Bolt, links, DalguProfile());
            CompiledSpell core = Build(Dalgu(Bolt, links));
            Assert.True(a.IsValid);
            Assert.Equal(core.Hash, a.Hash);
            Assert.Equal(a.Hash, b.Hash);
            Assert.Equal(Print(core), compiler.DebugPrint(a));
            Assert.Equal(compiler.DebugPrint(a), compiler.DebugPrint(b));
            Assert.Equal(TreeCap, ((SpellCompiler)compiler).TreeProjectileCap);
            Assert.Equal(6, compiler.BuildPreview(a).ProjectileCount);
        }

        [Fact]
        public void RuleFailures_AreDiagnostics_NotExceptions()
        {
            var compiler = new SpellCompiler(Db);
            CompiledSpell empty = compiler.Compile(BlockId.None, L(__, __, __, __), DalguProfile());
            Assert.False(empty.IsValid);
            AssertDiags(empty, (DiagCode.CoreEmpty, 0));
            CompiledSpell mismatch = compiler.Compile(Bolt, L(__, __, __), DalguProfile());
            Assert.False(mismatch.IsValid);
            AssertDiags(mismatch, (DiagCode.SlotCountMismatch, 0));
            CompiledSpell unknown = compiler.Compile(Bolt, L((BlockId)200, __, __, __), DalguProfile());
            Assert.False(unknown.IsValid);
            AssertDiags(unknown, (DiagCode.UnknownBlock, 1));
            SpellPreview p = compiler.BuildPreview(empty);
            Assert.False(p.IsValid);
            Assert.Empty(p.Nodes);
            Assert.Empty(p.Scopes);
            Assert.Equal(0, p.ProjectileCount);
            Assert.Single(p.Diagnostics);
        }

        // spec 02 HT-7: 거울상 변이가 있으면 J 발사 1회의 열은 총열 x 1.5 다(HeatPerShotHu 는 곱하기 전의 값 그대로)
        [Fact]
        public void FireHeat_IsHeatPerShot_AndMirrorAddsHalf()
        {
            var compiler = new SpellCompiler(Db);
            byte mirror = Mut(MutationId.MirrorImage);
            CompiledSpell plain = compiler.Compile(Bolt, L(__, __, __, __), DalguProfile());
            Assert.Equal(Hu(4), plain.HeatPerShotHu);
            Assert.Equal(Hu(4), plain.FireHeatHu);
            CompiledSpell mirrored = compiler.Compile(Bolt, L(__, __, __, __), DalguProfile(mutations: mirror));
            Assert.Equal(Hu(4), mirrored.HeatPerShotHu);
            Assert.Equal(Hu(6), mirrored.FireHeatHu);
            CompiledSpell split = compiler.Compile(Bolt, L(Split3, Trig, Fireball, __), DalguProfile(mutations: mirror));
            Assert.Equal(HuTenths(268), split.HeatPerShotHu);
            Assert.Equal(HuTenths(402), split.FireHeatHu);         // 26.8 x 1.5 = 40.2
            // 다른 변이는 발사 열을 바꾸지 않는다(과열 수치는 Heat 모듈의 몫이다)
            CompiledSpell glass = compiler.Compile(Bolt, L(__, __, __, __), DalguProfile(mutations: Mut(MutationId.GlassWand)));
            Assert.Equal(Hu(4), glass.FireHeatHu);
        }

        [Fact]
        public void PackWithoutSpellRules_CompilerCannotBeMade()
        {
            Assert.Throws<ContentException>(() => new SpellCompiler(WorldKit.EmptyDb()));
        }
    }
}
