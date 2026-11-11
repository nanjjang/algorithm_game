using System.IO;
using System.Text;
using System.Text.Json.Nodes;
using Xunit;

namespace Wizard.TaskGraph.Tests
{
    public sealed class PathAndJsonTests
    {
        [Theory]
        [InlineData("src/", "src/a.cs", true)]
        [InlineData("src/*/Generated/", "src/Wizard.Core/Generated/x.g.cs", true)]
        [InlineData("tools/*/*.csproj", "tools/Wizard.TaskGraph/", true)]
        [InlineData("tools/*/Program.cs", "tools/Wizard.TaskGraph/Program.cs", true)]
        [InlineData("src/*.cs", "src/x/y.cs", false)]
        [InlineData("a/b", "a/c", false)]
        [InlineData("a/b", "a/b/", true)]
        [InlineData("a/b.cs", "a/b.cs/c", false)]
        [InlineData("x*/", "xy*/", true)]
        [InlineData("a*b/", "c*d/", false)]
        public void Overlap(string a, string b, bool expected)
        {
            Assert.Equal(expected, PathPattern.Overlap(a, b));
            Assert.Equal(expected, PathPattern.Overlap(b, a));
        }

        [Theory]
        [InlineData("tools/Wizard.TaskGraph/", "tools/Wizard.TaskGraph/Program.cs", true)]
        [InlineData("tools/Wizard.TaskGraph/", "tools/Wizard.TaskGraph/Tests/X.cs", true)]
        [InlineData("tools/Wizard.TaskGraph/", "tools/Wizard.TaskGraphX/Program.cs", false)]
        [InlineData("tools/Wizard.TaskGraph/", "tools/Wizard.TaskGraph", false)]
        [InlineData("docs/dev/tasks.schema.json", "docs/dev/tasks.schema.json", true)]
        [InlineData("docs/dev/tasks.schema.json", "docs/dev/tasks.json", false)]
        [InlineData("src/*/*.csproj", "src/Wizard.Core/Wizard.Core.csproj", true)]
        [InlineData("src/*/*.csproj", "src/Wizard.Core/Sub/Wizard.Core.csproj", false)]
        public void Owns(string pattern, string file, bool expected)
        {
            Assert.Equal(expected, PathPattern.Owns(pattern, file));
        }

        [Theory]
        [InlineData("tests/golden/**/expected*.json", "tests/golden/m1/expected.json", true)]
        [InlineData("tests/golden/**/expected*.json", "tests/golden/expected-x64.json", true)]
        [InlineData("tests/golden/**/expected*.json", "tests/golden/a/b/expected.mono.json", true)]
        [InlineData("tests/golden/**/expected*.json", "tests/golden/a/actual.json", false)]
        [InlineData("src/*/Generated/*.g.cs", "src/Wizard.Sim/Generated/SimVersion.g.cs", true)]
        [InlineData("src/*/Generated/*.g.cs", "src/Wizard.Sim/Generated/SimVersion.g.cs.meta", false)]
        [InlineData("unity/Library/**", "unity/Library/a/b", true)]
        public void Glob(string glob, string file, bool expected)
        {
            Assert.Equal(expected, PathPattern.Glob(glob, file));
        }

        [Fact]
        public void RealTasksJson_RoundTripsByteForByte()
        {
            // add 가 tasks.json 을 다시 쓸 때 남의 줄을 바꾸지 않는다는 근거: 읽고 그대로 쓰면 바이트가 같다
            string text = File.ReadAllText(Fx.RealTasksPath, Encoding.UTF8);
            Assert.Equal(text, TaskDoc.Parse(text, "tasks.json").Serialize());
        }

        [Fact]
        public void Escaping_MatchesJsonStringify()
        {
            // 기대값은 node 의 JSON.stringify 규칙(ECMA-262 QuoteJSONString)에서 손으로 옮겼다
            var o = new JsonObject
            {
                ["s"] = "따옴표\" 역슬래시\\ 줄\n탭\t\u0001 \u2028 </b> 😀 \ud800",
                ["e"] = new JsonArray(),
                ["n"] = new JsonObject(),
                ["i"] = 12,
                ["z"] = null,
            };
            string expected = "{\n \"s\": \"따옴표\\\" 역슬래시\\\\ 줄\\n탭\\t\\u0001 \u2028 </b> 😀 \\ud800\",\n \"e\": [],\n \"n\": {},\n \"i\": 12,\n \"z\": null\n}\n";
            Assert.Equal(expected, JsStyleJson.Serialize(o, 1));
        }
    }
}
