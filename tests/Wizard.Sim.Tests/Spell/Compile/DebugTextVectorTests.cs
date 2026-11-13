using Wizard.Content;
using Wizard.Core;
using Wizard.Sim.Spell;
using Wizard.Sim.Spell.Compile;
using Xunit;
using static Wizard.Sim.Tests.Spell.Compile.CompileKit;

namespace Wizard.Sim.Tests.Spell.Compile
{
    // spec 02 16.10 가운데 글자 출력(TV-J01, J06). 미리보기(TV-J02~J05)는 PreviewVectorTests 에 있다.
    // 입력은 명세 14.5 의 글자 그대로다(spec 02 의 이름은 ContentIds 의 별칭으로 읽힌다). 출력의 블록 이름은 spec 14 id 라서
    // 예시를 CONTRACTS 2.1 대응표로 바꿔 읽는다: great_fireball → fireball. 그 밖의 글자는 예시 그대로다.
    public sealed class DebugTextVectorTests
    {
        private const string J01Input = "core=magic_bolt; links=split_3,on_hit_trigger,great_fireball,_";

        private const string J01Expected =
            "SPELL interval=9t heat=26.80 count=6/64 mirror_count=12/64 T=30.00\n" +
            "S0 depth=0 decay=1.00 split=3@1 heat=8.80 inst=3\n" +
            "  [0] P magic_bolt *owner\n" +
            "S1 depth=1 decay=0.85 trigger@2 split=1 heat=18.00 inst=3\n" +
            "  [3] P fireball *owner\n" +
            "DIAG none";

        [Fact]
        public void S02_TV_J01__ExampleLoadout_TextMatchesSpecExampleExactly()
        {
            Assert.True(LoadoutText.TryParse(J01Input, out LoadoutText text));
            Assert.Equal(CharacterId.Char1, text.Character);
            Assert.Equal(0, text.ExtraSlots);
            CompiledSpell m = Build(text.ToSource(4, PowBase.Decay085, false));   // 캐릭터 1: 연결 4칸, 0.85, 밑줄 없음
            Assert.Equal(J01Expected, CompileKit.Print(m));
        }

        [Fact]
        public void S02_TV_J06__FormatParseCompile_SameHash()
        {
            CompileSource[] sources =
            {
                Dalgu(Bolt, L(Split3, Trig, Fireball, __)),
                Dalgu(Fireball, L(Size, Pierce, Trig, Ice, Split2, __, Corrosion), ext: 3, mutations: Mut(MutationId.MirrorImage, MutationId.TwistedBarrel)),
                Galpi(Orb, L(Pierce, Trig, Chain, Homing, Frost)),
                Galpi(Bolt, L(Trig, Bolt, Trig, Bolt, Trig, Bolt, Trig, Bolt), ext: 3, mutations: Mut(MutationId.GlassWand)),
                Dalgu(__, L(__, __, __, __)),
            };
            foreach (CompileSource src in sources)
            {
                CompiledSpell first = Build(src);
                string written = LoadoutText.Format(src);
                Assert.True(LoadoutText.TryParse(written, out LoadoutText parsed), written);
                CompileSource again = parsed.ToSource(src.BaseLinks, src.DepthDecay, src.UnderlinePassive);
                CompiledSpell second = Build(again);
                Assert.Equal(first.Hash, second.Hash);
                Assert.Equal(CompileKit.Print(first), CompileKit.Print(second));
                Assert.Equal(written, LoadoutText.Format(again));
            }
        }

        [Fact]
        public void LoadoutText_RejectsBrokenInput()
        {
            string[] broken =
            {
                "",
                "links=_,_,_,_",                                   // core 없음
                "core=magic_bolt",                                 // links 없음
                "core=magic_bolt; links=_,_; core=fireball",       // 겹친 열쇠
                "core=dragon_breath; links=_",                     // 모르는 블록
                "core=magic_bolt; links=_; char=6",
                "core=magic_bolt; links=_; char=x",
                "core=magic_bolt; links=_; mut=shiny",
                "core=magic_bolt; links=_; speed=3",
            };
            foreach (string s in broken)
            {
                Assert.False(LoadoutText.TryParse(s, out _), s);
            }
            Assert.True(LoadoutText.TryParse("core=block.magic_bolt; links=block.split_two,_; char=3; ext=2; mut=mutation.mirror_image,gluttony", out LoadoutText t));
            Assert.Equal(new[] { BlockId.SplitTwo, BlockId.None }, t.Links);
            Assert.Equal(CharacterId.Char3, t.Character);
            Assert.Equal(2, t.ExtraSlots);
            Assert.Equal(Mut(MutationId.MirrorImage, MutationId.Gluttony), t.MutationBits);
            Assert.True(LoadoutText.TryParse("core=magic_bolt; links=_; mut=mirror,hair_trigger", out LoadoutText spec));   // 명세 문법의 이름
            Assert.Equal(Mut(MutationId.MirrorImage, MutationId.HypersensitiveCircuit), spec.MutationBits);
        }
    }
}
