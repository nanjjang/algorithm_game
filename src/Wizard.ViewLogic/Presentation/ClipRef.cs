namespace Wizard.ViewLogic.Presentation
{
    // CONTRACTS 13. 불투명 핸들
    public readonly struct ClipRef
    {
        public readonly int Handle;                       // 0 = 없음
        
        public ClipRef(int handle)
        {
        Handle = handle;
        }
    }
}
