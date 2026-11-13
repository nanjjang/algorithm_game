namespace Wizard.ViewLogic.Assets
{
    // 그림의 방향 3 + 반전(spec 14 4.6절 "옆 방향은 한쪽만 그리고 좌우로 뒤집는다", 5.3절 mirrorRightForLeft).
    // Left 는 Right 그림을 반전해 쓴다
    public enum SpriteFacing
    {
        Down,
        Right,
        Up,
        Left,
    }
}
