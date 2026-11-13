using System.Collections.Generic;
using System.Globalization;
using Wizard.ViewLogic.Presentation.Limits;

namespace Wizard.ViewLogic.Presentation.WandLooks
{
    // 등록 검사(spec 04 12.3절 "덧씌움이 지켜야 할 것", 12.5절 구간 상한). 결과는 문제 문구 목록이고 비면 통과다.
    //  - 기본 자료: 시트 행 순서가 5.4절 행 공식과 같다, 모든 단계 요청량이 구간 상한 이하(15.12절 149).
    //  - 덧씌움: 위험색 금지(150), 단계 실루엣을 바꾸지 못함(151), 요청량 상한, 상점 출처는 항목 0개, 지팡이 종류가 같다.
    // 윤곽이 2px 넘게 벗어나는지(Wg 8.4)는 그림이 와야 볼 수 있어 여기서 보지 않는다
    public static class WandLookChecks
    {
        public static IReadOnlyList<string> CheckBase(WandLookSet set, ParticleBudgetTable budget)
        {
            var problems = new List<string>();
            IReadOnlyList<string> rows = set.Sheet.HandRows;
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i] != WandLookSheet.HandRowName(i))
                {
                    problems.Add(set.WandId + ": hand_rows[" + Num(i) + "] must be " + WandLookSheet.HandRowName(i));
                }
            }
            foreach (WandStageLook s in set.Stages)
            {
                CheckRequest(set.WandId + " +" + Num(s.Stage), s.Stage, s.Particles, budget, problems);
            }
            // 가장 단순한 형태는 단계와 무관하게 같다(DECISIONS 2.7, effects.md 17절)
            WandSimplestDecor first = set.Stage(0).DuelDecor;
            foreach (WandStageLook s in set.Stages)
            {
                if (s.DuelDecor.Muzzle != first.Muzzle || s.DuelDecor.Hit != first.Hit)
                {
                    problems.Add(set.WandId + " +" + Num(s.Stage) + ": duel_decor must be the same at every stage");
                }
            }
            foreach (WandDetachedRange d in set.Detached)
            {
                if (d.MinStage > d.MaxStage)
                {
                    problems.Add(set.WandId + ": detached " + d.Variant + " has an empty stage range");
                }
            }
            return problems;
        }

        public static IReadOnlyList<string> CheckSkin(WandLookSet set, WandSkin skin, ParticleBudgetTable budget)
        {
            var problems = new List<string>();
            string where = set.WandId + " skin " + Num(skin.SkinId);
            if (skin.WandKind != set.WandKind)
            {
                problems.Add(where + ": belongs to another wand");
            }
            if (skin.SkinId == WandLookKey.DefaultSkin)
            {
                problems.Add(where + ": skin id 0 is the default look");
            }
            if (skin.Source == WandSkinSource.Shop)
            {
                // 상점은 값만 정의하고 항목 0개로 출시한다(D 1.14)
                problems.Add(where + ": shop skins ship with zero entries");
            }
            CheckPalette(where, skin.Palette, problems);
            foreach (WandStagePatch p in skin.PerStage)
            {
                string at = where + " +" + Num(p.Stage);
                if (p.Stage < 0 || p.Stage >= WandLookSheet.StageCount)
                {
                    problems.Add(at + ": stage out of range");
                    continue;
                }
                if (p.SilhouetteStage != p.Stage)
                {
                    // 한정판 겉모습은 색과 장식만 바꾼다. 다른 단계의 그림을 가진 패치는 단계 실루엣을 바꾼다(DECISIONS 2.7)
                    problems.Add(at + ": changes the stage silhouette (carries +" + Num(p.SilhouetteStage) + ")");
                }
                CheckPalette(at, p.Palette, problems);
                if (p.Particles.HasValue)
                {
                    CheckRequest(at, p.Stage, p.Particles.Value, budget, problems);
                }
            }
            return problems;
        }

        // 요청량이 구간 상한(effects.md 9.3, spec 04 12.5절) 이하인지
        public static void CheckRequest(string where, int stage, WandParticleRequest r, ParticleBudgetTable budget, List<string> problems)
        {
            WandTierCap cap = budget.TierCap(stage);
            Over(where, "idle", r.Idle, cap.Idle, problems);
            Over(where, "muzzle", r.Muzzle, cap.Muzzle, problems);
            Over(where, "shed", r.Shed, cap.ShedPerBullet, problems);
            Over(where, "hit", r.Hit, cap.Hit, problems);
            int trailCap = cap.TrailLengthPx < budget.Trail.MaxLengthPx ? cap.TrailLengthPx : budget.Trail.MaxLengthPx;
            Over(where, "trail_px", r.TrailPx, trailCap, problems);
        }

        private static void CheckPalette(string where, IReadOnlyList<int> palette, List<string> problems)
        {
            foreach (int rgb in palette)
            {
                if (FlashColor.FromRgb(rgb).IsDanger)
                {
                    // 위험색은 적의 공격과 예고 전용이다(DECISIONS 2.2)
                    problems.Add(where + ": danger color #" + rgb.ToString("X6", CultureInfo.InvariantCulture));
                }
            }
        }

        private static void Over(string where, string what, int value, int cap, List<string> problems)
        {
            if (value < 0 || value > cap)
            {
                problems.Add(where + ": " + what + " " + Num(value) + " exceeds the tier cap " + Num(cap));
            }
        }

        private static string Num(int n) => n.ToString(CultureInfo.InvariantCulture);
    }
}
