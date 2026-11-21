using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Wizard.Solo.View.Hud
{
    /// <summary>
    /// 전투 HUD의 솔로 임시 수치. 정본은 보충 조각 Resources/DataSolo/G17.json의 "hud" 절(ctx.Content.SoloExtra("G17"))이고,
    /// 조각이 없을 때를 위해 같은 값을 기본값으로 둔다. 요약서에 값이 있는 것은 HudLayout에 있다(여기에 두지 않는다).
    /// </summary>
    public sealed class HudTuning
    {
        public const string ExtraName = "G17";
        public const string ExtraSection = "hud";

        /// <summary>한 줄 안내의 길이(13.1절 "길이 미정").</summary>
        public float NoticeSeconds = 2.0f;
        /// <summary>훈수 한 줄(화면 위 한 줄, 01 요약서 9절 표)의 길이와 자리.</summary>
        public float MasterAdviceSeconds = 4.0f;
        public HudRect MasterAdvice = new HudRect(480, 104, 960, 24, 16);
        /// <summary>보스 웨이브에서 날·웨이브 줄이 (760,76)으로 내려와 경과 시간 칸(912,80)과 겹치므로 경과 시간을 내리는 y.</summary>
        public float BossWaveElapsedY = 104f;
        /// <summary>콤보 자리 "HUD"(초상 옆)의 왼쪽 위.</summary>
        public Vector2 ComboHudPos = new Vector2(124, 108);
        /// <summary>결정 칸 줄의 위 끝(발밑 기준 px, 위가 −).</summary>
        public float CrystalRowOffsetY = -100f;
        /// <summary>콤보 숫자 아래 2초 막대의 크기.</summary>
        public float ComboBarWidth = 48f, ComboBarHeight = 2f;
        /// <summary>콤보 끊김 연출(흐려지며 떨어짐 / 깨져 흩어짐)의 길이.</summary>
        public float ComboBreakSeconds = 0.4f;
        /// <summary>발사 때 회로 칸이 하나씩 켜지는 간격.</summary>
        public float CircuitStepSeconds = 0.05f;
        /// <summary>잠금 해제 때 링이 퍼지며 사라지는 길이.</summary>
        public float UnlockFlashSeconds = 0.25f;
        /// <summary>머리 위 "n번째 입력이 x초 늦음"의 길이와 자리.</summary>
        public float LateLineSeconds = 1.0f, LateLineOffsetY = -124f;
        /// <summary>불발 때 "지금 K" 두 번 깜빡의 전체 길이.</summary>
        public float FizzleBlinkSeconds = 0.3f;
        /// <summary>결정 깨짐(4프레임)·서리 방패 한 번 빛남의 길이.</summary>
        public float CrystalBreakSeconds = 0.2f, FrostShieldSeconds = 0.3f;
        /// <summary>웨이브 끝 환전: 남은 결정이 골드 표시로 날아가는 시간.</summary>
        public float WaveEndCoinSeconds = 0.4f;
        /// <summary>시야 화살표 자리(발밑 중심에서 px)·크기·보통 시야의 반투명.</summary>
        public float FacingArrowRadiusPx = 54f, FacingArrowSizePx = 12f, FacingArrowNormalAlpha = 0.5f;

        public static HudTuning Defaults() => new HudTuning();

        /// <summary>조각(최상위 또는 "hud" 절)에서 읽는다. null이면 기본값.</summary>
        public static HudTuning FromJson(JObject extra)
        {
            var t = new HudTuning();
            if (extra == null) return t;
            var o = extra[ExtraSection] as JObject ?? extra;
            t.NoticeSeconds = F(o, "notice_seconds", t.NoticeSeconds);
            t.MasterAdviceSeconds = F(o, "master_advice_seconds", t.MasterAdviceSeconds);
            t.MasterAdvice = R(o, "master_advice_rect", t.MasterAdvice);
            t.BossWaveElapsedY = F(o, "boss_wave_elapsed_y", t.BossWaveElapsedY);
            t.ComboHudPos = V(o, "combo_hud_pos", t.ComboHudPos);
            t.CrystalRowOffsetY = F(o, "crystal_row_offset_y", t.CrystalRowOffsetY);
            t.ComboBarWidth = F(o, "combo_bar_width", t.ComboBarWidth);
            t.ComboBarHeight = F(o, "combo_bar_height", t.ComboBarHeight);
            t.ComboBreakSeconds = F(o, "combo_break_seconds", t.ComboBreakSeconds);
            t.CircuitStepSeconds = F(o, "circuit_step_seconds", t.CircuitStepSeconds);
            t.UnlockFlashSeconds = F(o, "unlock_flash_seconds", t.UnlockFlashSeconds);
            t.LateLineSeconds = F(o, "late_line_seconds", t.LateLineSeconds);
            t.LateLineOffsetY = F(o, "late_line_offset_y", t.LateLineOffsetY);
            t.FizzleBlinkSeconds = F(o, "fizzle_blink_seconds", t.FizzleBlinkSeconds);
            t.CrystalBreakSeconds = F(o, "crystal_break_seconds", t.CrystalBreakSeconds);
            t.FrostShieldSeconds = F(o, "frost_shield_seconds", t.FrostShieldSeconds);
            t.WaveEndCoinSeconds = F(o, "wave_end_coin_seconds", t.WaveEndCoinSeconds);
            t.FacingArrowRadiusPx = F(o, "facing_arrow_radius_px", t.FacingArrowRadiusPx);
            t.FacingArrowSizePx = F(o, "facing_arrow_size_px", t.FacingArrowSizePx);
            t.FacingArrowNormalAlpha = F(o, "facing_arrow_normal_alpha", t.FacingArrowNormalAlpha);
            return t;
        }

        static float F(JObject o, string key, float def)
        {
            var t = o[key];
            return t != null && (t.Type == JTokenType.Integer || t.Type == JTokenType.Float) ? (float)(double)t : def;
        }

        static Vector2 V(JObject o, string key, Vector2 def)
        {
            var a = o[key] as JArray;
            if (a == null || a.Count < 2) return def;
            return new Vector2((float)(double)a[0], (float)(double)a[1]);
        }

        static HudRect R(JObject o, string key, HudRect def)
        {
            var a = o[key] as JArray;
            if (a == null || a.Count < 4) return def;
            return new HudRect((float)(double)a[0], (float)(double)a[1], (float)(double)a[2], (float)(double)a[3], def.FontPx);
        }
    }
}
