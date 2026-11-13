using System.Collections.Generic;
using System.Linq;
using Wizard.ViewLogic.Presentation;
using Xunit;

namespace Wizard.ViewLogic.Tests.Presentation.Bindings
{
    // EventBindingTable.Load 의 읽기 검사(작은 손 파일로)
    public sealed class EventBindingTableTests
    {
        internal const string NoneEmpty = "{ \"none\": [] }";

        internal static string BindingsJson(string lines, string fxCatalog = "{ \"fx\": \"fx_lock_x\", \"src\": \"20.3#4\" }, { \"fx\": \"fx_graze_ring\", \"src\": \"20.3#12\" }",
            string sfxCatalog = "\"sfx_c07\", \"sfx_c10\", \"sfx_t09a\"") =>
            "{ \"_note\": \"t\", \"graze_chain\": { \"window_seconds\": 2.0, \"max_step\": 5 }, \"sfx_catalog\": [" + sfxCatalog + "], " +
            "\"fx_catalog\": [" + fxCatalog + "], \"bindings\": [" + lines + "] }";

        internal static string Line(string key, string fx = "", string sfx = "", string shake = "none", int hitstop = 0, bool confirmedOnly = false) =>
            "{ \"event\": \"" + key + "\", \"fx\": [" + fx + "], \"sfx\": [" + sfx + "], \"shake\": \"" + shake + "\", \"hitstop_ms\": " + hitstop +
            ", \"confirmed_only\": " + (confirmedOnly ? "true" : "false") + " }";

        private static (EventBindingTable Table, List<string> Errors) Load(string bindings, string none = NoneEmpty)
        {
            var errors = new List<string>();
            EventBindingTable t = EventBindingTable.Load(bindings, none, errors);
            return (t, errors);
        }

        [Fact]
        public void ReadsALine()
        {
            (EventBindingTable t, List<string> errors) = Load(BindingsJson(Line("heat.lock_entered", "\"fx_lock_x\"", "\"sfx_c07\"", "medium", 120, true)));
            Assert.Empty(errors);
            Assert.True(t.TryGet("heat.lock_entered", out EventBinding? b));
            Assert.Equal(new[] { "fx_lock_x" }, b!.Fx);
            Assert.Equal(new[] { "sfx_c07" }, b.Sfx);
            Assert.Equal(BindingShake.Medium, b.Shake);
            Assert.Equal(120, b.HitstopMs);
            Assert.True(b.ConfirmedOnly);
            Assert.False(b.IsPlaceholder);
            Assert.Equal(120, t.GrazeChain.WindowTicks);
            Assert.Equal(5, t.GrazeChain.MaxStep);
            Assert.True(t.IsKnownFx("fx_graze_ring"));
            Assert.True(t.IsKnownSfx("sfx_t09a"));
            Assert.False(t.TryGet("heat.unlocked", out _));
        }

        [Fact]
        public void UnknownFxIdFails()
        {
            // 수락 2: 없는 식별자를 가리키면 검사가 실패한다
            (_, List<string> errors) = Load(BindingsJson(Line("heat.lock_entered", "\"fx_lock_y\"")));
            Assert.Contains(errors, e => e.Contains("fx 'fx_lock_y' is not in fx_catalog"));
        }

        [Fact]
        public void UnknownSfxIdFails()
        {
            (_, List<string> errors) = Load(BindingsJson(Line("heat.lock_entered", "", "\"sfx_c99\"")));
            Assert.Contains(errors, e => e.Contains("sfx 'sfx_c99' is not in sfx_catalog"));
        }

        [Fact]
        public void MalformedCatalogIdsFail()
        {
            (_, List<string> errors) = Load(BindingsJson("", "{ \"fx\": \"lock_x\", \"src\": \"20.3#4\" }", "\"sfx_c7\", \"sfx_x01\", \"sfx_t09d\""));
            Assert.Contains(errors, e => e.Contains("'lock_x' is not an fx id"));
            Assert.Contains(errors, e => e.Contains("'sfx_c7' is not an sfx id"));
            Assert.Contains(errors, e => e.Contains("'sfx_x01' is not an sfx id"));
            Assert.Contains(errors, e => e.Contains("'sfx_t09d' is not an sfx id"));
        }

        [Fact]
        public void DuplicateLineFails()
        {
            (_, List<string> errors) = Load(BindingsJson(Line("heat.lock_entered") + ", " + Line("heat.lock_entered")));
            Assert.Contains(errors, e => e.Contains("bound twice"));
        }

        [Fact]
        public void KeyBothBoundAndNoneFails()
        {
            (_, List<string> errors) = Load(BindingsJson(Line("sim.none")), "{ \"none\": [ { \"event\": \"sim.none\", \"reason\": \"not_an_event\" } ] }");
            Assert.Contains(errors, e => e.Contains("also has a binding line"));
        }

        [Fact]
        public void NoneListIsRead()
        {
            (EventBindingTable t, List<string> errors) = Load(BindingsJson(""), "{ \"_note\": \"x\", \"none\": [ { \"event\": \"sim.none\", \"reason\": \"not_an_event\" } ] }");
            Assert.Empty(errors);
            Assert.True(t.IsIntentionallyNone("sim.none"));
            Assert.Equal("not_an_event", t.NoneReason("sim.none"));
            Assert.Null(t.NoneReason("heat.lock_entered"));
        }

        [Fact]
        public void UnknownAndMissingFieldsFail()
        {
            string line = "{ \"event\": \"heat.lock_entered\", \"fx\": [], \"sfx\": [], \"shake\": \"none\", \"confirmed_only\": false, \"volume\": 3 }";
            (_, List<string> errors) = Load(BindingsJson(line));
            Assert.Contains(errors, e => e.Contains("unknown field 'volume'"));
            Assert.Contains(errors, e => e.Contains("missing field 'hitstop_ms'"));
        }

        [Theory]
        [InlineData("\"shake\": \"huge\"", "shake: one of")]
        [InlineData("\"hitstop_ms\": -1", "hitstop_ms: non-negative integer")]
        [InlineData("\"hitstop_ms\": 1.5", "hitstop_ms: non-negative integer")]
        [InlineData("\"confirmed_only\": \"yes\"", "confirmed_only: boolean")]
        public void BadValuesFail(string replacement, string expected)
        {
            string line = Line("heat.lock_entered");
            string field = replacement.Substring(0, replacement.IndexOf(':'));
            int at = line.IndexOf(field, System.StringComparison.Ordinal);
            int end = line.IndexOfAny(new[] { ',', '}' }, at);
            string bad = line.Substring(0, at) + replacement + line.Substring(end);
            (_, List<string> errors) = Load(BindingsJson(bad));
            Assert.Contains(errors, e => e.Contains(expected));
        }

        [Theory]
        [InlineData("HEAT.lock")]
        [InlineData("heat")]
        [InlineData("heat..lock")]
        [InlineData("heat.lock-x")]
        public void BadEventKeyFails(string key)
        {
            (_, List<string> errors) = Load(BindingsJson(Line(key)));
            Assert.Contains(errors, e => e.Contains("event key must be"));
        }

        [Theory]
        [InlineData("{ \"none\": [] ")]                                      // 닫히지 않음
        [InlineData("{ \"none\": [], }")]                                    // 끝 쉼표
        [InlineData("{ \"none\": [] } x")]                                   // 남는 글자
        [InlineData("{ \"none\": [], \"none\": [] }")]                       // 같은 열쇠
        [InlineData("// c\n{ \"none\": [] }")]                               // 주석
        [InlineData("[]")]                                                   // 뿌리가 객체가 아님
        public void MalformedJsonFails(string none)
        {
            (_, List<string> errors) = Load(BindingsJson(""), none);
            Assert.Contains(errors, e => e.StartsWith(EventBindingTable.NoneFile, System.StringComparison.Ordinal));
        }

        [Fact]
        public void JsonEscapesAndWhitespaceAreRead()
        {
            string none = "﻿{\r\n\t\"none\": [ { \"event\": \"sim\\u002enone\", \"reason\": \"not\\u005fan_event\" } ] }";
            (EventBindingTable t, List<string> errors) = Load(BindingsJson(""), none);
            Assert.Empty(errors);
            Assert.True(t.IsIntentionallyNone("sim.none"));
        }

        [Fact]
        public void GrazeWindowIsSecondsTimesTickRate()
        {
            string json = BindingsJson("").Replace("\"window_seconds\": 2.0", "\"window_seconds\": 0.5");
            (EventBindingTable t, List<string> errors) = Load(json);
            Assert.Empty(errors);
            Assert.Equal(30, t.GrazeChain.WindowTicks);
            (_, List<string> bad) = Load(BindingsJson("").Replace("\"window_seconds\": 2.0", "\"window_seconds\": 0"));
            Assert.Contains(bad, e => e.Contains("window_seconds"));
        }

        [Fact]
        public void IdGrammars()
        {
            Assert.True(EventBindingTable.IsSfxId("sfx_e01"));
            Assert.True(EventBindingTable.IsSfxId("sfx_t10c"));
            Assert.False(EventBindingTable.IsSfxId("sfx_e1"));
            Assert.False(EventBindingTable.IsSfxId("sfx_q01"));
            Assert.False(EventBindingTable.IsSfxId("sfx_e01x"));
            Assert.True(EventBindingTable.IsFxId("fx_lock_x"));
            Assert.True(EventBindingTable.IsFxId("ui_crystal_full"));
            Assert.False(EventBindingTable.IsFxId("fx_"));
            Assert.False(EventBindingTable.IsFxId("fx_Lock"));
            Assert.False(EventBindingTable.IsFxId("sfx_c01"));
            Assert.True(EventBindingTable.IsEventKey("spell.cast.crit"));
            Assert.False(EventBindingTable.IsEventKey("spell"));
            Assert.False(EventBindingTable.IsEventKey("1spell.cast"));
        }

        [Fact]
        public void CoverageReportsMissingKeys()
        {
            // 빈 표면 모든 SimEventType 과 UiEventKeys 가 빠진 것으로 나온다
            (EventBindingTable t, List<string> errors) = Load(BindingsJson(Line("made.up_key")));
            Assert.Empty(errors);
            List<string> problems = EventBindingCoverage.FindProblems(t);
            Assert.Contains(problems, p => p.Contains("SimEventType.HeatLockEntered"));
            Assert.Contains(problems, p => p.Contains("UiEventKeys 'duel.match_found'"));
            Assert.Contains(problems, p => p.Contains("'made.up_key' is neither"));
            Assert.Equal(EventBindingCoverage.AllSimEventTypes().Count + UiEventKeys.All.Count + 1, problems.Count);
        }
    }
}
