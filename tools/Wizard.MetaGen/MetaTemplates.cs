using System;
using System.Collections.Generic;
using System.IO;

namespace Wizard.MetaGen
{
    // .meta 의 바이트(CONTRACTS 11.5, T-34). Unity 가 연 뒤에도 바뀌지 않도록 Unity 6000.3.20f1 이 새 자산에 쓰는 꼴을 그대로 쓴다.
    // 이 표의 꼴은 이 기기의 Unity 6000.3.20f1 을 배치 모드로 열어 .meta 가 없는 자산에 Unity 가 쓴 바이트에서 가져왔다.
    //
    // - .cs(MonoImporter), .dll(PluginImporter): 가져오기 설정이 기본값이면 Unity 6 은 가져오기 블록을 쓰지 않는다.
    //   "fileFormatVersion: 2", "guid: …" 두 줄뿐이고 마지막 줄바꿈도 없다.
    // - 폴더: folderAsset: yes + DefaultImporter 블록.
    // - .asmdef: AssemblyDefinitionImporter, .asmref: AssemblyDefinitionReferenceImporter.
    // - 패키지 뿌리의 package.json(src/<패키지>/package.json): PackageManifestImporter. 그 밖의 .json 은 TextScriptImporter.
    // - 텍스트 자산(.txt .json .md .xml .bytes .csv .fnt .htm .html .yaml): TextScriptImporter.
    // - 전용 가져오기가 없는 확장자(.rsp .csproj .props .sh .mjs .yml .unity, 모르는 확장자): DefaultImporter.
    // - 전용 가져오기가 있는 자산(그림, 소리, 영상, 모델, 글꼴, 셰이더, UI 툴킷, 직렬화 자산, 입력 동작 등. GuidOnlyExtensions):
    //   블록이 Unity 판과 자산 내용에 따라 길게 달라지므로 GUID 두 줄만 쓴다. Unity 는 처음 열 때 GUID 를 그대로 두고 블록을 채운다.
    //   DefaultImporter 블록을 쓰면 안 된다: Unity 는 확장자의 가져오기(예: TextureImporter)로 가져오면서도 틀린 블록을
    //   고쳐 쓰지 않아, 기본 설정으로 굳은 채 남는다(6000.3.20f1 에서 확인). 그 파일의 설정은 반입 작업(M9-AST-03)이 맡는다.
    public static class MetaTemplates
    {
        private const string Header = "fileFormatVersion: 2\nguid: ";

        private const string CommonTail =
            "  externalObjects: {}\n" +
            "  userData: \n" +
            "  assetBundleName: \n" +
            "  assetBundleVariant: \n";

        private static readonly HashSet<string> TextScriptExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".txt", ".json", ".md", ".xml", ".bytes", ".csv", ".fnt", ".htm", ".html", ".yaml",
        };

        // 전용 가져오기가 있는 확장자(Unity 기본 가져오기와 이 프로젝트가 쓰는 패키지의 것). 빠진 확장자는 DefaultImporter 로 쓰이며,
        // 그래도 Unity 는 확장자의 가져오기로 가져오고 GUID 를 지킨다(블록만 기본값으로 남는다).
        private static readonly HashSet<string> GuidOnlyExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // 그림
            ".png", ".jpg", ".jpeg", ".tga", ".psd", ".tif", ".tiff", ".bmp", ".gif", ".exr", ".hdr", ".iff", ".pict", ".pic", ".pct",
            // 소리
            ".wav", ".mp3", ".ogg", ".aif", ".aiff", ".flac", ".mod", ".it", ".s3m", ".xm",
            // 영상
            ".mp4", ".mov", ".webm", ".ogv", ".m4v", ".avi", ".asf", ".wmv", ".mpg", ".mpeg", ".vp8", ".dv",
            // 모델
            ".fbx", ".obj", ".blend", ".dae", ".3ds", ".dxf", ".max", ".ma", ".mb", ".c4d", ".lxo", ".skp",
            // 글꼴
            ".ttf", ".otf", ".ttc", ".dfont",
            // 셰이더
            ".shader", ".compute", ".hlsl", ".cginc", ".glslinc", ".raytrace", ".shadergraph", ".shadersubgraph", ".shadervariants",
            // UI 툴킷
            ".uss", ".tss", ".uxml",
            // 직렬화 자산(NativeFormatImporter 의 mainObjectFileID 는 자산 종류마다 다르다)
            ".asset", ".mat", ".prefab", ".anim", ".controller", ".overridecontroller", ".mask", ".physicmaterial",
            ".physicsmaterial2d", ".guiskin", ".fontsettings", ".mixer", ".rendertexture", ".cubemap", ".flare",
            ".spriteatlas", ".spriteatlasv2", ".scenetemplate", ".playable", ".signal", ".lighting", ".terrainlayer",
            ".brush", ".giparams", ".preset",
            // 패키지의 ScriptedImporter
            ".inputactions",
            // 네이티브 플러그인
            ".so", ".a", ".dylib", ".bundle", ".jar", ".aar", ".jslib", ".jspre", ".m", ".mm", ".c", ".cpp", ".h", ".swift", ".java",
        };

        public static ImporterKind KindFor(AssetEntry entry)
        {
            if (entry.IsDirectory)
            {
                return ImporterKind.Folder;
            }
            string name = Path.GetFileName(entry.Path);
            string ext = Path.GetExtension(name);
            if (string.Equals(ext, ".cs", StringComparison.OrdinalIgnoreCase))
            {
                return ImporterKind.Mono;
            }
            if (string.Equals(ext, ".dll", StringComparison.OrdinalIgnoreCase))
            {
                return ImporterKind.Plugin;
            }
            if (string.Equals(ext, ".asmdef", StringComparison.OrdinalIgnoreCase))
            {
                return ImporterKind.AssemblyDefinition;
            }
            if (string.Equals(ext, ".asmref", StringComparison.OrdinalIgnoreCase))
            {
                return ImporterKind.AssemblyDefinitionReference;
            }
            if (IsPackageManifest(entry.Path))
            {
                return ImporterKind.PackageManifest;
            }
            if (TextScriptExtensions.Contains(ext))
            {
                return ImporterKind.TextScript;
            }
            if (GuidOnlyExtensions.Contains(ext))
            {
                return ImporterKind.GuidOnly;
            }
            return ImporterKind.Default;
        }

        // src/<패키지>/package.json 만 UPM 패키지의 매니페스트다(ARCHITECTURE 4.2). unity/Assets 아래의 package.json 은 보통 텍스트다.
        public static bool IsPackageManifest(string repoRelativePath)
        {
            string[] parts = repoRelativePath.Split('/');
            return parts.Length == 3 && parts[0] == "src" && parts[2] == "package.json";
        }

        public static string Render(ImporterKind kind, string guid)
        {
            switch (kind)
            {
                case ImporterKind.Mono:
                case ImporterKind.Plugin:
                case ImporterKind.GuidOnly:
                    return Header + guid;
                case ImporterKind.Folder:
                    return Header + guid + "\nfolderAsset: yes\nDefaultImporter:\n" + CommonTail;
                case ImporterKind.Default:
                    return Header + guid + "\nDefaultImporter:\n" + CommonTail;
                case ImporterKind.TextScript:
                    return Header + guid + "\nTextScriptImporter:\n" + CommonTail;
                case ImporterKind.AssemblyDefinition:
                    return Header + guid + "\nAssemblyDefinitionImporter:\n" + CommonTail;
                case ImporterKind.AssemblyDefinitionReference:
                    return Header + guid + "\nAssemblyDefinitionReferenceImporter:\n" + CommonTail;
                case ImporterKind.PackageManifest:
                    return Header + guid + "\nPackageManifestImporter:\n" + CommonTail;
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, "unknown importer kind");
            }
        }

        public static string RenderFor(AssetEntry entry)
        {
            return Render(KindFor(entry), MetaGuid.ForPath(entry.Path));
        }
    }
}
