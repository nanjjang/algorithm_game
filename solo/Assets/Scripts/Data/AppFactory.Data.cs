using Wizard.Solo.Data;

namespace Wizard.Solo
{
    /// <summary>F1 등록 자리(00 문서 2.1·4절 3단계): Resources/Data의 콘텐츠와 이름 표.</summary>
    public static partial class AppFactory
    {
        static partial void CreateContent(ref IContentDb content, ref INameTable names)
        {
            content = ContentDb.LoadFromResources();
            names = NameTable.LoadFromResources();
        }
    }
}
