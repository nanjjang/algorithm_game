using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Wizard.Solo.Art;
using Wizard.Solo.Data;
using Wizard.Solo.Meta;

namespace Wizard.Solo.Screens.Meta
{
    /// <summary>옷장 미리보기의 동작(08 11.2절, D-73).</summary>
    public enum WardrobePose { Idle = 0, Walk = 1, Sit = 2, Emote = 3 }

    /// <summary>격자 한 칸: 원래 모습(Id null) 또는 얻은 꾸미기.</summary>
    public struct WardrobeCell
    {
        public string Id;
        public bool Worn;
        public bool IsOriginal => Id == null;
    }

    /// <summary>
    /// 옷장 규칙(08 11.2절, 09 2.4절, 01 10.5절, 엔진 없음).
    /// - 분류 12개(cosmetic_cat.*). 꾸미기 id는 "cosmetic.&lt;분류&gt;.&lt;이름&gt;"이고 격자에는 **얻은 것만** 나온다(빈 칸·잠긴 칸·상점 표시 없음, TV-197).
    ///   첫 칸은 "원래 모습"(그 분류를 벗는다). 입는 중 = cosmetics.equipped[분류].
    /// - 야생 물: 그 캐릭터의 누적 웨이브가 문턱(DataSolo/G11.json wild_stain_total_waves = 0/100/500/2000) 이상인 수가 단계(1~4).
    ///   되돌아가지 않는다(wild_stain_stage_max), 낮은 단계·원래 모습(1단계)으로 입기는 된다(wild_stain_worn).
    /// </summary>
    public static class WardrobeModel
    {
        /// <summary>12분류(이름 열쇠 cosmetic_cat.&lt;분류&gt;). 순서 = 분류 목록 순서.</summary>
        public static readonly string[] Categories =
        {
            "headwear", "cloak", "wand", "trail", "footprints", "kill_effect", "seat", "emote", "crest", "border", "title", "ritual"
        };

        /// <summary>"옷" 분류(망토·옷 무늬). 맨 위에 야생 물 단계 고르기가 붙는다.</summary>
        public const string ClothesCategory = "cloak";
        public const string CosmeticPrefix = "cosmetic.";
        public const string WildStainNamePrefix = "wild_stain.";

        public static string CategoryNameKey(string category) => "cosmetic_cat." + category;
        public static string WildStainNameKey(int stage) => WildStainNamePrefix + stage;

        /// <summary>"cosmetic.&lt;분류&gt;.x" → 분류. 꼴이 다르면 null.</summary>
        public static string CategoryOf(string cosmeticId)
        {
            if (string.IsNullOrEmpty(cosmeticId) || !cosmeticId.StartsWith(CosmeticPrefix)) return null;
            int dot = cosmeticId.IndexOf('.', CosmeticPrefix.Length);
            return dot > CosmeticPrefix.Length ? cosmeticId.Substring(CosmeticPrefix.Length, dot - CosmeticPrefix.Length) : null;
        }

        /// <summary>그 분류에서 얻은 꾸미기(얻은 순서, 같은 id는 한 번).</summary>
        public static List<string> Owned(ProfileData p, string category)
        {
            var list = new List<string>();
            if (p == null || p.Cosmetics == null || p.Cosmetics.Owned == null) return list;
            for (int i = 0; i < p.Cosmetics.Owned.Count; i++)
            {
                string id = p.Cosmetics.Owned[i];
                if (CategoryOf(id) == category && !list.Contains(id)) list.Add(id);
            }
            return list;
        }

        /// <summary>그 분류에서 입는 중인 것(가진 것만. 없으면 null = 원래 모습).</summary>
        public static string Worn(ProfileData p, string category)
        {
            if (p == null || p.Cosmetics == null || p.Cosmetics.Equipped == null) return null;
            if (!p.Cosmetics.Equipped.TryGetValue(category, out var id) || string.IsNullOrEmpty(id)) return null;
            return p.Cosmetics.Owned != null && p.Cosmetics.Owned.Contains(id) ? id : null;
        }

        /// <summary>격자 칸: 원래 모습 + 얻은 것. 잠긴 칸·빈 칸은 만들지 않는다.</summary>
        public static List<WardrobeCell> Cells(ProfileData p, string category)
        {
            string worn = Worn(p, category);
            var cells = new List<WardrobeCell> { new WardrobeCell { Id = null, Worn = worn == null } };
            var owned = Owned(p, category);
            for (int i = 0; i < owned.Count; i++) cells.Add(new WardrobeCell { Id = owned[i], Worn = owned[i] == worn });
            return cells;
        }

        /// <summary>입힌다(null = 원래 모습). 가진 것이 아니면 false. 바뀌면 저장한다.</summary>
        public static bool Wear(IProfileStore store, string category, string cosmeticId)
        {
            var p = store != null ? store.Current : null;
            if (p == null || string.IsNullOrEmpty(category)) return false;
            if (cosmeticId != null && (CategoryOf(cosmeticId) != category || p.Cosmetics == null || p.Cosmetics.Owned == null
                || !p.Cosmetics.Owned.Contains(cosmeticId))) return false;
            if (p.Cosmetics == null) p.Cosmetics = new CosmeticsData();
            if (p.Cosmetics.Equipped == null) p.Cosmetics.Equipped = new Dictionary<string, string>();
            string before = Worn(p, category);
            if (before == cosmeticId) return true;
            if (cosmeticId == null) p.Cosmetics.Equipped.Remove(category);
            else p.Cosmetics.Equipped[category] = cosmeticId;
            store.Save();
            return true;
        }

        /// <summary>계정에 묶인 꾸미기(복수 보상 금 간 거울 조각, 05 10.3절).</summary>
        public static bool IsBound(string cosmeticId) => cosmeticId == Ids.Cosmetic.CrackedMirrorShard;

        /// <summary>얻은 곳 이름 열쇠(str.wardrobe.source.*). 프로필에 얻은 곳이 적혀 있지 않아 알 수 있는 것만: 복수 보상 = 기록. 모르면 null(줄을 그리지 않는다).</summary>
        public static string SourceKey(string cosmeticId) =>
            cosmeticId == Ids.Cosmetic.CrackedMirrorShard ? MetaKeys.WardrobeSourceRecord : null;

        // ---------------------------------------------------------------- 야생 물

        /// <summary>야생 물 문턱(누적 웨이브). DataSolo/G11.json wild_stain_total_waves. 없으면 빈 목록.</summary>
        public static List<int> StainThresholds(IContentDb db)
        {
            var list = new List<int>();
            var j = db != null ? db.SoloExtra("G11") : null;
            if (j != null && j["wild_stain_total_waves"] is JArray arr)
                foreach (var t in arr)
                    if (t.Type == JTokenType.Integer) list.Add((int)t);
            return list;
        }

        /// <summary>누적 웨이브 → 단계(문턱 이상인 수, 최소 1).</summary>
        public static int StainStage(int totalWaves, IReadOnlyList<int> thresholds)
        {
            int stage = 0;
            if (thresholds != null)
                for (int i = 0; i < thresholds.Count; i++)
                    if (totalWaves >= thresholds[i]) stage++;
            return stage < 1 ? 1 : stage;
        }

        /// <summary>그 캐릭터가 입을 수 있는 가장 높은 단계(저장된 최고와 누적 웨이브 단계 중 큰 것 — 되돌아가지 않는다).</summary>
        public static int StainMax(CharacterRecord rec, IReadOnlyList<int> thresholds)
        {
            if (rec == null) return 1;
            int s = StainStage(rec.TotalWaves, thresholds);
            return rec.WildStainStageMax > s ? rec.WildStainStageMax : s;
        }

        /// <summary>입는 단계(1~최고).</summary>
        public static int StainWorn(CharacterRecord rec, IReadOnlyList<int> thresholds)
        {
            if (rec == null) return 1;
            int max = StainMax(rec, thresholds);
            int w = rec.WildStainWorn;
            return w < 1 ? 1 : w > max ? max : w;
        }

        /// <summary>야생 물 단계를 입는다(1 = 원래 모습). 최고 단계보다 높으면 false. 바뀌면 저장한다.</summary>
        public static bool PickStain(IProfileStore store, int character, int stage, IReadOnlyList<int> thresholds)
        {
            var p = store != null ? store.Current : null;
            var rec = p != null ? p.Character(character) : null;
            if (rec == null) return false;
            int max = StainMax(rec, thresholds);
            if (stage < 1 || stage > max) return false;
            bool changed = rec.WildStainWorn != stage || rec.WildStainStageMax < max;
            rec.WildStainWorn = stage;
            if (rec.WildStainStageMax < max) rec.WildStainStageMax = max;
            if (changed) store.Save();
            return true;
        }

        // ---------------------------------------------------------------- 미리보기

        /// <summary>방향 돌리기: 아래 → 옆 → 위 → 아래.</summary>
        public static Facing3 NextFacing(Facing3 f) => f == Facing3.Down ? Facing3.Right : f == Facing3.Right ? Facing3.Up : Facing3.Down;

        /// <summary>동작 → 몸 클립 이름(players/manifest.json, 방향 접미사 없이). 감정표현은 인사(wave).</summary>
        public static string ClipOf(WardrobePose pose)
        {
            switch (pose)
            {
                case WardrobePose.Walk: return "walk";
                case WardrobePose.Sit: return "sit";
                case WardrobePose.Emote: return "wave";
                default: return "idle";
            }
        }

        /// <summary>그 동작·방향의 클립이 없으면 서 있는 그림으로 대신한다(D-73). has(클립, 방향) = 그림이 있나.</summary>
        public static string ResolveClip(WardrobePose pose, Facing3 facing, System.Func<string, Facing3, bool> has)
        {
            string clip = ClipOf(pose);
            if (pose == WardrobePose.Idle || has == null) return clip;
            return has(clip, facing) ? clip : ClipOf(WardrobePose.Idle);
        }

        /// <summary>같은 단추를 다시 누르면 서 있기로 돌아간다.</summary>
        public static WardrobePose Toggle(WardrobePose current, WardrobePose pressed) => current == pressed ? WardrobePose.Idle : pressed;
    }
}
