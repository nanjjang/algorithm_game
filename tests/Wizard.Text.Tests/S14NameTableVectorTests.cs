using System.Collections.Generic;
using Xunit;

namespace Wizard.Text.Tests
{
    // spec 14 7.4절 이름 표와 틀(TV-060 ~ TV-081). 기대값은 명세의 then 칸이다.
    public sealed class S14NameTableVectorTests
    {
        private static TextResolver Initial() => new TextResolver(NameFixtures.InitialTable());

        private static IReadOnlyDictionary<string, string> Args(string key, string value) =>
            new Dictionary<string, string> { [key] = value };

        [Theory]
        [InlineData("char.1", "제자 1")]
        [InlineData("char.2", "제자 2")]
        [InlineData("char.3", "제자 3")]
        [InlineData("char.4", "제자 4")]
        [InlineData("char.5", "제자 5")]
        public void S14_TV_060__CharactersAreDisciplesOneToFive(string key, string expected)
        {
            Assert.Equal(expected, Initial().Name(key));
        }

        [Fact]
        public void S14_TV_061__MasterResolvesThroughAlias()
        {
            TextResolver t = Initial();
            Assert.Equal("느루", t.Name("master"));
            Assert.Equal("느루", t.Name("npc.master"));
            Assert.True(t.Table.TryGet("master", out string v));
            Assert.Equal("느루", v);
        }

        [Theory]
        [InlineData("field", "한뎃벌")]
        [InlineData("plaza", "첫모닥 야영지")]
        [InlineData("arena", "돌마당")]
        public void S14_TV_062__PlaceAliases(string key, string expected)
        {
            Assert.Equal(expected, Initial().Name(key));
        }

        [Fact]
        public void S14_TV_063__TemplateWithCharFive()
        {
            Assert.Equal("엄청엄청 위대한 제자 5", Initial().ResolveTemplate("엄청엄청 위대한 {char.5}", null));
        }

        [Fact]
        public void S14_TV_064__StageTitleBandFourTemplate()
        {
            TextResolver t = Initial();
            Assert.Equal("엄청엄청 위대한 제자 1", t.Format("title.stage.1.b4"));
            Assert.Equal("엄청엄청 위대한 제자 1", t.Name("title.stage.1.b4"));
        }

        [Fact]
        public void S14_TV_065__CharOneIGaReadsIl()
        {
            Assert.Equal("제자 1이", Initial().ResolveTemplate("{char.1}{이/가}", null));
        }

        [Fact]
        public void S14_TV_066__CharTwoIGaReadsI()
        {
            Assert.Equal("제자 2가", Initial().ResolveTemplate("{char.2}{이/가}", null));
        }

        [Fact]
        public void S14_TV_067__CharThreeEulReulReadsSam()
        {
            Assert.Equal("제자 3을", Initial().ResolveTemplate("{char.3}{을/를}", null));
        }

        [Fact]
        public void S14_TV_068__CharFourEulReulReadsSa()
        {
            Assert.Equal("제자 4를", Initial().ResolveTemplate("{char.4}{을/를}", null));
        }

        [Fact]
        public void S14_TV_069__CharFiveEunNeunReadsO()
        {
            Assert.Equal("제자 5는", Initial().ResolveTemplate("{char.5}{은/는}", null));
        }

        [Fact]
        public void S14_TV_070__CharOneEuroRieulFinal()
        {
            Assert.Equal("제자 1로", Initial().ResolveTemplate("{char.1}{(으)로}", null));
        }

        [Fact]
        public void S14_TV_071__CharThreeEuroWithFinal()
        {
            Assert.Equal("제자 3으로", Initial().ResolveTemplate("{char.3}{(으)로}", null));
        }

        [Fact]
        public void S14_TV_072__RenamedCharOneNoFinal()
        {
            var t = new TextResolver(NameFixtures.InitialWith(NameFixtures.Kv("char.1", "달구")));
            Assert.Equal("달구가", t.ResolveTemplate("{char.1}{이/가}", null));
        }

        [Fact]
        public void S14_TV_073__RenamedCharThreeNoFinal()
        {
            var t = new TextResolver(NameFixtures.InitialWith(NameFixtures.Kv("char.3", "갈피")));
            Assert.Equal("갈피를", t.ResolveTemplate("{char.3}{을/를}", null));
        }

        [Fact]
        public void S14_TV_074__WandOneEulReul()
        {
            Assert.Equal("끓는 부지깽이를", Initial().ResolveTemplate("{wand.1}{을/를}", null));
        }

        [Fact]
        public void S14_TV_075__WandFourRieulCountsAsFinalForEulReul()
        {
            Assert.Equal("고드름 홀을", Initial().ResolveTemplate("{wand.4}{을/를}", null));
        }

        [Fact]
        public void S14_TV_076__WandFourEuroRieul()
        {
            Assert.Equal("고드름 홀로", Initial().ResolveTemplate("{wand.4}{(으)로}", null));
        }

        [Fact]
        public void S14_TV_077__HeirTemplateWithParam()
        {
            TextResolver t = Initial();
            Assert.Equal("제 3대 후계자", t.ResolveTemplate("제 {n}대 후계자", Args("n", "3")));
            Assert.Equal("제 3대 후계자", t.Format("title.season.heir", Args("n", "3")));
        }

        [Fact]
        public void S14_TV_078__TopDiscipleTemplateWithParam()
        {
            TextResolver t = Initial();
            Assert.Equal("제2기 수제자", t.ResolveTemplate("제{n}기 수제자", Args("n", "2")));
            Assert.Equal("제2기 수제자", t.Format("title.season.top_disciple", Args("n", "2")));
        }

        [Fact]
        public void S14_TV_079__DoubledBracesAreLiteral()
        {
            Assert.Equal("{char.1}", Initial().ResolveTemplate("{{char.1}}", null));
        }

        [Fact]
        public void S14_TV_080__MissingKeyReturnsKeyAndWarnsOnce()
        {
            var log = new List<string>();
            var t = new TextResolver(NameFixtures.InitialTable(), log.Add);
            Assert.Equal("boss.unknown", t.Name("boss.unknown"));
            Assert.Equal("boss.unknown", t.Name("boss.unknown"));
            Assert.False(t.TryName("boss.unknown", out string text));   // 표시 계층이 이것으로 회청을 고른다
            Assert.Equal("boss.unknown", text);
            Assert.Single(log);
            Assert.Contains("boss.unknown", log[0]);
        }

        [Fact]
        public void S14_TV_081__FinalSoundOfTenUsesLastDigitZero()
        {
            Josa.FinalSound("제자 10", out bool hasFinal, out bool isRieul);
            Assert.True(hasFinal);
            Assert.False(isRieul);
        }
    }
}
