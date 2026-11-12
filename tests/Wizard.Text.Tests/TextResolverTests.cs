using System;
using System.Collections.Generic;
using Xunit;

namespace Wizard.Text.Tests
{
    // spec 14 2.4절 틀 문법의 경계와 2.2절 "없는 열쇠"의 기록 규칙. [임시] 경계는 TextResolver 머리 주석과 같다.
    public sealed class TextResolverTests
    {
        private static TextResolver Initial(List<string>? log = null) =>
            new TextResolver(NameFixtures.InitialTable(), log == null ? (Action<string>?)null : log.Add);

        private static IReadOnlyDictionary<string, string> Args(string key, string value) =>
            new Dictionary<string, string> { [key] = value };

        [Fact]
        public void ImplementsContractInterface()
        {
            ITextResolver t = Initial();
            Assert.Equal("제자 1", t.Name("char.1"));
            Assert.Equal("엄청엄청 위대한 제자 2", t.Format("title.stage.2.b4"));
            Assert.Equal("제자 2", t.ResolveTemplate("{char.2}", null));
        }

        [Fact]
        public void MissingKey_ReleaseBuildHasNoLogAndStillReturnsKey()
        {
            TextResolver t = Initial();   // log 를 넘기지 않는다 = 개발 빌드가 아니다
            Assert.Equal("boss.unknown", t.Name("boss.unknown"));
            Assert.False(t.TryName("boss.unknown", out _));
        }

        [Fact]
        public void MissingKey_LogsOncePerKeyAcrossNameFormatAndTemplates()
        {
            var log = new List<string>();
            TextResolver t = Initial(log);
            for (int i = 0; i < 3; i++)
            {
                Assert.Equal("boss.unknown", t.Name("boss.unknown"));
                Assert.Equal("boss.unknown", t.Format("boss.unknown"));
                Assert.Equal("[boss.unknown]", t.ResolveTemplate("[{boss.unknown}]", null));
                Assert.Equal("elite.unknown", t.Name("elite.unknown"));
            }
            Assert.Equal(2, log.Count);
            Assert.Contains("boss.unknown", log[0]);
            Assert.Contains("elite.unknown", log[1]);
        }

        [Fact]
        public void PresentKey_TryNameTrue()
        {
            Assert.True(Initial().TryName("wand.2", out string text));
            Assert.Equal("깜빡이 등불대", text);
        }

        [Fact]
        public void ArgsWinOverNameTable()
        {
            Assert.Equal("다른 이름", Initial().ResolveTemplate("{char.1}", Args("char.1", "다른 이름")));
        }

        [Fact]
        public void ArgValueWithTemplateIsResolvedOneLevel()
        {
            // spec 14 2.4: v = ResolveTemplate(v, params) if v contains "{"
            Assert.Equal("제자 1님", Initial().ResolveTemplate("{nick}님", Args("nick", "{char.1}")));
        }

        [Fact]
        public void ArgValueTemplateUsesSameArgsAndJosaFollowsResolvedValue()
        {
            var args = new Dictionary<string, string> { ["who"] = "{char.3}의 {n}", ["n"] = "별빛" };
            Assert.Equal("제자 3의 별빛을", Initial().ResolveTemplate("{who}{을/를}", args));
        }

        [Fact]
        public void ArgValueWithoutOpenBraceIsLiteral()
        {
            // "{" 가 없으면 풀지 않는다(spec 14 2.4 "contains \"{\"")
            Assert.Equal("a }} b", Initial().ResolveTemplate("{x}", Args("x", "a }} b")));
        }

        [Fact]
        public void ArgValueEscapedBracesStayLiteral()
        {
            // 호출자가 "{" 를 "{{" 로 바꿔 넘기면 글자 그대로 남는다
            Assert.Equal("{char.1}님", Initial().ResolveTemplate("{x}님", Args("x", "{{char.1}}")));
        }

        [Fact]
        public void ArgValueNestedTwoLevelsLogsAndPrintsKey()
        {
            var log = new List<string>();
            var t = new TextResolver(NameFixtures.InitialWith(NameFixtures.Kv("test.wrap", "<{x}>")), log.Add);
            // 0단계 틀 → test.wrap(1단계) → x 값이 틀(2단계)이면 풀지 않고 열쇠를 찍는다
            Assert.Equal("[<x>]", t.ResolveTemplate("[{test.wrap}]", Args("x", "{char.1}")));
            Assert.Single(log);
            Assert.Contains("'x'", log[0]);
            // 자기 자신을 가리키는 값도 한도에서 멈춘다
            Assert.Equal("x", Initial().ResolveTemplate("{x}", Args("x", "{x}")));
        }

        [Fact]
        public void JosaFollowsArgValue()
        {
            TextResolver t = Initial();
            Assert.Equal("별빛이", t.ResolveTemplate("{x}{이/가}", Args("x", "별빛")));
            Assert.Equal("하늘을", t.ResolveTemplate("{x}{을/를}", Args("x", "하늘")));
            Assert.Equal("마루를", t.ResolveTemplate("{x}{을/를}", Args("x", "마루")));
        }

        [Fact]
        public void JosaWithoutPrecedingRefUsesFinalForm()
        {
            TextResolver t = Initial();
            Assert.Equal("이", t.ResolveTemplate("{이/가}", null));
            Assert.Equal("제자 1 이", t.ResolveTemplate("{char.1} {이/가}", null));   // 사이의 글자가 앞 Ref 를 지운다
            Assert.Equal("으로", t.ResolveTemplate("{(으)로}", null));
            Assert.Equal("이", t.ResolveTemplate("{x}{이/가}", Args("x", "")));       // 빈 값 = 앞 Ref 없음
        }

        [Fact]
        public void JosaAfterJosaUsesFinalForm()
        {
            Assert.Equal("제자 2가이", Initial().ResolveTemplate("{char.2}{이/가}{이/가}", null));
        }

        [Theory]
        [InlineData("과/와", "고드름 홀과", "깜빡이 등불대와")]
        [InlineData("아/야", "고드름 홀아", "깜빡이 등불대야")]
        [InlineData("이에요/예요", "고드름 홀이에요", "깜빡이 등불대예요")]
        [InlineData("이라/라", "고드름 홀이라", "깜빡이 등불대라")]
        public void OtherJosaPairs(string pair, string withFinal, string withoutFinal)
        {
            TextResolver t = Initial();
            Assert.Equal(withFinal, t.ResolveTemplate("{wand.4}{" + pair + "}", null));
            Assert.Equal(withoutFinal, t.ResolveTemplate("{wand.2}{" + pair + "}", null));
        }

        [Fact]
        public void EuroAfterVowelHangul()
        {
            Assert.Equal("깜빡이 등불대로", Initial().ResolveTemplate("{wand.2}{(으)로}", null));
        }

        [Fact]
        public void NestedTemplateOneLevelAllowedTwoLevelsLogged()
        {
            var log = new List<string>();
            var t = new TextResolver(NameFixtures.InitialWith(
                NameFixtures.Kv("test.inner", "위대한 {char.3}"),
                NameFixtures.Kv("test.outer", "<{test.inner}>"),
                NameFixtures.Kv("test.top", "[{test.outer}]")), log.Add);
            // 틀 안의 Ref 값이 틀(1단계)이면 푼다. 조사는 푼 값의 끝 글자로 고른다
            Assert.Equal("엄청엄청 위대한 제자 3을", t.ResolveTemplate("{title.stage.3.b4}{을/를}", null));
            Assert.Equal("<위대한 제자 3>", t.Name("test.outer"));
            Assert.Empty(log);
            // 2단계: test.top → test.outer(1단계) → test.inner(2단계, 틀) 는 풀지 않고 열쇠를 찍는다
            Assert.Equal("[<test.inner>]", t.Name("test.top"));
            Assert.Single(log);
            Assert.Contains("test.inner", log[0]);
        }

        [Fact]
        public void BracesEdgeCases()
        {
            TextResolver t = Initial();
            Assert.Equal("a { b", t.ResolveTemplate("a {{ b", null));
            Assert.Equal("a } b", t.ResolveTemplate("a }} b", null));
            Assert.Equal("a } b", t.ResolveTemplate("a } b", null));
            Assert.Equal("x {char.1", t.ResolveTemplate("x {char.1", null));   // 닫히지 않은 { 는 끝까지 글자
            Assert.Equal(string.Empty, t.ResolveTemplate(string.Empty, null));
            Assert.Equal("평문", t.ResolveTemplate("평문", null));
        }

        [Fact]
        public void NameValueWithEscapedBracesIsUnescaped()
        {
            var t = new TextResolver(NameFixtures.InitialWith(NameFixtures.Kv("test.brace", "{{x}}")));
            Assert.Equal("{x}", t.Name("test.brace"));
        }

        [Fact]
        public void NullArgumentsThrow()
        {
            TextResolver t = Initial();
            Assert.Throws<ArgumentNullException>(() => t.Name(null!));
            Assert.Throws<ArgumentNullException>(() => t.Format(null!));
            Assert.Throws<ArgumentNullException>(() => t.ResolveTemplate(null!, null));
            Assert.Throws<ArgumentNullException>(() => new TextResolver(null!));
        }

        [Fact]
        public void FormatMissingTemplateKeyReturnsKey()
        {
            Assert.Equal("title.season.unknown", Initial().Format("title.season.unknown", Args("n", "1")));
        }

        [Theory]
        [InlineData("제자 0", true, false)]
        [InlineData("제자 1", true, true)]
        [InlineData("제자 2", false, false)]
        [InlineData("제자 3", true, false)]
        [InlineData("제자 4", false, false)]
        [InlineData("제자 5", false, false)]
        [InlineData("제자 6", true, false)]
        [InlineData("제자 7", true, true)]
        [InlineData("제자 8", true, true)]
        [InlineData("제자 9", false, false)]
        [InlineData("홀", true, true)]
        [InlineData("밤", true, false)]
        [InlineData("대", false, false)]
        [InlineData("가", false, false)]
        [InlineData("힣", true, false)]
        [InlineData("boss", false, false)]
        [InlineData("", false, false)]
        [InlineData("밤 )", true, false)]
        [InlineData("홀\"]…", true, true)]
        [InlineData("가 ) ] \" …", false, false)]
        [InlineData(" ", false, false)]
        [InlineData("ㄱ", false, false)]
        public void FinalSoundTable(string s, bool hasFinal, bool isRieul)
        {
            Josa.FinalSound(s, out bool h, out bool r);
            Assert.Equal(hasFinal, h);
            Assert.Equal(isRieul, r);
        }
    }
}
