namespace Wizard.ViewLogic.Presentation
{
    // CONTRACTS 13. 불투명 핸들. Unity 쪽 구현이 실제 그림을 쥔다
    public readonly struct SpriteRef
    {
        public readonly int Handle;                       // 0 = 없음
        
        public SpriteRef(int handle)
        {
        Handle = handle;
        }
    }
}
