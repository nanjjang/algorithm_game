using System;
using UnityEditor;
using UnityEngine;

namespace Wizard.Solo.Editor
{
    /// <summary>임포트 설정 값(00 문서 10절, 09 요약서 1절). 테스트가 이 값을 대응표의 그림 크기와 맞춰 본다.</summary>
    public static class ArtImportRules
    {
        public const string ArtFolder = "Assets/Resources/Art/";
        public const int PixelsPerUnit = 64;
        /// <summary>고정 8192: 2048을 넘는 그림이 있다(field.weather.rain 7680×128, field.tower 2560×128, ritual/screen_crack 1920×3240).
        /// 줄어들면 manifest rect가 전부 어긋난다.</summary>
        public const int MaxTextureSize = 8192;

        public static bool AppliesTo(string assetPath)
        {
            return assetPath != null && assetPath.Replace('\\', '/').StartsWith(ArtFolder, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Assets/Resources/Art 아래 텍스처 = Sprite(Single), FullRect, Point, 압축 없음, PPU 64, mipmap 없음, 읽기 불필요,
    /// alphaIsTransparency, sRGB, 최대 크기 8192, npotScale None. TTF = Dynamic, HintedRaster, 글꼴 데이터 포함.
    /// 시트 자르기는 런타임(ArtLibrary)이 하므로 슬라이스를 meta에 굽지 않는다(Single).
    /// </summary>
    public sealed class ArtImportPostprocessor : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!ArtImportRules.AppliesTo(assetPath)) return;
            var ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.spritePixelsPerUnit = ArtImportRules.PixelsPerUnit;
            ti.filterMode = FilterMode.Point;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.mipmapEnabled = false;
            ti.isReadable = false;
            ti.alphaIsTransparency = true;
            ti.sRGBTexture = true;
            ti.npotScale = TextureImporterNPOTScale.None;
            ti.maxTextureSize = ArtImportRules.MaxTextureSize;
            ti.wrapMode = TextureWrapMode.Clamp;

            var s = new TextureImporterSettings();
            ti.ReadTextureSettings(s);
            s.spriteMeshType = SpriteMeshType.FullRect;
            s.spriteGenerateFallbackPhysicsShape = false;
            ti.SetTextureSettings(s);

            var def = ti.GetDefaultPlatformTextureSettings();
            def.maxTextureSize = ArtImportRules.MaxTextureSize;
            def.textureCompression = TextureImporterCompression.Uncompressed;
            def.format = TextureImporterFormat.RGBA32;
            ti.SetPlatformTextureSettings(def);
            // 플랫폼별 덮어쓰기가 있으면 같은 값으로(줄어들거나 압축되지 않게).
            foreach (var platform in new[] { "Standalone" })
            {
                var p = ti.GetPlatformTextureSettings(platform);
                if (!p.overridden) continue;
                p.maxTextureSize = ArtImportRules.MaxTextureSize;
                p.textureCompression = TextureImporterCompression.Uncompressed;
                p.format = TextureImporterFormat.RGBA32;
                ti.SetPlatformTextureSettings(p);
            }
        }

        void OnPreprocessAsset()
        {
            if (!ArtImportRules.AppliesTo(assetPath)) return;
            if (assetImporter is TrueTypeFontImporter fi)
            {
                fi.fontTextureCase = FontTextureCase.Dynamic;
                fi.fontRenderingMode = FontRenderingMode.HintedRaster;
                fi.includeFontData = true;
            }
        }
    }
}
