namespace Wizard.Server.Data
{
    // CONTRACTS 7.2. item_instance.origin. Paid 는 강화·매치·리그 코드가 읽지 않는다
    public enum ItemOrigin : byte
    {
        Earned = 0,
        Shop = 1,
        Trade = 2,
        Season = 3,
        Revenge = 4,
        Admin = 5,
        Paid = 6,
    }
}
