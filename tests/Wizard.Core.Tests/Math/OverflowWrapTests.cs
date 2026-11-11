using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Reflection.Emit;
using Wizard.Core;
using Wizard.TestKit;
using Xunit;

namespace Wizard.Core.Tests.Math
{
    // M1-CORE-01 수락 2: 덧셈·뺄셈·곱셈의 넘침이 Debug 와 Release 에서 같은 값으로 래핑된다(checked 문맥에서도).
    // (1) 호출자가 checked 문맥이어도 결과가 래핑 값이다. (2) 코어 어셈블리의 IL 에 넘침 검사 명령(*.ovf)이 하나도 없다.
    //     이 테스트는 Debug 와 Release 두 구성으로 돌린다(ci fast 의 test 단계, test-release.sh). IL 검사는 그 구성으로 빌드된
    //     Wizard.Core.dll 을 본다. (3) 빌드 설정이 넘침 검사를 켜지 않는다(켜면 (2)가 깨지지만, 설정 쪽에서도 막는다).
    public sealed class OverflowWrapTests
    {
        private static long Wrap(BigInteger v) => BigRef.Low64(v);

        [Fact]
        public void Fix64AddSubMul_WrapInsideCheckedContext()
        {
            long max = long.MaxValue, min = long.MinValue;
            checked
            {
                Assert.Equal(Wrap(new BigInteger(max) + 1), (Fix64.MaxValue + Fix64.FromRaw(1)).Raw);
                Assert.Equal(Wrap(new BigInteger(min) + min), (Fix64.MinValue + Fix64.MinValue).Raw);
                Assert.Equal(Wrap(new BigInteger(min) - 1), (Fix64.MinValue - Fix64.FromRaw(1)).Raw);
                Assert.Equal(Wrap(new BigInteger(0) - min), (Fix64.Zero - Fix64.MinValue).Raw);
                Assert.Equal(Wrap(-new BigInteger(min)), (-Fix64.MinValue).Raw);
                Assert.Equal(Wrap(BigInteger.Abs(new BigInteger(min))), Fix64.Abs(Fix64.MinValue).Raw);
                Assert.Equal(BigRef.FixMul(max, max), (Fix64.MaxValue * Fix64.MaxValue).Raw);
                Assert.Equal(BigRef.FixMul(min, min), (Fix64.MinValue * Fix64.MinValue).Raw);
                Assert.Equal(BigRef.FixMul(min, -(1L << 32)), (Fix64.MinValue * Fix64.FromInt(-1)).Raw);
                Assert.Equal(Wrap(new BigInteger(max) * 2), (Fix64.MaxValue * 2).Raw);
                Assert.Equal(Wrap(new BigInteger(min) * -1), (Fix64.MinValue * -1).Raw);
                Assert.Equal(Wrap(new BigInteger(max) * int.MinValue), (Fix64.MaxValue * int.MinValue).Raw);
                Assert.Equal(Fix64.MinValue, Fix64.MinValue / Fix64.FromInt(-1));
                Assert.Equal(Fix64.MinValue, Fix64.MinValue / -1);
                Assert.Equal(BigRef.FixDiv(max, 1), (Fix64.MaxValue / Fix64.FromRaw(1)).Raw);
                Assert.Equal(BigRef.MulDiv(min, -1000, 1000), Fix64.MulPermille(Fix64.MinValue, -1000).Raw);
                Assert.Equal(-2147483648, Fix64.MinValue.FloorToInt());
                Assert.Equal(int.MinValue, Fix64.MaxValue.CeilToInt());     // Raw + (One − 1) 이 래핑된다
                Assert.Equal(int.MinValue, Fix64.MaxValue.RoundToInt());
            }
        }

        [Fact]
        public void FixVec2AndHelpers_WrapInsideCheckedContext()
        {
            long max = long.MaxValue, min = long.MinValue;
            checked
            {
                var big = new FixVec2(Fix64.MaxValue, Fix64.MinValue);
                FixVec2 sum = big + big;
                Assert.Equal(Wrap(new BigInteger(max) * 2), sum.X.Raw);
                Assert.Equal(Wrap(new BigInteger(min) * 2), sum.Y.Raw);
                FixVec2 diff = big - new FixVec2(Fix64.MinValue, Fix64.MaxValue);
                Assert.Equal(Wrap(new BigInteger(max) - min), diff.X.Raw);
                Assert.Equal(Wrap(new BigInteger(min) - max), diff.Y.Raw);
                Assert.Equal(Wrap(new BigInteger(BigRef.FixMul(max, max)) + BigRef.FixMul(min, min)), big.LengthSq().Raw);
                Assert.Equal(Wrap(new BigInteger(BigRef.FixMul(max, max)) + BigRef.FixMul(min, min)), FixVec2.Dot(big, big).Raw);
                Assert.Equal(Wrap(new BigInteger(BigRef.FixMul(max, min)) - BigRef.FixMul(min, max)), FixVec2.Cross(big, big).Raw);

                Wide.Mul64(min, min, out long hi, out ulong lo);
                Assert.Equal(BigInteger.One << 126, BigRef.From128(hi, lo));
                Wide.UMul64(ulong.MaxValue, ulong.MaxValue, out ulong uhi, out ulong ulo);
                Assert.Equal(ulong.MaxValue - 1UL, uhi);
                Assert.Equal(1UL, ulo);
                Assert.Equal(0L, Wide.Div128By64(1, 0, 1));
                Assert.Equal(min, FixMath.DivLong(min, -1));
                Assert.Equal(int.MinValue, FixMath.DivInt(int.MinValue, -1));
                Assert.Equal(0L, FixMath.ModLong(min, -1));
                Assert.Equal(0, FixMath.ModInt(int.MinValue, -1));
                Assert.Equal(min, FixMath.MulDiv(min, 1, -1));
                Assert.Equal(0L, FixMath.MulDiv(min, min, 1));
            }
        }

        // 코어 어셈블리 전체에 넘침 검사 명령이 없다. checked 문맥이나 넘침 검사 컴파일 옵션이 들어오면 여기서 걸린다.
        [Fact]
        public void CoreAssembly_HasNoOverflowCheckingOpcodes()
        {
            Assembly core = typeof(Fix64).Assembly;
            Dictionary<short, OpCode> table = OpCodeTable();
            var found = new List<string>();
            int methods = 0;
            foreach (Type t in core.GetTypes())
            {
                const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
                IEnumerable<MethodBase> bodies = t.GetMethods(all).Cast<MethodBase>().Concat(t.GetConstructors(all));
                foreach (MethodBase m in bodies)
                {
                    byte[]? il = m.GetMethodBody()?.GetILAsByteArray();
                    if (il == null)
                    {
                        continue;
                    }
                    methods++;
                    foreach (OpCode op in Decode(il, table))
                    {
                        if (op.Name != null && op.Name.Contains(".ovf", StringComparison.Ordinal))
                        {
                            found.Add(t.FullName + "." + m.Name + ": " + op.Name);
                        }
                    }
                }
            }
            Assert.True(methods > 50, "too few method bodies scanned: " + methods);
            Assert.True(found.Count == 0, string.Join("\n", found.Distinct()));
        }

        // 디코더가 실제로 .ovf 를 알아보는지(위 테스트가 헛돌지 않는지)
        [Fact]
        public void OpcodeScanner_DetectsCheckedArithmeticInThisAssembly()
        {
            MethodInfo m = typeof(OverflowWrapTests).GetMethod(nameof(CheckedSample), BindingFlags.NonPublic | BindingFlags.Static)!;
            List<OpCode> ops = Decode(m.GetMethodBody()!.GetILAsByteArray()!, OpCodeTable()).ToList();
            Assert.Contains(ops, o => o.Name == "add.ovf");
            Assert.Contains(ops, o => o.Name == "mul.ovf");
        }

        private static long CheckedSample(long a, long b) => checked(a * b + a);

        [Fact]
        public void BuildSettings_DoNotTurnOnOverflowChecks()
        {
            string[] files =
            {
                "src/Wizard.Core/Wizard.Core.csproj", "src/Wizard.Core/csc.rsp", "Directory.Build.props", "Directory.Build.targets",
            };
            foreach (string rel in files)
            {
                string path = TestRepo.Path(rel);
                Assert.True(File.Exists(path), "missing " + rel);
                string text = File.ReadAllText(path);
                Assert.DoesNotContain("CheckForOverflowUnderflow", text, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("-checked", text, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("/checked", text, StringComparison.OrdinalIgnoreCase);
            }
        }

        private static Dictionary<short, OpCode> OpCodeTable()
        {
            var table = new Dictionary<short, OpCode>();
            foreach (FieldInfo f in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                if (f.GetValue(null) is OpCode op)
                {
                    table[op.Value] = op;
                }
            }
            return table;
        }

        private static IEnumerable<OpCode> Decode(byte[] il, Dictionary<short, OpCode> table)
        {
            int i = 0;
            while (i < il.Length)
            {
                short value = il[i] == 0xFE ? (short)(0xFE00 | il[i + 1]) : il[i];
                i += il[i] == 0xFE ? 2 : 1;
                if (!table.TryGetValue(value, out OpCode op))
                {
                    throw new InvalidOperationException("unknown IL opcode 0x" + value.ToString("x4"));
                }
                yield return op;
                switch (op.OperandType)
                {
                    case OperandType.InlineNone:
                        break;
                    case OperandType.ShortInlineBrTarget:
                    case OperandType.ShortInlineI:
                    case OperandType.ShortInlineVar:
                        i += 1;
                        break;
                    case OperandType.InlineVar:
                        i += 2;
                        break;
                    case OperandType.InlineI8:
                    case OperandType.InlineR:
                        i += 8;
                        break;
                    case OperandType.InlineSwitch:
                        int n = BitConverter.ToInt32(il, i);
                        i += 4 + 4 * n;
                        break;
                    default:
                        i += 4;
                        break;
                }
            }
        }
    }
}
