using System;
using UnityEngine;
using Wizard.Solo.Art;

namespace Wizard.Solo.Screens.Enhance
{
    /// <summary>강화 묶음 화면의 작은 도우미(엔진 쪽).</summary>
    public static class EnhanceUi
    {
        /// <summary>플레이 중이면 Destroy, 편집 모드(EditMode 테스트)면 DestroyImmediate.</summary>
        public static void Destroy(GameObject go)
        {
            if (go == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(go);
            else UnityEngine.Object.DestroyImmediate(go);
        }

        public const string GroupEnhance = "enhance", GroupRitual = "ritual";

        /// <summary>효과 애니(그 항목이 manifest에 있을 때만. 없으면 null — 대체 바둑판을 그리지 않고 도형으로 대신한다).</summary>
        public static SpriteAnim EffectAnim(GameContext ctx, string group, string key)
        {
            if (ctx == null || ctx.Art == null || string.IsNullOrEmpty(key)) return null;
            try
            {
                if (ctx.Art.EffectInfo(group, key) == null) return null;
                var a = ctx.Art.Effect(group, key);
                return a != null && a.Frames != null && a.Frames.Length > 0 ? a : null;
            }
            catch (Exception) { return null; }
        }

        /// <summary>효과의 한 프레임(없으면 null).</summary>
        public static Sprite EffectFrame(GameContext ctx, string group, string key, int frame = 0)
        {
            var a = EffectAnim(ctx, group, key);
            if (a == null) return null;
            int i = frame < 0 ? 0 : (frame >= a.Frames.Length ? a.Frames.Length - 1 : frame);
            return a.Frames[i];
        }

        /// <summary>"element.fire" → "fire"(ritual 묶음의 열쇠 꼬리).</summary>
        public static string ElementSuffix(string elementId)
        {
            if (string.IsNullOrEmpty(elementId)) return "arcane";
            int dot = elementId.IndexOf('.');
            return dot >= 0 ? elementId.Substring(dot + 1) : elementId;
        }

        /// <summary>자식을 전부 걷는다.</summary>
        public static void Clear(Transform parent)
        {
            if (parent == null) return;
            for (int i = parent.childCount - 1; i >= 0; i--) Destroy(parent.GetChild(i).gameObject);
        }
    }
}
