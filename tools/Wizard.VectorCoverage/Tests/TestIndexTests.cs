using System.Linq;
using Xunit;

namespace Wizard.VectorCoverage.Tests
{
    // 테스트 소스에서 벡터 테스트 메서드를 찾는 규칙(CLAUDE.md 8절: S<명세 번호>_<벡터 id>__<설명>)
    public sealed class TestIndexTests
    {
        [Fact]
        public void FactMethod_WithTheNameRule_IsAVectorTest()
        {
            TestIndex idx = Fx.Index(Fx.FactClass("S01_T_H04__LockAtExactly100"));
            VectorTest t = Assert.Single(idx.Tests);
            Assert.Equal("01", t.Spec);
            Assert.Equal("T-H04", t.Id);
            Assert.Equal("Demo.Tests.DemoTests.S01_T_H04__LockAtExactly100", t.Method);
            Assert.Equal("tests/T0.cs", t.File);
            Assert.Equal(7, t.Line);
            Assert.Empty(idx.Problems);
        }

        [Fact]
        public void TheoryNUnitAndUnityAttributes_AllCount_AndTheDescriptionIsOptional()
        {
            string src = string.Join("\n",
                "namespace X {",
                "  class A {",
                "    [Theory, InlineData(1)] public void S11_TV_11_017__Mail(int a) { }",
                "    [NUnit.Framework.Test] public void S03_B09c() { }",
                "    [UnityTest] public System.Collections.IEnumerator S04_N71__Frames() { yield break; }",
                "    [TestCase(1)] public void S02_TV_A01__Case(int a) { }",
                "    [FactAttribute] public void S01_T_H01__Long() { }",
                "  }",
                "}");
            TestIndex idx = Fx.Index(src);
            Assert.Equal(new[] { "S11:TV-11-017", "S03:B09c", "S04:N71", "S02:TV-A01", "S01:T-H01" }, idx.Tests.Select(t => t.Key).ToArray());
            Assert.Empty(idx.Problems);
        }

        [Fact]
        public void NamesInCommentsStringsAndNonTestMethods_DoNotCount()
        {
            string src = string.Join("\n",
                "namespace X {",
                "  class A {",
                "    // [Fact] public void S01_T_H01__InComment() { }",
                "    /* [Fact] public void S01_T_H02__InBlock() { } */",
                "    private const string Name = \"[Fact] public void S01_T_H03__InString() { }\";",
                "    public void S01_T_H04__NoAttribute() { }",
                "    [Fact] public void Helper_S01_T_H05() { }",
                "  }",
                "}");
            TestIndex idx = Fx.Index(src);
            Assert.Empty(idx.Tests);
            Assert.Empty(idx.Problems);
        }

        [Theory]
        [InlineData("S01__NoId")]
        [InlineData("S01_T_H04___ThreeUnderscores")]
        [InlineData("S01_T_H04__")]
        public void TestThatLooksLikeAVectorButBreaksTheNameRule_IsAProblem(string name)
        {
            TestIndex idx = Fx.Index(Fx.FactClass(name));
            Assert.Empty(idx.Tests);
            Assert.Contains(name, Assert.Single(idx.Problems));
        }

        [Fact]
        public void ScanRepo_SkipsBuildOutputAndUnityTildeFolders()
        {
            Assert.Contains("tests", TestIndex.ScanRoots);
            Assert.Contains("unity/Assets", TestIndex.ScanRoots);
            // 실제 저장소를 훑어도 예외 없이 끝나고, 이 테스트 파일 자신(문자열 속 이름)은 벡터 테스트로 세지 않는다
            TestIndex idx = TestIndex.ScanRepo(Fx.RepoRoot, Fx.RealConfig().TestName);
            Assert.DoesNotContain(idx.Tests, t => t.File.StartsWith("tools/Wizard.VectorCoverage/", System.StringComparison.Ordinal));
            Assert.DoesNotContain(idx.Tests, t => t.File.Contains("/artifacts/", System.StringComparison.Ordinal));
        }
    }
}
