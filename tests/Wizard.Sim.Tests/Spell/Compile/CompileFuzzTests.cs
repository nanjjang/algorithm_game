using System;
using Wizard.Content;
using Wizard.Core;
using Wizard.Sim.Spell;
using Wizard.Sim.Spell.Compile;
using Xunit;
using static Wizard.Sim.Tests.Spell.Compile.CompileKit;

namespace Wizard.Sim.Tests.Spell.Compile
{
    // 수락 조건 "같은 조립은 언제나 같은 트리와 같은 글자 출력을 낸다. 무작위 조립 10만 건에서 예외가 없고 투사체 수 상한을 넘지 않는다".
    // 조립은 코어 결정론 난수기(Pcg32)로 만든다(같은 씨앗이면 같은 10만 건). 캐릭터 1·3, 확장 0~3, 변이 비트, 빈 칸 포함.
    public sealed class CompileFuzzTests
    {
        private const int Count = 100_000;

        private static readonly BlockId[] Pool =
        {
            BlockId.None, BlockId.MagicBolt, BlockId.Fireball, BlockId.ChainLightning, BlockId.Boomerang, BlockId.IceSpike, BlockId.ShadowOrb,
            BlockId.SplitTwo, BlockId.SplitThree, BlockId.OnHitTrigger, BlockId.WallBounce, BlockId.DoubleSize, BlockId.Pierce,
            BlockId.Frost, BlockId.Homing, BlockId.TripleHeat, BlockId.Corrosion,
        };

        private static readonly BlockId[] Dense = { BlockId.MagicBolt, BlockId.IceSpike, BlockId.SplitThree, BlockId.SplitTwo, BlockId.OnHitTrigger };

        [Fact]
        public void RandomLoadouts_NoException_NeverOverCap_DiagnosticsSorted_Deterministic()
        {
            Pcg32 rng = RngStreams.Derive(20261113UL, RngStream.Mutation, 0, 0);
            int valid = 0, pruned = 0, prunedMirror = 0;
            for (int i = 0; i < Count; i++)
            {
                CompileSource src = Random(ref rng);
                CompiledSpell m = Build(src);
                if (!m.IsValid)
                {
                    Assert.Single(m.Diagnostics);
                    continue;
                }
                valid++;
                Assert.InRange(m.Scopes.Length, 1, SpellCompilerCore.MaxScopes);
                Assert.True(m.ExecutedCount(false) <= TreeCap, LoadoutText.Format(src));
                Assert.True(m.ExecutedCount(true) <= TreeCap, LoadoutText.Format(src));
                Assert.Equal(m.InstancesTotal * 2, m.InstancesTotalMirrored);
                Assert.True(m.MaxDepthMirrored <= m.MaxDepthNormal);
                for (int d = 0; d < m.Scopes.Length; d++)
                {
                    Assert.Equal(d, m.Scopes[d].Depth);
                }
                for (int k = 1; k < m.Diagnostics.Length; k++)
                {
                    CompileDiagnostic a = m.Diagnostics[k - 1], b = m.Diagnostics[k];
                    Assert.True(a.Slot < b.Slot || (a.Slot == b.Slot && a.Code <= b.Code), "diagnostics out of order");
                }
                if (m.MaxDepthNormal < m.Scopes.Length - 1)
                {
                    pruned++;
                }
                if (m.MaxDepthMirrored < m.Scopes.Length - 1)
                {
                    prunedMirror++;
                }
                if ((i & 1023) == 0)
                {
                    // 같은 원천을 다시 컴파일하면 같은 해시와 같은 글자
                    CompiledSpell again = Build(new CompileSource(src.Core, src.Links, src.Character, src.BaseLinks, src.ExtraSlots,
                                                                 src.DepthDecay, src.UnderlinePassive, src.MutationBits));
                    Assert.Equal(m.Hash, again.Hash);
                    Assert.Equal(CompileKit.Print(m), CompileKit.Print(again));
                }
            }
            // 표본이 가지치기 길을 실제로 지났는가
            Assert.True(valid > Count / 2, "valid " + valid);
            Assert.True(pruned > 0, "pruned " + pruned);
            Assert.True(prunedMirror > pruned, "mirror " + prunedMirror + " normal " + pruned);
        }

        private static CompileSource Random(ref Pcg32 rng)
        {
            bool galpi = rng.NextInt(2) == 1;
            int baseLinks = galpi ? 5 : 4;
            int ext = rng.NextInt(4);
            // 가끔 칸 수를 어긋나게 해서 오류 길도 지난다
            int len = baseLinks + ext + (rng.NextInt(50) == 0 ? 1 : 0);
            // 셋에 하나는 투사체·분열·트리거만으로 채운 촘촘한 조립(64 상한의 가지치기 길을 지나게)
            bool dense = rng.NextInt(3) == 0;
            var links = new BlockId[len];
            for (int k = 0; k < len; k++)
            {
                links[k] = dense ? Dense[rng.NextInt(Dense.Length)] : Pool[rng.NextInt(Pool.Length)];
            }
            // 코어는 대개 투사체(Pool 의 1..6), 열에 하나는 아무 블록(빈 코어·투사체가 아닌 코어의 오류 길)
            BlockId core = rng.NextInt(10) == 0 ? Pool[rng.NextInt(Pool.Length)] : Pool[1 + rng.NextInt(6)];
            byte mutations = (byte)(rng.NextInt(64) << 1);      // 변이 1~6 의 비트
            return galpi
                ? new CompileSource(core, links, CharacterId.Char3, baseLinks, ext, PowBase.Decay092, true, mutations)
                : new CompileSource(core, links, CharacterId.Char1, baseLinks, ext, PowBase.Decay085, false, mutations);
        }
    }
}
