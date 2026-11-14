using Newtonsoft.Json.Linq;
using Wizard.Solo.Art;

namespace Wizard.Solo.View.Effects
{
    /// <summary>
    /// 캐릭터 교체 묶음의 효과 고르기. 효과 묶음 "char.N"(supplement/solo/char.N/manifest.json, Tools/art_disciple1·2.py)에
    /// 그 캐릭터 전용 그림이 있으면 공용 그림 대신 그것을 쓴다. 어느 공용 그림을 무엇으로 바꾸는지는 데이터(G16.json "character_fx")에 둔다:
    /// <code>"character_fx": { "char.2": { "bands": [3, 6, 9, 12, 13], "map": { "wand/trail_lightning": "fx/trail_band_{band}" } } }</code>
    /// 열쇠 = "공용 묶음/공용 열쇠", 값 = 캐릭터 묶음의 열쇠. "{band}"는 지팡이 단계의 띠 번호(bands = 띠마다 마지막 단계, 오름차순)로 바뀐다.
    /// 캐릭터 묶음에 그 열쇠가 없으면 공용 그림 그대로(표시만 바뀌고 규칙·수치는 그대로다).
    /// </summary>
    public static class CharacterFx
    {
        public const string Section = "character_fx";

        /// <summary>지팡이 단계 → 띠 번호(bands에서 단계 이하인 첫 칸). bands가 없으면 0.</summary>
        public static int Band(EffectsTuning t, string charId, int wandStage)
        {
            if (t == null || charId == null) return 0;
            var bands = t.Ints(Section, charId, "bands");
            for (int i = 0; i < bands.Length; i++)
                if (wandStage <= bands[i]) return i;
            return bands.Length > 0 ? bands.Length - 1 : 0;
        }

        /// <summary>그 캐릭터가 공용 (group, key) 대신 쓸 캐릭터 묶음 열쇠. 없으면 null.</summary>
        public static string Key(EffectsTuning t, string charId, string group, string key, int wandStage = 0)
        {
            if (t == null || charId == null || group == null || key == null) return null;
            var map = t.Token(Section, charId, "map") as JObject;
            var v = map != null ? map[group + "/" + key] : null;
            if (v == null || v.Type != JTokenType.String) return null;
            string mapped = (string)v;
            if (mapped.Contains("{band}"))
                mapped = mapped.Replace("{band}", Band(t, charId, wandStage).ToString(System.Globalization.CultureInfo.InvariantCulture));
            return mapped;
        }

        /// <summary>캐릭터 묶음 그림(있으면) 또는 공용 그림. art가 null이면 null.</summary>
        public static SpriteAnim Resolve(IArtLibrary art, EffectsTuning t, string charId, string group, string key, int wandStage = 0)
        {
            if (art == null || key == null) return null;
            string mapped = Key(t, charId, group, key, wandStage);
            if (mapped != null && art.EffectInfo(charId, mapped) != null) return art.Effect(charId, mapped);
            return art.Effect(group, key);
        }

        /// <summary>캐릭터 묶음에만 있는 그림(공용 자리 없음, 예 벼락 질주 도착). 없으면 null.</summary>
        public static SpriteAnim Own(IArtLibrary art, string charId, string key)
        {
            if (art == null || charId == null || key == null || art.EffectInfo(charId, key) == null) return null;
            return art.Effect(charId, key);
        }
    }
}
