using System;
using System.Collections.Generic;
using Xunit;

namespace Wizard.Ci.Tests
{
    // pre-commit 의 금지 API 빠른 검사(구문). 틀림없는 꼴만 잡고 거짓 거절을 내지 않는다
    public sealed class ForbiddenApiScanTests
    {
        private static List<string> Core(string body) =>
            ForbiddenApiScan.Scan("src/Wizard.Sim/X.cs", "namespace Wizard.Sim\n{\n    public static class X\n    {\n" + body + "\n    }\n}\n", ForbiddenApiScan.Role.Core);

        [Theory]
        [InlineData("public static float F;", "실수형 'float'")]
        [InlineData("public static double D() => 0;", "실수형 'double'")]
        [InlineData("public static decimal M;", "실수형 'decimal'")]
        [InlineData("public static long L = (long)1.5;", "실수 리터럴 '1.5'")]
        [InlineData("public static int I = (int)2f;", "실수 리터럴 '2f'")]
        [InlineData("public static long T() => System.DateTime.Now.Ticks;", "'DateTime'")]
        [InlineData("public static long T() => DateTime.UtcNow.Ticks;", "'DateTime'")]
        [InlineData("public static object S() => new System.Diagnostics.Stopwatch();", "'Stopwatch'")]
        [InlineData("public static int C() => Environment.TickCount;", "'Environment.TickCount'")]
        [InlineData("public static object R() => new System.Random(1);", "'new System.Random'")]
        [InlineData("public static object R() => new Random(1);", "'new Random'")]
        [InlineData("public static object G() => Guid.NewGuid();", "'Guid.NewGuid'")]
        [InlineData("public static void P() { Parallel.For(0, 1, i => { }); }", "'Parallel'")]
        [InlineData("[ThreadStatic] public static int Tls;", "[ThreadStatic]")]
        [InlineData("public static byte[] B() => BitConverter.GetBytes(1);", "'BitConverter'")]
        [InlineData("public static void C(int[] a, int[] b) { Buffer.BlockCopy(a, 0, b, 0, 4); }", "'Buffer.BlockCopy'")]
        [InlineData("public static long M(long a) => Math.BigMul(a, a);", "'Math.BigMul'")]
        [InlineData("public static int K(int a) => checked(a + 1);", "'checked'")]
        [InlineData("public static unsafe void U() { }", "'unsafe'")]
        [InlineData("public static object V() => default(Int128);", "'Int128'")]
        public void CoreViolations_AreCaught(string member, string expected)
        {
            List<string> hits = Core(member);
            Assert.Contains(hits, h => h.Contains(expected, StringComparison.Ordinal) && h.StartsWith("src/Wizard.Sim/X.cs:5: ", StringComparison.Ordinal));
        }

        [Fact]
        public void UsingDirectives_AreCaught()
        {
            List<string> hits = ForbiddenApiScan.Scan("src/Wizard.Core/A.cs", "using System.Threading.Tasks;\nusing UnityEngine;\nnamespace Wizard.Core { }\n",
                ForbiddenApiScan.Role.Core);
            Assert.Contains(hits, h => h.Contains("using System.Threading.Tasks", StringComparison.Ordinal));
            Assert.Single(hits, h => h.StartsWith("src/Wizard.Core/A.cs:2: ", StringComparison.Ordinal));
        }

        [Theory]
        [InlineData("// float 은 쓰지 않는다. DateTime.Now 도")]
        [InlineData("/* new System.Random() */")]
        [InlineData("public const string Doc = \"float double DateTime Stopwatch\";")]
        [InlineData("public enum Pick { First, Random }")]
        [InlineData("public static int Pick(Mode m) => m == Mode.Random ? 1 : 0;")]
        [InlineData("public static int T(Player p) => p.Thread + p.DateTime;")]
        [InlineData("public static int Half(int x) => x / 2;")]
        [InlineData("public static long L = 1L << 32;")]
        [InlineData("public static int U(int a) => unchecked(a + 1);")]
        [InlineData("public static System.Collections.Generic.List<int> Sorted(System.Collections.Generic.List<int> l) => l;")]
        public void CoreNonViolations_AreNotCaught(string member)
        {
            Assert.Empty(Core(member));
        }

        [Fact]
        public void RulesBansOnlyFloatingPoint_NetcodeBansClocksAndSockets()
        {
            const string src = "namespace N { class A { double D; long T() => System.DateTime.Now.Ticks; object S = new System.Net.Sockets.Socket(default, default, default); } }";
            List<string> rules = ForbiddenApiScan.Scan("src/Wizard.Rules/A.cs", src, ForbiddenApiScan.Role.Rules);
            Assert.Single(rules);
            Assert.Contains("'double'", rules[0], StringComparison.Ordinal);

            List<string> net = ForbiddenApiScan.Scan("src/Wizard.Netcode/A.cs", src, ForbiddenApiScan.Role.Netcode);
            Assert.Contains(net, h => h.Contains("'DateTime'", StringComparison.Ordinal));
            Assert.Contains(net, h => h.Contains("'Socket'", StringComparison.Ordinal));
            Assert.DoesNotContain(net, h => h.Contains("double", StringComparison.Ordinal));
        }

        [Theory]
        [InlineData("src/Wizard.Core/Fix64.cs", ForbiddenApiScan.Role.Core)]
        [InlineData("src/Wizard.Content/Pack/A.cs", ForbiddenApiScan.Role.Core)]
        [InlineData("src/Wizard.Sim/Heat/Heat.cs", ForbiddenApiScan.Role.Core)]
        [InlineData("src/Wizard.Rules/Enhance.cs", ForbiddenApiScan.Role.Rules)]
        [InlineData("src/Wizard.Netcode/Clock.cs", ForbiddenApiScan.Role.Netcode)]
        [InlineData("src/Wizard.ViewLogic/FixConvert.cs", ForbiddenApiScan.Role.None)]
        [InlineData("server/Wizard.Server.Api/A.cs", ForbiddenApiScan.Role.None)]
        [InlineData("src/Wizard.Core/Wizard.Core.csproj", ForbiddenApiScan.Role.None)]
        public void Roles_FollowTheAssembly(string path, ForbiddenApiScan.Role role)
        {
            Assert.Equal(role, ForbiddenApiScan.RoleOf(path));
        }
    }
}
