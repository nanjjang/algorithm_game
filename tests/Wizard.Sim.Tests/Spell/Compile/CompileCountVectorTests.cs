using Wizard.Content;
using Wizard.Sim.Spell;
using Wizard.Sim.Spell.Compile;
using Xunit;
using static Wizard.Sim.Tests.Spell.Compile.CompileKit;

namespace Wizard.Sim.Tests.Spell.Compile
{
    // spec 02 16.5 투사체 수와 상한 가운데 컴파일러가 정하는 것(TV-E01~E06): 스코프별 수, 트리 실행 1회 64 상한의 가지치기.
    // 어느 경우에 거울상 쪽(MaxDepthMirrored)을 쓰는지(J·마력 방출만, 8.1)와 화면 상한·예산(TV-E07~E11)은 실행기의 몫이다.
    public sealed class CompileCountVectorTests
    {
        [Fact]
        public void S02_TV_E01__Split3TriggerFireballSplit3_3Plus9_Is12_NoPrune()
        {
            CompiledSpell m = Dalgu1(Bolt, Split3, Trig, Fireball, Split3);
            Assert.Equal(3, m.Scopes[0].InstancesTotal);
            Assert.Equal(9, m.Scopes[1].InstancesTotal);
            Assert.Equal(12, m.InstancesTotal);
            Assert.Equal(1, m.MaxDepthNormal);
            Assert.Equal(12, m.ExecutedCount(false));
            Assert.Empty(m.Diagnostics);
        }

        [Fact]
        public void S02_TV_E02__GalpiExt3_3Plus18Plus54_Is75_S2Omitted_Executes21_CapWarning()
        {
            CompiledSpell m = Build(Galpi(Bolt, L(Split3, Trig, Bolt, Bolt, Split3, Trig, Bolt, Split3), ext: 3));
            Assert.Equal(3, m.Scopes[0].InstancesTotal);
            Assert.Equal(18, m.Scopes[1].InstancesTotal);
            Assert.Equal(54, m.Scopes[2].InstancesTotal);
            Assert.Equal(75, m.InstancesTotal);
            Assert.Equal(1, m.MaxDepthNormal);                          // S2 생략
            Assert.Equal(21, m.ExecutedCount(false));
            AssertDiags(m, (DiagCode.ProjectileCap, 6));                // S2 를 연 트리거(slot 6)
            Assert.Equal(2, m.Diagnostics[0].ScopeDepth);
            Assert.Contains("S2 depth=2 decay=0.85 trigger@6 split=3@8 heat=8.80 inst=54 omitted", CompileKit.Print(m));
        }

        [Fact]
        public void S02_TV_E03__DalguExt3_6Plus54_Is60_NoPrune()
        {
            CompiledSpell m = Build(Dalgu(Bolt, L(Bolt, Split3, Trig, Bolt, Bolt, Bolt, Split3), ext: 3));
            Assert.Equal(6, m.Scopes[0].InstancesTotal);
            Assert.Equal(54, m.Scopes[1].InstancesTotal);
            Assert.Equal(60, m.InstancesTotal);
            Assert.Equal(1, m.MaxDepthNormal);
            Assert.Empty(m.Diagnostics);
        }

        [Fact]
        public void S02_TV_E04__E03WithMirror_120Over64_S1Omitted_Executes12()
        {
            CompiledSpell m = Build(Dalgu(Bolt, L(Bolt, Split3, Trig, Bolt, Bolt, Bolt, Split3), ext: 3, mutations: Mut(MutationId.MirrorImage)));
            Assert.Equal(120, m.InstancesTotalMirrored);
            Assert.Equal(0, m.MaxDepthMirrored);                        // 거울상 쪽은 S1 생략
            Assert.Equal(12, m.ExecutedCount(true));
        }

        [Fact]
        public void S02_TV_E05__E03WithMirror_WithoutMirrorPath_60()
        {
            // 비기(양익 사격)는 거울상을 받지 않으므로 보통 쪽(MaxDepthNormal)으로 실행한다. 보통 쪽은 거울상 변이가 있어도 60 그대로
            CompiledSpell m = Build(Dalgu(Bolt, L(Bolt, Split3, Trig, Bolt, Bolt, Bolt, Split3), ext: 3, mutations: Mut(MutationId.MirrorImage)));
            Assert.Equal(1, m.MaxDepthNormal);
            Assert.Equal(60, m.ExecutedCount(false));
            Assert.Empty(m.Diagnostics);
        }

        [Fact]
        public void S02_TV_E06__MaximumRootWithMirror_WithinCap_RootNeverPruned()
        {
            // 6.6: 루트만의 상한 = 9발 × 3갈래 × 거울상 2 = 54 ≤ 64. 실제로 만들 수 있는 루트(분열이 한 칸을 쓴다)는 그 아래다
            Assert.True(9 * 3 * 2 <= TreeCap);
            byte mirror = Mut(MutationId.MirrorImage);
            CompiledSpell allBolts = Build(Galpi(Bolt, L(Bolt, Bolt, Bolt, Bolt, Bolt, Bolt, Bolt, Bolt), ext: 3, mutations: mirror));
            Assert.Equal(9, allBolts.InstancesTotal);
            Assert.Equal(18, allBolts.ExecutedCount(true));
            CompiledSpell split = Build(Galpi(Bolt, L(Bolt, Bolt, Bolt, Bolt, Bolt, Bolt, Bolt, Split3), ext: 3, mutations: mirror));
            Assert.Equal(24, split.InstancesTotal);
            Assert.Equal(48, split.ExecutedCount(true));
            Assert.True(split.ExecutedCount(true) <= 9 * 3 * 2);
            Assert.Equal(0, split.MaxDepthMirrored);
            Assert.Empty(split.Diagnostics);
        }
    }
}
