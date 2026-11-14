using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Wizard.Solo.Art
{
    /// <summary>Resources/Art/solo_presentation.json(manifest에 없는 표시 값, D-37). 파일이 없거나 깨지면 Defaults.</summary>
    public sealed class ArtPresentation
    {
        public const string ResourcePath = ArtAliases.ResourceRoot + "solo_presentation";

        /// <summary>몬스터 클립 재생 속도(manifest에 fps가 없다). 파일이 없을 때의 값도 D-37의 8.</summary>
        public float MonsterClipFps = DefaultMonsterFps;
        public readonly HashSet<string> MonsterClipOnce = new HashSet<string>();

        const float DefaultMonsterFps = 8f;

        public bool MonsterClipLoops(string clip) => clip == null || !MonsterClipOnce.Contains(clip);

        public static ArtPresentation Parse(string json)
        {
            var p = new ArtPresentation();
            if (string.IsNullOrEmpty(json)) { p.MonsterClipOnce.Add("death"); return p; }
            try
            {
                var o = JObject.Parse(json);
                float fps = ManifestParser.Float(o["monster_clip_fps"], DefaultMonsterFps);
                if (fps > 0f) p.MonsterClipFps = fps;
                if (o["monster_clip_once"] is JArray once)
                    foreach (var t in once) { var s = ManifestParser.Str(t); if (s != null) p.MonsterClipOnce.Add(s); }
            }
            catch (Newtonsoft.Json.JsonException)
            {
                p.MonsterClipOnce.Add("death");
            }
            return p;
        }
    }
}
