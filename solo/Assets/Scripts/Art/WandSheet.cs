namespace Wizard.Solo.Art
{
    /// <summary>
    /// 지팡이 시트 칸 계산(09 요약서 3절, S14 5.4, TV-201~208). 원 구현 src/Wizard.ViewLogic/Assets/WandSheet.cs.
    /// 칸 크기·열 수는 manifest 값을 받는다(기본값은 manifest의 현재 값과 같다).
    /// </summary>
    public static class WandSheet
    {
        public const int StageCount = 14;
        public const int MaxStage = StageCount - 1;

        /// <summary>손 행: (방향 번호 × 2 + 자세) — down=0, right=1, up=2, hold=0, aim=1. 왼쪽은 right 행을 뒤집는다.</summary>
        public static int HandRow(Facing3 facing, bool aim) => (int)facing * 2 + (aim ? 1 : 0);

        public static PixelRect HandRect(int stage, Facing3 facing, bool aim, int cellW = 32, int cellH = 64)
        {
            stage = ClampStage(stage);
            return new PixelRect(stage * cellW, HandRow(facing, aim) * cellH, cellW, cellH);
        }

        /// <summary>단계 n → 열 n % 7, 행 n / 7.</summary>
        public static PixelRect InspectionRect(int stage, int columns = 7, int cell = 192)
        {
            stage = ClampStage(stage);
            if (columns < 1) columns = 1;
            return new PixelRect((stage % columns) * cell, (stage / columns) * cell, cell, cell);
        }

        public static PixelRect IconRect(int stage, int cell = 64)
        {
            stage = ClampStage(stage);
            return new PixelRect(stage * cell, 0, cell, cell);
        }

        /// <summary>지팡이 칸의 왼쪽 위 = 캐릭터 프레임의 hand 점 − 손잡이(16, 48). 예 hand (34,44) → (18,−4) (TV-208).</summary>
        public static PixelPoint AttachTopLeft(PixelPoint hand, PixelPoint grip)
        {
            return new PixelPoint(hand.X - grip.X, hand.Y - grip.Y);
        }

        public static PixelPoint AttachTopLeft(PixelPoint hand) => AttachTopLeft(hand, new PixelPoint(16, 48));

        /// <summary>왼쪽(반전) 그림에서 지팡이 단계 점의 x: x' = (칸 폭 − 1) − x [임시 U-04].</summary>
        public static int MirrorX(int x, int cellW = 32) => cellW - 1 - x;

        /// <summary>단계 묶음(I-G1): +0~3 → 0, +4~6 → 1, +7~9 → 2, +10~12 → 3, +13 → 4.</summary>
        public static int StageBand(int stage)
        {
            stage = ClampStage(stage);
            if (stage <= 3) return 0;
            if (stage <= 6) return 1;
            if (stage <= 9) return 2;
            if (stage <= 12) return 3;
            return 4;
        }

        /// <summary>I-G1 변형 이름(wand_tier_0_to_3 … wand_tier_13).</summary>
        public static string StageBandVariant(int stage)
        {
            switch (StageBand(stage))
            {
                case 0: return "wand_tier_0_to_3";
                case 1: return "wand_tier_4_to_6";
                case 2: return "wand_tier_7_to_9";
                case 3: return "wand_tier_10_to_12";
                default: return "wand_tier_13";
            }
        }

        public static int ClampStage(int stage) => stage < 0 ? 0 : stage > MaxStage ? MaxStage : stage;
    }
}
