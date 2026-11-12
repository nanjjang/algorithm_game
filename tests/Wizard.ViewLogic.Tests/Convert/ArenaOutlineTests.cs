using System.Numerics;
using Wizard.Content;
using Wizard.Core;
using Wizard.ViewLogic.Convert;
using Xunit;

namespace Wizard.ViewLogic.Tests.Convert
{
    // 전장 24×14칸(DECISIONS 2.3), 결투장 기둥 (9,3)(14,3)(9,10)(14,10)(world-characters 2.6)을 표시 좌표로
    public sealed class ArenaOutlineTests
    {
        [Fact]
        public void PracticeField_IsTwentyFourByFourteen_WithoutPillars()
        {
            ArenaOutline a = ArenaOutline.From(ConvertRig.Db.Arena(SimMode.Practice));
            Assert.Equal(new Vector2(24f, 14f), a.Size);
            Assert.Equal(new Vector2(12f, -7f), a.Center);
            Assert.Equal(0, a.PillarCount);
        }

        [Fact]
        public void DuelArena_PillarCentersAreHalfACellInFromTheirCorner()
        {
            ArenaOutline a = ArenaOutline.From(ConvertRig.Db.Arena(SimMode.Duel));
            Assert.Equal(4, a.PillarCount);
            Assert.Equal(new Vector2(9.5f, -3.5f), a.PillarCenter(0));
            Assert.Equal(new Vector2(14.5f, -3.5f), a.PillarCenter(1));
            Assert.Equal(new Vector2(9.5f, -10.5f), a.PillarCenter(2));
            Assert.Equal(new Vector2(14.5f, -10.5f), a.PillarCenter(3));
        }

        [Fact]
        public void HandMadeDefinition()
        {
            var def = new ArenaDef
            {
                Width = Fix64.FromInt(3),
                Height = Fix64.FromRatio(5, 2),
                Pillars = new[] { new FixVec2(Fix64.FromInt(1), Fix64.FromInt(0)) },
            };
            ArenaOutline a = ArenaOutline.From(def);
            Assert.Equal(new Vector2(3f, 2.5f), a.Size);
            Assert.Equal(new Vector2(1.5f, -0.5f), a.PillarCenter(0));
        }
    }
}
