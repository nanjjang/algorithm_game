namespace Wizard.ViewLogic.Presentation.WandLooks
{
    // spec 04 12.3절 SkinSource. 상점은 값만 정의하고 항목 0개(D 1.14). 번호는 더하기만 한다
    public enum WandSkinSource : byte
    {
        Default = 0,
        Record = 1,
        Season = 2,
        Trade = 3,
        Shop = 4,
    }
}
