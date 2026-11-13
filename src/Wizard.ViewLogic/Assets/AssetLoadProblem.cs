namespace Wizard.ViewLogic.Assets
{
    // 매니페스트를 읽다 만난 문제 하나. File 은 art/production 기준(별칭 표는 data/presentation/asset_aliases.json),
    // Pointer 는 그 파일 안의 위치. 문구는 개발 로그용 영어다(화면에 보이지 않는다)
    public sealed class AssetLoadProblem
    {
        public AssetLoadProblem(string file, string pointer, string message)
        {
            File = file;
            Pointer = pointer;
            Message = message;
        }

        public string File { get; }

        public string Pointer { get; }

        public string Message { get; }

        public override string ToString() => File + (Pointer.Length > 0 ? "#" + Pointer : "") + ": " + Message;
    }
}
