using System;
using System.Collections.Generic;
using UnityEngine;

namespace Wizard.Solo.Art
{
    public sealed partial class ArtLibrary
    {
        // ---------- UI 키트 ----------

        /// <summary>
        /// UI 키트 한 장(낱장 PNG의 첫 프레임). 9조각 border = nine_slice_ltrb를 Unity (L, B, R, T)로(00 문서 8.2절).
        /// variant null = 그 id의 아무 변형(manifest 순서 첫 것), state null = normal. 상태 그림이 없으면 UiStates.Fallback 순서.
        /// </summary>
        public UiSprite Ui(string id, string variant = null, string state = null) => UiSized(id, variant, state, 0);

        /// <summary>크기 갈래가 있는 UI(K-40 16/24/32, K-41, K-43, K-53 …)에서 높이가 sizePx에 가까운 것.</summary>
        public UiSprite UiSized(string id, string variant, string state, int sizePx)
        {
            var e = UiSelect.Pick(_ui.Of(id), variant, state, sizePx);
            if (e == null)
            {
                // UI manifest에 없는 것(I-M10·I-M12 …)은 솔로 보충 아이콘(supplement/solo)에서 찾는다.
                var extra = SoloIcon(UiKey(id, variant));
                if (extra != null)
                    return new UiSprite { Sprite = extra, NineSlice = false, Width = (int)extra.rect.width, Height = (int)extra.rect.height };
                Miss("ui " + id + "/" + (variant ?? "*") + "/" + (state ?? "*"));
                int s = sizePx > 0 ? sizePx : 48;
                return new UiSprite { Sprite = Placeholder(s, s), NineSlice = false, Width = s, Height = s };
            }
            return UiFromEntry(e, 0);
        }

        /// <summary>UI 항목 정보(frame_ms, hotspot, frame_offsets 같은 값은 원본에서). 없으면 null.</summary>
        public UiEntry UiInfo(string id, string variant = null, string state = null, int sizePx = 0)
            => UiSelect.Pick(_ui.Of(id), variant, state, sizePx);

        /// <summary>
        /// 여러 프레임 UI(K-44 불러오는 모닥불, K-40 선택 괄호, K-42 새 소식 점 …)의 애니. fps = 1000 / frame_ms(없으면 0), 반복.
        /// </summary>
        public SpriteAnim UiAnim(string id, string variant = null, string state = null, int sizePx = 0)
        {
            var e = UiSelect.Pick(_ui.Of(id), variant, state, sizePx);
            if (e == null)
            {
                if (SoloIconEntry(UiKey(id, variant)) != null) return Effect(SoloIconsGroup, UiKey(id, variant));
                return PlaceholderAnim("uia|" + id, sizePx > 0 ? sizePx : 48, sizePx > 0 ? sizePx : 48);
            }
            string key = "uia|" + e.File;
            if (_anims.TryGetValue(key, out var cached)) return cached;
            var frames = new Sprite[Math.Max(1, e.Frames)];
            for (int i = 0; i < frames.Length; i++) frames[i] = UiFromEntry(e, i).Sprite;
            float fps = e.FrameMs > 0 ? MsPerSecond / e.FrameMs : 0f;
            return CacheAnim(key, new SpriteAnim { Frames = frames, Fps = fps, Loop = true });
        }

        const float MsPerSecond = 1000f;
        const int SoloKitScale = 2;

        UiSprite UiFromEntry(UiEntry e, int frame)
        {
            var border = RectMath.LtrbToUnityBorder(e.NineSliceLtrb);
            bool nine = border != Vector4.zero;
            var pivot = e.HasOrigin ? RectMath.ToPivot(e.SpriteOrigin, e.Width, e.Height) : Center;
            Sprite sp = null;
            if (nine && e.Frames == 1)
            {
                // The same logical size and slice widths apply to both texture densities.
                string path = UiDir + "solo_kit/" + e.File.Substring(e.File.LastIndexOf('/') + 1);
                int width = e.Width * SoloKitScale, height = e.Height * SoloKitScale;
                var texture = Tex(path, null, optional: true);
                if (texture != null && texture.width == width && texture.height == height)
                    sp = Cut("ui|" + path, path, new PixelRect(0, 0, width, height),
                        pivot, border * SoloKitScale, pixelsPerUnit: RectMath.PixelsPerUnit * SoloKitScale);
            }
            if (sp == null)
                sp = Cut("ui|" + e.File + "|" + frame, UiDir + e.File, RectMath.StripFrame(frame, e.Width, e.Height), pivot, border);
            return new UiSprite { Sprite = sp, NineSlice = nine, Width = e.Width, Height = e.Height };
        }

        // ---------- 아이콘 ----------

        /// <summary>
        /// 콘텐츠 id → 아이콘(09 요약서 8절). block/art/element/passive/wand_trait/currency/mat/charm/item/emote/cosmetic → UI I-* 아이콘
        /// (크기 갈래 중 sizePx에 가까운 것), mutation.* → 보강 mutation_icons 칸(D-39), boss.* → 보스 HUD(32)·초상(64 이상),
        /// item.health_shard → 드랍 그림 첫 장. "ui:&lt;ID&gt;/&lt;변형&gt;"이면 그 UI 항목. 그림이 없는 것(tod.*, whim.*, frame.* …)과
        /// 별빛(표시 금지)은 대체 그림.
        /// </summary>
        public Sprite Icon(string contentId, int sizePx = 32)
        {
            if (sizePx <= 0) sizePx = 32;
            if (contentId == null || ArtAliases.IsHiddenIcon(contentId)) return Placeholder(sizePx, sizePx);

            if (contentId.StartsWith("ui:", StringComparison.Ordinal))
            {
                string rest = contentId.Substring(3);
                int slash = rest.IndexOf('/');
                string id = slash >= 0 ? rest.Substring(0, slash) : rest;
                string variant = slash >= 0 ? rest.Substring(slash + 1) : null;
                return UiSized(id, variant, null, sizePx).Sprite;
            }
            if (contentId.StartsWith("mutation.", StringComparison.Ordinal))
            {
                int idx = ArtAliases.MutationIconIndex(contentId);
                var anim = Effect(EnhanceGroup, MutationIconsKey);
                if (idx >= 0 && anim.Frames != null && idx < anim.Frames.Length && EffectInfo(EnhanceGroup, MutationIconsKey) != null) return anim.Frames[idx];
                return Placeholder(sizePx, sizePx);
            }
            if (contentId.StartsWith("boss.", StringComparison.Ordinal))
                return sizePx >= BossPortraitMinPx ? BossPortrait(contentId) : BossHudIcon(contentId);
            if (contentId == Ids.Item.HealthShard)
            {
                var a = Effect("loot", "health_shard");
                return a.Frames != null && a.Frames.Length > 0 ? a.Frames[0] : Placeholder(sizePx, sizePx);
            }
            if (ArtAliases.TryIconRule(contentId, out var uiIds, out var v))
            {
                var all = new List<UiEntry>();
                foreach (var id in uiIds)
                {
                    var l = _ui.Of(id);
                    if (l != null) all.AddRange(l);
                }
                var e = UiSelect.Pick(all, v, null, sizePx);
                if (e != null) return UiFromEntry(e, 0).Sprite;
            }
            // 시간대·변덕·웨이브 틀(I-W3·I-W1·I-W2)은 UI manifest에 없고 솔로 보충 아이콘에 콘텐츠 id 열쇠로 있다(09 9절).
            var solo = SoloIcon(contentId);
            if (solo != null) return solo;
            return Placeholder(sizePx, sizePx);
        }

        const string MutationIconsKey = "mutation_icons";
        const int BossPortraitMinPx = 64;

        // ---------- 배경 ----------

        /// <summary>
        /// 화면 배경(1920×1080 등) 한 장. key:
        /// "title" 또는 "S-0201" = 타이틀(backgrounds/title_1920x1080), "enhance.workbench" = 강화 화면 작업대(보강),
        /// UI id(예 "S-0501" 메인 메뉴, "S-1602" 정산, "S-2601" 불러오기) = 그 UI 항목, world 열쇠(예 "field.backplate") = 그 그림 첫 장.
        /// </summary>
        public Sprite Background(string key)
        {
            if (key == null) return Placeholder(BackgroundW, BackgroundH);
            if (key == TitleKey)
            {
                // 솔로 타이틀 그림(solo/TitleScreenBackground.png 사본)이 있으면 그것, 없으면 UI external S-0201.
                foreach (var path in TitleSoloPaths)
                    if (Tex(path, null, optional: true) != null) return CutWhole("bg|" + path, path);
                key = TitleUiId;
            }
            if (key == WorkbenchKey)
                return _enhanceBackground != null ? CutWhole("bg|" + key, _enhanceBackground) : Placeholder(BackgroundW, BackgroundH);
            if (_ui.External.TryGetValue(key, out var ext))
                return CutWhole("bg|" + key, ArtAliases.Normalize(UiDir + ext));
            var list = _ui.Of(key);
            if (list != null && list.Count > 0) return UiFromEntry(list[0], 0).Sprite;
            var w = WorldInfo(key);
            if (w != null)
            {
                var a = World(key);
                if (a.Frames != null && a.Frames.Length > 0) return a.Frames[0];
            }
            return Placeholder(BackgroundW, BackgroundH);
        }

        public const string TitleKey = "title", WorkbenchKey = "enhance.workbench";
        const string TitleUiId = "S-0201";
        /// <summary>"title"이 먼저 찾는 솔로 교체 그림(앞쪽이 우선). 사용자 교체본(_v2) → solo/TitleScreenBackground.png 사본.</summary>
        static readonly string[] TitleSoloPaths = { "backgrounds/title_1920x1080_v2.png", "backgrounds/title_solo_1672x941.png" };
        const int BackgroundW = 1920, BackgroundH = 1080;

        // ---------- 글꼴 ----------

        // D-42: F-12·F-24 = Galmuri11, F-16·F-32·F-48·F-64 = neodgm. 크기는 쓰는 쪽이 Text.fontSize = (int)UiFontId로 준다.
        const string GalmuriPath = ArtAliases.ResourceRoot + UiDir + "font/Galmuri11";
        const string NeodgmPath = ArtAliases.ResourceRoot + UiDir + "font/neodgm";
        const string BuiltinFontName = "LegacyRuntime.ttf";

        Font _galmuri, _neodgm, _builtin;

        /// <summary>그 크기가 쓰는 TTF의 Resources 경로(D-42).</summary>
        public static string FontPathFor(UiFontId size)
        {
            return size == UiFontId.F12 || size == UiFontId.F24 ? GalmuriPath : NeodgmPath;
        }

        public Font Font(UiFontId size)
        {
            bool galmuri = FontPathFor(size) == GalmuriPath;
            ref Font slot = ref galmuri ? ref _galmuri : ref _neodgm;
            if (slot == null)
            {
                slot = _src.LoadFont(galmuri ? GalmuriPath : NeodgmPath);
                if (slot == null) Warn("글꼴 없음: " + (galmuri ? GalmuriPath : NeodgmPath));
                else ApplyPointFilter(slot);
            }
            if (slot != null) return slot;
            if (_builtin == null)
            {
                try { _builtin = Resources.GetBuiltinResource<Font>(BuiltinFontName); }
                catch (Exception) { _builtin = null; }
            }
            return _builtin;
        }

        void OnFontTextureRebuilt(Font f)
        {
            if (f != null && (f == _galmuri || f == _neodgm)) ApplyPointFilter(f);
        }

        /// <summary>동적 글꼴 텍스처의 필터는 임포터로 못 정한다 → 다시 만들어질 때마다 Point(00 문서 10절).</summary>
        static void ApplyPointFilter(Font f)
        {
            var mat = f.material;
            if (mat != null && mat.mainTexture != null) mat.mainTexture.filterMode = FilterMode.Point;
        }
    }
}
