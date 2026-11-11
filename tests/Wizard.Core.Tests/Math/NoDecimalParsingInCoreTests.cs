using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using Wizard.Core;
using Wizard.TestKit;
using Xunit;

namespace Wizard.Core.Tests.Math
{
    // M1-CORE-01 수락 3: 십진 문자열 → Raw 변환은 도구 쪽(Wizard.DataBuild, TableGen)에만 있고 코어에는 없다(CLAUDE.md 5절, T-06).
    // 코어의 상수는 빌드 때 유리수에서 Raw 로 바꿔 소스에 박는다(spec 01 2.2). 실행 중에는 FromRatio(정수 둘)뿐이다.
    public sealed class NoDecimalParsingInCoreTests
    {
        private static readonly Type[] FixedPointTypes = { typeof(Fix64), typeof(FixVec2) };

        // 문자열을 받아 Fix64·FixVec2 를 내는(반환이나 out/ref) 메서드가 코어 어셈블리 어디에도 없다
        [Fact]
        public void CoreAssembly_HasNoStringToFixedPointConversion()
        {
            var found = new List<string>();
            const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
            foreach (Type t in typeof(Fix64).Assembly.GetTypes())
            {
                foreach (MethodInfo m in t.GetMethods(all))
                {
                    ParameterInfo[] ps = m.GetParameters();
                    bool takesText = false;
                    bool givesFixed = IsFixed(m.ReturnType);
                    foreach (ParameterInfo p in ps)
                    {
                        Type pt = p.ParameterType.IsByRef ? p.ParameterType.GetElementType()! : p.ParameterType;
                        if (pt == typeof(string) || pt == typeof(char[]) || pt == typeof(ReadOnlySpan<char>))
                        {
                            takesText = true;
                        }
                        if (p.ParameterType.IsByRef && IsFixed(pt))
                        {
                            givesFixed = true;
                        }
                    }
                    if (takesText && givesFixed)
                    {
                        found.Add(t.FullName + "." + m.Name);
                    }
                }
            }
            Assert.True(found.Count == 0, string.Join("\n", found));
        }

        [Fact]
        public void FixedPointTypes_HaveNoParseMembers()
        {
            foreach (Type t in FixedPointTypes)
            {
                foreach (MemberInfo m in t.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
                {
                    Assert.DoesNotContain("Parse", m.Name, StringComparison.Ordinal);
                    Assert.DoesNotContain("Decimal", m.Name, StringComparison.Ordinal);
                }
            }
        }

        // 소스에도 십진 해석이 없다: decimal 형식, 수 해석 함수(Parse/TryParse 를 Fix64·long·int 에 쓰는 것), NumberStyles
        [Fact]
        public void CoreSources_HaveNoDecimalParsing()
        {
            string root = TestRepo.Path("src/Wizard.Core");
            var bad = new Regex(@"\bdecimal\b|\b(?:long|int|ulong|uint|Fix64|Int64|Int32)\.(?:Try)?Parse\b|\bNumberStyles\b|\bConvert\.To(?:Int64|Int32|Decimal)\b", RegexOptions.CultureInvariant);
            var found = new List<string>();
            foreach (string file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                string[] lines = File.ReadAllLines(file);
                for (int i = 0; i < lines.Length; i++)
                {
                    string code = StripComment(lines[i]);
                    if (bad.IsMatch(code))
                    {
                        found.Add(Path.GetRelativePath(root, file) + ":" + (i + 1) + ": " + lines[i].Trim());
                    }
                }
            }
            Assert.True(found.Count == 0, string.Join("\n", found));
        }

        private static bool IsFixed(Type t) => Array.IndexOf(FixedPointTypes, t) >= 0;

        private static string StripComment(string line)
        {
            int c = line.IndexOf("//", StringComparison.Ordinal);
            return c < 0 ? line : line.Substring(0, c);
        }
    }
}
