using System.Collections.Generic;
using Wizard.Core;

namespace Wizard.TestKit.Shared.Arith
{
    // 산술 경계 벡터 묶음(tests/golden/arith/*.json)의 연산 이름을 코어 함수에 잇는 표(tests/golden/arith/README.md).
    // 인자와 결과는 Raw(정수)다. 128비트 값은 (hi: long, lo: ulong) 두 칸이다.
    // 표는 여기 한 벌뿐이다(T-61): dotnet 시험(ArithGoldenTests), dotnet 골든 실행기, Unity EditMode, 플레이어의 골든 실행기가
    // 같은 소스를 컴파일해 쓴다. unity/Assets 아래에는 Raw 를 만지는 코드를 둘 수 없어서(FixConvertSearchTests) Unity 쪽 사본이 없다.
    public sealed class CoreArithOps : IArithOps
    {
        // 표에 있는 연산 이름 전부. 묶음이 표에 없는 이름을 쓰면 UnknownOp 으로 실패한다.
        public static readonly string[] Ops =
        {
            "FixMath.DivInt", "FixMath.ModInt", "FixMath.DivLong", "FixMath.ModLong", "FixMath.MulDiv", "FixMath.SaturateToInt",
            "FixMath.Sqrt", "FixMath.PowTable", "FixMath.InvSqrt2Raw",
            "Fix64.Mul", "Fix64.Div", "Fix64.DivInt", "Fix64.MulInt", "Fix64.Add", "Fix64.Sub", "Fix64.Neg", "Fix64.Abs",
            "Fix64.MulPermille",
            "FixVec2.Normalized",
            "Wide.Mul64", "Wide.UMul64", "Wide.ShiftRight32", "Wide.Div128By64", "Wide.Compare128",
        };

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
                case "FixMath.ModLong":
                    results.Add(ArithValue.FromInt64(FixMath.ModLong(a[0].ToInt64(), a[1].ToInt64())));
                    return true;
                case "FixMath.MulDiv":
                    results.Add(ArithValue.FromInt64(FixMath.MulDiv(a[0].ToInt64(), a[1].ToInt64(), a[2].ToInt64())));
                    return true;
                case "FixMath.SaturateToInt":
                    results.Add(ArithValue.FromInt32(FixMath.SaturateToInt(a[0].ToInt64())));
                    return true;
                case "FixMath.Sqrt":
                    results.Add(ArithValue.FromInt64(FixMath.Sqrt(Raw(a[0])).Raw));
                    return true;
                case "FixMath.PowTable":
                    results.Add(ArithValue.FromInt64(FixMath.PowTable((PowBase)a[0].ToInt32(), a[1].ToInt32()).Raw));
                    return true;
                case "FixMath.InvSqrt2Raw":
                    results.Add(ArithValue.FromInt64(FixMath.InvSqrt2Raw));
                    return true;
                case "Fix64.Mul":
                    results.Add(ArithValue.FromInt64((Raw(a[0]) * Raw(a[1])).Raw));
                    return true;
                case "Fix64.Div":
                    results.Add(ArithValue.FromInt64((Raw(a[0]) / Raw(a[1])).Raw));
                    return true;
                case "Fix64.DivInt":
                    results.Add(ArithValue.FromInt64((Raw(a[0]) / a[1].ToInt32()).Raw));
                    return true;
                case "Fix64.MulInt":
                    results.Add(ArithValue.FromInt64((Raw(a[0]) * a[1].ToInt32()).Raw));
                    return true;
                case "Fix64.Add":
                    results.Add(ArithValue.FromInt64((Raw(a[0]) + Raw(a[1])).Raw));
                    return true;
                case "Fix64.Sub":
                    results.Add(ArithValue.FromInt64((Raw(a[0]) - Raw(a[1])).Raw));
                    return true;
                case "Fix64.Neg":
                    results.Add(ArithValue.FromInt64((-Raw(a[0])).Raw));
                    return true;
                case "Fix64.Abs":
                    results.Add(ArithValue.FromInt64(Fix64.Abs(Raw(a[0])).Raw));
                    return true;
                case "Fix64.MulPermille":
                    results.Add(ArithValue.FromInt64(Fix64.MulPermille(Raw(a[0]), a[1].ToInt32()).Raw));
                    return true;
                case "FixVec2.Normalized":
                {
                    FixVec2 n = new FixVec2(Raw(a[0]), Raw(a[1])).Normalized();
                    results.Add(ArithValue.FromInt64(n.X.Raw));
                    results.Add(ArithValue.FromInt64(n.Y.Raw));
                    return true;
                }
                case "Wide.Mul64":
                {
                    Wide.Mul64(a[0].ToInt64(), a[1].ToInt64(), out long hi, out ulong lo);
                    results.Add(ArithValue.FromInt64(hi));
                    results.Add(ArithValue.FromUInt64(lo));
                    return true;
                }
                case "Wide.UMul64":
                {
                    Wide.UMul64(a[0].ToUInt64(), a[1].ToUInt64(), out ulong hi, out ulong lo);
                    results.Add(ArithValue.FromUInt64(hi));
                    results.Add(ArithValue.FromUInt64(lo));
                    return true;
                }
                case "Wide.ShiftRight32":
                    results.Add(ArithValue.FromInt64(Wide.ShiftRight32(a[0].ToInt64(), a[1].ToUInt64())));
                    return true;
                case "Wide.Div128By64":
                    results.Add(ArithValue.FromInt64(Wide.Div128By64(a[0].ToInt64(), a[1].ToUInt64(), a[2].ToInt64())));
                    return true;
                case "Wide.Compare128":
                    results.Add(ArithValue.FromInt32(Wide.Compare128(a[0].ToInt64(), a[1].ToUInt64(), a[2].ToInt64(), a[3].ToUInt64())));
                    return true;
                default:
                    return false;
            }
        }

        private static Fix64 Raw(ArithValue v) => Fix64.FromRaw(v.ToInt64());
    }
}
