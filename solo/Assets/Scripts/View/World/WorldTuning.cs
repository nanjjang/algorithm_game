using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Wizard.Solo.View.World
{
    /// <summary>
    /// G14 표시 수치(Resources/DataSolo/G14.json, ctx.Content.SoloExtra("G14")). 코드에 수치를 박지 않으려고 한곳에서 읽는다.
    /// 파일이 없거나 열쇠가 빠지면 그 표시를 끄는 쪽(0, 빈 목록)으로 읽는다 — 값을 지어내지 않는다.
    /// </summary>
    public sealed class WorldTuning
    {
        public const string ExtraName = "G14";

        // 전장
        public readonly List<string> MapIds = new List<string>();
        public string FallbackTileId;
        public Vector2Int CenterClearCells;
        public string RimWallDuskId, RimWallNightId, CampfireId, MoonlightId, MoonId, TowerId, TowerWindowId;
        public Vector2 MoonPos, TowerPos;
        public Vector2Int TowerWindowOffsetPx;
        public int TowerWindowClosedFrame;
        public int[] TowerWindowThrowFrames = new int[0];
        public float TowerWindowThrowSeconds;
        public int SpawnOpenFrame;
        public int[] SpawnShakeFrames = new int[0];
        public float SpawnShakeSeconds;
        public float SpawnShakeJitterPx, SpawnShakeFogJitterPx;

        // 시간대
        public float VignetteAlpha, VignetteInnerFraction;
        public Color VignetteColor, MoonlightEdgeColor;
        public float MoonlightEdgeAlpha, MoonlightCircleAlpha;

        // 날씨
        public string RainId, FogId, PuddleId;
        /// <summary>반딧불(밤 가장자리 장식, G14 firefly_*).</summary>
        public string FireflyGroup, FireflyKey;
        public int FireflyCount;
        public float FireflyEdgeCells, FireflyDriftCells, FireflyDriftHz;
        public float RainOpacity, RainMaxOpacity, FogOpacity;
        public readonly List<Vector2Int> PuddleTiles = new List<Vector2Int>();
        public readonly List<string> DiscardedItemProps = new List<string>();
        public readonly List<Vector2Int> DiscardedItemTiles = new List<Vector2Int>();

        // 야영
        public Color InteractRingColor, DepartMarkerColor, DepartHoldRingColor;
        public float InteractRingAlpha, DepartMarkerAlpha, DepartMarkerInnerFraction;
        public int InteractRingWidthPx;
        public float DepartHoldRingRadiusCells, DepartHoldRingWidthCells, DepartHoldRingAlpha, DepartHoldCancelSeconds;
        public int DepartHoldRingSegments;

        // 캐릭터
        public float FeetBelowCenterCells;
        public Vector2 ShadowSizeCells;
        public Color ShadowColor;
        public float ShadowAlpha;
        public float InvulnBlinkHz, InvulnBlinkAlpha, InvulnReducedAlpha, InvulnReducedOutlinePx;
        public float HitFlashSeconds;
        public Color HitFlashColor;
        public int AfterimageCount, AfterimageGapTicks;
        public float AfterimageAlpha;
        public Color RimColor;
        public float RimPx;
        public float LayerZStep;

        // 클립
        public string ClipIdle, ClipWalk, ClipStance, ClipFire, ClipSpecial, ClipSignature, ClipHit, ClipDeath;
        public readonly List<string> DownOnlyClips = new List<string>();

        // 지팡이
        public readonly Dictionary<string, Vector2Int> WandUpSplit = new Dictionary<string, Vector2Int>();
        public Color WandCritGlowColor, WandLockColor, WandKilnLineColor, WandDebtRingColor;
        public float WandCritGlowAlpha, WandCritTintAmount, WandLockTintAmount, WandLockRebrightenSeconds;
        public int WandCritGlowRadiusPx, WandDebtRingRadiusPx;
        public int WandIdleParticlesNormal, WandIdleParticlesCritical, WandIdleParticlesLocked;
        public float WandParticleLifetimeSeconds, WandParticleRiseCells;
        public int WandParticleRadiusPx;
        public readonly Dictionary<string, Color> WandParticleColors = new Dictionary<string, Color>();
        public Vector2Int WandKilnLinePx;
        public readonly Dictionary<string, Vector2Int> WandDetachedStages = new Dictionary<string, Vector2Int>();
        public readonly Dictionary<string, float> WandDetachedHeatShrink = new Dictionary<string, float>();

        // 표식
        public Color MarkMutationColor, MarkScarColor, MarkCrackColor;
        /// <summary>변이 표식 고리 그림(G14 mark_mutation_ring_*). 열쇠가 없으면 그리지 않는다.</summary>
        public string MarkMutationRingGroup, MarkMutationRingKey;
        public int MarkMutationPx, MarkExpansionPx, MarkExpansionStepPx, MarkScarPx, MarkScarMax, MarkCrackMax;
        public readonly Dictionary<string, Color> MarkExpansionColors = new Dictionary<string, Color>();
        public readonly List<Vector2Int> MarkScarOffsetsPx = new List<Vector2Int>();
        public Vector2Int MarkCrackSizePx;
        public readonly List<Vector2Int> MarkCrackOffsetsPx = new List<Vector2Int>();

        // 덧그림
        /// <summary>char.N → 단계(2~4) → 부품 이름.</summary>
        public readonly Dictionary<string, Dictionary<int, List<string>>> WildStainParts = new Dictionary<string, Dictionary<int, List<string>>>();
        /// <summary>단계 → 같이 붙는 앞 단계들.</summary>
        public readonly Dictionary<int, List<int>> WildStainInheritance = new Dictionary<int, List<int>>();
        public string CrackedMirrorLayer;

        public static WorldTuning From(Data.IContentDb db) => Parse(db != null ? db.SoloExtra(ExtraName) : null);

        public static WorldTuning Parse(JObject j)
        {
            var t = new WorldTuning();
            if (j == null) j = new JObject();
            Strings(j["map_ids"], t.MapIds);
            t.FallbackTileId = Str(j, "fallback_tile_id");
            t.CenterClearCells = V2i(j["center_clear_cells"]);
            t.RimWallDuskId = Str(j, "rim_wall_dusk_id");
            t.RimWallNightId = Str(j, "rim_wall_night_id");
            t.CampfireId = Str(j, "campfire_id");
            t.MoonlightId = Str(j, "moonlight_id");
            t.MoonId = Str(j, "moon_id");
            t.MoonPos = V2(j["moon_pos"]);
            t.TowerId = Str(j, "tower_id");
            t.TowerPos = V2(j["tower_pos"]);
            t.TowerWindowId = Str(j, "tower_window_id");
            t.TowerWindowOffsetPx = V2i(j["tower_window_offset_px"]);
            t.TowerWindowClosedFrame = Int(j, "tower_window_closed_frame");
            t.TowerWindowThrowFrames = Ints(j["tower_window_throw_frames"]);
            t.TowerWindowThrowSeconds = F(j, "tower_window_throw_seconds");
            t.SpawnOpenFrame = Int(j, "spawn_open_frame");
            t.SpawnShakeFrames = Ints(j["spawn_shake_frames"]);
            t.SpawnShakeSeconds = F(j, "spawn_shake_seconds");
            t.SpawnShakeJitterPx = F(j, "spawn_shake_jitter_px");
            t.SpawnShakeFogJitterPx = F(j, "spawn_shake_fog_jitter_px");

            t.VignetteAlpha = F(j, "vignette_alpha");
            t.VignetteInnerFraction = F(j, "vignette_inner_fraction");
            t.VignetteColor = Col(j, "vignette_color");
            t.MoonlightEdgeColor = Col(j, "moonlight_edge_color");
            t.MoonlightEdgeAlpha = F(j, "moonlight_edge_alpha");
            t.MoonlightCircleAlpha = F(j, "moonlight_circle_alpha");

            t.RainId = Str(j, "rain_id");
            t.RainOpacity = F(j, "rain_opacity");
            t.RainMaxOpacity = F(j, "rain_max_opacity");
            t.FogId = Str(j, "fog_id");
            t.FogOpacity = F(j, "fog_opacity");
            t.PuddleId = Str(j, "puddle_id");
            t.FireflyGroup = Str(j, "firefly_group");
            t.FireflyKey = Str(j, "firefly_key");
            t.FireflyCount = Int(j, "firefly_count");
            t.FireflyEdgeCells = F(j, "firefly_edge_cells");
            t.FireflyDriftCells = F(j, "firefly_drift_cells");
            t.FireflyDriftHz = F(j, "firefly_drift_hz");
            V2iList(j["puddle_tiles"], t.PuddleTiles);
            Strings(j["discarded_item_props"], t.DiscardedItemProps);
            V2iList(j["discarded_item_tiles"], t.DiscardedItemTiles);

            t.InteractRingColor = Col(j, "interact_ring_color");
            t.InteractRingAlpha = F(j, "interact_ring_alpha");
            t.InteractRingWidthPx = Int(j, "interact_ring_width_px");
            t.DepartMarkerColor = Col(j, "depart_marker_color");
            t.DepartMarkerAlpha = F(j, "depart_marker_alpha");
            t.DepartMarkerInnerFraction = F(j, "depart_marker_inner_fraction");
            t.DepartHoldRingRadiusCells = F(j, "depart_hold_ring_radius_cells");
            t.DepartHoldRingWidthCells = F(j, "depart_hold_ring_width_cells");
            t.DepartHoldRingColor = Col(j, "depart_hold_ring_color");
            t.DepartHoldRingAlpha = F(j, "depart_hold_ring_alpha");
            t.DepartHoldRingSegments = Int(j, "depart_hold_ring_segments");
            t.DepartHoldCancelSeconds = F(j, "depart_hold_cancel_seconds");

            t.FeetBelowCenterCells = F(j, "feet_below_center_cells");
            t.ShadowSizeCells = V2(j["shadow_size_cells"]);
            t.ShadowColor = Col(j, "shadow_color");
            t.ShadowAlpha = F(j, "shadow_alpha");
            t.InvulnBlinkHz = F(j, "invuln_blink_hz");
            t.InvulnBlinkAlpha = F(j, "invuln_blink_alpha");
            t.InvulnReducedAlpha = F(j, "invuln_reduced_alpha");
            t.InvulnReducedOutlinePx = F(j, "invuln_reduced_outline_px");
            t.HitFlashSeconds = F(j, "hit_flash_seconds");
            t.HitFlashColor = Col(j, "hit_flash_color");
            t.AfterimageCount = Int(j, "afterimage_count");
            t.AfterimageAlpha = F(j, "afterimage_alpha");
            t.AfterimageGapTicks = Int(j, "afterimage_gap_ticks");
            t.RimColor = Col(j, "rim_color");
            t.RimPx = F(j, "rim_px");
            t.LayerZStep = F(j, "layer_z_step");

            t.ClipIdle = Str(j, "clip_idle");
            t.ClipWalk = Str(j, "clip_walk");
            t.ClipStance = Str(j, "clip_stance");
            t.ClipFire = Str(j, "clip_fire");
            t.ClipSpecial = Str(j, "clip_special");
            t.ClipSignature = Str(j, "clip_signature");
            t.ClipHit = Str(j, "clip_hit");
            t.ClipDeath = Str(j, "clip_death");
            Strings(j["down_only_clips"], t.DownOnlyClips);

            if (j["wand_up_split"] is JObject split)
                foreach (var kv in split)
                    if (kv.Value is JObject o) t.WandUpSplit[kv.Key] = new Vector2Int(Int(o, "split_y"), Int(o, "offset_x"));
            t.WandCritGlowColor = Col(j, "wand_crit_glow_color");
            t.WandCritGlowAlpha = F(j, "wand_crit_glow_alpha");
            t.WandCritGlowRadiusPx = Int(j, "wand_crit_glow_radius_px");
            t.WandCritTintAmount = F(j, "wand_crit_tint_amount");
            t.WandLockColor = Col(j, "wand_lock_color");
            t.WandLockTintAmount = F(j, "wand_lock_tint_amount");
            t.WandLockRebrightenSeconds = F(j, "wand_lock_rebrighten_seconds");
            if (j["wand_idle_particles"] is JObject ip)
            {
                t.WandIdleParticlesNormal = Int(ip, "normal");
                t.WandIdleParticlesCritical = Int(ip, "critical");
                t.WandIdleParticlesLocked = Int(ip, "locked");
            }
            t.WandParticleLifetimeSeconds = F(j, "wand_particle_lifetime_seconds");
            t.WandParticleRiseCells = F(j, "wand_particle_rise_cells");
            t.WandParticleRadiusPx = Int(j, "wand_particle_radius_px");
            ColorMap(j["wand_particle_colors"], t.WandParticleColors);
            t.WandKilnLineColor = Col(j, "wand_kiln_line_color");
            t.WandKilnLinePx = V2i(j["wand_kiln_line_px"]);
            if (j["wand_detached_stages"] is JObject ds)
                foreach (var kv in ds) t.WandDetachedStages[kv.Key] = V2i(kv.Value);
            if (j["wand_detached_heat_shrink"] is JObject hs)
                foreach (var kv in hs) t.WandDetachedHeatShrink[kv.Key] = kv.Value.Type == JTokenType.Float || kv.Value.Type == JTokenType.Integer ? kv.Value.Value<float>() : 1f;
            t.WandDebtRingColor = Col(j, "wand_debt_ring_color");
            t.WandDebtRingRadiusPx = Int(j, "wand_debt_ring_radius_px");

            t.MarkMutationColor = Col(j, "mark_mutation_color");
            t.MarkMutationPx = Int(j, "mark_mutation_px");
            t.MarkMutationRingGroup = Str(j, "mark_mutation_ring_group");
            t.MarkMutationRingKey = Str(j, "mark_mutation_ring_key");
            ColorMap(j["mark_expansion_colors"], t.MarkExpansionColors);
            t.MarkExpansionPx = Int(j, "mark_expansion_px");
            t.MarkExpansionStepPx = Int(j, "mark_expansion_step_px");
            t.MarkScarColor = Col(j, "mark_scar_color");
            t.MarkScarPx = Int(j, "mark_scar_px");
            t.MarkScarMax = Int(j, "mark_scar_max");
            V2iList(j["mark_scar_offsets_px"], t.MarkScarOffsetsPx);
            t.MarkCrackColor = Col(j, "mark_crack_color");
            t.MarkCrackSizePx = V2i(j["mark_crack_size_px"]);
            t.MarkCrackMax = Int(j, "mark_crack_max");
            V2iList(j["mark_crack_offsets_px"], t.MarkCrackOffsetsPx);

            if (j["wild_stain_parts"] is JObject wp)
                foreach (var kv in wp)
                {
                    var byStage = new Dictionary<int, List<string>>();
                    if (kv.Value is JObject stages)
                        foreach (var s in stages)
                        {
                            if (!int.TryParse(s.Key, out int st)) continue;
                            var list = new List<string>();
                            Strings(s.Value, list);
                            byStage[st] = list;
                        }
                    t.WildStainParts[kv.Key] = byStage;
                }
            if (j["wild_stain_inheritance"] is JObject inh)
                foreach (var kv in inh)
                {
                    if (!int.TryParse(kv.Key, out int st)) continue;
                    var list = new List<int>(Ints(kv.Value));
                    t.WildStainInheritance[st] = list;
                }
            t.CrackedMirrorLayer = Str(j, "cracked_mirror_layer");
            return t;
        }

        // ---------------------------------------------------------------- 읽기 도우미

        static string Str(JObject j, string key) => j[key] != null && j[key].Type == JTokenType.String ? (string)j[key] : null;

        static float F(JObject j, string key)
        {
            var v = j[key];
            return v != null && (v.Type == JTokenType.Float || v.Type == JTokenType.Integer) ? v.Value<float>() : 0f;
        }

        static int Int(JObject j, string key)
        {
            var v = j[key];
            return v != null && (v.Type == JTokenType.Integer || v.Type == JTokenType.Float) ? (int)v.Value<double>() : 0;
        }

        static int[] Ints(JToken t)
        {
            if (!(t is JArray a)) return new int[0];
            var r = new int[a.Count];
            for (int i = 0; i < a.Count; i++) r[i] = (int)a[i].Value<double>();
            return r;
        }

        static Vector2 V2(JToken t)
        {
            if (!(t is JArray a) || a.Count < 2) return Vector2.zero;
            return new Vector2(a[0].Value<float>(), a[1].Value<float>());
        }

        static Vector2Int V2i(JToken t)
        {
            if (!(t is JArray a) || a.Count < 2) return Vector2Int.zero;
            return new Vector2Int((int)a[0].Value<double>(), (int)a[1].Value<double>());
        }

        static void V2iList(JToken t, List<Vector2Int> into)
        {
            if (!(t is JArray a)) return;
            for (int i = 0; i < a.Count; i++) into.Add(V2i(a[i]));
        }

        static void Strings(JToken t, List<string> into)
        {
            if (!(t is JArray a)) return;
            for (int i = 0; i < a.Count; i++) if (a[i].Type == JTokenType.String) into.Add((string)a[i]);
        }

        static void ColorMap(JToken t, Dictionary<string, Color> into)
        {
            if (!(t is JObject o)) return;
            foreach (var kv in o)
                if (kv.Value.Type == JTokenType.String) into[kv.Key] = ParseColor((string)kv.Value);
        }

        static Color Col(JObject j, string key) => ParseColor(Str(j, key));

        /// <summary>"#RRGGBB" → 색(sRGB 값 그대로 — 00 문서 7절 색 공간). 꼴이 틀리면 투명.</summary>
        public static Color ParseColor(string hex)
        {
            if (string.IsNullOrEmpty(hex)) return new Color(0, 0, 0, 0);
            Color c;
            return ColorUtility.TryParseHtmlString(hex, out c) ? c : new Color(0, 0, 0, 0);
        }
    }
}
