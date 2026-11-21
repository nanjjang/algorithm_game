using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using Wizard.Solo.Art;

namespace Wizard.Solo.UI
{
    /// <summary>
    /// 사용자가 만든 공통 HUD 그림(Resources/Art/common/HUD/, Point 필터, 64PPU)을 그림 창고(IArtLibrary.CommonSprite)로 읽는 작은 도우미.
    /// 그림이 없거나 창고가 null이면 null을 돌려주고 부르는 쪽이 키트 그림이나 단색으로 그린다(예외 없음). 9조각 테두리는 여기서 정한 값으로
    /// 다시 자른다(게이지 틀·채움, 보스 이름판은 가로로만 늘어나게 좌우 끝만 고정; 판은 창고가 준 네 변 12px 그대로).
    /// 픽셀 그림이므로 키울 때는 정수배만 쓴다(PixelScale).
    /// </summary>
    public static class UiCommonArt
    {
        /// <summary>IArtLibrary.CommonFrames의 relPath 앞부분(Resources/Art/common 기준).</summary>
        public const string Folder = "HUD/";
        public const string Panel = "panel_9slice_48x48";
        public const string GaugeFrame = "gauge_frame_256x32";
        public const string GaugeFill = "gauge_fill_256x32";
        public const string BossNameplate = "boss_nameplate_512x64";
        public const string IconGold = "icon_gold_24x24";
        public const string IconScore = "icon_score_24x24";
        public const string IconKill = "icon_kill_24x24";
        public const string IconClock = "icon_clock_24x24";

        /// <summary>판 9조각 테두리(네 변 12px, README).</summary>
        public const int PanelBorderPx = 12;
        /// <summary>게이지 좌우 끝 고정 폭(px). 틀의 장식 끝이 늘어나지 않게.</summary>
        public const int GaugeCapPx = 12;
        /// <summary>보스 이름판 좌우 끝 고정 폭(px).</summary>
        public const int NameplateCapPx = 56;
        /// <summary>게이지 그림의 높이(px).</summary>
        public const float GaugeH = 32f;

        // 창고마다 따로(창고가 Dispose되면 같이 사라진다). 열쇠 = 이름|테두리.
        static readonly ConditionalWeakTable<IArtLibrary, Dictionary<string, Sprite>> _cache =
            new ConditionalWeakTable<IArtLibrary, Dictionary<string, Sprite>>();

        /// <summary>그대로 쓰는 그림(아이콘). 없으면 null.</summary>
        public static Sprite Get(IArtLibrary art, string name) => Load(art, name, Vector4.zero);

        /// <summary>판(9조각, 네 변 12px). 없으면 null.</summary>
        public static Sprite PanelSprite(IArtLibrary art) => Load(art, Panel, new Vector4(PanelBorderPx, PanelBorderPx, PanelBorderPx, PanelBorderPx));
        /// <summary>게이지 틀·채움(좌우 끝만 고정). 없으면 null.</summary>
        public static Sprite GaugeFrameSprite(IArtLibrary art) => Load(art, GaugeFrame, new Vector4(GaugeCapPx, 0, GaugeCapPx, 0));
        public static Sprite GaugeFillSprite(IArtLibrary art) => Load(art, GaugeFill, new Vector4(GaugeCapPx, 0, GaugeCapPx, 0));
        /// <summary>보스 이름판(좌우 끝만 고정). 없으면 null.</summary>
        public static Sprite NameplateSprite(IArtLibrary art) => Load(art, BossNameplate, new Vector4(NameplateCapPx, 0, NameplateCapPx, 0));

        public static bool HasPanel(IArtLibrary art) => PanelSprite(art) != null;

        /// <summary>Image에 판 그림을 입힌다(Sliced, 1px = 1px × scale). 그림이 없으면 false.</summary>
        public static bool ApplyPanel(IArtLibrary art, UnityEngine.UI.Image img, float alpha = 1f, int scale = 1)
        {
            var sp = PanelSprite(art);
            if (img == null || sp == null) return false;
            ApplySliced(img, sp, scale);
            img.color = new Color(1f, 1f, 1f, alpha);
            return true;
        }

        /// <summary>Image에 9조각 그림을 정수배로 입힌다.</summary>
        public static void ApplySliced(UnityEngine.UI.Image img, Sprite sp, int scale = 1)
        {
            if (img == null || sp == null) return;
            img.sprite = sp;
            img.type = UnityEngine.UI.Image.Type.Sliced;
            img.fillCenter = true;
            // 캔버스 referencePixelsPerUnit = 64 = 그림 PPU → 1px = 1px. 정수배로 키우려면 배율의 역수.
            img.pixelsPerUnitMultiplier = 1f / Mathf.Max(1, scale);
        }

        static Sprite Load(IArtLibrary art, string name, Vector4 border)
        {
            if (art == null) return null;
            var cache = _cache.GetOrCreateValue(art);
            string key = name + "|" + border;
            if (cache.TryGetValue(key, out var cached) && cached != null) return cached;
            Sprite result = null;
            try
            {
                // 창고가 시트 한 장을 통째로 잘라 준다(가운데 피벗, 64PPU; 이름에 9slice가 든 판은 네 변 12px).
                var src = art.CommonSprite(Folder + name);
                if (src != null)
                {
                    if (src.border == border) result = src;
                    else
                    {
                        result = Sprite.Create(src.texture, src.rect, new Vector2(0.5f, 0.5f), src.pixelsPerUnit, 0, SpriteMeshType.FullRect, border);
                        result.name = name;
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("UiCommonArt: " + name + ": " + ex.Message);
                result = null;
            }
            cache[key] = result;
            return result;
        }
    }
}
