namespace Wizard.Solo.Screens.Camp
{
    /// <summary>
    /// 조립 화면의 자리 번호. 판 중 명령(RunCommand.LoadoutMove의 A·B)과 같은 규칙(Core/Sim/RunTypes.cs):
    /// 0 = 코어, 1..L = 연결 칸, 100 + i = 보관함 i번째, 300 + i = 서고 블록 대기 칸 i번째, −1 = 없음(빼기).
    /// 200 + k는 이 화면만 쓰는 자리다: 판 밖(Library·LoadoutEdit)의 서고 칸 k번째(16종 표의 칸 번호). 명령으로 내보내지 않는다.
    /// </summary>
    public static class SlotRefs
    {
        public const int Core = 0;
        public const int StorageBase = 100;
        public const int LibraryBase = 200;
        public const int BenchBase = 300;
        public const int None = -1;

        public static int Link(int linkIndex0) => linkIndex0 + 1;
        public static int Storage(int i) => StorageBase + i;
        public static int Bench(int i) => BenchBase + i;
        public static int Library(int k) => LibraryBase + k;

        public static bool IsWand(int r, int linkCount) => r >= Core && r <= linkCount;
        public static bool IsStorage(int r) => r >= StorageBase && r < LibraryBase;
        public static bool IsLibrary(int r) => r >= LibraryBase && r < BenchBase;
        public static bool IsBench(int r) => r >= BenchBase;
        public static int IndexOf(int r) => IsBench(r) ? r - BenchBase : IsLibrary(r) ? r - LibraryBase : IsStorage(r) ? r - StorageBase : r;
    }
}
