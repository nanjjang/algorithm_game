using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Wizard.Solo.Data;

namespace Wizard.Solo.View.Enemies
{
    /// <summary>
    /// G15 표시 수치(Resources/DataSolo/G15.json). 값의 출처는 JSON의 _src 줄에 있다.
    /// 필드의 초깃값은 파일이 없거나 깨졌을 때만 쓰는 같은 값이다(ArtPresentation과 같은 방식) — 수치를 고치는 곳은 JSON이다.
    /// </summary>
    public sealed class EnemyViewTuning
    {
        public const string ExtraName = "G15";
        /// <summary>단위 환산: 1칸 = 64px(00 문서 머리).</summary>
        public const float Px = 1f / SimConst.PixelsPerUnit;

        // ---- 색
        public Color32 DangerRed = Hex("#FF3B30"), BulletBody = Hex("#FF2E88"), WeaponTint = Hex("#FF2E88");
        public Color32 BorderOuter = Hex("#000000"), BorderMid = Hex("#FF3B30"), BorderInner = Hex("#FFFFFF");
        public Color32 FrontWhite = Hex("#FFFFFF"), FrontBlack = Hex("#000000"), FlashWhite = Hex("#FFFFFF"), DarkBase = Hex("#000000");
        public Color32 RimLight = Hex("#EAF2FF"), EnemyOutline = Hex("#1B2440"), EliteGold = Hex("#FFD24A"), CorrosionEdge = Hex("#D9C7FF");
        public Color32 FrostEdge = Hex("#FF2E88"), FrostHatch = Hex("#FFFFFF"), HazardHatch = Hex("#FF3B30");
        public Color32 HarmlessFill = Hex("#E8E4D8"), HarmlessEdge = Hex("#F4F1EA"), StarfallRing = Hex("#FFF1C4");
        public Color32 ColorblindStripe = Hex("#000000"), ColorblindShotStripe = Hex("#FF9F1C");
        public Color32 HpBarBack = Hex("#1B2440"), HpBarFill = Hex("#E6E9F0"), Shadow = Hex("#000000"), SpawnBush = Hex("#5E6650");
        public Color32 BlockedPuff = Hex("#F4F6FA");

        // ---- 예고
        public float DarkBaseAlpha = 0.4f;
        public float BorderOuterPx = 2, BorderMidPx = 4, BorderInnerPx = 2, FrontWhitePx = 2, FrontBlackPx = 2;
        public float AimLineMinPx = 2, AimLineMaxPx = 8, AimLineCorePx = 2;
        public int FlashTicksBefore = 2;
        public float FlashLimitScreenFraction = 0.25f;
        public int FlashRefWidth = 1920, FlashRefHeight = 1080;
        public float FlashWindowSeconds = 1f, FlashBorderPx = 8;
        public int FillPctMin = 70, FillPctMax = 90;
        public float LockShakePx = 2, LockShakeSeconds = 0.1f, CancelFadeSeconds = 0.15f;
        public float HarmlessBaseAlpha = 0.2f, HarmlessFillAlpha = 0.35f;
        public float ColorblindStripePx = 4, ColorblindPitchPx = 16, ColorblindAngle = 45, ColorblindStripeAlpha = 0.45f;
        public int CircleSegments = 64;
        public readonly Dictionary<string, string> ImpactSprites = new Dictionary<string, string>();
        public readonly Dictionary<string, Vector2> ImpactSpriteCells = new Dictionary<string, Vector2>();

        // ---- 남는 구역
        public float HatchPx = 4, HatchPitchPx = 16, HatchAngle = 45, HatchAlpha = 1f;
        public float HazardBlinkLeadSeconds = 1f, HazardBlinkPerSecond = 2f, HazardBlinkDimAlpha = 0.3f;
        public float FrostEdgeOuterPx = 2, FrostEdgeMidPx = 4;
        public readonly Dictionary<string, string> HazardSprites = new Dictionary<string, string>();

        // ---- 적 탄
        public float ShotSmallRadius = 0.12f, ShotLargeRadius = 0.18f, ShotLargeMinRadius = 0.15f;
        public string ShotSmallKey = "round_small", ShotLargeKey = "round_large";
        public readonly HashSet<string> RoundShotKeys = new HashSet<string>();
        public float LobPeakHeight = 1.5f;
        public string LobShadowKey = "seed_shadow";
        public float BlockedSeconds = 0.2f, BlockedRadiusPx = 12, BlockedAlpha = 0.5f;
        public float ShotStripePx = 2, ShotStripePitchPx = 6;

        // ---- 적 몸
        public float RimPx = 2, EliteOutlinePx = 2;
        public float ShadowAlpha = 0.35f, ShadowWidthPerRadius = 2.2f, ShadowHeightRatio = 0.45f, ShadowMinWidth = 0.4f;
        public float FlyHeight = 0.5f, FlyBobCells = 0.06f, FlyBobPerSecond = 1.5f;
        public float HitFlashSeconds = 0.0334f, HitPushPx = 2, WeaponTintAmount = 0.45f;
        public float EliteHpBarWidth = 0.75f, EliteHpBarPx = 4, EliteHpBarGapPx = 4, EliteColumnSeconds = 0.5f;
        public float EliteOverlaySinkPx = 6, HornGapPx = 2;
        public int SlowFullTicks = 60, CorrosionFullTicks = 180;
        public readonly HashSet<SlowSlot> SlowDoubleSlots = new HashSet<SlowSlot>();
        public float CorrosionPx = 2, CorrosionDashDegrees = 18, CorrosionGapDegrees = 12;
        public float FrozenLargePx = 80, StaggerShakePx = 1, SpawnShakePx = 2, SpawnShakePerSecond = 12, SpawnBushRadius = 0.35f;
        public int SpawnDefaultTicks = 30;
        public string VineNoticeKey = "soil_crack", MoundKey = "soil_crack";
        public readonly HashSet<string> BurrowVisibleClips = new HashSet<string>();
        public int ThinIceMaxMarks = 5;
        public float RageRimFromPx = 2, RageRimToPx = 6, RageRimSeconds = 0.3f;
        public float RageSmokePerStackPerSecond = 2, RageSmokeRiseCells = 0.6f;
        public float[] RageSmokeEffectsScale = { 1f, 0.5f, 0f };
        public float BossDeathSlowSeconds = 1f, BossDeathFlashSeconds = 0.1f;
        public int BossRimPercentMin = 60;
        public string SerpentSegmentEntry = "boss.night_serpent.segment", SerpentTailEntry = "boss.night_serpent.tail";
        public string SerpentWarnPattern = "scale_volley";
        public string DummyWorldAsset = "facility.training_dummy";
        public float DummyHitSeconds = 0.4f;

        // ---- 장애물
        public readonly Dictionary<string, ObstacleFrames> Obstacles = new Dictionary<string, ObstacleFrames>();
        public float ObstacleExpireFadeSeconds = 0.2f;

        // ---- 별똥
        public float StarRotateDegPerSecond = 90, StarDashDegrees = 15, StarGapDegrees = 15, StarRingPx = 3, StarFallHeight = 4, StarBlinkPerSecond = 2;
        public string StarFallAsset = "field.meteor.fall", StarSparkleAsset = "field.meteor.sparkle", StarGroundEffect = "meteor_ground";

        public sealed class ObstacleFrames
        {
            public string Effect;
            public int[] Spawn = new int[0], Idle = new int[0], Break = new int[0];
        }

        /// <summary>최근 1초 번쩍 넓이 한도(px²) = 기준 화면 넓이 × 비율. 05 요약서 1.2절: 1920×1080의 25% = 518,400.</summary>
        public float FlashLimitPx2 => FlashRefWidth * (float)FlashRefHeight * FlashLimitScreenFraction;
        public int FlashWindowTicks => Mathf.Max(1, SimConst.Ticks(FlashWindowSeconds));

        public static EnemyViewTuning Load(IContentDb db)
        {
            JObject o = null;
            try { o = db != null ? db.SoloExtra(ExtraName) : null; }
            catch (System.Exception) { o = null; }
            return Parse(o);
        }

        public static EnemyViewTuning Parse(JObject o)
        {
            var t = new EnemyViewTuning();
            t.FillDefaults();
            if (o == null) return t;
            try { t.Read(o); }
            catch (System.Exception) { /* 깨진 값은 초깃값으로 둔다 */ }
            return t;
        }

        void FillDefaults()
        {
            ImpactSprites["Circle"] = "impact_circle"; ImpactSprites["Ring"] = "impact_circle"; ImpactSprites["Fan"] = "impact_fan";
            ImpactSprites["Band"] = "impact_lane"; ImpactSprites["AimLine"] = "impact_lane";
            ImpactSpriteCells["impact_circle"] = new Vector2(2, 2); ImpactSpriteCells["impact_fan"] = new Vector2(2, 2); ImpactSpriteCells["impact_lane"] = new Vector2(2, 1);
            HazardSprites["ThornField"] = "thorn_ground"; HazardSprites["EmberStrip"] = "ember_lane";
            HazardSprites["SporeCloud"] = "spore_cloud"; HazardSprites["FrostField"] = "frost_field";
            foreach (var k in new[] { "round_small", "round_large", "ice_shard", "vine_seed", "mirror_shards" }) RoundShotKeys.Add(k);
            SlowDoubleSlots.Add(SlowSlot.IceSpike); SlowDoubleSlots.Add(SlowSlot.ThinIce);
            foreach (var k in new[] { "burrow", "sink", "rise", "emerge" }) BurrowVisibleClips.Add(k);
            Obstacles["boulder"] = new ObstacleFrames { Effect = "rock", Spawn = new[] { 0, 1 }, Idle = new[] { 2 }, Break = new[] { 3, 4 } };
            Obstacles["ice_pillar"] = new ObstacleFrames { Effect = "ice_pillar", Spawn = new[] { 0, 1 }, Idle = new[] { 2 }, Break = new[] { 3, 4, 5 } };
        }

        void Read(JObject o)
        {
            if (o["colors"] is JObject c)
            {
                DangerRed = C(c, "danger_zone_red", DangerRed); BulletBody = C(c, "danger_bullet_body", BulletBody); WeaponTint = C(c, "weapon_tint", WeaponTint);
                BorderOuter = C(c, "zone_border_outer", BorderOuter); BorderMid = C(c, "zone_border_mid", BorderMid); BorderInner = C(c, "zone_border_inner", BorderInner);
                FrontWhite = C(c, "front_white", FrontWhite); FrontBlack = C(c, "front_black", FrontBlack); FlashWhite = C(c, "flash_white", FlashWhite);
                DarkBase = C(c, "dark_base", DarkBase); RimLight = C(c, "rim_light", RimLight); EnemyOutline = C(c, "enemy_outline", EnemyOutline);
                EliteGold = C(c, "elite_gold", EliteGold); CorrosionEdge = C(c, "corrosion_edge", CorrosionEdge);
                FrostEdge = C(c, "frost_field_edge", FrostEdge); FrostHatch = C(c, "frost_field_hatch", FrostHatch); HazardHatch = C(c, "hazard_hatch", HazardHatch);
                HarmlessFill = C(c, "harmless_fill", HarmlessFill); HarmlessEdge = C(c, "harmless_edge", HarmlessEdge); StarfallRing = C(c, "starfall_ring", StarfallRing);
                ColorblindStripe = C(c, "colorblind_stripe", ColorblindStripe); ColorblindShotStripe = C(c, "colorblind_shot_stripe", ColorblindShotStripe);
                HpBarBack = C(c, "hp_bar_back", HpBarBack); HpBarFill = C(c, "hp_bar_fill", HpBarFill); Shadow = C(c, "shadow", Shadow);
                SpawnBush = C(c, "spawn_bush", SpawnBush); BlockedPuff = C(c, "blocked_puff", BlockedPuff);
            }
            if (o["telegraph"] is JObject tg)
            {
                DarkBaseAlpha = F(tg, "dark_base_alpha", DarkBaseAlpha);
                BorderOuterPx = F(tg, "border_outer_px", BorderOuterPx); BorderMidPx = F(tg, "border_mid_px", BorderMidPx); BorderInnerPx = F(tg, "border_inner_px", BorderInnerPx);
                FrontWhitePx = F(tg, "front_white_px", FrontWhitePx); FrontBlackPx = F(tg, "front_black_px", FrontBlackPx);
                AimLineMinPx = F(tg, "aim_line_min_px", AimLineMinPx); AimLineMaxPx = F(tg, "aim_line_max_px", AimLineMaxPx); AimLineCorePx = F(tg, "aim_line_core_px", AimLineCorePx);
                FlashTicksBefore = I(tg, "flash_ticks_before", FlashTicksBefore);
                FlashLimitScreenFraction = F(tg, "flash_limit_screen_fraction", FlashLimitScreenFraction);
                if (tg["flash_ref_screen"] is JArray rs && rs.Count == 2) { FlashRefWidth = (int)rs[0]; FlashRefHeight = (int)rs[1]; }
                FlashWindowSeconds = F(tg, "flash_window_seconds", FlashWindowSeconds); FlashBorderPx = F(tg, "flash_border_px", FlashBorderPx);
                FillPctMin = I(tg, "fill_pct_min", FillPctMin); FillPctMax = I(tg, "fill_pct_max", FillPctMax);
                LockShakePx = F(tg, "lock_shake_px", LockShakePx); LockShakeSeconds = F(tg, "lock_shake_seconds", LockShakeSeconds);
                CancelFadeSeconds = F(tg, "cancel_fade_seconds", CancelFadeSeconds);
                HarmlessBaseAlpha = F(tg, "harmless_base_alpha", HarmlessBaseAlpha); HarmlessFillAlpha = F(tg, "harmless_fill_alpha", HarmlessFillAlpha);
                ColorblindStripePx = F(tg, "colorblind_stripe_px", ColorblindStripePx); ColorblindPitchPx = F(tg, "colorblind_pitch_px", ColorblindPitchPx);
                ColorblindAngle = F(tg, "colorblind_angle_degrees", ColorblindAngle); ColorblindStripeAlpha = F(tg, "colorblind_stripe_alpha", ColorblindStripeAlpha);
                CircleSegments = Mathf.Max(8, I(tg, "circle_segments", CircleSegments));
                if (tg["impact_sprites"] is JObject im) { ImpactSprites.Clear(); foreach (var p in im.Properties()) ImpactSprites[p.Name] = (string)p.Value; }
                if (tg["impact_sprite_cells"] is JObject ic)
                {
                    ImpactSpriteCells.Clear();
                    foreach (var p in ic.Properties()) if (p.Value is JArray a && a.Count == 2) ImpactSpriteCells[p.Name] = new Vector2((float)a[0], (float)a[1]);
                }
            }
            if (o["hazard"] is JObject hz)
            {
                HatchPx = F(hz, "hatch_px", HatchPx); HatchPitchPx = F(hz, "hatch_pitch_px", HatchPitchPx); HatchAngle = F(hz, "hatch_angle_degrees", HatchAngle);
                HatchAlpha = F(hz, "hatch_alpha", HatchAlpha);
                HazardBlinkLeadSeconds = F(hz, "blink_lead_seconds", HazardBlinkLeadSeconds); HazardBlinkPerSecond = F(hz, "blink_per_second", HazardBlinkPerSecond);
                HazardBlinkDimAlpha = F(hz, "blink_dim_alpha", HazardBlinkDimAlpha);
                FrostEdgeOuterPx = F(hz, "frost_edge_outer_px", FrostEdgeOuterPx); FrostEdgeMidPx = F(hz, "frost_edge_mid_px", FrostEdgeMidPx);
                if (hz["sprites"] is JObject sp) { HazardSprites.Clear(); foreach (var p in sp.Properties()) HazardSprites[p.Name] = (string)p.Value; }
            }
            if (o["shot"] is JObject sh)
            {
                ShotSmallRadius = F(sh, "small_radius", ShotSmallRadius); ShotLargeRadius = F(sh, "large_radius", ShotLargeRadius);
                ShotLargeMinRadius = F(sh, "large_min_radius", ShotLargeMinRadius);
                ShotSmallKey = S(sh, "small_key", ShotSmallKey); ShotLargeKey = S(sh, "large_key", ShotLargeKey);
                if (sh["round_keys"] is JArray rk) { RoundShotKeys.Clear(); foreach (var k in rk) RoundShotKeys.Add((string)k); }
                LobPeakHeight = F(sh, "lob_peak_height", LobPeakHeight); LobShadowKey = S(sh, "lob_shadow_key", LobShadowKey);
                BlockedSeconds = F(sh, "blocked_seconds", BlockedSeconds); BlockedRadiusPx = F(sh, "blocked_radius_px", BlockedRadiusPx);
                BlockedAlpha = F(sh, "blocked_alpha", BlockedAlpha);
                ShotStripePx = F(sh, "colorblind_stripe_px", ShotStripePx); ShotStripePitchPx = F(sh, "colorblind_pitch_px", ShotStripePitchPx);
            }
            if (o["enemy"] is JObject en)
            {
                RimPx = F(en, "rim_px", RimPx); EliteOutlinePx = F(en, "elite_outline_px", EliteOutlinePx);
                ShadowAlpha = F(en, "shadow_alpha", ShadowAlpha); ShadowWidthPerRadius = F(en, "shadow_width_per_radius", ShadowWidthPerRadius);
                ShadowHeightRatio = F(en, "shadow_height_ratio", ShadowHeightRatio); ShadowMinWidth = F(en, "shadow_min_width", ShadowMinWidth);
                FlyHeight = F(en, "fly_height", FlyHeight); FlyBobCells = F(en, "fly_bob_cells", FlyBobCells); FlyBobPerSecond = F(en, "fly_bob_per_second", FlyBobPerSecond);
                HitFlashSeconds = F(en, "hit_flash_seconds", HitFlashSeconds); HitPushPx = F(en, "hit_push_px", HitPushPx);
                WeaponTintAmount = F(en, "weapon_tint_amount", WeaponTintAmount);
                EliteHpBarWidth = F(en, "elite_hp_bar_width", EliteHpBarWidth); EliteHpBarPx = F(en, "elite_hp_bar_px", EliteHpBarPx);
                EliteHpBarGapPx = F(en, "elite_hp_bar_gap_px", EliteHpBarGapPx); EliteColumnSeconds = F(en, "elite_column_seconds", EliteColumnSeconds);
                EliteOverlaySinkPx = F(en, "elite_overlay_sink_px", EliteOverlaySinkPx); HornGapPx = F(en, "horn_gap_px", HornGapPx);
                SlowFullTicks = I(en, "slow_full_ticks", SlowFullTicks); CorrosionFullTicks = I(en, "corrosion_full_ticks", CorrosionFullTicks);
                if (en["slow_double_slots"] is JArray ds)
                {
                    SlowDoubleSlots.Clear();
                    foreach (var s in ds) if (System.Enum.TryParse((string)s, out SlowSlot slot)) SlowDoubleSlots.Add(slot);
                }
                CorrosionPx = F(en, "corrosion_px", CorrosionPx); CorrosionDashDegrees = F(en, "corrosion_dash_degrees", CorrosionDashDegrees);
                CorrosionGapDegrees = F(en, "corrosion_gap_degrees", CorrosionGapDegrees);
                FrozenLargePx = F(en, "frozen_large_px", FrozenLargePx); StaggerShakePx = F(en, "stagger_shake_px", StaggerShakePx);
                SpawnShakePx = F(en, "spawn_shake_px", SpawnShakePx); SpawnShakePerSecond = F(en, "spawn_shake_per_second", SpawnShakePerSecond);
                SpawnBushRadius = F(en, "spawn_bush_radius", SpawnBushRadius); SpawnDefaultTicks = I(en, "spawn_default_ticks", SpawnDefaultTicks);
                VineNoticeKey = S(en, "vine_notice_key", VineNoticeKey); MoundKey = S(en, "mound_key", MoundKey);
                if (en["burrow_visible_clips"] is JArray bc) { BurrowVisibleClips.Clear(); foreach (var k in bc) BurrowVisibleClips.Add((string)k); }
                ThinIceMaxMarks = I(en, "thin_ice_max_marks", ThinIceMaxMarks);
                RageRimFromPx = F(en, "rage_rim_from_px", RageRimFromPx); RageRimToPx = F(en, "rage_rim_to_px", RageRimToPx);
                RageRimSeconds = F(en, "rage_rim_seconds", RageRimSeconds);
                RageSmokePerStackPerSecond = F(en, "rage_smoke_per_stack_per_second", RageSmokePerStackPerSecond);
                RageSmokeRiseCells = F(en, "rage_smoke_rise_cells", RageSmokeRiseCells);
                if (en["rage_smoke_effects_scale"] is JArray rse && rse.Count > 0)
                {
                    RageSmokeEffectsScale = new float[rse.Count];
                    for (int i = 0; i < rse.Count; i++) RageSmokeEffectsScale[i] = (float)rse[i];
                }
                BossDeathSlowSeconds = F(en, "boss_death_slow_seconds", BossDeathSlowSeconds);
                BossDeathFlashSeconds = F(en, "boss_death_flash_seconds", BossDeathFlashSeconds);
                BossRimPercentMin = I(en, "boss_rim_percent_min", BossRimPercentMin);
                SerpentSegmentEntry = S(en, "serpent_segment_entry", SerpentSegmentEntry); SerpentTailEntry = S(en, "serpent_tail_entry", SerpentTailEntry);
                SerpentWarnPattern = S(en, "serpent_warn_pattern", SerpentWarnPattern);
                DummyWorldAsset = S(en, "dummy_world_asset", DummyWorldAsset); DummyHitSeconds = F(en, "dummy_hit_seconds", DummyHitSeconds);
            }
            if (o["obstacle"] is JObject ob)
            {
                if (ob["kinds"] is JObject kinds)
                {
                    Obstacles.Clear();
                    foreach (var p in kinds.Properties())
                    {
                        if (!(p.Value is JObject k)) continue;
                        Obstacles[p.Name] = new ObstacleFrames { Effect = S(k, "effect", p.Name), Spawn = Ints(k["spawn"]), Idle = Ints(k["idle"]), Break = Ints(k["break"]) };
                    }
                }
                ObstacleExpireFadeSeconds = F(ob, "expire_fade_seconds", ObstacleExpireFadeSeconds);
            }
            if (o["starfall"] is JObject st)
            {
                StarRotateDegPerSecond = F(st, "ring_rotate_degrees_per_second", StarRotateDegPerSecond);
                StarDashDegrees = F(st, "dash_degrees", StarDashDegrees); StarGapDegrees = F(st, "gap_degrees", StarGapDegrees);
                StarRingPx = F(st, "ring_px", StarRingPx); StarFallHeight = F(st, "fall_height", StarFallHeight);
                StarBlinkPerSecond = F(st, "blink_per_second", StarBlinkPerSecond);
                StarFallAsset = S(st, "fall_asset", StarFallAsset); StarSparkleAsset = S(st, "sparkle_asset", StarSparkleAsset);
                StarGroundEffect = S(st, "ground_effect", StarGroundEffect);
            }
        }

        static float F(JObject o, string k, float d) { var t = o[k]; return t != null && (t.Type == JTokenType.Float || t.Type == JTokenType.Integer) ? (float)t : d; }
        static int I(JObject o, string k, int d) { var t = o[k]; return t != null && (t.Type == JTokenType.Float || t.Type == JTokenType.Integer) ? (int)(float)t : d; }
        static string S(JObject o, string k, string d) { var t = o[k]; return t != null && t.Type == JTokenType.String ? (string)t : d; }
        static Color32 C(JObject o, string k, Color32 d) { var t = o[k]; return t != null && t.Type == JTokenType.String ? Hex((string)t, d) : d; }
        static int[] Ints(JToken t)
        {
            if (!(t is JArray a)) return new int[0];
            var r = new int[a.Count];
            for (int i = 0; i < r.Length; i++) r[i] = (int)a[i];
            return r;
        }

        /// <summary>"#RRGGBB" 또는 "#RRGGBBAA"(sRGB 값 그대로 — 00 문서 7절 색 공간).</summary>
        public static Color32 Hex(string s) => Hex(s, new Color32(255, 0, 255, 255));

        public static Color32 Hex(string s, Color32 fallback)
        {
            if (string.IsNullOrEmpty(s)) return fallback;
            if (s[0] == '#') s = s.Substring(1);
            if (s.Length != 6 && s.Length != 8) return fallback;
            if (!uint.TryParse(s, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out uint v)) return fallback;
            if (s.Length == 6) v = (v << 8) | 0xFF;
            return new Color32((byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v);
        }
    }
}
