using System;
using Wizard.Core;

namespace Wizard.TestKit.Shared.Fuzz
{
    // 씨앗 고정 퍼저. 주 씨앗에서 사례마다 64비트 사례 씨앗을 뽑고, 사례 씨앗 하나로 입력을 만든다.
    // 실패하면 고정된 순서로 입력을 줄여(줄여도 여전히 실패하는 가장 앞 후보를 받아들이기를 더 줄지 않을 때까지)
    // 재현용 씨앗과 줄인 입력을 보고한다. 난수는 Wizard.Core.Pcg32 라서 dotnet·Mono·IL2CPP 에서 같은 입력이 나온다.
    public static class Fuzzer
    {
        // 주 씨앗의 흐름 번호와 사례의 흐름 번호. 값 자체에 뜻은 없고 바꾸면 모든 퍼저 사례가 바뀐다.
        private const ulong MasterStream = 0x66757a7aUL;   // "fuzz"
        private const ulong CaseStream = 0x63617365UL;     // "case"

        public static FuzzResult<T> Run<T>(FuzzOptions options, FuzzGen<T> generate, FuzzProperty<T> property,
                                           IFuzzShrinker<T>? shrinker, Func<T, string> format)
        {
            var master = new Pcg32(options.Seed, MasterStream);
            for (int i = 0; i < options.Cases; i++)
            {
                ulong caseSeed = options.ReplayCaseSeed ?? NextSeed(ref master);
                T input = Generate(caseSeed, generate);
                string? message = Check(property, input);
                if (message == null)
                {
                    continue;
                }
                T minimized = input;
                string minimizedMessage = message;
                int steps = 0;
                if (shrinker != null)
                {
                    Shrink(shrinker, property, options.MaxShrinkSteps, ref minimized, ref minimizedMessage, ref steps);
                }
                return new FuzzResult<T>(options.Seed, i + 1, options.Cases, i, caseSeed, input, format(input), minimized, format(minimized),
                    minimizedMessage, steps);
            }
            return new FuzzResult<T>(options.Seed, options.Cases, options.Cases);
        }

        // 사례 씨앗으로 입력을 다시 만든다(실패 보고의 사례 씨앗을 넘기면 원래 입력이 나온다)
        public static T Generate<T>(ulong caseSeed, FuzzGen<T> generate)
        {
            var rng = new Pcg32(caseSeed, CaseStream);
            return generate(ref rng);
        }

        // 주 씨앗에서 i 번째 사례 씨앗(0부터)
        public static ulong CaseSeed(ulong seed, int index)
        {
            var master = new Pcg32(seed, MasterStream);
            ulong s = 0;
            for (int i = 0; i <= index; i++)
            {
                s = NextSeed(ref master);
            }
            return s;
        }

        private static ulong NextSeed(ref Pcg32 master)
        {
            ulong hi = master.NextU32();
            ulong lo = master.NextU32();
            return (hi << 32) | lo;
        }

        private static string? Check<T>(FuzzProperty<T> property, T input)
        {
            try
            {
                return property(input);
            }
            catch (Exception ex)
            {
                return "threw " + ex.GetType().FullName + ": " + ex.Message;
            }
        }

        private static void Shrink<T>(IFuzzShrinker<T> shrinker, FuzzProperty<T> property, int maxSteps, ref T current, ref string message,
                                      ref int steps)
        {
            bool improved = true;
            while (improved && steps < maxSteps)
            {
                improved = false;
                foreach (T candidate in shrinker.Shrink(current))
                {
                    if (steps >= maxSteps)
                    {
                        break;
                    }
                    steps++;
                    string? m = Check(property, candidate);
                    if (m != null)
                    {
                        current = candidate;
                        message = m;
                        improved = true;
                        break;
                    }
                }
            }
        }
    }
}
