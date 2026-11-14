using System;
using UnityEngine;
using Wizard.Solo.Art;

namespace Wizard.Solo.Screens.Enhance
{
    /// <summary>
    /// 강화 연출 공통 그림(Assets/Resources/Art/common/Enhancement·Combat — 사용자 제작, solo/CommonAssets/README.md).
    /// 그림 창고(IArtLibrary.CommonFrames)가 파일 이름의 _WxH·_fN대로 잘라 준 칸들을 위→아래, 왼쪽→오른쪽 순으로 돌려준다.
    /// 창고가 null이거나 그림이 없으면 빈 배열(그 연출만 빠진다).
    /// </summary>
    public static class EnhanceCommonArt
    {
        /// <summary>IArtLibrary.CommonFrames의 relPath 앞부분(Resources/Art/common 기준).</summary>
        public const string Dir = "Enhancement/", CombatDir = "Combat/";
        public const string SuccessBurst = "success_burst_256x256_f8";
        public const string Aura7 = "aura_07_blue_128x128_f6", Aura10 = "aura_10_purple_128x128_f6", Aura13 = "aura_13_prismatic_128x128_f6";
        public const string FailureCracks = "failure_cracks_1920x1080";
        public const string BannerSuccess = "result_banner_success_480x120", BannerFailure = "result_banner_failure_480x120";
        public const string LightRay = "light_ray_32x512";
        public const string BloomGlow = "bloom_glow_64x64", Shockwave = "shockwave_ring_128x128_f5", Pillar = "level_wave_pillar_64x256_f4";

        /// <summary>프레임 빠르기(초당). 원본에 fps가 없어 표시용으로 정한다.</summary>
        public const float BurstFps = 16f, AuraFps = 8f, ShockFps = 14f, PillarFps = 10f;

        public static Sprite[] Frames(IArtLibrary art, string dir, string file)
        {
            if (art == null || string.IsNullOrEmpty(file)) return Array.Empty<Sprite>();
            Sprite[] list;
            try { list = art.CommonFrames(dir + file); }   // 창고가 캐시한다
            catch (Exception) { list = null; }
            return list ?? Array.Empty<Sprite>();
        }

        public static Sprite[] Enh(IArtLibrary art, string file) => Frames(art, Dir, file);
        public static Sprite[] Combat(IArtLibrary art, string file) => Frames(art, CombatDir, file);

        public static Sprite First(Sprite[] frames) => frames != null && frames.Length > 0 ? frames[0] : null;

        /// <summary>효과 재생기에 넘길 애니(그림이 없으면 null).</summary>
        public static SpriteAnim Anim(Sprite[] frames, float fps, bool loop, string key)
        {
            if (frames == null || frames.Length == 0) return null;
            return new SpriteAnim { Key = key, Frames = frames, Fps = fps, Loop = loop };
        }

        /// <summary>도착 단계의 반복 오라(+7 파랑 / +10 보라 / +13 무지개 금). +6 이하는 null.</summary>
        public static string AuraFor(int stage) => stage >= 13 ? Aura13 : stage >= 10 ? Aura10 : stage >= 7 ? Aura7 : null;
    }
}
