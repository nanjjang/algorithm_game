using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Wizard.Client;
using Wizard.Sim.Input;
using Wizard.TestKit;
using Xunit;

namespace Wizard.Client.Tests.Input
{
    // 수락 조건: IInputSource 계약에 CurrentKeys(지금의 논리 키 상태)와 SetDuelPolling이 있다. 순번을 매기는 코드가 이 폴더에 없다.
    public sealed class InputSourceContractTests
    {
        [Fact]
        public void InputSourceHasContractMembers()
        {
            Type t = typeof(IInputSource);
            Assert.True(t.IsInterface);

            PropertyInfo current = t.GetProperty(nameof(IInputSource.CurrentKeys))!;
            Assert.Equal(typeof(KeyMask), current.PropertyType);
            Assert.True(current.CanRead);
            Assert.False(current.CanWrite);

            MethodInfo polling = t.GetMethod(nameof(IInputSource.SetDuelPolling))!;
            Assert.Equal(typeof(void), polling.ReturnType);
            Assert.Equal(new[] { typeof(bool) }, polling.GetParameters().Select(p => p.ParameterType).ToArray());

            MethodInfo drain = t.GetMethod(nameof(IInputSource.Drain))!;
            Assert.Equal(typeof(int), drain.ReturnType);
            Assert.Equal(new[] { typeof(Span<RawKeyEvent>) }, drain.GetParameters().Select(p => p.ParameterType).ToArray());

            MethodInfo bindings = t.GetMethod(nameof(IInputSource.SetBindings))!;
            Assert.Equal(new[] { typeof(KeyBindings) }, bindings.GetParameters().Select(p => p.ParameterType).ToArray());

            Assert.Equal(4, t.GetMembers().Count(m => m.MemberType == MemberTypes.Method && !((MethodInfo)m).IsSpecialName) + t.GetProperties().Length);
        }

        [Fact]
        public void RawKeyEventAndInputClockCarryOnlyContractFields()
        {
            Assert.Equal(new[] { "Down", "Key", "TimeSeconds" }, typeof(RawKeyEvent).GetFields().Select(f => f.Name).OrderBy(n => n, StringComparer.Ordinal));
            Assert.Equal(typeof(double), typeof(RawKeyEvent).GetField(nameof(RawKeyEvent.TimeSeconds))!.FieldType);
            Assert.Equal(new[] { "EpochSeconds", "TickSeconds" }, typeof(InputClock).GetFields().Select(f => f.Name).OrderBy(n => n, StringComparer.Ordinal));
        }

        // CONTRACTS 3.1: 순번을 매기는 곳은 InputRecorder와 DuelClientCore 둘뿐이다. 주석을 뺀 코드에 순번 관련 이름이 없어야 한다
        [Fact]
        public void InputFolderHasNoSequenceNumberingCode()
        {
            string dir = TestRepo.Path("src/Wizard.Client/Input");
            string[] files = Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories);
            Assert.NotEmpty(files);
            var forbidden = new Regex(@"\bSeq\b|\bSeqCounter\b|\bInputEvent\b|\bInputRecorder\b|\bDuelClientCore\b|\.Next\s*\(", RegexOptions.CultureInvariant);
            foreach (string file in files)
            {
                string code = string.Join("\n", File.ReadAllLines(file).Select(StripLineComment));
                Match m = forbidden.Match(code);
                Assert.False(m.Success, $"{Path.GetFileName(file)} has sequence-numbering code: '{m.Value}'");
            }
        }

        // 순번 없는 출력: Quantize의 출력 형식과 TakeForTick의 출력 형식이 QuantizedKey다
        [Fact]
        public void QueueSignaturesMatchContract()
        {
            MethodInfo enqueue = typeof(TickInputQueue).GetMethod(nameof(TickInputQueue.Enqueue))!;
            Assert.Equal(new[] { typeof(ReadOnlySpan<QuantizedKey>) }, enqueue.GetParameters().Select(p => p.ParameterType).ToArray());
            MethodInfo take = typeof(TickInputQueue).GetMethod(nameof(TickInputQueue.TakeForTick))!;
            Assert.Equal(typeof(int), take.ReturnType);
            Assert.Equal(new[] { typeof(uint), typeof(Span<QuantizedKey>) }, take.GetParameters().Select(p => p.ParameterType).ToArray());
            Assert.True(typeof(TickInputQueue).IsSealed);
            Assert.True(typeof(InputQuantizer).IsSealed);
        }

        private static string StripLineComment(string line)
        {
            int i = line.IndexOf("//", StringComparison.Ordinal);
            return i < 0 ? line : line.Substring(0, i);
        }
    }
}
