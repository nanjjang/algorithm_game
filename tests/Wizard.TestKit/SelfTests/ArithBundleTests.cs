using System;
using System.Collections.Generic;
using System.Linq;
using Wizard.Core;
using Wizard.TestKit.Shared.Arith;
using Wizard.TestKit.Shared.Golden;
using Xunit;

namespace Wizard.TestKit.SelfTests
{
    // 산술 경계 벡터 묶음(tests/golden/arith/)의 형식과 실행 틀
    public sealed class ArithBundleTests
    {
        // 표본 묶음의 연산 표. 실제 표는 M1-CORE-01 이 만든다
        internal sealed class CoreOps : IArithOps
        {
            public bool TryEvaluate(string op, IReadOnlyList<ArithValue> a, List<ArithValue> results)
            {
                switch (op)
                {
                    case "FixMath.DivInt":
                        results.Add(ArithValue.FromInt32(FixMath.DivInt(a[0].ToInt32(), a[1].ToInt32())));
                        return true;
                    case "FixMath.ModInt":
                        results.Add(ArithValue.FromInt32(FixMath.ModInt(a[0].ToInt32(), a[1].ToInt32())));
                        return true;
                    case "FixMath.DivLong":
                        results.Add(ArithValue.FromInt64(FixMath.DivLong(a[0].ToInt64(), a[1].ToInt64())));
                        return true;
                    default:
                        return false;
                }
            }
        }

        // 런타임 예외를 흉내 내는 표(0 제수를 코드가 검사하지 않는 구현)
        private sealed class NaiveOps : IArithOps
        {
            public bool TryEvaluate(string op, IReadOnlyList<ArithValue> a, List<ArithValue> results)
            {
                if (op != "FixMath.DivInt")
                {
                    return false;
                }
                int d = a[1].ToInt32();
                if (d == 0)
                {
                    throw new DivideByZeroException();
                }
                results.Add(ArithValue.FromInt32(unchecked(a[0].ToInt32() / d)));
                return true;
            }
        }

        private static ArithBundle Sample() => GoldenCatalog.Load(GoldenFormatTests.SampleRoot).ArithBundles.Single();

        [Fact]
        public void SampleBundle_Loads()
        {
            ArithBundle b = Sample();
            Assert.Equal("sample", b.Name);
            Assert.Equal(new[] { "divint-min-neg1", "modint-min-neg1", "divint-by-zero", "divlong-min-neg1" }, b.Cases.Select(c => c.Id).ToArray());
            Assert.Equal(ArithFaults.SimInvariant, b.Cases[2].Fault);
            Assert.Null(b.Cases[2].Expect);
            Assert.Equal(long.MinValue, b.Cases[3].Args[0].ToInt64());
        }

        // 기대값은 CONTRACTS 1.1: 제수 0 → SimInvariantException, MinValue ÷ −1 → MinValue, MinValue % −1 → 0
        [Fact]
        public void SampleBundle_PassesAgainstTheCoreDivisionHelpers()
        {
            ArithBundle b = Sample();
            List<ArithCaseResult> results = ArithRunner.Run(b, new CoreOps());
            for (int i = 0; i < b.Cases.Count; i++)
            {
                Assert.True(results[i].Satisfies(b.Cases[i]), b.Cases[i].Id + ": " + results[i].Describe());
            }
        }

        [Fact]
        public void RuntimeExceptions_AndUnknownOps_AreClassified_AndFail()
        {
            ArithBundle b = Sample();
            List<ArithCaseResult> results = ArithRunner.Run(b, new NaiveOps());
            Assert.Equal("Exception:System.DivideByZeroException", results[2].Fault);
            Assert.False(results[2].Satisfies(b.Cases[2]));
            Assert.Equal(ArithFaults.UnknownOp, results[1].Fault);
            Assert.False(results[1].Satisfies(b.Cases[1]));
        }

        [Theory]
        [InlineData("-9223372036854775808", 0x8000000000000000UL)]
        [InlineData("18446744073709551615", 0xffffffffffffffffUL)]
        [InlineData("-1", 0xffffffffffffffffUL)]
        [InlineData("0x7fffffffffffffff", 0x7fffffffffffffffUL)]
        [InlineData("0", 0UL)]
        public void Values_ParseToBitPatterns(string text, ulong bits)
        {
            Assert.True(ArithValue.TryParse(text, out ArithValue v));
            Assert.Equal(bits, v.Bits);
        }

        [Theory]
        [InlineData("18446744073709551616")]
        [InlineData("-9223372036854775809")]
        [InlineData("0x")]
        [InlineData("0x10000000000000000")]
        [InlineData("1.5")]
        [InlineData("-0")]
        [InlineData("007")]
        [InlineData("+1")]
        [InlineData("")]
        public void Values_RejectOtherText(string text)
        {
            Assert.False(ArithValue.TryParse(text, out _));
        }

        [Fact]
        public void ToInt32_RejectsOutOfRange()
        {
            Assert.Throws<FormatException>(() => ArithValue.Parse("0xffffffff").ToInt32());
            Assert.Equal(-1, ArithValue.Parse("-1").ToInt32());
        }

        [Theory]
        [InlineData(@"{ ""format"": 1, ""bundle"": ""other"", ""cases"": [ { ""id"": ""a"", ""op"": ""x"", ""args"": [], ""expect"": [""1""] } ] }", "does not match the file name")]
        [InlineData(@"{ ""format"": 1, ""bundle"": ""b"", ""cases"": [] }", "'cases' is empty")]
        [InlineData(@"{ ""format"": 1, ""bundle"": ""b"", ""cases"": [ { ""id"": ""a"", ""op"": ""x"", ""args"": [1], ""expect"": [""1""] } ] }", "must be a string")]
        [InlineData(@"{ ""format"": 1, ""bundle"": ""b"", ""cases"": [ { ""id"": ""a"", ""op"": ""x"", ""args"": [], ""expect"": [""1""], ""fault"": ""SimInvariant"" } ] }", "exactly one of")]
        [InlineData(@"{ ""format"": 1, ""bundle"": ""b"", ""cases"": [ { ""id"": ""a"", ""op"": ""x"", ""args"": [] } ] }", "exactly one of")]
        [InlineData(@"{ ""format"": 1, ""bundle"": ""b"", ""cases"": [ { ""id"": ""a"", ""op"": ""x"", ""args"": [], ""fault"": ""DivideByZero"" } ] }", "not an expectable class")]
        [InlineData(@"{ ""format"": 1, ""bundle"": ""b"", ""cases"": [ { ""id"": ""a"", ""op"": ""x"", ""args"": [], ""expect"": [""1""] }, { ""id"": ""a"", ""op"": ""x"", ""args"": [], ""expect"": [""1""] } ] }", "duplicate case id")]
        public void Bundle_FormatProblems_AreReported(string json, string expected)
        {
            GoldenFormatException ex = Assert.Throws<GoldenFormatException>(() => ArithBundle.Parse("b", json));
            Assert.Contains(ex.Problems, p => p.Contains(expected));
        }
    }
}
