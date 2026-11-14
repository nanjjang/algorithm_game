using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using Wizard.Solo.Art;

namespace Wizard.Solo.View.Effects
{
    /// <summary>
    /// 사용자가 만든 공용 전투 이펙트 시트(Resources/Art/common/Combat, 이름의 _WxH·_fN대로 잘림)를 그림 창고(IArtLibrary.CommonFrames)로 읽는다.
    /// 창고가 null이거나 없는 시트면 null(부르는 쪽이 코드 그림으로 대신). 머티리얼은 만들지 않는다(풀이 넣는다).
    /// </summary>
    public static class CombatFxArt
    {
        /// <summary>IArtLibrary.CommonFrames의 relPath 앞부분(Resources/Art/common 기준).</summary>
        public const string Root = "Combat/";
        public const string HitSparkWhite = "hit_spark_white_32x32_f4", HitSparkYellow = "hit_spark_yellow_32x32_f4", HitSparkIce = "hit_spark_ice_32x32_f4";
        public const string MuzzleFire = "muzzle_flash_fire_32x32_f3", MuzzleIce = "muzzle_flash_ice_32x32_f3",
            MuzzleLightning = "muzzle_flash_lightning_32x32_f3", MuzzleShadow = "muzzle_flash_shadow_32x32_f3";
        public const string KillNormal = "kill_burst_normal_64x64_f6", KillElite = "kill_burst_elite_64x64_f6";
        public const string Shockwave = "shockwave_ring_128x128_f5", BloomGlow = "bloom_glow_64x64", WavePillar = "level_wave_pillar_64x256_f4";

        // 창고마다 따로(창고가 사라지면 같이 사라진다). 열쇠 = 시트@fps.
        static readonly ConditionalWeakTable<IArtLibrary, Dictionary<string, SpriteAnim>> _cache =
            new ConditionalWeakTable<IArtLibrary, Dictionary<string, SpriteAnim>>();

        /// <summary>시트 하나를 애니로(fps = 초당 장 수, 반복 안 함). 없으면 null.</summary>
        public static SpriteAnim Anim(IArtLibrary art, string sheet, float fps)
        {
            if (art == null || string.IsNullOrEmpty(sheet)) return null;
            var cache = _cache.GetOrCreateValue(art);
            string key = sheet + "@" + fps.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (cache.TryGetValue(key, out var a)) return a;
            Sprite[] frames = null;
            try { frames = art.CommonFrames(Root + sheet); } catch (Exception) { frames = null; }
            a = frames == null || frames.Length == 0 || frames[0] == null
                ? null
                : new SpriteAnim { Key = sheet, Frames = frames, Fps = fps > 0f ? fps : 12f, Loop = false };
            cache[key] = a;
            return a;
        }

        /// <summary>한 장짜리(빛 덩이 등). 없으면 null.</summary>
        public static Sprite Single(IArtLibrary art, string sheet)
        {
            var a = Anim(art, sheet, 1f);
            return a != null ? a.Frames[0] : null;
        }

        /// <summary>속성 → 적중 불꽃 시트(얼음은 얼음색, 번개·불은 노랑, 나머지 흰색 — 흰색은 속성색으로 입힌다).</summary>
        public static string HitSparkFor(string elementId, out bool tint)
        {
            tint = false;
            if (elementId == Ids.Element.Ice) return HitSparkIce;
            if (elementId == Ids.Element.Lightning || elementId == Ids.Element.Fire) return HitSparkYellow;
            tint = true;
            return HitSparkWhite;
        }

        /// <summary>속성 → 발사 섬광 시트(불·얼음·번개·어둠). 비전·무 속성은 번개 시트를 속성색으로 입힌다.</summary>
        public static string MuzzleFor(string elementId, out bool tint)
        {
            tint = false;
            if (elementId == Ids.Element.Fire) return MuzzleFire;
            if (elementId == Ids.Element.Ice) return MuzzleIce;
            if (elementId == Ids.Element.Lightning) return MuzzleLightning;
            if (elementId == Ids.Element.Dark) return MuzzleShadow;
            tint = true;
            return MuzzleLightning;
        }
    }
}
