namespace Wizard.Content
{
    // CONTRACTS 2.1. 비트 번호 = (int)ArtId. 값은 (ArtMask)(1 << (int)id)로 만든다
    [System.Flags]
    public enum ArtMask : ushort
    {
        None = 0,
    }
}
