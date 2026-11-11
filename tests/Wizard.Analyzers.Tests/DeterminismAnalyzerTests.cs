using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Xunit;
using static Wizard.Analyzers.Tests.AnalyzerHarness;

namespace Wizard.Analyzers.Tests
{
    // 진단 ID 마다 양성(잡아야 하는 것)과 음성(잡으면 안 되는 것)을 하나 이상 둔다(M0-BT-02).
    public sealed class DeterminismAnalyzerTests
    {
        private const string Core = "Wizard.Core";
        private const string Sim = "Wizard.Sim";

        private static string M(string body, string usings = "") =>
            usings + "\nnamespace Wizard.Probe { public sealed class P { " + body + " } }";

        [Fact]
        public void WZ0001_FloatingPoint()
        {
            Assert.Contains("WZ0001", Ids(Core, M("double F() => 1.5;")));
            Assert.Contains("WZ0001", Ids(Sim, M("float x;")));
            Assert.Contains("WZ0001", Ids("Wizard.Rules", M("decimal D(decimal d) => d;")));
            Assert.Contains("WZ0001", Ids(Core, M("long F() => (long)System.Math.Sqrt(4);")));
            Assert.DoesNotContain("WZ0001", Ids(Core, M("long F(long a) => a * 2;")));
            Assert.DoesNotContain("WZ0001", Ids("Wizard.ViewLogic", M("float F() => 1f;")));
            Assert.DoesNotContain("WZ0001", Ids("Wizard.Client", M("double F() => 2.0;")));
        }

        [Fact]
        public void WZ0002_SystemRandom()
        {
            Assert.Contains("WZ0002", Ids(Sim, M("int F() => new System.Random(1).Next();")));
            Assert.DoesNotContain("WZ0002", Ids("Wizard.Client", M("int F() => new System.Random(1).Next();")));
        }

        [Fact]
        public void WZ0003_Clock()
        {
            Assert.Contains("WZ0003", Ids(Core, M("long F() => System.DateTime.UtcNow.Ticks;")));
            Assert.Contains("WZ0003", Ids("Wizard.Netcode", M("long F() => System.Diagnostics.Stopwatch.GetTimestamp();")));
            Assert.Contains("WZ0003", Ids(Sim, M("int F() => System.Environment.TickCount;")));
            Assert.DoesNotContain("WZ0003", Ids("Wizard.Client", M("long F() => System.DateTime.UtcNow.Ticks;")));
            Assert.DoesNotContain("WZ0003", Ids(Core, M("int F() => System.Environment.ProcessorCount;")));
        }

        [Fact]
        public void WZ0004_NewGuid()
        {
            Assert.Contains("WZ0004", Ids(Core, M("System.Guid F() => System.Guid.NewGuid();")));
            Assert.DoesNotContain("WZ0004", Ids(Core, M("System.Guid F() => System.Guid.Empty;")));
        }

        [Fact]
        public void WZ0005_HashOrderedIteration()
        {
            const string u = "using System.Collections.Generic; using System.Linq;";
            Assert.Contains("WZ0005", Ids(Sim, M("int F(Dictionary<int,int> d) { int s = 0; foreach (var kv in d) s += kv.Key; return s; }", u)));
            Assert.Contains("WZ0005", Ids(Sim, M("int F(HashSet<int> h) { int s = 0; foreach (var v in h) s += v; return s; }", u)));
            Assert.Contains("WZ0005", Ids(Sim, M("int F(Dictionary<int,int> d) { int s = 0; foreach (var k in d.Keys) s += k; return s; }", u)));
            Assert.Contains("WZ0005", Ids(Sim, M("int F(Dictionary<int,int> d) => d.Select(kv => kv.Key).FirstOrDefault();", u)));
            Assert.DoesNotContain("WZ0005", Ids(Sim, M("int F(int[] a) { int s = 0; foreach (var v in a) s += v; return s; }", u)));
            Assert.DoesNotContain("WZ0005", Ids(Sim, M("int F(Dictionary<int,int> d) => d.TryGetValue(1, out int v) ? v : 0;", u)));
        }

        [Fact]
        public void WZ0006_UnstableSort()
        {
            const string u = "using System.Collections.Generic; using System.Linq;";
            Assert.Contains("WZ0006", Ids(Core, M("void F(int[] a) => System.Array.Sort(a);", u)));
            Assert.Contains("WZ0006", Ids(Core, M("void F(List<int> a) => a.Sort();", u)));
            Assert.Contains("WZ0006", Ids(Core, M("int[] F(int[] a) => a.OrderBy(x => x).ToArray();", u)));
            Assert.DoesNotContain("WZ0006", Ids(Core, M("int[] F(int[] a) => a.Where(x => x > 0).ToArray();", u)));
        }

        [Fact]
        public void WZ0007_CultureDependentStrings()
        {
            Assert.Contains("WZ0007", Ids(Core, M("int F(string a, string b) => string.Compare(a, b);")));
            Assert.Contains("WZ0007", Ids(Core, M("string F(string a) => a.ToUpper();")));
            Assert.Contains("WZ0007", Ids(Core, M("string F(int a) => a.ToString();")));
            Assert.Contains("WZ0007", Ids(Core, M("bool F(string a) => a.StartsWith(\"x\");")));
            Assert.Contains("WZ0007", Ids(Core, M("bool F(string a, string b) => string.Equals(a, b, System.StringComparison.CurrentCulture);")));
            Assert.DoesNotContain("WZ0007", Ids(Core, M("int F(string a, string b) => string.CompareOrdinal(a, b);")));
            Assert.DoesNotContain("WZ0007", Ids(Core, M("string F(int a) => a.ToString(System.Globalization.CultureInfo.InvariantCulture);")));
            Assert.DoesNotContain("WZ0007", Ids(Core, M("bool F(string a) => a.StartsWith(\"x\", System.StringComparison.Ordinal);")));
            Assert.DoesNotContain("WZ0007", Ids("Wizard.Client", M("string F(string a) => a.ToUpper();")));
        }

        [Fact]
        public void WZ0008_Threading()
        {
            Assert.Contains("WZ0008", Ids(Sim, M("void F() => System.Threading.Tasks.Task.Run(() => { });")));
            Assert.Contains("WZ0008", Ids(Sim, M("void F() => new System.Threading.Thread(() => { }).Start();")));
            Assert.Contains("WZ0008", Ids(Sim, M("void F() => System.Threading.Tasks.Parallel.For(0, 2, i => { });")));
            Assert.DoesNotContain("WZ0008", Ids("Wizard.Client", M("System.Threading.Tasks.Task F() => System.Threading.Tasks.Task.CompletedTask;")));
        }

        [Fact]
        public void WZ0009_GetHashCode()
        {
            Assert.Contains("WZ0009", Ids(Core, M("int F(string s) => s.GetHashCode();")));
            Assert.DoesNotContain("WZ0009", Ids(Core, M("int _v; public override int GetHashCode() => _v.GetHashCode(); public override bool Equals(object? o) => false;")));
        }

        [Fact]
        public void WZ0010_MutableStatic()
        {
            Assert.Contains("WZ0010", Ids(Core, M("static int s_count;")));
            Assert.Contains("WZ0010", Ids(Core, M("static readonly int[] Table = { 1 };")));
            Assert.Contains("WZ0010", Ids(Core, M("static readonly System.Text.StringBuilder Sb = new System.Text.StringBuilder();")));
            Assert.Contains("WZ0010", Ids(Core, M("public static int Prop { get; set; }")));
            Assert.DoesNotContain("WZ0010", Ids(Core, M("const int A = 1; static readonly int B = 2; static readonly string C = \"c\"; public static int D => 3;")));
            Assert.DoesNotContain("WZ0010", Ids(Core, M("static readonly int[] Table = { 1 };"), "/repo/src/Wizard.Core/Generated/Table.g.cs"));
            Assert.Contains("WZ0010", Ids(Core, M("static readonly int[] Table = { 1 };"), "/repo/src/Wizard.Core/Tables/Table.g.cs"));
            Assert.DoesNotContain("WZ0010", Ids("Wizard.Client", M("static int s_count;")));
        }

        [Fact]
        public void WZ0011_ThreadStatic()
        {
            Assert.Contains("WZ0011", Ids(Core, M("[System.ThreadStatic] static int t;")));
            Assert.Contains("WZ0011", Ids(Core, M("readonly System.Threading.ThreadLocal<int> _t = new System.Threading.ThreadLocal<int>();")));
            Assert.DoesNotContain("WZ0011", Ids(Core, M("int _t;")));
        }

        [Fact]
        public void WZ0012_Unsafe()
        {
            Assert.Contains("WZ0012", Ids(Core, M("unsafe int F(int* p) => *p;")));
            Assert.Contains("WZ0012", Ids(Core, M("int F(int[] a) { unsafe { fixed (int* p = a) { return *p; } } }")));
            Assert.DoesNotContain("WZ0012", Ids(Core, M("int F() { System.Span<int> s = stackalloc int[2]; return s[0]; }")));
        }

        [Fact]
        public void WZ0013_MemoryReinterpret()
        {
            Assert.Contains("WZ0013", Ids(Core, M("byte[] F(int a) => System.BitConverter.GetBytes(a);")));
            Assert.Contains("WZ0013", Ids(Core, M("void F(byte[] a, byte[] b) => System.Buffer.BlockCopy(a, 0, b, 0, 1);")));
            Assert.Contains("WZ0013", Ids(Core, M("int F(System.Span<byte> s) => System.Runtime.InteropServices.MemoryMarshal.Read<int>(s);")));
            Assert.DoesNotContain("WZ0013", Ids("Wizard.Protocol", M("byte[] F(int a) => System.BitConverter.GetBytes(a);")));
        }

        [Fact]
        public void WZ0014_RawDivision()
        {
            Assert.Contains("WZ0014", Ids(Core, M("int F(int a, int b) => a / b;")));
            Assert.Contains("WZ0014", Ids(Core, M("long F(long a, long b) => a % b;")));
            Assert.Contains("WZ0014", Ids(Sim, M("int F(int a, int b) { a /= b; return a; }")));
            Assert.DoesNotContain("WZ0014", Ids(Core, M("int F(int a) => a / 2 + a % 360;")));
            Assert.DoesNotContain("WZ0014", Ids(Core, "namespace Wizard.Core { public static class FixMath { public static int D(int a, int b) => a / b; } }"));
            Assert.DoesNotContain("WZ0014", Ids(Core, "namespace Wizard.Core { public readonly struct Q { public readonly long R; public static Q operator /(Q a, Q b) => a; public static Q F(Q a, Q b) => a / b; } }"));
            Assert.DoesNotContain("WZ0014", Ids("Wizard.Client", M("int F(int a, int b) => a / b;")));
        }

        [Fact]
        public void WZ0015_Checked()
        {
            Assert.Contains("WZ0015", Ids(Core, M("int F(int a, int b) => checked(a + b);")));
            Assert.Contains("WZ0015", Ids(Core, M("int F(int a, int b) { checked { return a + b; } }")));
            Assert.DoesNotContain("WZ0015", Ids(Core, M("int F(int a, int b) => unchecked(a + b);")));
        }

        [Fact]
        public void WZ0016_Suppression()
        {
            Assert.Contains("WZ0016", Ids(Core, "#pragma warning disable WZ0001\n" + M("int x;")));
            Assert.Contains("WZ0016", Ids(Core, M("[System.Diagnostics.CodeAnalysis.SuppressMessage(\"Determinism\", \"WZ0001\")] int F() => 1;")));
            Assert.Contains("WZ0016", Ids(Core, new[] { (M("int x;"), "/repo/src/P.cs") }, null,
                new Dictionary<string, ReportDiagnostic> { ["WZ0014"] = ReportDiagnostic.Warn }));
            Assert.DoesNotContain("WZ0016", Ids("Wizard.Client", "#pragma warning disable CS0168\n" + M("int x;")));
        }

        [Fact]
        public void WZ0017_OutsideCoreHash()
        {
            const string rules = "namespace Wizard.Content.Rules { public sealed class NetRuleSet { } }";
            const string errors = "namespace Wizard.Core { public enum ErrorCode { None } }";
            (string, string) use = (M("Wizard.Content.Rules.NetRuleSet? N; Wizard.Core.ErrorCode E;"), "/repo/src/Wizard.Sim/Use.cs");
            Assert.Contains("WZ0017", Ids(Sim, new[] { use, (rules, "/repo/src/X/Rules/N.cs"), (errors, "/repo/src/X/Errors/E.cs") }));
            (string, string) contentUse = (M("Wizard.Content.Rules.NetRuleSet? N;"), "/repo/src/Wizard.Content/Pack/ContentDb.cs");
            (string, string) contentRulesUse = (M("Wizard.Content.Rules.NetRuleSet? N;"), "/repo/src/Wizard.Content/Rules/ContentDb.Rules.cs");
            Assert.Contains("WZ0017", Ids("Wizard.Content", new[] { contentUse, (rules, "/repo/src/Wizard.Content/Rules/Defs/N.cs") }));
            Assert.DoesNotContain("WZ0017", Ids("Wizard.Content", new[] { contentRulesUse, (rules, "/repo/src/Wizard.Content/Rules/Defs/N.cs") }));
            Assert.DoesNotContain("WZ0017", Ids("Wizard.Rules", new[] { use, (rules, "/repo/src/X/Rules/N.cs"), (errors, "/repo/src/X/Errors/E.cs") }));
        }

        [Fact]
        public void WZ0019_Sockets()
        {
            Assert.Contains("WZ0019", Ids("Wizard.Netcode", M("System.Net.Sockets.Socket? S;")));
            Assert.DoesNotContain("WZ0019", Ids("Wizard.Client", M("System.Net.Sockets.Socket? S;")));
        }

        [Fact]
        public void WZ0020_PaidValues()
        {
            const string ids = "namespace Wizard.Content { public enum CurrencyCode : byte { Gold = 0, SeasonCoin = 1, Starlight = 2 } public enum CurrencyBucket : byte { Earned = 0, Paid = 1, Free = 2 } }";
            (string, string) defs = (ids, "/repo/src/Wizard.Content/Ids/C.cs");
            Assert.Contains("WZ0020", Ids(Sim, new[] { (M("Wizard.Content.CurrencyCode F() => Wizard.Content.CurrencyCode.Starlight;"), "/r/a.cs"), defs }));
            Assert.Contains("WZ0020", Ids("Wizard.Rules", new[] { ("namespace Wizard.Rules.Enhance { class E { object F() => Wizard.Content.CurrencyBucket.Paid; } }", "/r/a.cs"), defs }));
            Assert.Contains("WZ0020", Ids("Wizard.Rules", new[] { ("namespace Wizard.Rules.League { class E { object F() => Wizard.Content.CurrencyCode.Starlight; } }", "/r/a.cs"), defs }));
            Assert.DoesNotContain("WZ0020", Ids("Wizard.Rules", new[] { ("namespace Wizard.Rules.Economy { class E { object F() => Wizard.Content.CurrencyCode.Starlight; } }", "/r/a.cs"), defs }));
            Assert.DoesNotContain("WZ0020", Ids(Sim, new[] { (M("object F() => Wizard.Content.CurrencyCode.Gold;"), "/r/a.cs"), defs }));
        }

        [Fact]
        public void WZ0021_ForbiddenSyntaxInSharedPackages()
        {
            Assert.Contains("WZ0021", Ids("Wizard.Client", "namespace Wizard.Client { public record R(int A); }"));
            Assert.Contains("WZ0021", Ids("Wizard.Protocol", "namespace Wizard.Protocol { public class C { public int A { get; init; } } }"));
            Assert.DoesNotContain("WZ0021", Ids("Wizard.Server.App", "namespace Wizard.Server.App { public record R(int A); }"));
            Assert.DoesNotContain("WZ0021", Ids("Wizard.Client", "namespace Wizard.Client { public class C { public int A { get; set; } } }"));
        }

        [Fact]
        public void WZ0022_ForbiddenApiInSharedPackages()
        {
            Assert.Contains("WZ0022", Ids("Wizard.Protocol", M("string F(object o) => System.Text.Json.JsonSerializer.Serialize(o);")));
            Assert.Contains("WZ0022", Ids("Wizard.Client", M("long F(long a, long b) => System.Math.BigMul(a, b, out long lo);")));
            Assert.Contains("WZ0022", Ids("Wizard.Client", M("System.Int128 X;")));
            Assert.DoesNotContain("WZ0022", Ids("Wizard.Server.Api", M("string F(object o) => System.Text.Json.JsonSerializer.Serialize(o);")));
        }

        [Fact]
        public void WZ0023_LayerReferences()
        {
            MetadataReference rules = EmptyAssembly("Wizard.Rules");
            MetadataReference sim = EmptyAssembly("Wizard.Sim");
            MetadataReference fakes = EmptyAssembly("Wizard.Server.Payments.Fakes");
            MetadataReference data = EmptyAssembly("Wizard.Server.Data");
            MetadataReference client = EmptyAssembly("Wizard.Client");
            var code = new[] { (M("int x;"), "/r/a.cs") };
            Assert.Contains("WZ0023", Ids(Sim, code, new[] { rules }));                          // 아래가 위를 참조
            Assert.Contains("WZ0023", Ids("Wizard.Server.Api", code, new[] { fakes }));          // Api 는 가짜 결제를 참조하지 않는다
            Assert.Contains("WZ0023", Ids("Wizard.Server.Duel", code, new[] { data }));          // 결투 프로세스는 DB 를 모른다
            Assert.Contains("WZ0023", Ids("Wizard.Server.App", code, new[] { client }));         // 서버는 클라이언트를 참조하지 않는다
            Assert.DoesNotContain("WZ0023", Ids("Wizard.Rules", code, new[] { sim }));
            Assert.DoesNotContain("WZ0023", Ids("Wizard.Server.Api.DevHost", code, new[] { fakes }));
        }
    }
}
