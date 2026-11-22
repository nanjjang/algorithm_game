using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Wizard.Solo.Art;

namespace Wizard.Solo.Tests.PlayMode.Integration
{
    /// <summary>
    /// 솔로 셰이더의 색 충실도(00 문서 7절, D-70). 프로젝트의 색 공간은 Linear인데 코드의 색 상수(UiTheme, 위험색 #FF3B30 등)는 전부 sRGB 값이다.
    /// 흰 그림에 색 C를 입히면 화면의 점이 C여야 한다 — 길 넷 전부에서: SpriteRenderer.color, MaterialPropertyBlock의 덮기 색,
    /// 가산 혼합, 도형 메시의 정점 색. 어긋나면 위험색이 분홍으로 뜨는 식으로 색의 뜻이 깨진다.
    /// 작은 RenderTexture에 임시 카메라로 직접 그려 가운데 점을 읽는다(배치 모드에서도 돈다. 그래픽 장치는 있어야 한다).
    /// </summary>
    public class SoloShaderColorTests
    {
        const int Size = 32;
        const int Tolerance = 3; // 8비트 채널 오차(색 공간 변환 반올림)
        static readonly Vector3 Far = new Vector3(2000f, 2000f, 0f);

        // 위험색 빨강, 비전 청록, 중간 회색(감마가 어긋나면 가장 크게 틀어진다), 호박색
        static readonly Color32[] Samples =
        {
            new Color32(0xFF, 0x3B, 0x30, 0xFF), new Color32(0x4F, 0xE3, 0xC1, 0xFF),
            new Color32(0x80, 0x80, 0x80, 0xFF), new Color32(0xF2, 0xA3, 0x3A, 0xFF)
        };

        ArtLibrary _art;
        Camera _cam;
        RenderTexture _rt;
        readonly List<Object> _made = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            _made.Clear();
            _art = new ArtLibrary();
            var go = new GameObject("color-test-camera");
            _made.Add(go);
            _cam = go.AddComponent<Camera>();
            _cam.orthographic = true;
            _cam.orthographicSize = 1f;
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = Color.black;
            _cam.nearClipPlane = 0.1f;
            _cam.farClipPlane = 50f;
            _cam.transform.position = Far + new Vector3(0f, 0f, -10f);
            _cam.enabled = false;
            _rt = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32);
            _made.Add(_rt);
            _cam.targetTexture = _rt;
        }

        [TearDown]
        public void TearDown()
        {
            if (_cam != null) _cam.targetTexture = null;
            for (int i = 0; i < _made.Count; i++) if (_made[i] != null) Object.Destroy(_made[i]);
            _made.Clear();
            _art?.Dispose();
        }

        SpriteRenderer WhiteSprite(SpriteMaterialKind kind)
        {
            var go = new GameObject("color-test-sprite");
            _made.Add(go);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _art.WhitePixel;            // PPU 1 → 1칸
            sr.sharedMaterial = _art.Material(kind);
            go.transform.position = Far;
            go.transform.localScale = new Vector3(8f, 8f, 1f);
            return sr;
        }

        Color32 ReadCenter()
        {
            _cam.Render();
            var prev = RenderTexture.active;
            RenderTexture.active = _rt;
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
            tex.Apply(false);
            RenderTexture.active = prev;
            Color32 c = tex.GetPixel(Size / 2, Size / 2);
            Object.Destroy(tex);
            return c;
        }

        static void AssertSame(Color32 expected, Color32 actual, string what)
        {
            string msg = what + ": expected #" + ColorUtility.ToHtmlStringRGB(expected) + " but rendered #" + ColorUtility.ToHtmlStringRGB(actual);
            Assert.LessOrEqual(Mathf.Abs(expected.r - actual.r), Tolerance, msg);
            Assert.LessOrEqual(Mathf.Abs(expected.g - actual.g), Tolerance, msg);
            Assert.LessOrEqual(Mathf.Abs(expected.b - actual.b), Tolerance, msg);
        }

        [UnityTest]
        public IEnumerator Sprite_RendererColor_RendersAsGiven()
        {
            var sr = WhiteSprite(SpriteMaterialKind.Sprite);
            yield return null;
            for (int i = 0; i < Samples.Length; i++)
            {
                sr.color = Samples[i];
                yield return null;
                AssertSame(Samples[i], ReadCenter(), "Solo/Sprite SpriteRenderer.color");
            }
        }

        [UnityTest]
        public IEnumerator Sprite_FlashColor_RendersAsGiven()
        {
            var sr = WhiteSprite(SpriteMaterialKind.Sprite);
            var block = new MaterialPropertyBlock();
            yield return null;
            for (int i = 0; i < Samples.Length; i++)
            {
                sr.GetPropertyBlock(block);
                block.SetColor(SpriteShaderProps.FlashColor, Samples[i]);
                block.SetFloat(SpriteShaderProps.FlashAmount, 1f);
                sr.SetPropertyBlock(block);
                yield return null;
                AssertSame(Samples[i], ReadCenter(), "Solo/Sprite flash color");
            }
        }

        [UnityTest]
        public IEnumerator SpriteAdditive_RendererColor_OnBlack_RendersAsGiven()
        {
            var sr = WhiteSprite(SpriteMaterialKind.SpriteAdditive);
            yield return null;
            for (int i = 0; i < Samples.Length; i++)
            {
                sr.color = Samples[i];
                yield return null;
                AssertSame(Samples[i], ReadCenter(), "Solo/SpriteAdditive SpriteRenderer.color over black");
            }
        }

        [UnityTest]
        public IEnumerator Shape_VertexColor_RendersAsGiven()
        {
            var go = new GameObject("color-test-shape", typeof(MeshFilter), typeof(MeshRenderer));
            _made.Add(go);
            go.transform.position = Far;
            var mesh = new Mesh { name = "color-test-quad" };
            _made.Add(mesh);
            mesh.vertices = new[] { new Vector3(-4f, -4f), new Vector3(4f, -4f), new Vector3(4f, 4f), new Vector3(-4f, 4f) };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = _art.Material(SpriteMaterialKind.Shape);
            yield return null;
            for (int i = 0; i < Samples.Length; i++)
            {
                Color c = Samples[i];
                mesh.colors = new[] { c, c, c, c };
                yield return null;
                AssertSame(Samples[i], ReadCenter(), "Solo/Shape Mesh.colors");
                mesh.colors32 = new[] { Samples[i], Samples[i], Samples[i], Samples[i] };
                yield return null;
                AssertSame(Samples[i], ReadCenter(), "Solo/Shape Mesh.colors32");
            }
        }

        [UnityTest]
        public IEnumerator Shape_HalfAlpha_BlendsInDisplaySpaceOrder()
        {
            // 반투명 예고 채움: 알파 0.5의 흰색을 검정 위에 → 선형 혼합이면 #BCBCBC(선형 0.5). 색이 아니라 "채움이 반쯤 보인다"만 본다.
            var go = new GameObject("color-test-shape-alpha", typeof(MeshFilter), typeof(MeshRenderer));
            _made.Add(go);
            go.transform.position = Far;
            var mesh = new Mesh { name = "color-test-quad-alpha" };
            _made.Add(mesh);
            mesh.vertices = new[] { new Vector3(-4f, -4f), new Vector3(4f, -4f), new Vector3(4f, 4f), new Vector3(-4f, 4f) };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            var half = new Color(1f, 1f, 1f, 0.5f);
            mesh.colors = new[] { half, half, half, half };
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = _art.Material(SpriteMaterialKind.Shape);
            yield return null;
            Color32 px = ReadCenter();
            Assert.Greater(px.r, 0x60, "half-alpha white over black is visible");
            Assert.Less(px.r, 0xE0, "half-alpha white over black is not opaque");
        }
    }
}
