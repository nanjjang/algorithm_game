using UnityEngine;
using Wizard.Solo.Art;

namespace Wizard.Solo.View.Enemies
{
    /// <summary>풀에서 빌린 MeshRenderer 하나와 이 층이 만든 Mesh 하나. 돌려주기 전에 Mesh를 부순다(ISpritePool.RentMesh 약속).</summary>
    public sealed class MeshSlot
    {
        public MeshRenderer Renderer;
        public Mesh Mesh;
        readonly ISpritePool _pool;

        public MeshSlot(ISpritePool pool, int sortingOrder, Transform parent = null)
        {
            _pool = pool;
            Renderer = pool != null ? pool.RentMesh(sortingOrder, SpriteMaterialKind.Shape) : null;
            if (Renderer == null) return;
            if (parent != null) Renderer.transform.SetParent(parent, false);
            Mesh = new Mesh { name = "G15Shape" };
            Mesh.MarkDynamic();
            var mf = Renderer.GetComponent<MeshFilter>();
            if (mf != null) mf.sharedMesh = Mesh;
            Renderer.sortingOrder = sortingOrder;
        }

        public void Upload(ShapeMeshBuilder b)
        {
            if (Mesh == null) return;
            b.Upload(Mesh);
            if (Renderer != null) Renderer.enabled = b.TriangleCount > 0;
        }

        public void Release()
        {
            if (Renderer != null)
            {
                var mf = Renderer.GetComponent<MeshFilter>();
                if (mf != null) mf.sharedMesh = null;
                _pool?.Return(Renderer);
            }
            if (Mesh != null) DestroyObj(Mesh);
            Renderer = null;
            Mesh = null;
        }

        public static void DestroyObj(Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Object.Destroy(o);
            else Object.DestroyImmediate(o);
        }
    }

    /// <summary>스프라이트 빌리기·돌려주기 도우미(없으면 빌리고, 필요 없으면 돌려준다).</summary>
    public static class PoolUtil
    {
        public static SpriteRenderer Ensure(ISpritePool pool, ref SpriteRenderer sr, Sprite sprite, int order, Transform parent)
        {
            if (sr == null)
            {
                if (pool == null) return null;
                sr = pool.Rent(sprite, order, SpriteMaterialKind.Sprite);
                if (sr == null) return null;
                if (parent != null) sr.transform.SetParent(parent, false);
            }
            if (sr.sprite != sprite) sr.sprite = sprite;
            if (sr.sortingOrder != order) sr.sortingOrder = order;
            if (!sr.enabled) sr.enabled = true;
            return sr;
        }

        public static void Release(ISpritePool pool, ref SpriteRenderer sr)
        {
            if (sr == null) return;
            pool?.Return(sr);
            sr = null;
        }

        /// <summary>반복하지 않는 효과(manifest loop=false)도 계속 돌리고 싶을 때의 프레임.</summary>
        public static Sprite LoopFrame(SpriteAnim a, float seconds)
        {
            if (a == null || a.Frames == null || a.Frames.Length == 0) return null;
            float fps = a.Fps > 0f ? a.Fps : 12f;
            int i = (int)(Mathf.Max(0f, seconds) * fps) % a.Frames.Length;
            return a.Frames[i];
        }

        /// <summary>frames 목록의 칸을 차례로(반복 여부 선택).</summary>
        public static Sprite SeqFrame(SpriteAnim a, int[] frames, float seconds, bool loop)
        {
            if (a == null || a.Frames == null || a.Frames.Length == 0) return null;
            if (frames == null || frames.Length == 0) return a.Frames[0];
            float fps = a.Fps > 0f ? a.Fps : 12f;
            int i = (int)(Mathf.Max(0f, seconds) * fps);
            i = loop ? i % frames.Length : Mathf.Min(i, frames.Length - 1);
            int f = Mathf.Clamp(frames[i], 0, a.Frames.Length - 1);
            return a.Frames[f];
        }

        public static float SeqDuration(SpriteAnim a, int[] frames)
        {
            float fps = a != null && a.Fps > 0f ? a.Fps : 12f;
            return (frames != null ? frames.Length : 1) / fps;
        }

        /// <summary>스프라이트의 기준점에서 위 끝까지(칸).</summary>
        public static float TopAbovePivot(Sprite s)
        {
            if (s == null) return 0.5f;
            float ppu = s.pixelsPerUnit > 0f ? s.pixelsPerUnit : SimConst.PixelsPerUnit;
            return (s.rect.height - s.pivot.y) / ppu;
        }

        /// <summary>스프라이트의 기준점에서 가운데까지(칸, 위 +).</summary>
        public static float CenterAbovePivot(Sprite s)
        {
            if (s == null) return 0.25f;
            float ppu = s.pixelsPerUnit > 0f ? s.pixelsPerUnit : SimConst.PixelsPerUnit;
            return (s.rect.height * 0.5f - s.pivot.y) / ppu;
        }

        public static float WidthCells(Sprite s)
        {
            if (s == null) return 1f;
            float ppu = s.pixelsPerUnit > 0f ? s.pixelsPerUnit : SimConst.PixelsPerUnit;
            return s.rect.width / ppu;
        }
    }
}
