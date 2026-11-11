namespace Wizard.Content
{
    // CONTRACTS 2.1. spec 14 3.15. 일곱 개. 서버 전체 채널은 없다(DECISIONS 2.7)
    public enum ChatChannel : byte
    {
        None = 0,
        Nearby = 1,
        Plaza = 2,
        Trade = 3,
        Whisper = 4,
        Friends = 5,
        DuelRoom = 6,
        System = 7,
    }
}
