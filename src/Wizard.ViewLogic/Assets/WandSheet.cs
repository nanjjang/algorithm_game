using System;
using System.Collections.Generic;
using Wizard.ViewLogic.Common;

namespace Wizard.ViewLogic.Assets
{
    // 지팡이 한 종류(wand.N)의 시트와 단계별 점(spec 14 5.4절). 칸 고르기는 5.4절 의사코드 그대로다:
    //   WandHandRect(stage, dir, pose)  = (stage × 칸 폭, 행 × 칸 높이)   행 = hand.rows 에서 "<dir>_<pose>" 의 위치
    //   WandInspectionRect(stage)       = ((stage % 열) × 폭, (stage / 열) × 높이)   — 14단계를 7×2 로
    //   WandIconRect(stage)             = (stage × 폭, 0)
    //   AttachWand: 지팡이 왼쪽 위 = 캐릭터 프레임의 Hand − 손잡이 기준점(grip)
    // 겉모습(skin_id)별 시트는 아직 없다. 지금은 겉모습 = 기본 하나 [임시]
    public sealed class WandSheet
    {
        public const string DownKey = "down";
        public const string RightKey = "right";
        public const string UpKey = "up";
        public const string HoldKey = "hold";
        public const string AimKey = "aim";
        public const char RowSeparator = '_';

        private readonly IReadOnlyList<string> _handRows;

        public WandSheet(string wandId, PixelPoint grip, SheetGrid hand, IReadOnlyList<string> handRows,
            SheetGrid inspection, SheetGrid icon, IReadOnlyList<WandStagePoints> stages)
        {
            WandId = wandId;
            Grip = grip;
            Hand = hand;
            _handRows = handRows;
            Inspection = inspection;
            Icon = icon;
            Stages = stages;
        }

        public string WandId { get; }

        // 손 그림 칸 안의 손잡이 기준점(MAN-wd grip = (16, 48))
        public PixelPoint Grip { get; }

        public SheetGrid Hand { get; }

        public IReadOnlyList<string> HandRows => _handRows;

        public SheetGrid Inspection { get; }

        public SheetGrid Icon { get; }

        public IReadOnlyList<WandStagePoints> Stages { get; }

        // 단계 수 = 손 그림의 열 수(0..13 → 14)
        public int StageCount => Hand.Columns;

        public PixelRect HandRect(int stage, SpriteFacing facing, WandPose pose)
        {
            CheckStage(stage);
            return Hand.CellRect(stage, HandRow(facing, pose));
        }

        public PixelRect InspectionRect(int stage)
        {
            CheckStage(stage);
            return Inspection.CellRect(stage);
        }

        public PixelRect IconRect(int stage)
        {
            CheckStage(stage);
            return Icon.CellRect(stage);
        }

        // hand.rows 에서의 행. Left 는 Right 의 행을 반전해 쓴다
        public int HandRow(SpriteFacing facing, WandPose pose)
        {
            string key = DirKey(facing) + RowSeparator + (pose == WandPose.Aim ? AimKey : HoldKey);
            for (int i = 0; i < _handRows.Count; i++)
            {
                if (string.Equals(_handRows[i], key, StringComparison.Ordinal))
                {
                    return i;
                }
            }
            throw new InvalidOperationException("hand sheet of " + WandId + " has no row " + key);
        }

        public WandStagePoints PointsOf(int stage)
        {
            CheckStage(stage);
            foreach (WandStagePoints p in Stages)
            {
                if (p.Stage == stage)
                {
                    return p;
                }
            }
            throw new InvalidOperationException("wand " + WandId + " has no points for this stage");
        }

        // 왼쪽(반전) 그림의 x: 칸 폭 32 이면 31 − x (spec 14 5.4절 [임시])
        public int MirrorX(int x) => Hand.Cell.Width - 1 - x;

        // AttachWand(charFrame, wandStage): 손잡이 기준점을 캐릭터 프레임의 손 점에 맞춘 지팡이 칸의 왼쪽 위
        public PixelPoint AttachTopLeft(PixelPoint hand) => AttachTopLeft(hand, Grip);

        public static PixelPoint AttachTopLeft(PixelPoint hand, PixelPoint grip) => new PixelPoint(hand.X - grip.X, hand.Y - grip.Y);

        public static string DirKey(SpriteFacing facing)
        {
            switch (facing)
            {
                case SpriteFacing.Down: return DownKey;
                case SpriteFacing.Right:
                case SpriteFacing.Left: return RightKey;
                case SpriteFacing.Up: return UpKey;
                default: throw new ArgumentOutOfRangeException(nameof(facing), facing, null);
            }
        }

        private void CheckStage(int stage)
        {
            if (stage < 0 || stage >= StageCount)
            {
                throw new ArgumentOutOfRangeException(nameof(stage));
            }
        }
    }
}
