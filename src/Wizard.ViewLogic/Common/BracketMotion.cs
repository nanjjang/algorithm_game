namespace Wizard.ViewLogic.Common
{
    // 괄호 겹그림의 꼴(spec 14 4.3 "괄호 겹그림"). Moving = 키보드 커서(2프레임으로 벌어졌다 오므라든다), Still = 확정(멈춤)
    public enum BracketMotion
    {
        None = 0,
        Moving = 1,
        Still = 2,
    }
}
