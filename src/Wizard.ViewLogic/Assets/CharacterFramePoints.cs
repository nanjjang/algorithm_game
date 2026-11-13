using Wizard.ViewLogic.Common;

namespace Wizard.ViewLogic.Assets
{
    // 캐릭터 프레임 하나의 붙는 점(프레임 안 px, 왼쪽 위 원점). spec 14 5.3절 PlayerFrame, 4.6절 "2~5층은 이 점에 붙는다"
    public readonly struct CharacterFramePoints
    {
        public readonly PixelPoint Feet;
        public readonly PixelPoint Hand;
        public readonly PixelPoint Head;
        public readonly PixelPoint Shoulder;

        public CharacterFramePoints(PixelPoint feet, PixelPoint hand, PixelPoint head, PixelPoint shoulder)
        {
            Feet = feet;
            Hand = hand;
            Head = head;
            Shoulder = shoulder;
        }
    }
}
