using System;
using Wizard.TestKit.Shared.Fuzz;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Wizard.TestKit
{
    // 퍼저의 xUnit 연결. 실패하면 재현용 씨앗과 줄인 입력을 테스트 출력과 실패 메시지에 같이 남긴다.
    public static class FuzzAssert
    {
        public static FuzzResult<T> Holds<T>(ITestOutputHelper? output, FuzzOptions options, FuzzGen<T> generate, FuzzProperty<T> property,
                                             IFuzzShrinker<T>? shrinker, Func<T, string> format)
        {
            FuzzResult<T> result = Fuzzer.Run(options, generate, property, shrinker, format);
            output?.WriteLine(result.Report);
            if (!result.Passed)
            {
                throw new XunitException(result.Report);
            }
            return result;
        }

        public static FuzzResult<byte[]> BytesHold(ITestOutputHelper? output, FuzzOptions options, int minLength, int maxLength,
                                                   FuzzProperty<byte[]> property)
        {
            return Holds(output, options, FuzzGens.Bytes(minLength, maxLength), property, FuzzShrinkers.Bytes, FuzzFormat.Hex);
        }
    }
}
