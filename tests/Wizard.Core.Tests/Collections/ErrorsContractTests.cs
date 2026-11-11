using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Wizard.Core;
using Wizard.TestKit;
using Xunit;

namespace Wizard.Core.Tests.Collections
{
    // CONTRACTS 0.2, DECISIONS-TECH T-18. M1-CORE-05 수락 조건 3·6.
    // 테스트 폴더가 카드의 소유 경로(tests/Wizard.Core.Tests/Collections/)라 오류 타입의 대조도 여기에 둔다.
    public sealed class ErrorsContractTests
    {
        private static readonly Regex Member = new Regex(@"\b([A-Za-z_][A-Za-z0-9_]*)\s*=\s*([0-9]+)\b", RegexOptions.CultureInvariant);

        // 수락 3: ErrorCode 의 이름과 번호가 CONTRACTS 0.2절의 코드 블록과 한 글자도 다르지 않다(빠짐, 더함, 순서, 번호 모두)
        [Fact]
        public void ErrorCode_NamesAndNumbersEqualContracts02()
        {
            List<(string Name, int Value)> contract = ParseContractEnum(out string underlying);
            Assert.Equal("ushort", underlying);
            Assert.Equal(typeof(ushort), Enum.GetUnderlyingType(typeof(ErrorCode)));

            var actual = new List<(string Name, int Value)>();
            foreach (ErrorCode code in (ErrorCode[])Enum.GetValues(typeof(ErrorCode)))
            {
                actual.Add((code.ToString(), (int)code));
            }
            Assert.Equal(contract.Count, actual.Count);
            for (int i = 0; i < contract.Count; i++)
            {
                Assert.Equal(contract[i], actual[i]);
            }
            // 같은 번호에 두 이름이 붙지 않았다(GetValues 는 번호마다 하나만 돌려주므로 이름 수로 따로 본다)
            Assert.Equal(contract.Count, Enum.GetNames(typeof(ErrorCode)).Length);
        }

        // 원본 파일의 선언 순서도 계약과 같다(리플렉션은 번호순이라 원문 순서를 따로 본다)
        [Fact]
        public void ErrorCode_SourceDeclarationOrderEqualsContracts02()
        {
            List<(string Name, int Value)> contract = ParseContractEnum(out _);
            string source = File.ReadAllText(TestRepo.Path("src/Wizard.Core/Errors/ErrorCode.cs"));
            List<(string Name, int Value)> declared = ParseEnumBody(source, "public enum ErrorCode : ushort", out _);
            Assert.Equal(contract, declared);
        }

        // 수락 6: ErrorCode, Error, Result<T>, Unit 은 src/Wizard.Core/Errors/(코어 버전 해시 밖). SimInvariantException 은 해시 안
        [Fact]
        public void ErrorTypesLiveInErrorsFolderAndInvariantExceptionInsideHash()
        {
            Assert.Equal("src/Wizard.Core/Errors/ErrorCode.cs", DeclaringFile(@"\benum\s+ErrorCode\b"));
            Assert.Equal("src/Wizard.Core/Errors/Error.cs", DeclaringFile(@"\bstruct\s+Error\b"));
            Assert.Equal("src/Wizard.Core/Errors/Result.cs", DeclaringFile(@"\bstruct\s+Result\s*<"));
            Assert.Equal("src/Wizard.Core/Errors/Unit.cs", DeclaringFile(@"\bstruct\s+Unit\b"));

            string exception = DeclaringFile(@"\bclass\s+SimInvariantException\b");
            Assert.True(InCoreVersionHash(exception), exception + " must be inside the core version hash");
            foreach (string f in new[] { "ErrorCode", "Error", "Result", "Unit" })
            {
                Assert.False(InCoreVersionHash("src/Wizard.Core/Errors/" + f + ".cs"));
            }

            // 공개 형식과 네임스페이스는 계약 그대로(namespace Wizard.Core)
            foreach (Type t in new[] { typeof(ErrorCode), typeof(Error), typeof(Result<>), typeof(Unit), typeof(SimInvariantException) })
            {
                Assert.Equal("Wizard.Core", t.Namespace);
                Assert.True(t.IsPublic);
            }
            Assert.True(typeof(Error).IsValueType);
            Assert.True(typeof(Result<>).IsValueType);
            Assert.True(typeof(Unit).IsValueType);
            Assert.True(typeof(SimInvariantException).IsSealed);
        }

        [Fact]
        public void Result_And_Error_Behave()
        {
            Result<Unit> ok = Result<Unit>.Success(default);
            Assert.True(ok.Ok);
            Result<int> fail = Result<int>.Fail(new Error(ErrorCode.RateLimited, "dev", 1500));
            Assert.False(fail.Ok);
            Assert.Equal(ErrorCode.RateLimited, fail.Error.Code);
            Assert.Equal("dev", fail.Error.Detail);
            Assert.Equal(1500, fail.Error.RetryAfterMs);
            Result<string> byCode = Result<string>.Fail(ErrorCode.NotFound);
            Assert.Equal(ErrorCode.NotFound, byCode.Error.Code);
            Assert.Null(byCode.Error.Detail);
            Assert.Equal(0, byCode.Error.RetryAfterMs);
            Assert.Equal(ErrorCode.None, ok.Error.Code);
        }

        // DECISIONS-TECH T-18 의 대상 목록(tools/Wizard.VersionStamp 와 같은 규칙): src/Wizard.Core/** 에서 Errors/ 와 Generated/ 밖의 .cs
        private static bool InCoreVersionHash(string rel) =>
            rel.StartsWith("src/Wizard.Core/", StringComparison.Ordinal)
            && rel.EndsWith(".cs", StringComparison.Ordinal)
            && !rel.StartsWith("src/Wizard.Core/Errors/", StringComparison.Ordinal)
            && (!rel.StartsWith("src/Wizard.Core/Generated/", StringComparison.Ordinal) || rel.EndsWith(".g.cs", StringComparison.Ordinal));

        private static string DeclaringFile(string pattern)
        {
            var re = new Regex(pattern, RegexOptions.CultureInvariant);
            string root = TestRepo.Path("src/Wizard.Core");
            var hits = new List<string>();
            foreach (string path in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                string rel = "src/Wizard.Core/" + Path.GetRelativePath(root, path).Replace('\\', '/');
                if (rel.Contains("/obj/", StringComparison.Ordinal) || rel.Contains("/bin/", StringComparison.Ordinal))
                {
                    continue;
                }
                if (re.IsMatch(File.ReadAllText(path)))
                {
                    hits.Add(rel);
                }
            }
            Assert.Single(hits);
            return hits[0];
        }

        private static List<(string Name, int Value)> ParseContractEnum(out string underlying)
        {
            string text = File.ReadAllText(TestRepo.Path("docs/dev/CONTRACTS.md"));
            int section = text.IndexOf("### 0.2 ", StringComparison.Ordinal);
            int next = text.IndexOf("### 0.3 ", StringComparison.Ordinal);
            Assert.True(section >= 0 && next > section, "CONTRACTS 0.2 section not found");
            return ParseEnumBody(text.Substring(section, next - section), "public enum ErrorCode : ", out underlying);
        }

        // "public enum ErrorCode : <형식> {" 부터 처음 닫는 "}" 까지에서 "이름 = 번호" 를 순서대로 읽는다. 줄 주석은 버린다
        private static List<(string Name, int Value)> ParseEnumBody(string text, string head, out string underlying)
        {
            int start = text.IndexOf(head, StringComparison.Ordinal);
            Assert.True(start >= 0, "enum head not found: " + head);
            int open = text.IndexOf('{', start);
            int close = text.IndexOf('}', open);
            Assert.True(open > start && close > open);
            string afterColon = text.Substring(start, open - start);
            underlying = afterColon.Substring(afterColon.LastIndexOf(':') + 1).Trim();
            var result = new List<(string Name, int Value)>();
            foreach (string rawLine in text.Substring(open + 1, close - open - 1).Split('\n'))
            {
                string line = rawLine;
                int comment = line.IndexOf("//", StringComparison.Ordinal);
                if (comment >= 0)
                {
                    line = line.Substring(0, comment);
                }
                foreach (Match m in Member.Matches(line))
                {
                    result.Add((m.Groups[1].Value, int.Parse(m.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture)));
                }
            }
            Assert.NotEmpty(result);
            return result;
        }
    }
}
