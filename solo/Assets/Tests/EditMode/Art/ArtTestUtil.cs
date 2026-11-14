using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Wizard.Solo.Art;

namespace Wizard.Solo.Tests.Art
{
    /// <summary>테스트 공용: 프로젝트 경로, 복사된 manifest 읽기, 디스크에서 읽는 가짜 그림 원천.</summary>
    public static class ArtTestUtil
    {
        public static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName.Replace('\\', '/');
        public static string ArtRoot => ProjectRoot + "/Assets/Resources/Art";

        public static string ReadArt(string relative) => File.ReadAllText(ArtRoot + "/" + relative, System.Text.Encoding.UTF8);
    }

    /// <summary>
    /// Assets/Resources 아래 파일을 직접 읽는 원천(임포터·meta 없이도 돈다). 텍스처는 PNG를 LoadImage로 올리고 몇 번 읽었는지 센다.
    /// </summary>
    public sealed class DiskArtSource : IArtAssetSource
    {
        readonly string _resources;
        public readonly List<string> TextureLoads = new List<string>();
        public readonly List<string> TextureUnloads = new List<string>();
        public bool ProvideShaders;

        public DiskArtSource(string resourcesRoot = null)
        {
            _resources = resourcesRoot ?? ArtTestUtil.ProjectRoot + "/Assets/Resources";
        }

        public string LoadText(string resourcePath)
        {
            foreach (var ext in new[] { ".json", ".txt" })
            {
                string p = _resources + "/" + resourcePath + ext;
                if (File.Exists(p)) return File.ReadAllText(p, System.Text.Encoding.UTF8);
            }
            return null;
        }

        public Texture2D LoadTexture(string resourcePath)
        {
            string p = _resources + "/" + resourcePath + ".png";
            if (!File.Exists(p)) return null;
            TextureLoads.Add(resourcePath);
            var t = new Texture2D(2, 2, TextureFormat.RGBA32, false) { name = resourcePath, hideFlags = HideFlags.DontSave };
            t.LoadImage(File.ReadAllBytes(p), false);
            return t;
        }

        public Font LoadFont(string resourcePath) => null;

        public Shader LoadShader(string resourcePath) => ProvideShaders ? Resources.Load<Shader>(resourcePath) : null;

        public void UnloadTexture(Texture2D texture)
        {
            if (texture == null) return;
            TextureUnloads.Add(texture.name);
            Object.DestroyImmediate(texture);
        }
    }
}
