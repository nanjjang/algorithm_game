using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Wizard.Sim.Events;
using Wizard.ViewLogic.Presentation;
using Xunit;

namespace Wizard.ViewLogic.Tests.Presentation.UiEvents
{
    // CONTRACTS 13절 UiEventKeys: 구역, 꼴, 겹침 없음, All 과 상수의 일치
    public sealed class UiEventKeysTests
    {
        [Fact]
        public void AllEqualsEveryPublicConstant()
        {
            List<string> constants = typeof(UiEventKeys).GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(f => f.IsLiteral && f.FieldType == typeof(string))
                .Select(f => (string)f.GetRawConstantValue()!)
                .ToList();
            Assert.Equal(constants.OrderBy(k => k, StringComparer.Ordinal), UiEventKeys.All.OrderBy(k => k, StringComparer.Ordinal));
            Assert.Equal(UiEventKeys.All.Count, UiEventKeys.All.Distinct(StringComparer.Ordinal).Count());
        }

        [Fact]
        public void ConstantNameIsPascalOfKey()
        {
            // duel.match_found ↔ DuelMatchFound(CONTRACTS 13절의 예와 같은 꼴)
            foreach (FieldInfo f in typeof(UiEventKeys).GetFields(BindingFlags.Public | BindingFlags.Static).Where(f => f.IsLiteral))
            {
                string key = (string)f.GetRawConstantValue()!;
                string pascal = string.Concat(key.Split('.', '_').Select(p => char.ToUpperInvariant(p[0]) + p.Substring(1)));
                Assert.Equal(pascal, f.Name);
            }
        }

        [Fact]
        public void EveryKeyIsZonedSnakeCase()
        {
            Assert.All(UiEventKeys.All, k => Assert.True(UiEventZones.IsValidKey(k), k));
            // 여덟 구역이 모두 쓰인다
            var zones = new HashSet<string>(UiEventKeys.All.Select(k => k.Substring(0, k.IndexOf('.'))), StringComparer.Ordinal);
            Assert.Equal(UiEventZones.All.OrderBy(z => z, StringComparer.Ordinal), zones.OrderBy(z => z, StringComparer.Ordinal));
        }

        [Fact]
        public void NoKeyIsAlsoASimEventKey()
        {
            var sim = new HashSet<string>(EventBindingCoverage.AllSimEventTypes().Select(SimEventDocs.BindingKey), StringComparer.Ordinal);
            Assert.DoesNotContain(UiEventKeys.All, sim.Contains);
        }

        [Fact]
        public void ContractExamplesKeepTheirValues()
        {
            Assert.Equal("duel.match_found", UiEventKeys.DuelMatchFound);
            Assert.Equal("duel.queue_enter", UiEventKeys.DuelQueueEnter);
            Assert.Equal("duel.rating_changed", UiEventKeys.DuelRatingChanged);
            Assert.Equal("duel.input_dropped", UiEventKeys.DuelInputDropped);
            Assert.Equal("duel.margin_band_changed", UiEventKeys.DuelMarginBandChanged);
        }

        [Theory]
        [InlineData("ui.common.press", true)]
        [InlineData("net.server.connected", true)]
        [InlineData("season.leaderboard_top3", true)]
        [InlineData("heat.lock_entered", false)]     // 코어 구역
        [InlineData("ui", false)]
        [InlineData("ui.", false)]
        [InlineData("ui..press", false)]
        [InlineData("ui.Press", false)]
        [InlineData("ui.press-x", false)]
        [InlineData(".ui.press", false)]
        public void ZoneKeyGrammar(string key, bool valid)
        {
            Assert.Equal(valid, UiEventZones.IsValidKey(key));
        }

        [Fact]
        public void WorkingNamesAreNotInKeys()
        {
            // CLAUDE.md 5절: 작업용 이름(로마자)은 열쇠에 쓰지 않는다. names-scan 이 src/ 를 보지만 열쇠의 출처(명세 이름)에 들어 있어 따로 막는다
            string[] working = { "dalgu", "chorong", "galpi", "seonge", "geumeum", "nuru", "handeot", "dolmadang", "cheotmodak" };
            Assert.DoesNotContain(UiEventKeys.All, k => working.Any(w => k.Contains(w, StringComparison.Ordinal)));
        }
    }
}
