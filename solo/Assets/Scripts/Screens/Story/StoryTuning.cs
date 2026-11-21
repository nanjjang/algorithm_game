using System;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Wizard.Solo.Data;
using Wizard.Solo.Sim;

namespace Wizard.Solo.Screens.Story
{
    /// <summary>화면 기준(1920×1080, 왼쪽 위 원점 px)의 칸.</summary>
    public struct StoryRect
    {
        public float X, Y, W, H;
        public StoryRect(float x, float y, float w, float h) { X = x; Y = y; W = w; H = h; }
    }

    /// <summary>
    /// 튜토리얼과 첫 만남·복수 연출의 수치. 정본은 수치 조각 Resources/DataSolo/G25.json(ctx.Content.SoloExtra("G25"))이고,
    /// 조각이 없을 때를 위해 같은 값을 기본값으로 둔다. 데이터에 같은 값이 이미 있는 것(복수 264·84틱 = camp.json,
    /// 되비침 +114틱·쓰러짐 4.0초 = bosses.json first_encounter, 보스 등장 1.2초 = bosses.json common)은 그쪽이 이긴다.
    /// </summary>
    public sealed class StoryTuning
    {
        public const string ExtraName = "G25";

        // ---- 튜토리얼 (08 요약서 6.4절)
        public int TutorialStepCount = 5;
        /// <summary>완료(TutorialStepDone) 뒤 다음 단계로 넘기기까지(솔로 임시).</summary>
        public int TutorialAdvanceDelayTicks = 60;
        public StoryRect Balloon = new StoryRect(560, 200, 800, 120);

        // ---- 보스 등장 (05 요약서 10.3절)
        public StoryRect IntroBand = new StoryRect(480, 232, 960, 96);
        public int IntroTicks = 72;
        public int IntroRevengeLineTicks = 72;

        // ---- 첫 만남 (05 요약서 10.2절, 10 요약서 3.8절)
        public StoryRect Dialog = new StoryRect(480, 920, 960, 96);
        public int MirrorCrackTicks = 24;
        public int[] ReflectionBurstTicks = { 24, 54, 84 };
        public int ReflectionBurstLifeTicks = 30;
        public float ReflectionBurstRadiusPx = 160f;
        public int ReflectionHpZeroTicks = 114;
        public float ReflectionFlashOpacity = 0.25f;
        public int FlashOnTicks = 3, FlashOffTicks = 18;
        public int CollapseTicks = 240;
        public float CollapseOverlayAlpha = 0.35f;
        public int CollapseFadeInTicks = 6;
        public int CollapseColorReturnTick = 192;
        public int CollapseDissolveTick = 156;
        public int DissolveCount = 24;
        public int DissolveLifeTicks = 30;
        public int FirstNoteTicks = 240;
        public int FirstNoteSlideTicks = 12;

        // ---- 복수 (05 요약서 10.3절: 처치 틱 기준)
        public int RevengeSlowEnd = 60;
        public int RevengeFlashTick = 60;
        public int RevengeOrnamentStart = 84;
        public int RevengeOrnamentEnd = 144;
        public int RevengeBandStart = 144;
        public int RevengeEnd = 264;
        public int RevengeSkipHint = 84;
        public float RevengeFlashOpacity = 0.25f;
        public int RevengeShardCount = 12;
        public float RevengeOrnamentPx = 24f;
        public Vector2 OrnamentOffsetCells = new Vector2(0.5f, -1.25f);
        public float RevengeCrackPx = 96f, RevengeShardPx = 8f, RevengeShardSpreadPx = 220f;
        public StoryRect TitleBand = new StoryRect(560, 400, 800, 64);

        // ---- 표시 자리(솔로 임시)
        public Vector2 InputLockedOffsetPx = new Vector2(0f, -140f);
        public float ParticlePx = 6f, DissolveRisePx = 64f;

        // ---- 번쩍 관리기 (08 요약서 14.1절)
        public float FlashFullCap = 0.35f, FlashReducedCap = 0.15f;
        public int FlashMaxPerWindow = 2, FlashWindowTicks = 60, FlashMinIntervalTicks = 24;
        public float FlashBorderPx = 8f;
        public int FlashBorderTicks = 9;

        // ---- 그 밖
        public float FinalRageEdgePx = 8f;
        public float ShakeBigPx = 12f, ShakeBigSeconds = 0.35f;

        public static StoryTuning Defaults() => new StoryTuning();

        /// <summary>콘텐츠에서 읽는다(조각 G25 + camp.json + bosses.json). db가 null이면 기본값.</summary>
        public static StoryTuning From(IContentDb db)
        {
            JObject extra = null;
            if (db != null)
            {
                try { extra = db.SoloExtra(ExtraName); }
                catch (Exception) { extra = null; }
            }
            var t = FromJson(extra);
            if (db == null) return t;
            try
            {
                var camp = db.Camp;
                if (camp != null)
                {
                    if (camp.RevengeClearTicks > 0) t.RevengeEnd = camp.RevengeClearTicks;
                    if (camp.RevengeSkipAfterTicks > 0) t.RevengeSkipHint = camp.RevengeSkipAfterTicks;
                }
                var common = db.BossCommon;
                if (common != null && common.IntroSeconds > 0f) t.IntroTicks = SimConst.Ticks(common.IntroSeconds);
                var phantom = db.Boss(Ids.Boss.MirrorPhantom);
                var fe = phantom != null ? phantom.FirstEncounter : null;
                if (fe != null)
                {
                    if (fe.FinisherHpZeroTicks > 0) t.ReflectionHpZeroTicks = fe.FinisherHpZeroTicks;
                    if (fe.CollapseSeconds > 0f) t.CollapseTicks = SimConst.Ticks(fe.CollapseSeconds);
                }
            }
            catch (Exception) { }
            return t;
        }

        /// <summary>조각(최상위에 "tutorial"·"story" 절)에서 읽는다. null이면 기본값.</summary>
        public static StoryTuning FromJson(JObject extra)
        {
            var t = new StoryTuning();
            if (extra == null) return t;
            var tut = extra["tutorial"] as JObject;
            if (tut != null)
            {
                t.TutorialStepCount = I(tut, "step_count", t.TutorialStepCount);
                var d = tut["advance_delay_seconds"];
                if (IsNum(d)) t.TutorialAdvanceDelayTicks = SimConst.Ticks((float)(double)d);
                t.Balloon = R(tut, "balloon_rect", t.Balloon);
            }
            var s = extra["story"] as JObject;
            if (s == null) return t;
            t.IntroBand = R(s, "intro_band_rect", t.IntroBand);
            t.IntroRevengeLineTicks = Sec(s, "intro_revenge_line_seconds", t.IntroRevengeLineTicks);
            t.Dialog = R(s, "dialog_rect", t.Dialog);
            t.MirrorCrackTicks = Sec(s, "mirror_crack_seconds", t.MirrorCrackTicks);
            var bursts = s["reflection_burst_ticks"] as JArray;
            if (bursts != null && bursts.Count > 0)
            {
                var arr = new int[bursts.Count];
                bool ok = true;
                for (int i = 0; i < arr.Length; i++)
                {
                    if (!IsNum(bursts[i])) { ok = false; break; }
                    arr[i] = (int)(double)bursts[i];
                }
                if (ok) t.ReflectionBurstTicks = arr;
            }
            t.ReflectionBurstLifeTicks = I(s, "reflection_burst_life_ticks", t.ReflectionBurstLifeTicks);
            t.ReflectionBurstRadiusPx = F(s, "reflection_burst_radius_px", t.ReflectionBurstRadiusPx);
            t.ReflectionHpZeroTicks = I(s, "reflection_hp_zero_ticks", t.ReflectionHpZeroTicks);
            t.ReflectionFlashOpacity = F(s, "reflection_flash_opacity", t.ReflectionFlashOpacity);
            t.FlashOnTicks = I(s, "flash_on_ticks", t.FlashOnTicks);
            t.FlashOffTicks = I(s, "flash_off_ticks", t.FlashOffTicks);
            t.CollapseTicks = I(s, "collapse_ticks", t.CollapseTicks);
            t.CollapseOverlayAlpha = F(s, "collapse_overlay_alpha", t.CollapseOverlayAlpha);
            t.CollapseFadeInTicks = I(s, "collapse_fade_in_ticks", t.CollapseFadeInTicks);
            t.CollapseColorReturnTick = I(s, "collapse_color_return_tick", t.CollapseColorReturnTick);
            t.CollapseDissolveTick = I(s, "collapse_dissolve_tick", t.CollapseDissolveTick);
            t.DissolveCount = I(s, "dissolve_count", t.DissolveCount);
            t.DissolveLifeTicks = I(s, "dissolve_life_ticks", t.DissolveLifeTicks);
            t.FirstNoteTicks = I(s, "first_note_ticks", t.FirstNoteTicks);
            t.FirstNoteSlideTicks = Sec(s, "first_note_slide_seconds", t.FirstNoteSlideTicks);
            var r = s["revenge"] as JObject;
            if (r != null)
            {
                t.RevengeSlowEnd = I(r, "slow_end_tick", t.RevengeSlowEnd);
                t.RevengeFlashTick = I(r, "flash_tick", t.RevengeFlashTick);
                t.RevengeOrnamentStart = I(r, "ornament_start_tick", t.RevengeOrnamentStart);
                t.RevengeOrnamentEnd = I(r, "ornament_end_tick", t.RevengeOrnamentEnd);
                t.RevengeBandStart = I(r, "band_start_tick", t.RevengeBandStart);
                t.RevengeEnd = I(r, "end_tick", t.RevengeEnd);
                t.RevengeSkipHint = I(r, "skip_hint_tick", t.RevengeSkipHint);
                t.RevengeFlashOpacity = F(r, "flash_opacity", t.RevengeFlashOpacity);
                t.RevengeShardCount = I(r, "shard_count", t.RevengeShardCount);
                t.RevengeOrnamentPx = F(r, "ornament_px", t.RevengeOrnamentPx);
                t.OrnamentOffsetCells = V(r, "ornament_offset_cells", t.OrnamentOffsetCells);
                t.RevengeCrackPx = F(r, "crack_px", t.RevengeCrackPx);
                t.RevengeShardPx = F(r, "shard_px", t.RevengeShardPx);
                t.RevengeShardSpreadPx = F(r, "shard_spread_px", t.RevengeShardSpreadPx);
            }
            t.TitleBand = R(s, "title_band_rect", t.TitleBand);
            t.InputLockedOffsetPx = V(s, "input_locked_offset_px", t.InputLockedOffsetPx);
            t.ParticlePx = F(s, "particle_px", t.ParticlePx);
            t.DissolveRisePx = F(s, "dissolve_rise_px", t.DissolveRisePx);
            var fl = s["flash_limits"] as JObject;
            if (fl != null)
            {
                t.FlashFullCap = F(fl, "full_cap", t.FlashFullCap);
                t.FlashReducedCap = F(fl, "reduced_cap", t.FlashReducedCap);
                t.FlashMaxPerWindow = I(fl, "max_per_window", t.FlashMaxPerWindow);
                t.FlashWindowTicks = I(fl, "window_ticks", t.FlashWindowTicks);
                t.FlashMinIntervalTicks = I(fl, "min_interval_ticks", t.FlashMinIntervalTicks);
                t.FlashBorderPx = F(fl, "border_px", t.FlashBorderPx);
                t.FlashBorderTicks = I(fl, "border_ticks", t.FlashBorderTicks);
            }
            t.FinalRageEdgePx = F(s, "final_rage_edge_px", t.FinalRageEdgePx);
            t.ShakeBigPx = F(s, "shake_big_px", t.ShakeBigPx);
            t.ShakeBigSeconds = F(s, "shake_big_seconds", t.ShakeBigSeconds);
            return t;
        }

        static bool IsNum(JToken t) => t != null && (t.Type == JTokenType.Integer || t.Type == JTokenType.Float);

        static float F(JObject o, string key, float def)
        {
            var t = o[key];
            return IsNum(t) ? (float)(double)t : def;
        }

        static int I(JObject o, string key, int def)
        {
            var t = o[key];
            return IsNum(t) ? (int)Math.Round((double)t, MidpointRounding.AwayFromZero) : def;
        }

        /// <summary>초 값을 틱으로.</summary>
        static int Sec(JObject o, string key, int defTicks)
        {
            var t = o[key];
            return IsNum(t) ? SimConst.Ticks((float)(double)t) : defTicks;
        }

        static Vector2 V(JObject o, string key, Vector2 def)
        {
            var a = o[key] as JArray;
            if (a == null || a.Count < 2 || !IsNum(a[0]) || !IsNum(a[1])) return def;
            return new Vector2((float)(double)a[0], (float)(double)a[1]);
        }

        static StoryRect R(JObject o, string key, StoryRect def)
        {
            var a = o[key] as JArray;
            if (a == null || a.Count < 4) return def;
            for (int i = 0; i < 4; i++) if (!IsNum(a[i])) return def;
            return new StoryRect((float)(double)a[0], (float)(double)a[1], (float)(double)a[2], (float)(double)a[3]);
        }

        /// <summary>표시용(StoryLayer): 초 → 틱 환산을 보이게 쓰는 도우미.</summary>
        public static float TicksToSeconds(int ticks) => ticks * SimConst.TickSeconds;

    }
}
