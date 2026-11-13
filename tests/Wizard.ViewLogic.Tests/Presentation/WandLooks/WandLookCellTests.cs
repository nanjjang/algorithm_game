using Wizard.ViewLogic.Assets;
using Wizard.ViewLogic.Common;
using Wizard.ViewLogic.Presentation.WandLooks;
using Wizard.ViewLogic.Tests.Assets;
using Xunit;

namespace Wizard.ViewLogic.Tests.Presentation.WandLooks
{
    // 수락 조건 2: 셀 선택 함수가 spec 14 5.4절 의사코드와 같다.
    // 기대값은 의사코드를 시험 안에 그대로 옮겨 쓴 것과, 그림 매니페스트를 읽은 레지스트리의 칸 사각형이다
    public sealed class WandLookCellTests
    {
        private static readonly SpriteFacing[] Facings = { SpriteFacing.Down, SpriteFacing.Right, SpriteFacing.Up, SpriteFacing.Left };
        private static readonly WandPose[] Poses = { WandPose.Hold, WandPose.Aim };

        // function WandHandRect(stage, dir, pose): row = DirIndex(dir) * 2 + (pose == aim ? 1 : 0)   // down=0, right=1, up=2
        //                                         return (stage * 32, row * 64, 32, 64)
        private static PixelRect PseudoHand(int stage, SpriteFacing dir, WandPose pose)
        {
            int dirIndex = dir == SpriteFacing.Down ? 0 : dir == SpriteFacing.Up ? 2 : 1;   // left 는 right 반전
            int row = dirIndex * 2 + (pose == WandPose.Aim ? 1 : 0);
            return new PixelRect(stage * 32, row * 64, 32, 64);
        }

        private static PixelRect PseudoInspection(int stage) => new PixelRect((stage % 7) * 192, (stage / 7) * 192, 192, 192);

        private static PixelRect PseudoIcon(int stage) => new PixelRect(stage * 64, 0, 64, 64);

        [Fact]
        public void CellSelectionEqualsSpec14_5_4Pseudocode()
        {
            foreach (WandLookSet set in WandLooksRepo.Table.Sets)
            {
                WandLookSheet s = set.Sheet;
                for (int stage = 0; stage < 14; stage++)
                {
                    foreach (SpriteFacing f in Facings)
                    {
                        foreach (WandPose p in Poses)
                        {
                            Assert.Equal(PseudoHand(stage, f, p), s.HandRect(stage, f, p));
                        }
                    }
                    Assert.Equal(PseudoInspection(stage), s.InspectionRect(stage));
                    Assert.Equal(PseudoIcon(stage), s.IconRect(stage));
                }
            }
        }

        [Fact]
        public void CellSelectionEqualsArtManifestSheets()
        {
            // 행 이름 순서까지 같은지: 레지스트리의 WandSheet 는 hand.rows 이름으로 행을 찾는다
            foreach (WandLookSet set in WandLooksRepo.Table.Sets)
            {
                Assert.True(RepoAssets.Registry.TryGetWand(set.WandId, out WandSheet art));
                for (int stage = 0; stage < 14; stage++)
                {
                    foreach (SpriteFacing f in Facings)
                    {
                        foreach (WandPose p in Poses)
                        {
                            Assert.Equal(art.HandRect(stage, f, p), set.Sheet.HandRect(stage, f, p));
                        }
                    }
                    Assert.Equal(art.InspectionRect(stage), set.Sheet.InspectionRect(stage));
                    Assert.Equal(art.IconRect(stage), set.Sheet.IconRect(stage));
                }
            }
        }

        [Fact]
        public void AttachAndMirrorFollowSpec14_5_4()
        {
            WandLookSheet s = WandLooksRepo.Table.Get(1).Sheet;
            // wandTopLeft = charFrame.Hand - (16, 48)
            Assert.Equal(new PixelPoint(40 - 16, 70 - 48), s.AttachTopLeft(new PixelPoint(40, 70)));
            // 왼쪽은 x 를 31 − x 로 반전 [임시]
            Assert.Equal(31, s.MirrorX(0));
            Assert.Equal(15, s.MirrorX(16));
            Assert.Equal(new PixelPoint(31 - 12, 37), s.PointFor(new PixelPoint(12, 37), SpriteFacing.Left));
            Assert.Equal(new PixelPoint(12, 37), s.PointFor(new PixelPoint(12, 37), SpriteFacing.Right));
        }

        [Fact]
        public void StageOutOfRangeThrows()
        {
            WandLookSheet s = WandLooksRepo.Table.Get(1).Sheet;
            Assert.Throws<System.ArgumentOutOfRangeException>(() => s.HandRect(14, SpriteFacing.Down, WandPose.Hold));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => s.IconRect(-1));
        }
    }
}
