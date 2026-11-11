using System.Linq;
using System.Text.RegularExpressions;
using Wizard.Core;
using Wizard.ViewLogic;
using Wizard.ViewLogic.Presentation;
using Xunit;

namespace Wizard.ViewLogic.Tests
{
    // CONTRACTS 13, T-29·T-31
    public sealed class PresentationContractTests
    {
        [Fact]
        public void FixConvert_ScalesAndFlipsY()
        {
            Assert.Equal(1f, FixConvert.ToFloat(Fix64.One));
            Assert.Equal(-0.5f, FixConvert.ToFloat(Fix64.FromRatio(-1, 2)));
            System.Numerics.Vector2 p = FixConvert.ToWorld(new FixVec2(Fix64.FromInt(3), Fix64.FromInt(2)));
            Assert.Equal(3f, p.X);
            Assert.Equal(-2f, p.Y);
            Assert.Equal(90f, FixConvert.ToDegrees(Angle.FromCenti(9000)));
        }

        [Fact]
        public void UiEventKeys_AreUniqueSectionedSnakeCase()
        {
            var pattern = new Regex("^(ui|net|duel|plaza|notice|trade|season|run)\\.[a-z0-9_]+(\\.[a-z0-9_]+)*$");
            Assert.Equal(UiEventKeys.All.Count, UiEventKeys.All.Distinct().Count());
            Assert.All(UiEventKeys.All, k => Assert.Matches(pattern, k));
        }

        [Fact]
        public void NullPlayersAcceptEverything()
        {
            var fx = new NullFxPlayer();
            fx.Play(new FxRequest("fx_lock_steam", default, 0f, Wizard.Content.Element.Fire, 0, 0, Wizard.Netcode.Duel.EventPhase.Confirmed));
            fx.StopAll();
            var sfx = new NullSfxPlayer();
            sfx.Play("sfx_c07");
            sfx.SetBus(SfxBus.Master, 1f);
            Assert.Equal(1, fx.PlayCount);
            Assert.Equal(1, sfx.PlayCount);
        }
    }
}
