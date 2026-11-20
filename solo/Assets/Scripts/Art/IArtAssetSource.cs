using UnityEngine;

namespace Wizard.Solo.Art
{
    /// <summary>
    /// ArtLibrary가 그림·글·글꼴·셰이더를 읽는 곳. 실행 = ResourcesArtSource(Resources.Load, LoadAll 금지).
    /// 테스트는 디스크에서 읽는 가짜를 넣어 Resources 없이 자르기·캐시·내리기를 시험한다.
    /// 경로는 Resources 경로("Art/players/manifest" 꼴, 확장자 없음). 없으면 null.
    /// </summary>
    public interface IArtAssetSource
    {
        string LoadText(string resourcePath);
        Texture2D LoadTexture(string resourcePath);
        Font LoadFont(string resourcePath);
        Shader LoadShader(string resourcePath);
        /// <summary>LoadTexture로 받은 텍스처를 내린다(Release).</summary>
        void UnloadTexture(Texture2D texture);
    }

    /// <summary>Resources 폴더에서 읽는다. 처음 요청 때 한 장씩(00 문서 10절 "늦게 읽는다").</summary>
    public sealed class ResourcesArtSource : IArtAssetSource
    {
        public string LoadText(string resourcePath)
        {
            var t = Resources.Load<TextAsset>(resourcePath);
            if (t == null) return null;
            string s = t.text;
            Resources.UnloadAsset(t);
            return s;
        }

        public Texture2D LoadTexture(string resourcePath) => Resources.Load<Texture2D>(resourcePath);
        public Font LoadFont(string resourcePath) => Resources.Load<Font>(resourcePath);
        public Shader LoadShader(string resourcePath) => Resources.Load<Shader>(resourcePath);

        public void UnloadTexture(Texture2D texture)
        {
            if (texture != null) Resources.UnloadAsset(texture);
        }
    }
}
