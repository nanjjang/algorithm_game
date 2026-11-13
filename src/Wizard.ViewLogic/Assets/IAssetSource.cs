namespace Wizard.ViewLogic.Assets
{
    // 매니페스트를 읽는 곳. 경로는 art/production 기준이고 '/'로 나눈다("ui/manifest.json").
    // dotnet 시험과 도구는 DirectoryAssetSource, Unity 쪽(M9-AST-03)은 반입된 텍스트 에셋으로 구현한다.
    // 로더는 파일 이름을 해석하지 않는다. 그림 파일은 열지 않고 있는지만 묻는다(UI external, spec 14 5.7절)
    public interface IAssetSource
    {
        bool TryReadText(string path, out string text);

        bool Exists(string path);
    }
}
