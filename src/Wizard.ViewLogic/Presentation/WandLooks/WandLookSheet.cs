using System;
using System.Collections.Generic;
using Wizard.ViewLogic.Assets;
using Wizard.ViewLogic.Common;

namespace Wizard.ViewLogic.Presentation.WandLooks
{
    // 지팡이 시트의 칸 규격과 셀 선택(spec 14 5.4절 의사코드 그대로):
    //   WandHandRect(stage, dir, pose):  row = DirIndex(dir) * 2 + (pose == aim ? 1 : 0)   // down=0, right=1, up=2
    //                                    return (stage * 32, row * 64, 32, 64)
    //   WandInspectionRect(stage):       ((stage % 7) * 192, (stage / 7) * 192, 192, 192)
    //   WandIconRect(stage):             (stage * 64, 0, 64, 64)
    //   AttachWand(charFrame, stage):    wandTopLeft = charFrame.Hand - (16, 48)
    // left 는 right 칸을 반전해 쓰고 점의 x 는 31 − x 다([임시]).
    public sealed class WandLookSheet
    {
        public const int StageCount = 14;     // +0..+13 (자료구조 크기)
        public const int DirCount = 3;        // 아래·옆·위
        public const int PoseCount = 2;       // 들기·내밀기

        public WandLookSheet(PixelPoint grip, PixelSize handCell, IReadOnlyList<string> handRows, PixelSize inspectionCell,
            int inspectionColumns, PixelSize iconCell)
        {
            Grip = grip;
            HandCell = handCell;
            HandRows = handRows;
            InspectionCell = inspectionCell;
            InspectionColumns = inspectionColumns;
            IconCell = iconCell;
        }

        public PixelPoint Grip { get; }

        public PixelSize HandCell { get; }

        // 시트의 행 이름 순서. 5.4절의 행 공식(DirIndex × 2 + 자세)과 같은 순서여야 한다(WandLookChecks 가 본다)
        public IReadOnlyList<string> HandRows { get; }

        public PixelSize InspectionCell { get; }

        public int InspectionColumns { get; }

        public PixelSize IconCell { get; }

        public static int DirIndex(SpriteFacing facing)
        {
            switch (facing)
            {
                case SpriteFacing.Down: return 0;
                case SpriteFacing.Right:
                case SpriteFacing.Left: return 1;
                case SpriteFacing.Up: return 2;
                default: throw new ArgumentOutOfRangeException(nameof(facing), facing, null);
            }
        }

        public static int HandRow(SpriteFacing facing, WandPose pose) => DirIndex(facing) * 2 + (pose == WandPose.Aim ? 1 : 0);

        // 행 공식이 기대하는 행 이름("down_hold" …)
        public static string HandRowName(int row)
        {
            string dir = (row / 2) switch { 0 => WandSheet.DownKey, 1 => WandSheet.RightKey, 2 => WandSheet.UpKey, _ => throw new ArgumentOutOfRangeException(nameof(row)) };
            return dir + WandSheet.RowSeparator + (row % 2 == 1 ? WandSheet.AimKey : WandSheet.HoldKey);
        }

        public PixelRect HandRect(int stage, SpriteFacing facing, WandPose pose)
        {
            CheckStage(stage);
            int row = HandRow(facing, pose);
            return new PixelRect(stage * HandCell.Width, row * HandCell.Height, HandCell.Width, HandCell.Height);
        }

        public PixelRect InspectionRect(int stage)
        {
            CheckStage(stage);
            return new PixelRect((stage % InspectionColumns) * InspectionCell.Width, (stage / InspectionColumns) * InspectionCell.Height,
                InspectionCell.Width, InspectionCell.Height);
        }

        public PixelRect IconRect(int stage)
        {
            CheckStage(stage);
            return new PixelRect(stage * IconCell.Width, 0, IconCell.Width, IconCell.Height);
        }

        // 손잡이 기준점을 캐릭터 프레임의 손 점에 맞춘 지팡이 칸의 왼쪽 위
        public PixelPoint AttachTopLeft(PixelPoint charFrameHand) => new PixelPoint(charFrameHand.X - Grip.X, charFrameHand.Y - Grip.Y);

        // 반전(왼쪽) 그림의 x
        public int MirrorX(int x) => HandCell.Width - 1 - x;

        // 방향을 반영한 점(왼쪽이면 x 반전)
        public PixelPoint PointFor(PixelPoint p, SpriteFacing facing) => facing == SpriteFacing.Left ? new PixelPoint(MirrorX(p.X), p.Y) : p;

        private static void CheckStage(int stage)
        {
            if (stage < 0 || stage >= StageCount)
            {
                throw new ArgumentOutOfRangeException(nameof(stage));
            }
        }
    }
}
