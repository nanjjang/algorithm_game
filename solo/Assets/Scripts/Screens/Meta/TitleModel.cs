using System.Collections.Generic;
using Wizard.Solo.Data;
using Wizard.Solo.Meta;

namespace Wizard.Solo.Screens.Meta
{
    /// <summary>
    /// 칭호 규칙(01 10.5절, 08 11.1절, 엔진 없음). 가진 칭호 = 프로필에 적힌 기록·손맛·복수 칭호 + 다섯 캐릭터의 강화 단계 칭호
    /// <c>title.stage.N.bK</c>(지금 든 지팡이 단계에서 계산 — 단계가 떨어지면 칭호도 내려간다). 묶음 K = 그 단계 이하의 마일스톤 수
    /// (enhance.json milestones [4, 7, 10, 13] → +0~3 b0, +4~6 b1, +7~9 b2, +10~12 b3, +13 b4).
    /// 강화 단계 칭호를 고르면 titles.equipped에 그때의 bK 열쇠를 적고, 읽을 때는 Resolve가 지금 단계의 bK로 바꿔 읽는다.
    /// </summary>
    public static class TitleModel
    {
        public const string StagePrefix = "title.stage.";

        /// <summary>마일스톤(enhance.json milestones). 콘텐츠가 없으면 빈 목록(→ 언제나 b0).</summary>
        public static IReadOnlyList<int> Milestones(IContentDb db) =>
            db != null && db.Enhance != null && db.Enhance.Milestones != null ? db.Enhance.Milestones : (IReadOnlyList<int>)new int[0];

        /// <summary>단계 → 칭호 묶음 번호(b0~b4).</summary>
        public static int Band(IReadOnlyList<int> milestones, int stage)
        {
            int band = 0;
            if (milestones == null) return band;
            for (int i = 0; i < milestones.Count; i++)
                if (milestones[i] <= stage) band++;
            return band;
        }

        public static string StageTitleId(int character, int band) => StagePrefix + character + ".b" + band;

        /// <summary>그 캐릭터가 지금 든 지팡이의 단계(없으면 0).</summary>
        public static int HeldStage(ProfileData p, int character)
        {
            if (p == null) return 0;
            var c = p.Character(character);
            if (c == null) return 0;
            var w = p.Wand(c.EquippedWandId);
            return w != null ? w.Stage : 0;
        }

        /// <summary>강화 단계 칭호 열쇠면 캐릭터 번호, 아니면 0.</summary>
        public static int StageTitleCharacter(string titleId)
        {
            if (string.IsNullOrEmpty(titleId) || !titleId.StartsWith(StagePrefix)) return 0;
            int dot = titleId.IndexOf('.', StagePrefix.Length);
            if (dot < 0) return 0;
            return int.TryParse(titleId.Substring(StagePrefix.Length, dot - StagePrefix.Length), out int n) && n >= 1 && n <= Ids.Char.Count
                ? n : 0;
        }

        /// <summary>고를 수 있는 칭호(순서: 프로필에 얻은 순서 → 캐릭터 1~5의 강화 단계 칭호). 같은 열쇠는 한 번만.</summary>
        public static List<string> Available(ProfileData p, IReadOnlyList<int> milestones)
        {
            var list = new List<string>();
            if (p == null) return list;
            if (p.Titles != null && p.Titles.Owned != null)
                for (int i = 0; i < p.Titles.Owned.Count; i++)
                {
                    string t = p.Titles.Owned[i];
                    if (string.IsNullOrEmpty(t) || StageTitleCharacter(t) != 0 || list.Contains(t)) continue;
                    list.Add(t);
                }
            for (int c = 1; c <= Ids.Char.Count; c++)
            {
                if (p.Character(c) == null) continue;
                list.Add(StageTitleId(c, Band(milestones, HeldStage(p, c))));
            }
            return list;
        }

        /// <summary>붙인 칭호(지금 단계로 고친 열쇠). 없거나 더는 가지지 않은 칭호면 null.</summary>
        public static string Resolve(ProfileData p, IReadOnlyList<int> milestones)
        {
            if (p == null || p.Titles == null || string.IsNullOrEmpty(p.Titles.Equipped)) return null;
            string eq = p.Titles.Equipped;
            int ch = StageTitleCharacter(eq);
            if (ch != 0) return p.Character(ch) != null ? StageTitleId(ch, Band(milestones, HeldStage(p, ch))) : null;
            return p.Titles.Owned != null && p.Titles.Owned.Contains(eq) ? eq : null;
        }

        /// <summary>칭호를 붙인다(null = 칭호 없음). 가진 칭호가 아니면 false. 바꾸면 저장한다.</summary>
        public static bool Equip(IProfileStore store, string titleId, IReadOnlyList<int> milestones)
        {
            var p = store != null ? store.Current : null;
            if (p == null) return false;
            if (titleId != null && !Available(p, milestones).Contains(titleId)) return false;
            if (p.Titles == null) p.Titles = new TitlesData();
            if (p.Titles.Equipped == titleId) return true;
            p.Titles.Equipped = titleId;
            store.Save();
            return true;
        }
    }
}
