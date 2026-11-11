namespace Wizard.MetaGen
{
    // .meta 에 쓰는 가져오기 블록의 종류. MetaTemplates 가 바이트로 바꾼다.
    public enum ImporterKind
    {
        GuidOnly = 0,
        Folder = 1,
        Mono = 2,
        Plugin = 3,
        AssemblyDefinition = 4,
        AssemblyDefinitionReference = 5,
        PackageManifest = 6,
        TextScript = 7,
        Default = 8,
    }
}
