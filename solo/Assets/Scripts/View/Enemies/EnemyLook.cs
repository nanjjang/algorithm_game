using System;
using System.Collections.Generic;
using UnityEngine;

namespace Wizard.Solo.View.Enemies
{
    /// <summary>적 몸 그림의 고르기 규칙(순수 함수). 09 요약서 4절, 05 요약서 1.1·7절.</summary>
    public static class EnemyLook
    {
        /// <summary>그림은 좌우 반전만(05 요약서 4절: flipX = facing.x &lt; 0).</summary>
        public static bool FlipX(Vector2 facing) => facing.x < 0f;

        /// <summary>
        /// 그릴 manifest 항목과 클립을 고른다.
        /// 엘리트는 바탕 종 그림을 쓰되, 엘리트 전용 항목이 그 클립을 가지면 그것(elite.bat_dusk.dive의 dive, elite.wisp_stray.twin의 hover).
        /// 바탕 그림에 없는 클립을 "&lt;id&gt;.ground" 항목이 가지면 그것(boss.storm_owl.ground의 land).
        /// 아무 데도 없으면 바탕 항목 + 원래 클립 이름(그림 창고가 첫 클립으로 대신한다).
        /// </summary>
        public static void ResolveClip(Func<string, IReadOnlyList<string>> clipNames, string defId, bool isElite, string clip,
            out string entryId, out string clipName)
        {
            clipName = string.IsNullOrEmpty(clip) ? "idle" : clip;
            entryId = defId;
            if (defId == null || clipNames == null) return;
            if (isElite && defId.StartsWith("enemy.", StringComparison.Ordinal))
            {
                string key = defId.Substring("enemy.".Length);
                string own = "elite." + key + "." + clipName;
                if (Has(clipNames, own, clipName)) { entryId = own; return; }
                string twin = "elite." + key + ".twin";
                if (Has(clipNames, twin, clipName)) { entryId = twin; return; }
            }
            if (Has(clipNames, defId, clipName)) return;
            string ground = defId + ".ground";
            if (Has(clipNames, ground, clipName)) entryId = ground;
        }

        static bool Has(Func<string, IReadOnlyList<string>> clipNames, string entry, string clip)
        {
            var list = clipNames(entry);
            if (list == null) return false;
            for (int i = 0; i < list.Count; i++) if (list[i] == clip) return true;
            return false;
        }

        /// <summary>
        /// 몸 외곽선(셰이더 _OutlineColor/_OutlineSize, MaterialPropertyBlock): 엘리트 = 금색 #FFD24A 2px(05 요약서 7절),
        /// 그 밖 = 밝은 림 #EAF2FF 2px, 세기 = 시간대 림 %(낮 20 / 해질녘 40 / 밤·보스 60)를 알파로.
        /// </summary>
        public static void Outline(bool elite, int rimPercent, EnemyViewTuning t, out Color32 color, out float sizePx)
        {
            if (elite)
            {
                color = t.EliteGold;
                sizePx = t.EliteOutlinePx;
                return;
            }
            color = DangerColor.WithAlpha(t.RimLight, Mathf.Clamp01(rimPercent / 100f));
            sizePx = rimPercent > 0 ? t.RimPx : 0f;
        }

        /// <summary>격노 스택 +1의 림 굵기: 0.3초 동안 2px → 6px(05 요약서 8.3절, 10 요약서 3.8절). 끝나면 평소 굵기.</summary>
        public static float RageRimPx(float elapsed, EnemyViewTuning t, float normalPx)
        {
            if (elapsed < 0f || elapsed >= t.RageRimSeconds || t.RageRimSeconds <= 0f) return normalPx;
            return Mathf.Lerp(t.RageRimFromPx, t.RageRimToPx, elapsed / t.RageRimSeconds);
        }

        /// <summary>감속 고리가 두 줄인가: 가장 센 감속을 건 출처가 얼음 송곳·살얼음 칸이면 두 줄(03 요약서 10.3절, 10 요약서 3.7절).</summary>
        public static bool SlowIsDouble(Wizard.Solo.Sim.EnemyState e, int tick, EnemyViewTuning t, out int remainTicks)
        {
            remainTicks = 0;
            float best = 0f;
            int bestSlot = -1;
            for (int i = 0; i < e.Slows.Length; i++)
            {
                if (tick >= e.Slows[i].UntilTick || e.Slows[i].Strength <= best) continue;
                best = e.Slows[i].Strength;
                bestSlot = i;
                remainTicks = e.Slows[i].UntilTick - tick;
            }
            return bestSlot >= 0 && t.SlowDoubleSlots.Contains((Wizard.Solo.SlowSlot)bestSlot);
        }
    }
}
