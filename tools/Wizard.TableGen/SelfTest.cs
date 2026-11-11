using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text;

namespace Wizard.TableGen
{
    // tablegen --self-test. 수락 조건(M0-BT-03)을 확인한다.
    // 기준값은 TableMath 와 다른 방법으로 따로 계산한다: (1) 다른 π 공식(가우스)과 320비트 정밀도, 45도 위쪽은 cos 급수,
    // (2) 정확한 대수값(sin 30·45·60도), (3) 실행 환경의 배정밀도 Math.Sin(도구 쪽 시험에만 쓴다. 생성기는 쓰지 않는다).
    // 가짜 저장소는 <뿌리>/artifacts/selftest/tablegen/ 에 만든다(커밋하지 않는 폴더).
    public static class SelfTest
    {
        private const int RefBits = 320;
        private static readonly BigInteger RawOne = BigInteger.One << 32;

        private sealed class Ctx
        {
            public readonly List<string> Failures = new List<string>();
            public int Passed;

            public void Check(string name, bool ok, string detail)
            {
                if (ok)
                {
                    Passed++;
                }
                else
                {
                    Failures.Add($"FAIL  tablegen self-test {name}: {detail}");
                }
            }
        }

        public static int Run(string realRoot, TextWriter stdout, TextWriter stderr)
        {
            var ctx = new Ctx();
            long[] sin = TableMath.SinRaw();
            SinShape(ctx, sin);
            SinExactAnchors(ctx, sin);
            SinAgainstIndependentReference(ctx, sin);
            SinAgainstDoubleLibrary(ctx, sin);
            PowAgainstSpec(ctx);
            Deterministic(ctx);
            CheckModeDiagnostics(ctx, realRoot);
            foreach (string f in ctx.Failures)
            {
                stderr.WriteLine(f);
            }
            stdout.WriteLine($"tablegen self-test: {ctx.Passed} passed, {ctx.Failures.Count} failed");
            return ctx.Failures.Count == 0 ? 0 : 1;
        }

        // 0~90.00도, 9,001칸. 0에서 시작해 2^32 로 끝나고 순증가한다.
        private static void SinShape(Ctx ctx, long[] sin)
        {
            ctx.Check("sin.length", sin.Length == 9001, $"length {sin.Length}");
            ctx.Check("sin.zero", sin[0] == 0, sin[0].ToString());
            ctx.Check("sin.ninety", sin[9000] == 4294967296L, sin[9000].ToString());
            bool increasing = true;
            int at = -1;
            for (int k = 1; k < sin.Length; k++)
            {
                if (sin[k] <= sin[k - 1])
                {
                    increasing = false;
                    at = k;
                    break;
                }
            }
            ctx.Check("sin.strictly-increasing", increasing, $"not increasing at k={at}");
        }

        // 정확한 값: sin 30° = 1/2, sin 45° = √2/2 (CONTRACTS 1.1·spec 01 2.2 의 INV_SQRT2 Raw 3,037,000,500), sin 60° = √3/2.
        private static void SinExactAnchors(Ctx ctx, long[] sin)
        {
            ctx.Check("sin.30deg", sin[3000] == 2147483648L, sin[3000].ToString());
            ctx.Check("sin.45deg.contract", sin[4500] == 3037000500L, sin[4500].ToString());
            // round(sqrt(N)) = (isqrt(4N) + 1) / 2 (N 이 제곱수가 아니면 동률이 없다)
            BigInteger half2 = (IntSqrt(4 * (BigInteger.One << 63)) + 1) / 2;       // √(2^63) = 2^32·√2/2
            BigInteger half3 = (IntSqrt(4 * (3 * (BigInteger.One << 62))) + 1) / 2; // √(3·2^62) = 2^32·√3/2
            ctx.Check("sin.45deg.isqrt", sin[4500] == (long)half2, $"{sin[4500]} vs {half2}");
            ctx.Check("sin.60deg.isqrt", sin[6000] == (long)half3, $"{sin[6000]} vs {half3}");
        }

        // 수락 2: 고정밀 기준값과 1 Raw 안. 기준값은 다른 π 공식, 다른 정밀도, 45도 위쪽은 cos(90°−a) 급수로 따로 만든다.
        // 같은 반올림(가장 가까운 정수, 같으면 짝수)이면 값이 정확히 같아야 하므로 그것도 본다.
        private static void SinAgainstIndependentReference(Ctx ctx, long[] sin)
        {
            BigInteger one = BigInteger.One << RefBits;
            BigInteger pi = 48 * AtanInv(18, one) + 32 * AtanInv(57, one) - 20 * AtanInv(239, one);
            int worstK = -1;
            BigInteger worst = BigInteger.Zero;
            int inexact = 0;
            int firstInexact = -1;
            for (int k = 0; k <= 9000; k++)
            {
                BigInteger value;
                if (k <= 4500)
                {
                    value = SinSeries(pi * k / 18000, one);
                }
                else
                {
                    value = CosSeries(pi * (9000 - k) / 18000, one);
                }
                BigInteger reference = RoundHalfEven(value * RawOne, one);
                BigInteger diff = BigInteger.Abs(reference - sin[k]);
                if (diff > worst)
                {
                    worst = diff;
                    worstK = k;
                }
                if (!diff.IsZero)
                {
                    inexact++;
                    if (firstInexact < 0)
                    {
                        firstInexact = k;
                    }
                }
            }
            ctx.Check("sin.reference.within-1-raw", worst <= 1, $"max |table - reference| = {worst} at k={worstK}");
            ctx.Check("sin.reference.exact", inexact == 0, $"{inexact} entries differ from the correctly rounded reference (first k={firstInexact})");
        }

        // 수락 2(다른 출처): 플랫폼 배정밀도 sin 과도 1 Raw 안. 배정밀도 오차는 2^32 배율에서 1e-6 Raw 수준이다.
        private static void SinAgainstDoubleLibrary(Ctx ctx, long[] sin)
        {
            double worst = 0;
            int worstK = -1;
            for (int k = 0; k <= 9000; k++)
            {
                double expected = Math.Sin(k * Math.PI / 18000.0) * 4294967296.0;
                double diff = Math.Abs(expected - sin[k]);
                if (diff > worst)
                {
                    worst = diff;
                    worstK = k;
                }
            }
            ctx.Check("sin.double.within-1-raw", worst <= 1.0, $"max |table - Math.Sin| = {worst} at k={worstK}");
        }

        // 거듭제곱 표: spec 02 2.2절 상수 표와 11절 강화 배율 표의 Raw.
        private static void PowAgainstSpec(Ctx ctx)
        {
            long[] enhance =
            {
                4294967296L, 4939212390L, 5680094249L, 6532108386L, 7511924644L, 8638713341L, 9934520342L,
                11424698393L, 13138403152L, 15109163625L, 17375538169L, 19981868894L, 22979149228L, 26426021613L,
            };
            long[] e = TableMath.PowRaw(115, 100, 14);
            ctx.Check("pow.enhance115.length", e.Length == 14, e.Length.ToString());
            for (int n = 0; n < enhance.Length && n < e.Length; n++)
            {
                ctx.Check($"pow.enhance115[{n}]", e[n] == enhance[n], $"{e[n]} vs spec {enhance[n]}");
            }
            long[] d085 = TableMath.PowRaw(85, 100, 16);
            ctx.Check("pow.decay085.spec", d085.Length == 16 && d085[1] == 3650722202L && d085[2] == 3103113871L && d085[3] == 2637646791L,
                $"{d085[1]}, {d085[2]}, {d085[3]}");
            long[] d092 = TableMath.PowRaw(92, 100, 16);
            ctx.Check("pow.decay092.spec", d092.Length == 16 && d092[1] == 3951369912L && d092[2] == 3635260319L && d092[3] == 3344439494L,
                $"{d092[1]}, {d092[2]}, {d092[3]}");
            long[] c07 = TableMath.PowRaw(7, 10, 16);
            ctx.Check("pow.chain07.spec", c07.Length == 16 && c07[0] == 4294967296L && c07[1] == 3006477107L, $"{c07[0]}, {c07[1]}");
            // 동률은 짝수로: 0.49 × 2^32 = 2,104,533,975.04 → 2,104,533,975 / 반올림 규칙 자체는 RoundDiv 로 본다
            ctx.Check("pow.chain07[2]", c07[2] == 2104533975L, c07[2].ToString());
            ctx.Check("round.half-even.up", TableMath.RoundDiv(5, 2) == 2 && TableMath.RoundDiv(7, 2) == 4, "5/2 → 2, 7/2 → 4");
            ctx.Check("round.half-even.negative", TableMath.RoundDiv(-5, 2) == -2 && TableMath.RoundDiv(-7, 2) == -4, "-5/2 → -2, -7/2 → -4");
            ctx.Check("round.nearest", TableMath.RoundDiv(16, 10) == 2 && TableMath.RoundDiv(14, 10) == 1, "1.6 → 2, 1.4 → 1");
        }

        // 같은 입력이면 같은 바이트.
        private static void Deterministic(Ctx ctx)
        {
            var a = Program.Outputs();
            var b = Program.Outputs();
            bool same = a.Count == b.Count;
            for (int i = 0; same && i < a.Count; i++)
            {
                same = a[i].Name == b[i].Name && a[i].Text == b[i].Text;
            }
            ctx.Check("deterministic.outputs", same, "two runs produced different text");
            foreach (var (name, text) in a)
            {
                ctx.Check("output.lf " + name, text.IndexOf('\r') < 0 && text.EndsWith("}\n", StringComparison.Ordinal), "output must use LF");
            }
        }

        // 일부러 틀린 입력: 손으로 고친 표, 없는 표, 모르는 옵션은 정해진 진단으로 실패한다.
        private static void CheckModeDiagnostics(Ctx ctx, string realRoot)
        {
            string root = Path.Combine(realRoot, "artifacts", "selftest", "tablegen");
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
            try
            {
                Directory.CreateDirectory(root);
                var err0 = new StringWriter();
                ctx.Check("check.missing.exit", Program.Run(root, "--check", TextWriter.Null, err0) == 1, "missing tables accepted");
                ctx.Check("check.missing.message", err0.ToString().StartsWith("FAIL  src/Wizard.Core/Generated/SinTable.g.cs is missing", StringComparison.Ordinal), err0.ToString().Trim());

                ctx.Check("check.write", Program.Run(root, "--write", TextWriter.Null, TextWriter.Null) == 0, "write failed");
                ctx.Check("check.fresh", Program.Run(root, "--check", TextWriter.Null, TextWriter.Null) == 0, "check after write failed");

                string sinPath = Path.Combine(root, "src/Wizard.Core/Generated/SinTable.g.cs");
                string text = File.ReadAllText(sinPath);
                File.WriteAllText(sinPath, text.Replace(" 749613L,", " 749614L,"), new UTF8Encoding(false));
                var err = new StringWriter();
                ctx.Check("check.one-raw-off.exit", Program.Run(root, "--check", TextWriter.Null, err) == 1, "a table edited by one Raw was accepted");
                ctx.Check("check.one-raw-off.message", err.ToString().Trim() == "FAIL  src/Wizard.Core/Generated/SinTable.g.cs differs from tablegen output. Run tools/Wizard.TableGen and commit the file.",
                    err.ToString().Trim());

                File.WriteAllText(sinPath, text.Replace("\n", "\r\n"), new UTF8Encoding(false));
                ctx.Check("check.crlf", Program.Run(root, "--check", TextWriter.Null, TextWriter.Null) == 1, "CRLF table was accepted");

                var err2 = new StringWriter();
                ctx.Check("check.unknown-option", Program.Run(root, "--frobnicate", TextWriter.Null, err2) == 2
                    && err2.ToString().StartsWith("FAIL  unknown option", StringComparison.Ordinal), err2.ToString().Trim());
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, true);
                }
            }
        }

        // atan(1/n) × one (기준값 쪽 구현. TableMath 와 코드를 나누지 않는다)
        private static BigInteger AtanInv(int n, BigInteger one)
        {
            BigInteger sum = BigInteger.Zero;
            BigInteger power = one / n;            // one / n^(2k+1)
            BigInteger n2 = n * n;
            for (int k = 0; !power.IsZero; k++)
            {
                BigInteger t = power / (2 * k + 1);
                sum = (k % 2 == 0) ? sum + t : sum - t;
                power /= n2;
            }
            return sum;
        }

        // sin(x) × one = Σ (−1)^i x^(2i+1)/(2i+1)!
        private static BigInteger SinSeries(BigInteger x, BigInteger one)
        {
            BigInteger sum = BigInteger.Zero;
            BigInteger term = x;
            BigInteger xx = x * x / one;
            for (int i = 0; !term.IsZero; i++)
            {
                sum += term;
                term = -term * xx / one / ((2 * i + 2) * (2 * i + 3));
            }
            return sum;
        }

        // cos(x) × one = Σ (−1)^i x^(2i)/(2i)!
        private static BigInteger CosSeries(BigInteger x, BigInteger one)
        {
            BigInteger sum = BigInteger.Zero;
            BigInteger term = one;
            BigInteger xx = x * x / one;
            for (int i = 0; !term.IsZero; i++)
            {
                sum += term;
                term = -term * xx / one / ((2 * i + 1) * (2 * i + 2));
            }
            return sum;
        }

        // v / d 를 가장 가까운 정수로, 같으면 짝수(기준값 쪽 구현)
        private static BigInteger RoundHalfEven(BigInteger v, BigInteger d)
        {
            BigInteger floor = BigInteger.Divide(v, d);
            if (v.Sign < 0 && floor * d != v)
            {
                floor -= 1;
            }
            BigInteger rem2 = (v - floor * d) * 2;
            int c = rem2.CompareTo(d);
            if (c > 0 || (c == 0 && !floor.IsEven))
            {
                return floor + 1;
            }
            return floor;
        }

        private static BigInteger IntSqrt(BigInteger n)
        {
            if (n.IsZero)
            {
                return n;
            }
            BigInteger x = BigInteger.One << (int)((n.GetBitLength() + 1) / 2);
            while (true)
            {
                BigInteger y = (x + n / x) / 2;
                if (y >= x)
                {
                    return x;
                }
                x = y;
            }
        }
    }
}
