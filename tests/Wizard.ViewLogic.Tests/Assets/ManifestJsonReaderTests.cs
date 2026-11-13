using System.Linq;
using Wizard.ViewLogic.Assets.Json;
using Xunit;

namespace Wizard.ViewLogic.Tests.Assets
{
    // 매니페스트 판독기: RFC 8259, 주석·끝 쉼표·같은 열쇠 거절, 속성 순서 보존, 정수만 정수로 읽힘
    public sealed class ManifestJsonReaderTests
    {
        [Fact]
        public void ParsesNestedValues_AndKeepsPropertyOrder()
        {
            ManifestJson j = ManifestJsonReader.Parse("{\"b\": [1, -2, 3.5, 1e3], \"a\": {\"x\": true, \"y\": null}, \"s\": \"\\u00e9\\n\\\"q\\\"\"}");
            Assert.Equal(new[] { "b", "a", "s" }, j.Properties.Select(p => p.Key).ToArray());
            ManifestJson b = j.Get("b")!;
            Assert.True(b[0].TryGetInt(out int one));
            Assert.Equal(1, one);
            Assert.True(b[1].TryGetInt(out int minusTwo));
            Assert.Equal(-2, minusTwo);
            Assert.False(b[2].TryGetInt(out _));   // 소수는 정수가 아니다
            Assert.False(b[3].TryGetInt(out _));   // 지수 표기도
            Assert.Equal("3.5", b[2].RawText);
            Assert.True(j.Get("a")!.Get("x")!.TryGetBool(out bool x) && x);
            Assert.Null(j.Get("a")!.Get("y"));      // null 은 없는 것으로
            Assert.True(j.Get("a")!.Has("y"));
            Assert.True(j.Get("s")!.TryGetString(out string s));
            Assert.Equal("\u00e9\n\"q\"", s);
            Assert.Equal("/a/x", j.Get("a")!.Get("x")!.Pointer);
            Assert.Equal("/b/2", b[2].Pointer);
        }

        [Theory]
        [InlineData("{\"a\": 1, \"a\": 2}")]
        [InlineData("[1, 2,]")]
        [InlineData("{\"a\": 1,}")]
        [InlineData("// c\n{}")]
        [InlineData("{\"a\": 01}")]
        [InlineData("{\"a\": 1} x")]
        [InlineData("\"open")]
        [InlineData("{\"a\": tru}")]
        [InlineData("")]
        [InlineData("[\"\\x\"]")]
        public void RejectsInvalidJson(string text)
        {
            Assert.Throws<ManifestJsonException>(() => ManifestJsonReader.Parse(text));
        }

        [Fact]
        public void RejectsTooDeepNesting()
        {
            string deep = new string('[', ManifestJsonReader.MaxDepth + 2) + new string(']', ManifestJsonReader.MaxDepth + 2);
            Assert.Throws<ManifestJsonException>(() => ManifestJsonReader.Parse(deep));
        }

        [Fact]
        public void IntArrays()
        {
            ManifestJson j = ManifestJsonReader.Parse("{\"r\": [1, 2, 3, 4], \"bad\": [1, \"2\"]}");
            Assert.True(j.Get("r")!.TryGetIntArray(4, out int[] r));
            Assert.Equal(new[] { 1, 2, 3, 4 }, r);
            Assert.False(j.Get("r")!.TryGetIntArray(2, out _));
            Assert.False(j.Get("bad")!.TryGetIntArray(2, out _));
        }

        [Fact]
        public void SkipsUtf8Bom()
        {
            ManifestJson j = ManifestJsonReader.Parse("\uFEFF{\"a\": 1}");
            Assert.Equal(1, j.Count);
        }
    }
}
