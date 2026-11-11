using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;
using static Wizard.Analyzers.Tests.AnalyzerHarness;

namespace Wizard.Analyzers.Tests
{
    // 진단 ID 마다 양성(잡아야 하는 것)과 음성(잡으면 안 되는 것)을 하나 이상 둔다(M0-BT-02).
    // 억제는 SuppressionTests, 생성 코드는 GeneratedCodeTests, 네임스페이스 = 폴더는 NamespaceFolderTests 에 있다.
    public sealed class DeterminismAnalyzerTests
    {
        private const string Core = "Wizard.Core";
        private const string Content = "Wizard.Content";
        private const string Sim = "Wizard.Sim";
        private const string Rules = "Wizard.Rules";
        private const string Netcode = "Wizard.Netcode";
        private const string Client = "Wizard.Client";

        private static string M(string body, string usings = "") =>
            usings + "\nnamespace Wizard.Probe { public sealed class P { " + body + " } }";

        [Fact]
        public void WZ0001_FloatingPoint()
        {
            Assert.Contains("WZ0001", Ids(Core, M("double F() => 1.5;")));
            Assert.Contains("WZ0001", Ids(Content, M("float x;")));
            Assert.Contains("WZ0001", Ids(Sim, M("float x;")));
            Assert.Contains("WZ0001", Ids(Rules, M("decimal D(decimal d) => d;")));
            Assert.Contains("WZ0001", Ids(Rules, M("long F() => (long)(3 * 0.5);")));
            Assert.Contains("WZ0001", Ids(Core, M("long F() => (long)System.Math.Sqrt(4);")));
            Assert.Contains("WZ0001", Ids(Core, M("System.Numerics.Vector2 V;")));
            Assert.Contains("WZ0001", Ids(Sim, M("int F(System.Collections.Generic.List<float> a) => a.Count;")));
            Assert.DoesNotContain("WZ0001", Ids(Core, M("long F(long a) => a * 2;")));
            Assert.DoesNotContain("WZ0001", Ids("Wizard.ViewLogic", M("float F() => 1f;")));
            Assert.DoesNotContain("WZ0001", Ids(Client, M("double F() => 2.0;")));
            Assert.DoesNotContain("WZ0001", Ids(Netcode, M("double F() => 2.0;")));
        }

        [Fact]
        public void WZ0002_SystemRandom()
        {
            Assert.Contains("WZ0002", Ids(Sim, M("int F() => new System.Random(1).Next();")));
            Assert.Contains("WZ0002", Ids(Core, M("int F() => System.Security.Cryptography.RandomNumberGenerator.GetInt32(4);")));
            Assert.DoesNotContain("WZ0002", Ids(Client, M("int F() => new System.Random(1).Next();")));
            // 이름만 같은 다른 타입은 막지 않는다
            Assert.DoesNotContain("WZ0002", Ids(Core, "namespace Wizard.Core { public struct Random { } public sealed class P { Random R; } }"));
        }

        [Fact]
        public void WZ0003_Clock()
        {
            Assert.Contains("WZ0003", Ids(Core, M("long F() => System.DateTime.UtcNow.Ticks;")));
            Assert.Contains("WZ0003", Ids(Content, M("System.DateTimeOffset T;")));
            Assert.Contains("WZ0003", Ids(Netcode, M("long F() => System.Diagnostics.Stopwatch.GetTimestamp();")));
            Assert.Contains("WZ0003", Ids(Netcode, M("long F() => System.DateTime.UtcNow.Ticks;")));
            Assert.Contains("WZ0003", Ids(Sim, M("int F() => System.Environment.TickCount;")));
            Assert.Contains("WZ0003", Ids(Sim, M("long F() => System.Environment.TickCount64;")));
            Assert.DoesNotContain("WZ0003", Ids(Client, M("long F() => System.DateTime.UtcNow.Ticks;")));
            Assert.DoesNotContain("WZ0003", Ids(Core, M("int F() => System.Environment.ProcessorCount;")));
        }

        [Fact]
        public void WZ0004_NewGuid()
        {
            Assert.Contains("WZ0004", Ids(Core, M("System.Guid F() => System.Guid.NewGuid();")));
            Assert.Contains("WZ0004", Ids(Sim, M("string F() => System.Guid.NewGuid().ToString(\"N\", null);")));
            Assert.DoesNotContain("WZ0004", Ids(Core, M("System.Guid F() => System.Guid.Empty;")));
            Assert.DoesNotContain("WZ0004", Ids(Client, M("System.Guid F() => System.Guid.NewGuid();")));
        }

        [Fact]
        public void WZ0005_HashOrderedIteration()
        {
            const string u = "using System.Collections.Generic; using System.Linq;";
            Assert.Contains("WZ0005", Ids(Sim, M("int F(Dictionary<int,int> d) { int s = 0; foreach (var kv in d) s += kv.Key; return s; }", u)));
            Assert.Contains("WZ0005", Ids(Sim, M("int F(HashSet<int> h) { int s = 0; foreach (var v in h) s += v; return s; }", u)));
            Assert.Contains("WZ0005", Ids(Sim, M("int F(Dictionary<int,int> d) { int s = 0; foreach (var k in d.Keys) s += k; return s; }", u)));
            Assert.Contains("WZ0005", Ids(Sim, M("int F(Dictionary<int,int> d) => d.Select(kv => kv.Key).FirstOrDefault();", u)));
            Assert.Contains("WZ0005", Ids(Core, M("int F(IReadOnlyDictionary<int,int> d) { int s = 0; foreach (var kv in d) s += kv.Value; return s; }", u)));
            Assert.Contains("WZ0005", Ids(Core, M("IEnumerable<int> F(IReadOnlyDictionary<int,int> d) => d.Keys;", u)));
            Assert.Contains("WZ0005", Ids(Core, M("List<int> F(HashSet<int> h) => new List<int>(h);", u)));
            Assert.Contains("WZ0005", Ids(Core, M("void F(HashSet<int> h, int[] a) => h.CopyTo(a);", u)));
            Assert.Contains("WZ0005", Ids(Content, M("int F(Dictionary<int,int> d) { int s = 0; foreach (var kv in (IEnumerable<KeyValuePair<int,int>>)d) s += kv.Key; return s; }", u)));
            Assert.DoesNotContain("WZ0005", Ids(Sim, M("int F(int[] a) { int s = 0; foreach (var v in a) s += v; return s; }", u)));
            Assert.DoesNotContain("WZ0005", Ids(Sim, M("int F(Dictionary<int,int> d) => d.TryGetValue(1, out int v) ? v : d.Count + (d.ContainsKey(2) ? 1 : 0);", u)));
            Assert.DoesNotContain("WZ0005", Ids(Client, M("int F(HashSet<int> h) { int s = 0; foreach (var v in h) s += v; return s; }", u)));
        }

        [Fact]
        public void WZ0005_HashOrderedIterationThroughDerivedAndConstrainedTypes()
        {
            const string u = "using System.Collections.Generic; using System.Linq;";
            static string D(string body) =>
                "using System.Collections.Generic; using System.Linq;\nnamespace Wizard.Probe { public sealed class Map : Dictionary<int,int> { } " +
                "public class Set : HashSet<int> { } public sealed class SubSet : Set { } public interface IMap : IReadOnlyDictionary<int,int> { } " +
                "public sealed class Ordered : SortedDictionary<int,int> { } public sealed class Seq : List<int> { } " +
                "public sealed class P { " + body + " } }";
            // 상속한 클래스(한 단계, 두 단계), 그 위의 LINQ·Keys, 제약한 타입 매개변수, 넓힌 인터페이스
            Assert.Contains("WZ0005", Ids(Sim, D("int F(Map map) { int s = 0; foreach (var kv in map) s += kv.Key; return s; }")));
            Assert.Contains("WZ0005", Ids(Sim, D("int F(Set set) { int s = 0; foreach (var v in set) s += v; return s; }")));
            Assert.Contains("WZ0005", Ids(Sim, D("int F(SubSet set) { int s = 0; foreach (var v in set) s += v; return s; }")));
            Assert.Contains("WZ0005", Ids(Sim, D("int F(Map map) => map.Select(kv => kv.Key).First();")));
            Assert.Contains("WZ0005", Ids(Sim, D("int F(Map map) { int s = 0; foreach (var k in map.Keys) s += k; return s; }")));
            Assert.Contains("WZ0005", Ids(Sim, D("void F(Map map, KeyValuePair<int,int>[] a) => ((ICollection<KeyValuePair<int,int>>)map).CopyTo(a, 0);")));
            Assert.Contains("WZ0005", Ids(Sim, D("int F<T>(T d) where T : Dictionary<int,int> { int s = 0; foreach (var kv in d) s += kv.Key; return s; }")));
            Assert.Contains("WZ0005", Ids(Sim, D("int F<T>(T d) where T : HashSet<int> => d.Sum();")));
            Assert.Contains("WZ0005", Ids(Core, D("int F<T>(T d) where T : IDictionary<int,int> { int s = 0; foreach (var kv in d) s += kv.Key; return s; }")));
            Assert.Contains("WZ0005", Ids(Core, D("int F(IMap m) { int s = 0; foreach (var kv in m) s += kv.Key; return s; }")));
            Assert.Contains("WZ0005", Ids(Content, D("int F<T, U>(U d) where T : Dictionary<int,int> where U : T { int s = 0; foreach (var kv in d) s += kv.Key; return s; }")));
            // 순서가 정해진 컬렉션은 상속하거나 제약해도 막지 않는다(구현한 인터페이스는 따라가지 않는다)
            Assert.DoesNotContain("WZ0005", Ids(Sim, D("int F(Ordered o) { int s = 0; foreach (var kv in o) s += kv.Key; return s; }")));
            Assert.DoesNotContain("WZ0005", Ids(Sim, M("int F(SortedSet<int> o) { int s = 0; foreach (var v in o) s += v; return s; }", u)));
            Assert.DoesNotContain("WZ0005", Ids(Sim, D("int F(Seq q) => q.Select(x => x).First();")));
            Assert.DoesNotContain("WZ0005", Ids(Sim, D("int F<T>(T q) where T : List<int> { int s = 0; foreach (var v in q) s += v; return s; }")));
            Assert.DoesNotContain("WZ0005", Ids(Sim, D("int F(Map map) => map.TryGetValue(1, out int v) ? v : map.Count;")));
            Assert.DoesNotContain("WZ0005", Ids(Client, D("int F(Map map) { int s = 0; foreach (var kv in map) s += kv.Key; return s; }")));
        }

        [Fact]
        public void WZ0006_UnstableSort()
        {
            const string u = "using System.Collections.Generic; using System.Linq;";
            Assert.Contains("WZ0006", Ids(Core, M("void F(int[] a) => System.Array.Sort(a);", u)));
            Assert.Contains("WZ0006", Ids(Core, M("void F(List<int> a) => a.Sort();", u)));
            Assert.Contains("WZ0006", Ids(Sim, M("void F(List<int> a) => a.Sort((x, y) => x - y);", u)));
            Assert.Contains("WZ0006", Ids(Core, M("int[] F(int[] a) => a.OrderBy(x => x).ToArray();", u)));
            Assert.Contains("WZ0006", Ids(Content, M("int[] F(int[] a) => a.OrderBy(x => x).ThenByDescending(x => -x).ToArray();", u)));
            Assert.Contains("WZ0006", Ids(Core, M("void F(System.Span<int> s) => System.MemoryExtensions.Sort(s);", u)));
            Assert.Contains("WZ0006", Ids(Core, M("System.Action<int[]> F() => System.Array.Sort;", u)));   // 메서드 그룹
            Assert.DoesNotContain("WZ0006", Ids(Core, M("int[] F(int[] a) => a.Where(x => x > 0).ToArray();", u)));
            Assert.DoesNotContain("WZ0006", Ids(Core, M("void F(int[] a) => Wizard.Probe.DetSort.Sort(a); } public static class DetSort { public static void Sort(int[] a) { }", u)));
        }

        // ARCHITECTURE 3.3 표의 마지막 행: Array.Sort·List<T>.Sort 는 코어 밖의 공유 어셈블리에서도 쓰지 않는다
        [Fact]
        public void WZ0006_UnstableSortInEverySharedAssembly()
        {
            const string u = "using System; using System.Collections.Generic; using System.Linq;";
            foreach (string asm in new[] { Rules, "Wizard.Protocol", Netcode, Client, "Wizard.ViewLogic", "Wizard.Text", "Wizard.TestKit.Shared" })
            {
                Assert.Contains("WZ0006", Ids(asm, M("public static void A(int[] a) => System.Array.Sort(a);", u)));
                Assert.Contains("WZ0006", Ids(asm, M("public static void B(List<int> l) => l.Sort();", u)));
                Assert.Contains("WZ0006", Ids(asm, M("public static void C(string[] k, int[] v) => Array.Sort(k, v, StringComparer.Ordinal);", u)));
                Assert.Contains("WZ0006", Ids(asm, M("public static void D(List<int> l) => l.Sort((x, y) => x.CompareTo(y));", u)));
            }
            Assert.Contains("WZ0006", Ids(Rules, "using System;\nnamespace Wizard.Rules.Enhance { static class E { static void F(int[] a) => Array.Sort(a); } }"));
            // 3.3 의 '대신' 칸(완전 순서 비교자) 가운데 기계로 확인되는 꼴: 문자열 하나의 열을 StringComparer.Ordinal 로(코어 밖만)
            Assert.DoesNotContain("WZ0006", Ids("Wizard.TestKit.Shared", M("void F(List<string> names) => names.Sort(StringComparer.Ordinal);", u)));
            Assert.DoesNotContain("WZ0006", Ids(Client, M("void F(string[] a) => Array.Sort(a, StringComparer.Ordinal);", u)));
            Assert.DoesNotContain("WZ0006", Ids(Client, M("void F(string[] a) => Array.Sort(a, 0, a.Length, StringComparer.Ordinal);", u)));
            // 그 밖의 비교자(대소문자 무시는 글자가 다른 동률이 생긴다, 람다, 비교자 없음), keys·items 짝, 메서드 그룹, 코어는 막는다
            Assert.Contains("WZ0006", Ids("Wizard.TestKit.Shared", M("void F(List<string> names) => names.Sort(StringComparer.OrdinalIgnoreCase);", u)));
            Assert.Contains("WZ0006", Ids(Client, M("void F(List<string> names) => names.Sort((x, y) => string.CompareOrdinal(x, y));", u)));
            Assert.Contains("WZ0006", Ids(Client, M("void F(List<string> names) => names.Sort();", u)));
            Assert.Contains("WZ0006", Ids(Client, M("void F(string[] k, int[] v) => Array.Sort(k, v, StringComparer.Ordinal);", u)));
            Assert.Contains("WZ0006", Ids(Client, M("Action<string[], IComparer<string>> F() => Array.Sort;", u)));
            Assert.Contains("WZ0006", Ids(Core, M("void F(List<string> names) => names.Sort(StringComparer.Ordinal);", u)));
            Assert.Contains("WZ0006", Ids(Sim, M("void F(string[] a) => Array.Sort(a, StringComparer.Ordinal);", u)));
            // LINQ OrderBy(안정 정렬)는 3.3 표에 없다. 코어 밖에서는 막지 않는다(T-11 은 코어)
            Assert.DoesNotContain("WZ0006", Ids(Client, M("int[] F(int[] a) => a.OrderBy(x => x).ToArray();", u)));
            Assert.DoesNotContain("WZ0006", Ids(Netcode, M("int[] F(int[] a) => a.Where(x => x > 0).ToArray();", u)));
            // 공유 밖(서버, 도구)은 이 분석기의 정렬 규칙 대상이 아니다
            Assert.DoesNotContain("WZ0006", Ids("Wizard.Server.App", M("void F(int[] a) => System.Array.Sort(a);", u)));
            Assert.DoesNotContain("WZ0006", Ids("Wizard.TableGen", M("void F(int[] a) => System.Array.Sort(a);", u)));
        }

        [Fact]
        public void WZ0007_CultureDependentStrings()
        {
            Assert.Contains("WZ0007", Ids(Core, M("int F(string a, string b) => string.Compare(a, b);")));
            Assert.Contains("WZ0007", Ids(Core, M("int F(string a, string b) => a.CompareTo(b);")));
            Assert.Contains("WZ0007", Ids(Core, M("string F(string a) => a.ToUpper();")));
            Assert.Contains("WZ0007", Ids(Core, M("char F(char c) => char.ToLower(c);")));
            Assert.Contains("WZ0007", Ids(Core, M("string F(int a) => a.ToString();")));
            Assert.Contains("WZ0007", Ids(Core, M("int F(string a) => int.Parse(a);")));
            Assert.Contains("WZ0007", Ids(Core, M("bool F(string a) => a.StartsWith(\"x\");")));
            Assert.Contains("WZ0007", Ids(Core, M("bool F(string a, string b) => string.Equals(a, b, System.StringComparison.CurrentCulture);")));
            Assert.Contains("WZ0007", Ids(Core, M("bool F(string a, string b) => string.Equals(a, b, System.StringComparison.InvariantCultureIgnoreCase);")));
            Assert.Contains("WZ0007", Ids(Sim, M("string F(int a) => $\"n={a}\";")));
            Assert.Contains("WZ0007", Ids(Sim, M("string F(long a) => \"n=\" + a;")));
            Assert.Contains("WZ0007", Ids(Content, M("string F(int a) => string.Format(\"{0}\", a);")));
            Assert.Contains("WZ0007", Ids(Core, M("System.StringComparer F() => System.StringComparer.InvariantCulture;")));
            Assert.Contains("WZ0007", Ids(Core, M("object F() => System.Globalization.CultureInfo.CurrentCulture;")));
            Assert.Contains("WZ0007", Ids(Core, M("void F(System.Text.StringBuilder b, int a) => b.Append(a);")));
            Assert.DoesNotContain("WZ0007", Ids(Core, M("int F(string a, string b) => string.CompareOrdinal(a, b);")));
            Assert.DoesNotContain("WZ0007", Ids(Core, M("string F(int a) => a.ToString(System.Globalization.CultureInfo.InvariantCulture);")));
            Assert.DoesNotContain("WZ0007", Ids(Core, M("bool F(string a) => a.StartsWith(\"x\", System.StringComparison.Ordinal);")));
            Assert.DoesNotContain("WZ0007", Ids(Core, M("string F(string a) => a.ToUpperInvariant() + \"x\" + 'c' + $\"{a}\";")));
            Assert.DoesNotContain("WZ0007", Ids(Client, M("string F(string a) => a.ToUpper();")));
        }

        // 기본 문자열 비교자(Comparer<string>.Default = 현재 문화권)로 가는 길. 비교자를 명시하면 허용한다.
        [Fact]
        public void WZ0007_DefaultStringComparer()
        {
            const string u = "using System; using System.Collections; using System.Collections.Generic; using System.Linq;";
            // 정렬 컬렉션(비교자 없음, null 비교자, 다른 컬렉션에서 복사, 비제네릭 SortedList)
            Assert.Contains("WZ0007", Ids(Sim, M("int F(IEnumerable<string> ids) { var s = new SortedSet<string>(ids); int n = 0; foreach (var x in s) n += x.Length; return n; }", u)));
            Assert.Contains("WZ0007", Ids(Sim, M("int F() { var d = new SortedDictionary<string, int>(); int n = 0; foreach (var kv in d) n += kv.Value; return n; }", u)));
            Assert.Contains("WZ0007", Ids(Sim, M("object F() => new SortedSet<string> { \"item.a\", \"item.B\" };", u)));
            Assert.Contains("WZ0007", Ids(Core, M("object F() => new SortedList<string, int>();", u)));
            Assert.Contains("WZ0007", Ids(Core, M("object F(IDictionary<string, int> d) => new SortedDictionary<string, int>(d);", u)));
            Assert.Contains("WZ0007", Ids(Content, M("object F() => new SortedSet<string>((IComparer<string>?)null);", u)));
            Assert.Contains("WZ0007", Ids(Content, M("object F() => new SortedList();", u)));
            Assert.Contains("WZ0007", Ids(Sim, M("object F() => new SortedSet<object>();", u)));
            Assert.Contains("WZ0007", Ids(Sim, M("object F() => new SortedSet<(string, int)>();", u)));
            Assert.Contains("WZ0007", Ids(Sim, M("object F<T>() => new SortedSet<T>();", u)));
            Assert.Contains("WZ0007", Ids(Sim, M("object F() => System.Collections.Immutable.ImmutableSortedSet.Create<string>(\"a\");", u)));
            Assert.Contains("WZ0007", Ids(Sim, M("object F(string[] a) => System.Collections.Immutable.ImmutableSortedSet.ToImmutableSortedSet(a);", u)));
            Assert.Contains("WZ0007", Ids(Sim, M("object F() => System.Collections.Immutable.ImmutableSortedSet<string>.Empty;", u)));
            // 기본 비교자 그 자체
            Assert.Contains("WZ0007", Ids(Sim, M("int F(string a, string b) => Comparer<string>.Default.Compare(a, b);", u)));
            Assert.Contains("WZ0007", Ids(Sim, M("int F(string a, string b) => System.Collections.Comparer.Default.Compare(a, b);", u)));
            Assert.Contains("WZ0007", Ids(Sim, M("int F(string a, string b) => System.Collections.Comparer.DefaultInvariant.Compare(a, b);", u)));
            Assert.Contains("WZ0007", Ids(Core, M("int F<T>(T a, T b) => Comparer<T>.Default.Compare(a, b);", u)));
            Assert.Contains("WZ0007", Ids(Core, M("object F() => StringComparer.Create(System.Globalization.CultureInfo.InvariantCulture, false);", u)));
            Assert.Contains("WZ0007", Ids(Core, M("object F(StringComparison c) => StringComparer.FromComparison(c);", u)));
            // 기본 비교자를 쓰는 메서드
            Assert.Contains("WZ0007", Ids(Sim, M("int F(List<string> l, string k) => l.BinarySearch(k);", u)));
            Assert.Contains("WZ0007", Ids(Sim, M("int F(string[] a, string k) => Array.BinarySearch(a, k);", u)));
            Assert.Contains("WZ0007", Ids(Sim, M("int F(string[] a, string k) => Array.BinarySearch((Array)a, k);", u)));
            Assert.Contains("WZ0007", Ids(Sim, M("int F(List<string> l, string k) => l.BinarySearch(k, null);", u)));
            Assert.Contains("WZ0007", Ids(Sim, M("string F(string[] a) => a.Min()!;", u)));
            Assert.Contains("WZ0007", Ids(Sim, M("string F(int[] a) => a.Max(x => x.ToString(System.Globalization.CultureInfo.InvariantCulture))!;", u)));
            Assert.Contains("WZ0007", Ids(Sim, M("int F(string a, string b) => ((IComparable<string>)a).CompareTo(b);", u)));
            Assert.Contains("WZ0007", Ids(Sim, M("int F(string a, string b) => ((IComparable)a).CompareTo(b);", u)));
            Assert.Contains("WZ0007", Ids(Sim, M("int F((string, int) a, (string, int) b) => a.CompareTo(b);", u)));
            Assert.Contains("WZ0007", Ids(Sim, M("Comparison<string> F() => string.Compare;", u)));
            // T : IComparable<T> 로 제약한 도우미에 문자열을 넣는다(추론한 인자, 명시한 인자, 제네릭 타입)
            const string helper = "} public static class Order { public static int Cmp<T>(T a, T b) where T : IComparable<T> => a.CompareTo(b); } " +
                                  "public sealed class Box<T> where T : IComparable<T> { } public sealed class Q {";
            Assert.Contains("WZ0007", Ids(Sim, M("int F(string a, string b) => Order.Cmp(a, b); " + helper, u)));
            Assert.Contains("WZ0007", Ids(Sim, M("int F(string a, string b) => Order.Cmp<string>(a, b); " + helper, u)));
            Assert.Contains("WZ0007", Ids(Sim, M("object F() => new Box<string>(); " + helper, u)));

            // 음성: 비교자를 명시한 꼴, 문자열이 아닌 키, 문자열이 들어갈 수 없는 타입
            Assert.DoesNotContain("WZ0007", Ids(Sim, M("int F(IEnumerable<string> ids) { var s = new SortedSet<string>(ids, StringComparer.Ordinal); int n = 0; foreach (var x in s) n += x.Length; return n; }", u)));
            Assert.DoesNotContain("WZ0007", Ids(Sim, M("object F() => new SortedSet<string>(StringComparer.Ordinal) { \"item.a\", \"item.B\" };", u)));
            Assert.DoesNotContain("WZ0007", Ids(Sim, M("object F() => new SortedDictionary<string, int>(StringComparer.Ordinal);", u)));
            Assert.DoesNotContain("WZ0007", Ids(Sim, M("object F() => new SortedList<string, int>(StringComparer.Ordinal);", u)));
            Assert.DoesNotContain("WZ0007", Ids(Sim, M("object F() => System.Collections.Immutable.ImmutableSortedSet.Create<string>(StringComparer.Ordinal, \"a\");", u)));
            Assert.DoesNotContain("WZ0007", Ids(Sim, M("int F() { var s = new SortedSet<int>(); var d = new SortedDictionary<long, int>(); return s.Count + d.Count; }", u)));
            Assert.DoesNotContain("WZ0007", Ids(Sim, M("object F<T>() where T : struct => new SortedSet<T>();", u)));
            Assert.DoesNotContain("WZ0007", Ids(Sim, M("int F(int a, int b) => Comparer<int>.Default.Compare(a, b) + Comparer<(int, long)>.Default.Compare((a, 1), (b, 2));", u)));
            Assert.DoesNotContain("WZ0007", Ids(Sim, M("int F(string a, string b) => StringComparer.Ordinal.Compare(a, b) + string.CompareOrdinal(a, b);", u)));
            Assert.DoesNotContain("WZ0007", Ids(Sim, M("int F(List<string> l, string k) => l.BinarySearch(k, StringComparer.Ordinal);", u)));
            Assert.DoesNotContain("WZ0007", Ids(Sim, M("int F(int[] a) => Array.BinarySearch(a, 3) + new List<int>(a).BinarySearch(4);", u)));
            Assert.DoesNotContain("WZ0007", Ids(Sim, M("int F(string[] a) => a.Min(s => s.Length) + new[] { 1, 2 }.Max();", u)));
            Assert.DoesNotContain("WZ0007", Ids(Sim, M("int F(int a, int b) => ((IComparable<int>)a).CompareTo(b) + (a, 1L).CompareTo((b, 2L));", u)));
            Assert.DoesNotContain("WZ0007", Ids(Sim, M("int F(long a, long b) => Order.Cmp(a, b); " + helper, u)));
            Assert.DoesNotContain("WZ0007", Ids(Sim, M("bool F(string a, string b) => EqualityComparer<string>.Default.Equals(a, b) && StringComparer.FromComparison(StringComparison.Ordinal).Equals(a, b);", u)));
            // 코어 밖은 WZ0007 대상이 아니다
            Assert.DoesNotContain("WZ0007", Ids(Client, M("object F() => new SortedSet<string>();", u)));
            Assert.DoesNotContain("WZ0007", Ids(Rules, M("int F(string a, string b) => Comparer<string>.Default.Compare(a, b);", u)));
        }

        [Fact]
        public void WZ0008_Threading()
        {
            Assert.Contains("WZ0008", Ids(Sim, M("void F() => System.Threading.Tasks.Task.Run(() => { });")));
            Assert.Contains("WZ0008", Ids(Sim, M("void F() => new System.Threading.Thread(() => { }).Start();")));
            Assert.Contains("WZ0008", Ids(Sim, M("void F() => System.Threading.Tasks.Parallel.For(0, 2, i => { });")));
            Assert.Contains("WZ0008", Ids(Core, M("int _a; int F() => System.Threading.Interlocked.Increment(ref _a);")));
            Assert.Contains("WZ0008", Ids(Core, M("readonly object _g = new object(); int F() { lock (_g) { return 1; } }")));
            Assert.Contains("WZ0008", Ids(Content, M("async void F() { }")));
            Assert.Contains("WZ0008", Ids(Core, M("volatile int _v;")));
            Assert.Contains("WZ0008", Ids(Core, M("int F(int[] a) => System.Linq.ParallelEnumerable.Sum(System.Linq.ParallelEnumerable.AsParallel(a));")));
            Assert.DoesNotContain("WZ0008", Ids(Core, M("int F(int[] a) { int s = 0; for (int i = 0; i < a.Length; i++) s += a[i]; return s; }")));
            Assert.DoesNotContain("WZ0008", Ids(Client, M("System.Threading.Tasks.Task F() => System.Threading.Tasks.Task.CompletedTask;")));
        }

        [Fact]
        public void WZ0009_GetHashCode()
        {
            Assert.Contains("WZ0009", Ids(Core, M("int F(string s) => s.GetHashCode();")));
            Assert.Contains("WZ0009", Ids(Sim, M("int F(int a, int b) => System.HashCode.Combine(a, b);")));
            Assert.Contains("WZ0009", Ids(Sim, M("int F(object o) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(o);")));
            Assert.Contains("WZ0009", Ids(Content, M("int F(int a) => System.Collections.Generic.EqualityComparer<int>.Default.GetHashCode(a);")));
            Assert.DoesNotContain("WZ0009", Ids(Core, M("int _v; public override int GetHashCode() => _v.GetHashCode(); public override bool Equals(object? o) => false;")));
            Assert.DoesNotContain("WZ0009", Ids(Core,
                "namespace Wizard.Probe { public sealed class C : System.Collections.Generic.IEqualityComparer<long> { " +
                "public bool Equals(long a, long b) => a == b; public int GetHashCode(long a) => System.HashCode.Combine(a); } }"));
            Assert.DoesNotContain("WZ0009", Ids(Client, M("int F(string s) => s.GetHashCode();")));
        }

        [Fact]
        public void WZ0010_MutableStatic()
        {
            Assert.Contains("WZ0010", Ids(Core, M("static int s_count;")));
            Assert.Contains("WZ0010", Ids(Core, M("static readonly int[] Table = { 1 };")));
            Assert.Contains("WZ0010", Ids(Sim, M("static readonly System.Text.StringBuilder Sb = new System.Text.StringBuilder();")));
            Assert.Contains("WZ0010", Ids(Content, M("public static int Prop { get; set; }")));
            Assert.Contains("WZ0010", Ids(Core, M("public static int[] Table { get; } = { 1 };")));
            Assert.Contains("WZ0010", Ids(Core, M("public static event System.Action? Changed;")));
            Assert.DoesNotContain("WZ0010", Ids(Core, M(
                "const int A = 1; static readonly int B = 2; static readonly string C = \"c\"; public static int D => 3; " +
                "static readonly System.TimeSpan E = default; public static long G { get; } = 4;")));
            Assert.DoesNotContain("WZ0010", Ids(Client, M("static int s_count;")));
        }

        [Fact]
        public void WZ0010_ReadonlyStructWrappingMutableStateIsMutableStatic()
        {
            static string S(string body) =>
                "namespace Wizard.Probe { public enum K { A } " +
                "public readonly struct Pool { public readonly int[] Items; public Pool(int n) { Items = new int[n]; } } " +
                "public readonly struct Boxed { public readonly System.Collections.Generic.List<int> Items; public Boxed(int n) { Items = new System.Collections.Generic.List<int>(n); } } " +
                "public readonly struct Outer { public readonly Pool Inner; public Outer(int n) { Inner = new Pool(n); } } " +
                "public readonly struct AutoArr { public int[] Items { get; } public AutoArr(int n) { Items = new int[n]; } } " +
                "public readonly struct Obj { public readonly object O; public Obj(object o) { O = o; } } " +
                "public readonly struct Wrap<T> { public readonly T Value; public Wrap(T v) { Value = v; } } " +
                "public readonly struct Raw { public readonly long A; public readonly K Kind; public readonly string Name; public Raw(long a) { A = a; Kind = K.A; Name = \"\"; } } " +
                "public readonly struct Pair { public readonly Raw X; public readonly Raw Y; public Pair(long a) { X = new Raw(a); Y = new Raw(a); } } " +
                "public sealed class P { " + body + " } }";
            // 배열·가변 객체를 감싼 readonly struct(직접, 자동 속성, 한 겹 더 감싼 것, 제네릭 인자로 들어온 것), BCL 의 감싼 구조체
            Assert.Contains("WZ0010", Ids(Sim, S("static readonly System.Memory<int> Mem = new int[8];")));
            Assert.Contains("WZ0010", Ids(Sim, S("static readonly Pool Scratch = new Pool(8);")));
            Assert.Contains("WZ0010", Ids(Core, S("static readonly System.ArraySegment<int> Seg = new System.ArraySegment<int>(new int[8]);")));
            Assert.Contains("WZ0010", Ids(Content, S("static readonly System.Collections.Generic.KeyValuePair<int, int[]> Kv = default;")));
            Assert.Contains("WZ0010", Ids(Sim, S("static readonly Boxed List = new Boxed(4);")));
            Assert.Contains("WZ0010", Ids(Sim, S("static readonly Outer Nested = new Outer(4);")));
            Assert.Contains("WZ0010", Ids(Sim, S("static readonly AutoArr Auto = new AutoArr(4);")));
            Assert.Contains("WZ0010", Ids(Sim, S("static readonly Obj Any = new Obj(new int[1]);")));
            Assert.Contains("WZ0010", Ids(Sim, S("static readonly Wrap<int[]> Wrapped = new Wrap<int[]>(new int[1]);")));
            Assert.Contains("WZ0010", Ids(Sim, S("public static Pool Prop { get; } = new Pool(8);")));
            Assert.Contains("WZ0010", Ids(Sim, S("static readonly System.ReadOnlyMemory<int> Ro = new int[8];")));
            // 원시형·열거형·문자열과 그것만 담은 readonly struct(겹쳐도, 제네릭 인자로 채워도)는 불변이다
            Assert.DoesNotContain("WZ0010", Ids(Sim, S("static readonly Raw R = new Raw(1); static readonly Pair Q = new Pair(2); " +
                "static readonly Wrap<long> W = new Wrap<long>(3); static readonly Wrap<Raw> WR = default; public static Raw Prop { get; } = new Raw(4); " +
                "static readonly System.Collections.Generic.KeyValuePair<int, int> Kv = default; static readonly System.TimeSpan T = default;")));
            Assert.DoesNotContain("WZ0010", Ids(Client, S("static readonly Pool Scratch = new Pool(8);")));
        }

        [Fact]
        public void WZ0010_StaticArraysOnlyInGeneratedTableAllowList()
        {
            const string table = "namespace Wizard.Core { internal static class SinTable { internal static readonly long[] Raw = { 0L, 1L }; } }";
            Assert.DoesNotContain("WZ0010", Ids(Core, table, "/repo/src/Wizard.Core/Generated/SinTable.g.cs"));
            Assert.DoesNotContain("WZ0010", Ids(Core, table, "/repo/src/Wizard.Core/Generated/PowTables.g.cs"));
            // 허용 목록에 없는 생성 파일, 생성 폴더 밖, 다른 어셈블리는 막는다
            Assert.Contains("WZ0010", Ids(Core, table, "/repo/src/Wizard.Core/Generated/Other.g.cs"));
            Assert.Contains("WZ0010", Ids(Core, table, "/repo/src/Wizard.Core/Tables/SinTable.g.cs"));
            Assert.Contains("WZ0010", Ids(Sim, table, "/repo/src/Wizard.Core/Generated/SinTable.g.cs"));
            // 허용 파일 안이어도 배열이 아닌 가변 정적은 막는다
            Assert.Contains("WZ0010", Ids(Core, "namespace Wizard.Core { internal static class SinTable { internal static int Hits; } }",
                "/repo/src/Wizard.Core/Generated/SinTable.g.cs"));
        }

        [Fact]
        public void WZ0011_ThreadStatic()
        {
            Assert.Contains("WZ0011", Ids(Core, M("[System.ThreadStatic] static int t;")));
            Assert.Contains("WZ0011", Ids(Sim, M("readonly System.Threading.ThreadLocal<int> _t = new System.Threading.ThreadLocal<int>();")));
            Assert.DoesNotContain("WZ0011", Ids(Core, M("int _t;")));
            Assert.DoesNotContain("WZ0011", Ids(Client, M("[System.ThreadStatic] static int t;")));
        }

        [Fact]
        public void WZ0012_Unsafe()
        {
            Assert.Contains("WZ0012", Ids(Core, M("unsafe int F(int* p) => *p;")));
            Assert.Contains("WZ0012", Ids(Sim, M("int F(int[] a) { unsafe { fixed (int* p = a) { return *p; } } }")));
            Assert.Contains("WZ0012", Ids(Core, new[] { (M("int x;"), "/r/a.cs") }, new Setup { AllowUnsafe = true }));   // AllowUnsafeBlocks
            Assert.DoesNotContain("WZ0012", Ids(Core, M("int F() { System.Span<int> s = stackalloc int[2]; return s[0]; }")));
            Assert.DoesNotContain("WZ0012", Ids(Client, new[] { (M("unsafe int F(int* p) => *p;"), "/r/a.cs") }, new Setup { AllowUnsafe = true }));
        }

        [Fact]
        public void WZ0013_MemoryReinterpret()
        {
            Assert.Contains("WZ0013", Ids(Core, M("byte[] F(int a) => System.BitConverter.GetBytes(a);")));
            Assert.Contains("WZ0013", Ids(Core, M("void F(byte[] a, byte[] b) => System.Buffer.BlockCopy(a, 0, b, 0, 1);")));
            Assert.Contains("WZ0013", Ids(Core, M("int F(System.Span<byte> s) => System.Runtime.InteropServices.MemoryMarshal.Read<int>(s);")));
            Assert.Contains("WZ0013", Ids(Sim, M("int F() => System.Runtime.InteropServices.Marshal.SizeOf<long>();")));
            Assert.Contains("WZ0013", Ids(Sim, M("int F(long a) => System.Runtime.CompilerServices.Unsafe.As<long, int>(ref a);")));
            Assert.Contains("WZ0013", Ids(Content,
                "namespace Wizard.Probe { [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Explicit)] " +
                "public struct U { [System.Runtime.InteropServices.FieldOffset(0)] public int A; [System.Runtime.InteropServices.FieldOffset(0)] public uint B; } }"));
            Assert.DoesNotContain("WZ0013", Ids(Core, M("byte F(int a) => (byte)(a >> 8);")));
            Assert.DoesNotContain("WZ0013", Ids("Wizard.Protocol", M("byte[] F(int a) => System.BitConverter.GetBytes(a);")));
        }

        [Fact]
        public void WZ0014_RawDivision()
        {
            Assert.Contains("WZ0014", Ids(Core, M("int F(int a, int b) => a / b;")));
            Assert.Contains("WZ0014", Ids(Core, M("long F(long a, long b) => a % b;")));
            Assert.Contains("WZ0014", Ids(Sim, M("int F(int a, int b) { a /= b; return a; }")));
            Assert.Contains("WZ0014", Ids(Content, M("long F(long a) => a / -1;")));
            Assert.Contains("WZ0014", Ids(Core, M("int F(int a, int b) => System.Math.DivRem(a, b, out int r) + r;")));
            Assert.DoesNotContain("WZ0014", Ids(Core, M("const int K = 7; int F(int a) => a / 2 + a % 360 + a / K;")));
            Assert.DoesNotContain("WZ0014", Ids(Core, "namespace Wizard.Core { public static class FixMath { public static int D(int a, int b) => a / b; } }"));
            Assert.DoesNotContain("WZ0014", Ids(Core,
                "namespace Wizard.Core { public static class Wide { public static long D(long a, long b) { System.Func<long> f = () => a % b; return f(); } } }"));
            Assert.DoesNotContain("WZ0014", Ids(Core, "namespace Wizard.Core { public readonly struct Q { public readonly long R; public static Q operator /(Q a, Q b) => a; public static Q F(Q a, Q b) => a / b; } }"));
            Assert.DoesNotContain("WZ0014", Ids(Client, M("int F(int a, int b) => a / b;")));
        }

        [Fact]
        public void WZ0015_Checked()
        {
            Assert.Contains("WZ0015", Ids(Core, M("int F(int a, int b) => checked(a + b);")));
            Assert.Contains("WZ0015", Ids(Sim, M("int F(int a, int b) { checked { return a + b; } }")));
            Assert.Contains("WZ0015", Ids(Core, new[] { (M("int x;"), "/r/a.cs") }, new Setup { CheckOverflow = true }));   // CheckForOverflowUnderflow
            Assert.DoesNotContain("WZ0015", Ids(Core, M("int F(int a, int b) => unchecked(a + b);")));
            Assert.DoesNotContain("WZ0015", Ids(Client, M("int F(int a, int b) => checked(a + b);")));
        }

        [Fact]
        public void WZ0017_OutsideCoreHash()
        {
            const string rules = "namespace Wizard.Content.Rules { public sealed class NetRuleSet { } }";
            const string errors = "namespace Wizard.Core { public enum ErrorCode { None } public readonly struct Unit { } public readonly struct Result<T> { } }";
            (string, string) use = (M("Wizard.Content.Rules.NetRuleSet? N; Wizard.Core.ErrorCode E;"), "/repo/src/Wizard.Sim/Use.cs");
            Assert.Contains("WZ0017", Ids(Sim, new[] { use, (rules, "/repo/src/X/Rules/N.cs"), (errors, "/repo/src/X/Errors/E.cs") }));
            (string, string) unit = (M("Wizard.Core.Result<Wizard.Core.Unit> R;"), "/repo/src/Wizard.Sim/Use.cs");
            Assert.Contains("WZ0017", Ids(Sim, new[] { unit, (errors, "/repo/src/X/Errors/E.cs") }));
            (string, string) contentUse = (M("Wizard.Content.Rules.NetRuleSet? N;"), "/repo/src/Wizard.Content/Pack/ContentDb.cs");
            (string, string) contentRulesUse = (M("Wizard.Content.Rules.NetRuleSet? N;"), "/repo/src/Wizard.Content/Rules/ContentDb.Rules.cs");
            Assert.Contains("WZ0017", Ids(Content, new[] { contentUse, (rules, "/repo/src/Wizard.Content/Rules/Defs/N.cs") }));
            // 해시 안의 Content 가 ErrorCode·Error·Result 를 참조해도 막는다
            const string error = "namespace Wizard.Core { public readonly struct Error { } }";
            (string, string) contentErr = (M("Wizard.Core.ErrorCode E; Wizard.Core.Error X;"), "/repo/src/Wizard.Content/Pack/ContentDb.cs");
            Assert.Contains("WZ0017", Ids(Content, new[] { contentErr, (errors, "/repo/src/X/Errors/E.cs"), (error, "/repo/src/X/Errors/Error.cs") }));
            (string, string) contentResult = (M("Wizard.Core.Result<int> R;"), "/repo/src/Wizard.Content/Ids/X.cs");
            Assert.Contains("WZ0017", Ids(Content, new[] { contentResult, (errors, "/repo/src/X/Errors/E.cs") }));
            (string, string) contentRulesErr = (M("Wizard.Core.ErrorCode E;"), "/repo/src/Wizard.Content/Rules/Defs/Uses.cs");
            Assert.DoesNotContain("WZ0017", Ids(Content, new[] { contentRulesErr, (errors, "/repo/src/X/Errors/E.cs") }));
            // 저장소 위쪽 경로에 Rules 가 있어도 해시 안의 파일이다
            (string, string) underRulesRepo = (M("Wizard.Content.Rules.NetRuleSet? N;"), "/home/Rules/repo/src/Wizard.Content/Pack/X.cs");
            Assert.Contains("WZ0017", Ids(Content, new[] { underRulesRepo, (rules, "/home/Rules/repo/src/Wizard.Content/Rules/Defs/N.cs") }));
            Assert.DoesNotContain("WZ0017", Ids(Content, new[] { contentRulesUse, (rules, "/repo/src/Wizard.Content/Rules/Defs/N.cs") }));
            Assert.DoesNotContain("WZ0017", Ids(Rules, new[] { use, (rules, "/repo/src/X/Rules/N.cs"), (errors, "/repo/src/X/Errors/E.cs") }));
            Assert.DoesNotContain("WZ0017", Ids(Core, new[] { (M("Wizard.Core.ErrorCode E;"), "/repo/src/Wizard.Core/Pcg32.cs"), (errors, "/repo/src/Wizard.Core/Errors/E.cs") }));
        }

        // 실제 빌드처럼 Core 와 Content 를 컴파일한 어셈블리로 참조한다. 해시 안의 타입(ContentDb)에 붙은 규칙 쪽 접근자(CONTRACTS 2.3)와
        // Result·ErrorCode 를 돌려주거나 받는 멤버는 이름에 해시 밖 타입이 나오지 않아도 막는다.
        private static readonly Lazy<MetadataReference> CoreRef = new Lazy<MetadataReference>(() => CompiledAssembly(Core,
            "namespace Wizard.Core { public enum ErrorCode { None = 0, Busy = 1 } public readonly struct Unit { } " +
            "public readonly struct Error { public readonly ErrorCode Code; } public readonly struct Result<T> { public readonly T Value; public bool Ok => true; } }"));

        private static readonly Lazy<MetadataReference> ContentRef = new Lazy<MetadataReference>(() => CompiledAssembly(Content,
            "using System.Collections.Generic;\n" +
            "namespace Wizard.Content.Rules { public sealed class NetRuleSet { public int Delay; } public sealed class TitleDef { } public sealed class ItemDef { } }\n" +
            "namespace Wizard.Content { public readonly struct ItemCode { public readonly int V; } public sealed class BlockDef { public int Hp; } " +
            "public sealed partial class ContentDb { public IReadOnlyList<BlockDef> Blocks => null!; public int Version => 1; public BlockDef Block(int id) => null!; } " +
            "public sealed partial class ContentDb { public Wizard.Content.Rules.NetRuleSet Net => null!; " +
            "public IReadOnlyList<Wizard.Content.Rules.TitleDef> Titles => null!; public Wizard.Content.Rules.ItemDef Item(ItemCode code) => null!; " +
            "public Wizard.Content.Rules.TitleDef[] TitleArray => null!; [System.Runtime.CompilerServices.IndexerName(\"At\")] public Wizard.Content.Rules.ItemDef this[ItemCode c] => null!; } " +
            "public static class Api { public static Wizard.Core.Result<int> Try() => default; public static Wizard.Core.ErrorCode Check() => default; " +
            "public static int Run(System.Func<Wizard.Core.ErrorCode, int> f) => 0; public static int Take(Wizard.Core.Error e) => 0; } " +
            "public sealed class Holder { public Holder(Wizard.Core.ErrorCode code) { } public Holder() { } } " +
            "public readonly struct Gate { public readonly int V; public static Wizard.Core.ErrorCode operator !(Gate g) => default; public static int operator -(Gate g) => 0; } }", CoreRef.Value));

        private static string[] SimUses(string body) =>
            Ids(Sim, new[] { (M(body, "using Wizard.Content;"), "/repo/src/Wizard.Sim/Use.cs") }, new[] { CoreRef.Value, ContentRef.Value });

        [Fact]
        public void WZ0017_MemberSignatureOutsideCoreHash()
        {
            // 규칙 쪽 접근자: 속성, 제네릭 인자 속의 타입, 배열, 메서드 반환, 인덱서
            Assert.Contains("WZ0017", SimUses("int F(ContentDb db) => db.Titles.Count;"));
            Assert.Contains("WZ0017", SimUses("object F(ContentDb db) => db.Net;"));
            Assert.Contains("WZ0017", SimUses("int F(ContentDb db) => db.Net.Delay;"));
            Assert.Contains("WZ0017", SimUses("bool F(ContentDb db) => db.Item(default) != null;"));
            Assert.Contains("WZ0017", SimUses("int F(ContentDb db) => db.TitleArray.Length;"));
            Assert.Contains("WZ0017", SimUses("object F(ContentDb db) => db[default];"));
            Assert.Contains("WZ0017", SimUses("void F(ContentDb db) { var n = db.Net; }"));
            // Result·ErrorCode·Error 를 돌려주거나 받는 멤버(반환, 대리자 매개변수, 매개변수, 생성자)
            Assert.Contains("WZ0017", SimUses("void F() { _ = Api.Try(); }"));
            Assert.Contains("WZ0017", SimUses("bool F() => Api.Check() == default;"));
            Assert.Contains("WZ0017", SimUses("int F() => Api.Run(e => 0);"));
            Assert.Contains("WZ0017", SimUses("int F() => Api.Take(default);"));
            Assert.Contains("WZ0017", SimUses("object F() => new Holder(default);"));
            Assert.Contains("WZ0017", SimUses("object F(Gate g) => !g;"));
            // 해시 안의 접근자와 해시 밖 타입이 나오지 않는 멤버는 막지 않는다
            Assert.DoesNotContain("WZ0017", SimUses("int F(ContentDb db) => db.Blocks.Count + db.Version + db.Block(1).Hp;"));
            Assert.DoesNotContain("WZ0017", SimUses("object F() => new Holder();"));
            Assert.DoesNotContain("WZ0017", SimUses("int F(Gate g) => -g;"));
            // 같은 접근자를 해시 밖 어셈블리(Rules)가 부르는 것은 막지 않는다
            Assert.DoesNotContain("WZ0017", Ids(Rules, new[] { (M("int F(ContentDb db) => db.Titles.Count + (Api.Check() == default ? 1 : 0);", "using Wizard.Content;"), "/repo/src/Wizard.Rules/Use.cs") },
                new[] { CoreRef.Value, ContentRef.Value }));
        }

        [Fact]
        public void WZ0017_MemberSignatureInsideContentHashSide()
        {
            // 같은 컴파일 안: 해시 안의 Content 파일이 규칙 쪽 부분 클래스의 접근자를 부르면 막고, 규칙 쪽 파일 안에서는 허용한다
            const string rules = "namespace Wizard.Content.Rules { public sealed class NetRuleSet { } }";
            const string part = "namespace Wizard.Content { public sealed partial class ContentDb { public Wizard.Content.Rules.NetRuleSet Net => null!; } }";
            const string pack = "namespace Wizard.Content { public sealed partial class ContentDb { public int Version => 1; } }";
            (string, string)[] tree =
            {
                (rules, "/repo/src/Wizard.Content/Rules/Defs/NetRuleSet.cs"),
                (part, "/repo/src/Wizard.Content/Rules/ContentDb.Rules.cs"),
                (pack, "/repo/src/Wizard.Content/Pack/ContentDb.cs"),
            };
            const string hashUse = "namespace Wizard.Content { public static class Use { public static bool F(ContentDb db) => db.Net != null; } }";
            const string rulesUse = "namespace Wizard.Content.Rules { public static class Use { public static bool F(Wizard.Content.ContentDb db) => db.Net != null; } }";
            Assert.Contains("WZ0017", Ids(Content, tree.Concat(new[] { (hashUse, "/repo/src/Wizard.Content/Pack/Use.cs") }).ToArray()));
            Assert.DoesNotContain("WZ0017", Ids(Content, tree.Concat(new[] { (rulesUse, "/repo/src/Wizard.Content/Rules/Use.cs") }).ToArray()));
        }

        [Fact]
        public void WZ0019_Sockets()
        {
            Assert.Contains("WZ0019", Ids(Netcode, M("System.Net.Sockets.Socket? S;")));
            Assert.Contains("WZ0019", Ids(Netcode, M("System.Net.Sockets.UdpClient? U;")));
            Assert.Contains("WZ0019", Ids(Netcode, M("System.Net.WebSockets.WebSocket? W;")));
            Assert.DoesNotContain("WZ0019", Ids(Client, M("System.Net.Sockets.Socket? S;")));
            Assert.DoesNotContain("WZ0019", Ids(Netcode, M("byte[] Packet = new byte[0];")));
        }

        [Fact]
        public void WZ0020_PaidValues()
        {
            const string ids = "namespace Wizard.Content { public enum CurrencyCode : byte { Gold = 0, SeasonCoin = 1, Starlight = 2 } public enum CurrencyBucket : byte { Earned = 0, Paid = 1, Free = 2 } public enum ItemOrigin : byte { Earned = 0, Paid = 6 } public sealed class Entitlement { public int Source { get; set; } } }";
            (string, string) defs = (ids, "/repo/src/Wizard.Content/Ids/C.cs");
            Assert.Contains("WZ0020", Ids(Sim, new[] { (M("Wizard.Content.CurrencyCode F() => Wizard.Content.CurrencyCode.Starlight;"), "/r/a.cs"), defs }));
            Assert.Contains("WZ0020", Ids(Sim, new[] { (M("object F() => Wizard.Content.ItemOrigin.Paid;"), "/r/a.cs"), defs }));
            Assert.Contains("WZ0020", Ids(Sim, new[] { (M("int F(Wizard.Content.Entitlement e) => e.Source;"), "/r/a.cs"), defs }));
            Assert.Contains("WZ0020", Ids(Rules, new[] { ("namespace Wizard.Rules.Enhance { class E { object F() => Wizard.Content.CurrencyBucket.Paid; } }", "/r/a.cs"), defs }));
            Assert.Contains("WZ0020", Ids(Rules, new[] { ("namespace Wizard.Rules.League { class E { object F() => Wizard.Content.CurrencyCode.Starlight; } }", "/r/a.cs"), defs }));
            Assert.Contains("WZ0020", Ids(Rules, new[] { ("namespace Wizard.Rules.Match.Queue { class E { object F() => Wizard.Content.CurrencyCode.Starlight; } }", "/r/a.cs"), defs }));
            Assert.DoesNotContain("WZ0020", Ids(Rules, new[] { ("namespace Wizard.Rules.Economy { class E { object F() => Wizard.Content.CurrencyCode.Starlight; } }", "/r/a.cs"), defs }));
            Assert.DoesNotContain("WZ0020", Ids(Rules, new[] { ("namespace Wizard.Rules.Enhancements { class E { object F() => Wizard.Content.CurrencyCode.Starlight; } }", "/r/a.cs"), defs }));
            Assert.DoesNotContain("WZ0020", Ids(Sim, new[] { (M("object F() => Wizard.Content.CurrencyCode.Gold;"), "/r/a.cs"), defs }));
            Assert.DoesNotContain("WZ0020", Ids(Client, new[] { (M("object F() => Wizard.Content.CurrencyCode.Starlight;"), "/r/a.cs"), defs }));
        }

        [Fact]
        public void WZ0021_ForbiddenSyntaxInSharedPackages()
        {
            Assert.Contains("WZ0021", Ids(Client, "namespace Wizard.Client { public record R(int A); }"));
            Assert.Contains("WZ0021", Ids("Wizard.Protocol", "namespace Wizard.Protocol { public class C { public int A { get; init; } } }"));
            Assert.Contains("WZ0021", Ids(Core, new[] { ("namespace Wizard.Core { public record struct R(int A); }", "/r/a.cs") }, new Setup { Language = LanguageVersion.CSharp10 }));
            Assert.Contains("WZ0021", Ids(Sim, new[] { ("namespace Wizard.Sim;\npublic sealed class C { }", "/r/a.cs") }, new Setup { Language = LanguageVersion.CSharp10 }));
            Assert.Contains("WZ0021", Ids("Wizard.Text", new[] { ("global using System;\nnamespace Wizard.Text { }", "/r/a.cs") }, new Setup { Language = LanguageVersion.CSharp10 }));
            Assert.Contains("WZ0021", Ids("Wizard.ViewLogic", new[] { (M("bool F(int[] a) => a is [1, 2];"), "/r/a.cs") }, new Setup { Language = LanguageVersion.Preview }));
            Assert.Contains("WZ0021", Ids("Wizard.ViewLogic", new[] { (M("string S = \"\"\"raw\"\"\";"), "/r/a.cs") }, new Setup { Language = LanguageVersion.Preview }));
            Assert.Contains("WZ0021", Ids(Client, new[] { (M("public required int A { get; set; }"), "/r/a.cs") }, new Setup { Language = LanguageVersion.Preview }));
            // C# 9 를 넘는 언어 판 자체(문법을 하나도 쓰지 않아도)
            Assert.Contains("WZ0021", Ids(Netcode, new[] { (M("int x;"), "/r/a.cs") }, new Setup { Language = LanguageVersion.CSharp10 }));
            Assert.DoesNotContain("WZ0021", Ids("Wizard.Server.App", "namespace Wizard.Server.App { public record R(int A); }"));
            Assert.DoesNotContain("WZ0021", Ids("Wizard.Server.App", new[] { ("namespace Wizard.Server.App;\npublic sealed class C { }", "/r/a.cs") }, new Setup { Language = LanguageVersion.CSharp10 }));
            Assert.DoesNotContain("WZ0021", Ids(Client, "using System;\nnamespace Wizard.Client { public class C { public int A { get; set; } } }"));
        }

        [Fact]
        public void WZ0022_ForbiddenApiInSharedPackages()
        {
            Assert.Contains("WZ0022", Ids("Wizard.Protocol", M("string F(object o) => System.Text.Json.JsonSerializer.Serialize(o);")));
            Assert.Contains("WZ0022", Ids(Client, M("long F(long a, long b) => System.Math.BigMul(a, b, out long lo);")));
            Assert.Contains("WZ0022", Ids(Client, M("System.Int128 X;")));
            Assert.Contains("WZ0022", Ids(Core, M("int F(uint a) => System.Numerics.BitOperations.LeadingZeroCount(a);")));
            Assert.Contains("WZ0022", Ids(Client, M("object? C(byte[] k) => new System.Security.Cryptography.ChaCha20Poly1305(k);")));
            Assert.Contains("WZ0022", Ids(Sim, M("T Add<T>(T a, T b) where T : System.Numerics.INumber<T> => a + b;")));
            Assert.DoesNotContain("WZ0022", Ids(Client, M("long F(int a, int b) => System.Math.BigMul(a, b);")));
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
            MetadataReference engine = EmptyAssembly("UnityEngine.CoreModule");
            var code = new[] { (M("int x;"), "/r/a.cs") };
            Assert.Contains("WZ0023", Ids(Sim, code, new[] { rules }));                          // 아래가 위를 참조
            Assert.Contains("WZ0023", Ids("Wizard.Server.Api", code, new[] { fakes }));          // Api 는 가짜 결제를 참조하지 않는다
            Assert.Contains("WZ0023", Ids("Wizard.Server.Duel", code, new[] { data }));          // 결투 프로세스는 DB 를 모른다
            Assert.Contains("WZ0023", Ids("Wizard.Server.App", code, new[] { client }));         // 서버는 클라이언트를 참조하지 않는다
            Assert.Contains("WZ0023", Ids("Wizard.ViewLogic", code, new[] { engine }));          // 공유 패키지는 엔진을 참조하지 않는다
            Assert.DoesNotContain("WZ0023", Ids(Rules, code, new[] { sim }));
            Assert.DoesNotContain("WZ0023", Ids("Wizard.Server.Api.DevHost", code, new[] { fakes }));
        }

        [Fact]
        public void UnrelatedAssembliesAreNotAnalyzed()
        {
            Assert.Empty(Ids("Wizard.Core.Tests", M("static int s; double F(int a, int b) => a / b + new System.Random().Next();")));
            Assert.Empty(Ids("Wizard.TableGen", M("static int s; double F(int a, int b) => a / b;")));
        }
    }
}
