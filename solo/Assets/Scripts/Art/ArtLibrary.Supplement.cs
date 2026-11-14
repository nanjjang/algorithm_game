using System;
using System.Collections.Generic;
using UnityEngine;

namespace Wizard.Solo.Art
{
    public sealed partial class ArtLibrary
    {
        // ---------- 솔로 보충 그림(09 요약서 9절 빠진 그림) ----------
        //
        // Resources/Art/supplement/solo/manifest.json(Tools/art_supplement.py가 만든다, 효과 manifest 형식)을 읽는다.
        // 묶음 이름이 기존 효과 묶음(player/special/ritual/loot/wand)과 같으면 그 묶음에 열쇠를 더하고,
        // 새 묶음(ambient = 반딧불, icons = 아이콘)은 그대로 더한다.
        // icons 묶음의 열쇠: 콘텐츠 id(tod.*, whim.*, frame.*)는 Icon(contentId)가, "UI id/변형"(I-M10/borrowed_tag,
        // I-M11/bundle_open, I-M12/bag_slot_empty|filled|hero)은 Ui·UiSized·UiAnim·Icon("ui:…")이 UI manifest에 없을 때 쓴다.

        public const string SoloIconsGroup = "icons";
        const string SoloSupplementDir = "supplement/solo/";

        void LoadSoloSupplement()
        {
            MergeSupplement(SoloSupplementDir);
            // 캐릭터 교체 묶음의 새 그림(원본 자리가 없는 것): supplement/solo/char.N/manifest.json, 묶음 이름 "char.N"
            // (Tools/art_disciple2.py가 만든다). 열쇠 = 그 묶음 manifest의 graphics 열쇠(예 "fx/bolt_dash_trail").
            for (int n = 1; n <= ArtAliases.CharacterCount; n++) MergeSupplement(SoloSupplementDir + Ids.Char.Of(n) + "/");
        }

        void MergeSupplement(string dir)
        {
            var extra = Parse(dir + "manifest", j => ManifestParser.ParseEffects(j, dir), optional: true);
            if (extra == null) return;
            foreach (var group in extra.Groups)
            {
                if (_effects.ByName.TryGetValue(group.Name, out var original)) ApplyEffectOverrides(original, group);
                else AddGroup(group);
            }
        }

        /// <summary>보충 아이콘 묶음의 항목(없으면 null).</summary>
        EffectEntry SoloIconEntry(string key)
        {
            if (key == null || !_effects.ByName.TryGetValue(SoloIconsGroup, out var g)) return null;
            return g.ByKey.TryGetValue(key, out var e) && e.Frames.Count > 0 ? e : null;
        }

        /// <summary>보충 아이콘 한 장(첫 프레임). 없으면 null.</summary>
        Sprite SoloIcon(string key)
        {
            if (SoloIconEntry(key) == null) return null;
            var a = Effect(SoloIconsGroup, key);
            return a.Frames != null && a.Frames.Length > 0 ? a.Frames[0] : null;
        }

        static string UiKey(string id, string variant) => variant != null ? id + "/" + variant : id;

        // ---------- 공통 에셋(Resources/Art/common, 이미 meta에서 잘린 시트) ----------

        const string CommonRoot = ArtAliases.ResourceRoot + "common/";
        readonly Dictionary<string, Sprite[]> _common = new Dictionary<string, Sprite[]>();

        /// <summary>
        /// Resources/Art/common 아래 시트의 프레임들(relPath = "Combat/hit_spark_ice_32x32_f4" 꼴, 확장자 없음).
        /// meta에 잘려 있는 스프라이트를 이름 끝 _NN 번호 순으로 준다(한 장짜리 시트면 1개). 캐시한다. 없으면 빈 배열.
        /// 이 폴더는 사용자가 Unity에서 잘라 둔 것이라 manifest 대신 meta 슬라이스를 읽는다(LoadAll은 그 시트 한 장만 읽는다).
        /// </summary>
        public Sprite[] CommonFrames(string relPath)
        {
            if (string.IsNullOrEmpty(relPath)) return Array.Empty<Sprite>();
            if (_common.TryGetValue(relPath, out var cached)) return cached;
            // ArtImportPostprocessor가 Resources/Art 아래를 Single로 들이므로(시트 자르기는 런타임 몫), 파일 이름의
            // "_WxH"(한 프레임 크기)와 "_fN"(프레임 수)로 여기서 자른다. 줄은 위에서 아래, 줄 안은 왼쪽에서 오른쪽(CommonAssets/README).
            var tex = Resources.Load<Texture2D>(CommonRoot + relPath);
            Sprite[] frames;
            if (tex == null)
            {
                frames = Array.Empty<Sprite>();
                Miss("common " + relPath);
            }
            else
            {
                tex.filterMode = FilterMode.Point;
                string file = relPath.Substring(relPath.LastIndexOf('/') + 1);
                ParseCommonName(file, tex.width, tex.height, out int fw, out int fh, out int count);
                int cols = Math.Max(1, tex.width / fw), rows = Math.Max(1, tex.height / fh);
                count = Math.Min(count, cols * rows);
                // 9-slice 패널은 네 변 12px(README). 빛줄기는 아래 가운데 피벗.
                var border = file.Contains("9slice") ? new Vector4(12, 12, 12, 12) : Vector4.zero;
                var pivot = file.StartsWith("light_ray", StringComparison.Ordinal) ? new Vector2(0.5f, 0f) : new Vector2(0.5f, 0.5f);
                frames = new Sprite[count];
                for (int i = 0; i < count; i++)
                {
                    int cx = i % cols, cy = i / cols;
                    var rect = new Rect(cx * fw, tex.height - (cy + 1) * fh, fw, fh);
                    var s = Sprite.Create(tex, rect, pivot, RectMath.PixelsPerUnit, 0, SpriteMeshType.FullRect, border, false);
                    s.name = file + "_" + i.ToString("00");
                    frames[i] = s;
                }
            }
            _common[relPath] = frames;
            return frames;
        }

        /// <summary>공통 에셋 한 장(시트의 첫 프레임). 없으면 null(부르는 쪽이 대신 그린다).</summary>
        public Sprite CommonSprite(string relPath)
        {
            var f = CommonFrames(relPath);
            return f.Length > 0 ? f[0] : null;
        }

        /// <summary>
        /// 공통 에셋 파일 이름에서 한 프레임 크기("_WxH", 마지막 것)와 프레임 수("_fN")를 읽는다.
        /// 크기 표기가 없거나 시트보다 크면 시트 전체를 한 프레임으로 본다. 프레임 수가 없으면 시트에 들어가는 칸 수.
        /// </summary>
        static void ParseCommonName(string file, int texW, int texH, out int fw, out int fh, out int count)
        {
            fw = texW; fh = texH; count = -1;
            var parts = file.Split('_');
            for (int i = 0; i < parts.Length; i++)
            {
                string p = parts[i];
                int x = p.IndexOf('x');
                if (x > 0 && x < p.Length - 1 && int.TryParse(p.Substring(0, x), out int w) && int.TryParse(p.Substring(x + 1), out int h)
                    && w > 0 && h > 0 && w <= texW && h <= texH)
                { fw = w; fh = h; }
                else if (p.Length > 1 && p[0] == 'f' && int.TryParse(p.Substring(1), out int n) && n > 0)
                    count = n;
            }
            if (count < 0) count = Math.Max(1, (texW / fw) * (texH / fh));
        }
    }
}
