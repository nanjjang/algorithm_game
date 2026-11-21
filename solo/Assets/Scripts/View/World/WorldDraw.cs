using UnityEngine;
using Wizard.Solo.Art;

namespace Wizard.Solo.View.World
{
    /// <summary>표시 층 공용 그리기 도우미(풀에서 빌린 렌더러에 그림·위치·색·PropertyBlock을 넣는다).</summary>
    public static class WorldDraw
    {
        /// <summary>애니의 지금 장(초). 프레임이 없으면 null.</summary>
        public static Sprite Frame(SpriteAnim anim, float seconds) => anim != null ? anim.At(seconds) : null;

        /// <summary>애니의 n번째 장(범위 밖은 자른다).</summary>
        public static Sprite FrameAt(SpriteAnim anim, int index)
        {
            if (anim == null || anim.Frames == null || anim.Frames.Length == 0) return null;
            if (index < 0) index = 0;
            if (index >= anim.Frames.Length) index = anim.Frames.Length - 1;
            return anim.Frames[index];
        }

        public static void Place(SpriteRenderer sr, Vector3 viewPos, float z = 0f)
        {
            if (sr == null) return;
            sr.transform.localPosition = new Vector3(viewPos.x, viewPos.y, z);
        }

        public static void SetAlpha(SpriteRenderer sr, Color baseColor, float alpha)
        {
            if (sr == null) return;
            sr.color = new Color(baseColor.r, baseColor.g, baseColor.b, baseColor.a * Mathf.Clamp01(alpha));
        }

        /// <summary>단색 덮기·외곽선(SpriteShaderProps)을 PropertyBlock으로 넣는다. 다 0이면 블록을 비운다.</summary>
        public static void SetShaderProps(SpriteRenderer sr, MaterialPropertyBlock block, Color flashColor, float flashAmount,
            Color outlineColor, float outlineSize)
        {
            if (sr == null) return;
            if (flashAmount <= 0f && outlineSize <= 0f)
            {
                sr.SetPropertyBlock(null);
                return;
            }
            block.Clear();
            block.SetColor(SpriteShaderProps.FlashColor, flashColor);
            block.SetFloat(SpriteShaderProps.FlashAmount, Mathf.Clamp01(flashAmount));
            block.SetColor(SpriteShaderProps.OutlineColor, outlineColor);
            block.SetFloat(SpriteShaderProps.OutlineSize, Mathf.Max(0f, outlineSize));
            sr.SetPropertyBlock(block);
        }

        /// <summary>WhitePixel(PPU 1)로 가로 w·세로 h칸 네모.</summary>
        public static void Rect(SpriteRenderer sr, Vector3 center, float w, float h, Color color, float z = 0f)
        {
            if (sr == null) return;
            sr.transform.localPosition = new Vector3(center.x, center.y, z);
            sr.transform.localScale = new Vector3(w, h, 1f);
            sr.color = color;
        }
    }
}
