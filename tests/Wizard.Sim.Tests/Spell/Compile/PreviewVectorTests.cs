using System;
using Wizard.Content;
using Wizard.Sim.Spell;
using Wizard.Sim.Spell.Compile;
using Xunit;
using static Wizard.Sim.Tests.Spell.Compile.CompileKit;

namespace Wizard.Sim.Tests.Spell.Compile
{
    // spec 02 16.10 가운데 미리보기(TV-J02~J05)와 14.4 표의 자료. 미리보기는 글자를 담지 않는다(CONTRACTS 3.4, T-57):
    // "발사 1회 = 투사체 6개 · 열 26.8", "×0.72" 같은 글자는 ViewLogic 이 이 값(N, H, 꼬리표의 백분의 일)을 이름 표의 틀로 그린다.
    public sealed class PreviewVectorTests
    {
        private const int WarningLineCap = 3;        // spec 02 14.4 "경고 줄은 최대 3줄". 화면의 값이라 시험 입력으로 준다

        private static SpellPreview Preview(CompiledSpell m) => SpellPreviewBuilder.Build(m);

        [Fact]
        public void S02_TV_J02__BottomLine_6Projectiles_Heat26Point8()
        {
            SpellPreview p = Preview(Dalgu1(Bolt, Split3, Trig, Fireball, __));
            Assert.True(p.IsValid);
            Assert.Equal(6, p.ProjectileCount);                 // "투사체 6개": 가지치기 뒤 Σ InstancesTotal
            Assert.Equal(HuTenths(268), p.HeatPerShotHu);       // "열 26.8"
        }

        [Fact]
        public void S02_TV_J02__BottomLine_CountsOnlyExecutedScopes()
        {
            // TV-E02: 합 75 > 64 → S2 생략, 실행 21. 열은 가지치기와 무관하게 모든 스코프(6.4)
            CompiledSpell m = Build(Galpi(Bolt, L(Split3, Trig, Bolt, Bolt, Split3, Trig, Bolt, Split3), ext: 3));
            SpellPreview p = Preview(m);
            Assert.Equal(21, p.ProjectileCount);
            Assert.Equal(m.HeatPerShotHu, p.HeatPerShotHu);
        }

        [Fact]
        public void S02_TV_J03__Depth2Label_Default0Point72_Galpi0Point84()
        {
            SpellPreview dalgu = Preview(Dalgu1(Bolt, Trig, Bolt, Trig, Bolt));
            Assert.Equal(72, dalgu.Scopes[2].DepthLabelHundredths);      // 0.7225 를 소수 둘째 자리 내림
            SpellPreview galpi = Preview(Build(Galpi(Bolt, L(Trig, Bolt, Trig, Bolt, __))));
            Assert.Equal(84, galpi.Scopes[2].DepthLabelHundredths);      // 0.8464
        }

        [Fact]
        public void S02_TV_J03__AllDepthLabels_FloorAtTheSecondDecimal()
        {
            // 14.4: "×0.85", "×0.72", 갈피 "×0.92", "×0.84". 깊이 3은 0.614125 → 0.61, 0.778688 → 0.77
            SpellPreview dalgu = Preview(Build(Dalgu(Bolt, L(Trig, Bolt, Trig, Bolt, Trig, Bolt, __), ext: 3)));
            Assert.Equal(new[] { 100, 85, 72, 61 }, Array.ConvertAll(dalgu.Scopes, s => s.DepthLabelHundredths));
            SpellPreview galpi = Preview(Build(Galpi(Bolt, L(Trig, Bolt, Trig, Bolt, Trig, Bolt, __, __), ext: 3)));
            Assert.Equal(new[] { 100, 92, 84, 77 }, Array.ConvertAll(galpi.Scopes, s => s.DepthLabelHundredths));
        }

        // 진단 다섯: 효과 없는 관통(1), 겹친 분열(3), 투사체 없는 트리거 둘(4, 5), 넷째 트리거(7)
        private static CompiledSpell FiveDiagnostics() =>
            Build(Galpi(Orb, L(Pierce, Split3, Split2, Trig, Trig, Trig, Trig, Bolt), ext: 3));

        [Fact]
        public void S02_TV_J04__FiveDiagnostics_WarningLinesAreTheFirstThreeInSortOrder()
        {
            CompiledSpell m = FiveDiagnostics();
            AssertDiags(m, (DiagCode.NoEffect, 1), (DiagCode.SplitDuplicate, 3), (DiagCode.TriggerNoProjectile, 4),
                        (DiagCode.TriggerNoProjectile, 5), (DiagCode.TriggerDepth, 7));
            SpellPreview p = Preview(m);
            Assert.Equal(5, p.Diagnostics.Length);
            ReadOnlySpan<CompileDiagnostic> lines = p.WarningLines(WarningLineCap);
            Assert.Equal(3, lines.Length);
            Assert.Equal((DiagCode.NoEffect, 1), (lines[0].Code, lines[0].Slot));
            Assert.Equal((DiagCode.SplitDuplicate, 3), (lines[1].Code, lines[1].Slot));
            Assert.Equal((DiagCode.TriggerNoProjectile, 4), (lines[2].Code, lines[2].Slot));
            // 진단이 줄 수보다 적으면 있는 만큼
            Assert.Equal(1, Preview(Dalgu1(Orb, Pierce, __, __, __)).WarningLines(WarningLineCap).Length);
            Assert.Equal(0, Preview(Dalgu1(Bolt, __, __, __, __)).WarningLines(WarningLineCap).Length);
        }

        [Fact]
        public void S02_TV_J05__WarningBadge_OnTheNodeOfThatSlot()
        {
            SpellPreview p = Preview(FiveDiagnostics());
            // 변형 꼬리(slot 1 관통)의 딱지
            Assert.Equal(new[] { 1 }, p.Scopes[0].ModifierSlots);
            Assert.Equal(new[] { true }, p.Scopes[0].ModifierHasWarning);
            // 투사체가 없는 스코프를 연 트리거(slot 4, 5)의 딱지. slot 6 의 트리거가 연 S3 은 투사체가 있다
            Assert.True(p.Scopes[1].TriggerHasWarning);
            Assert.True(p.Scopes[2].TriggerHasWarning);
            Assert.False(p.Scopes[3].TriggerHasWarning);
            // 트리에 마디가 없는 칸(무시된 분열 3, 무시된 트리거 7)도 회로 칸으로 묻는다
            for (int slot = 0; slot <= 8; slot++)
            {
                bool expected = slot == 1 || slot == 3 || slot == 4 || slot == 5 || slot == 7;
                Assert.Equal(expected, p.SlotHasWarning(slot));
            }
            // 투사체 마디에는 걸린 진단이 없다
            Assert.All(p.Nodes, n => Assert.False(n.HasWarning));

            // TV-C16: 그림자 구체 + 관통 → slot 2 의 변형에 딱지
            SpellPreview orb = Preview(Dalgu1(Orb, Orb, Pierce, __, __));
            Assert.Equal(new[] { 2 }, orb.Scopes[0].ModifierSlots);
            Assert.Equal(new[] { true }, orb.Scopes[0].ModifierHasWarning);
            // TV-C17: 마력탄에는 효과가 있으므로 딱지가 없다
            SpellPreview mixed = Preview(Dalgu1(Bolt, Orb, Pierce, __, __));
            Assert.Equal(new[] { false }, mixed.Scopes[0].ModifierHasWarning);
            Assert.False(mixed.SlotHasWarning(2));
        }

        [Fact]
        public void Nodes_AreNumberedByScopeThenSlot_OwnerAndBranchesAndTails()
        {
            // core=magic_bolt, L(ice_spike, split_3, double_size, on_hit_trigger, great_fireball, magic_bolt, split_2) (달구 + 확장 3)
            CompiledSpell m = Build(Dalgu(Bolt, L(Ice, Split3, Size, Trig, Fireball, Bolt, Split2), ext: 3));
            SpellPreview p = Preview(m);
            Assert.Equal(2, p.Scopes.Length);
            Assert.Equal(4, p.Nodes.Length);
            (int Index, int Depth, int Slot, BlockId Block, bool Owner)[] expected =
            {
                (0, 0, 0, Bolt, true), (1, 0, 1, Ice, false), (2, 1, 5, Fireball, true), (3, 1, 6, Bolt, false),
            };
            for (int i = 0; i < expected.Length; i++)
            {
                PreviewNode n = p.Nodes[i];
                Assert.Equal(expected[i], (n.Index, n.ScopeDepth, n.Slot, n.Block, n.IsOwner));
            }
            PreviewScope s0 = p.Scopes[0], s1 = p.Scopes[1];
            Assert.Equal((0, 2, 3, 2), (s0.FirstNode, s0.NodeCount, s0.SplitCount, s0.SplitSlot));
            Assert.Equal((2, 2, 2, 7), (s1.FirstNode, s1.NodeCount, s1.SplitCount, s1.SplitSlot));
            Assert.Equal(SpellScope.NoSlot, s0.TriggerSlot);
            Assert.Equal(4, s1.TriggerSlot);
            Assert.Equal(new[] { 3 }, s0.ModifierSlots);
            Assert.Empty(s1.ModifierSlots);
            Assert.False(s0.Omitted);
            Assert.False(s1.Omitted);
            // 커서 연동: 회로 칸 → 마디
            Assert.Equal(2, p.NodeOfSlot(5));
            Assert.Equal(-1, p.NodeOfSlot(3));
            Assert.Equal(-1, p.NodeOfSlot(99));
        }

        [Fact]
        public void OmittedScope_IsMarked_AndItsTriggerHasTheCapWarning()
        {
            // TV-E02 "미리보기 S2 점선"
            SpellPreview p = Preview(Build(Galpi(Bolt, L(Split3, Trig, Bolt, Bolt, Split3, Trig, Bolt, Split3), ext: 3)));
            Assert.Equal(new[] { false, false, true }, Array.ConvertAll(p.Scopes, s => s.Omitted));
            Assert.True(p.Scopes[2].TriggerHasWarning);
            Assert.True(p.SlotHasWarning(6));
            Assert.Equal(1, p.Scopes[2].NodeCount);            // 생략된 스코프의 마디도 (점선으로) 그린다
        }

        [Fact]
        public void SameSpell_SamePreview()
        {
            CompiledSpell m = FiveDiagnostics();
            SpellPreview a = Preview(m), b = Preview(Build(Galpi(Orb, L(Pierce, Split3, Split2, Trig, Trig, Trig, Trig, Bolt), ext: 3)));
            Assert.Equal(a.Nodes.Length, b.Nodes.Length);
            for (int i = 0; i < a.Nodes.Length; i++)
            {
                Assert.Equal((a.Nodes[i].Index, a.Nodes[i].Slot, a.Nodes[i].Block), (b.Nodes[i].Index, b.Nodes[i].Slot, b.Nodes[i].Block));
            }
            Assert.Equal(Array.ConvertAll(a.Scopes, s => s.DepthLabelHundredths), Array.ConvertAll(b.Scopes, s => s.DepthLabelHundredths));
            Assert.Equal(a.ProjectileCount, b.ProjectileCount);
        }
    }
}
