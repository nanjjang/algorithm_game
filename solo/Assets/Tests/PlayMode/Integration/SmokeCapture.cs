using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using Wizard.Solo.UI;

namespace Wizard.Solo.Tests.PlayMode.Integration
{
    /// <summary>
    /// 화면 갈무리(사람이 눈으로 확인하는 PNG). 길이 둘이다.
    /// - 창이 있을 때(배치 모드가 아님): ScreenCapture.CaptureScreenshot — 게임 창에 실제로 나온 그림 그대로.
    /// - 배치 모드: 게임 창이 없어 화면이 그려지지 않는다(ScreenCapture는 파일을 만들지 않는다). 그래서 기준 해상도 1920×1080의
    ///   RenderTexture에 월드 카메라를 직접 그린다. Screen Space Overlay 캔버스는 카메라에 잡히지 않으므로 갈무리하는 동안만
    ///   Screen Space Camera(같은 카메라, 순서 +10000, 배율 1)로 바꿨다가 되돌린다. 그리는 것은 같은 객체·같은 셰이더·같은 글꼴이다.
    /// </summary>
    public static class SmokeCapture
    {
        public const int Width = UiScale.RefWidth, Height = UiScale.RefHeight;
        const int CanvasOrderLift = 10000;
        const int MaxWaitFrames = 120;

        sealed class Saved
        {
            public Canvas Canvas;
            public RenderMode Mode;
            public Camera Camera;
            public float PlaneDistance;
            public int Order;
            public CanvasScaler Scaler;
            public float Scale;
            public UiScaleDriver Driver;
            public bool DriverEnabled;
        }

        /// <summary>path에 PNG를 남긴다. 끝나면 used에 쓴 길("screen" / "offscreen")이 들어간다.</summary>
        public static IEnumerator Capture(Camera worldCamera, string path, System.Action<string> used)
        {
            if (File.Exists(path)) File.Delete(path);
            if (!Application.isBatchMode)
            {
                ScreenCapture.CaptureScreenshot(path);
                for (int i = 0; i < MaxWaitFrames && !(File.Exists(path) && new FileInfo(path).Length > 0); i++) yield return null;
                if (File.Exists(path) && new FileInfo(path).Length > 0)
                {
                    used?.Invoke("screen");
                    yield break;
                }
            }
            RenderOffscreen(worldCamera, path);
            used?.Invoke("offscreen");
        }

        static void RenderOffscreen(Camera cam, string path)
        {
            var saved = new List<Saved>();
            var canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            var rt = RenderTexture.GetTemporary(Width, Height, 24, RenderTextureFormat.ARGB32);
            var prevTarget = cam.targetTexture;
            float prevSize = cam.orthographicSize;
            var prevActive = RenderTexture.active;
            Texture2D tex = null;
            try
            {
                for (int i = 0; i < canvases.Length; i++)
                {
                    var c = canvases[i];
                    if (!c.isActiveAndEnabled || !c.isRootCanvas || c.renderMode != RenderMode.ScreenSpaceOverlay) continue;
                    var s = new Saved
                    {
                        Canvas = c, Mode = c.renderMode, Camera = c.worldCamera, PlaneDistance = c.planeDistance, Order = c.sortingOrder,
                        Scaler = c.GetComponent<CanvasScaler>(), Driver = c.GetComponent<UiScaleDriver>()
                    };
                    if (s.Scaler != null) s.Scale = s.Scaler.scaleFactor;
                    if (s.Driver != null) { s.DriverEnabled = s.Driver.enabled; s.Driver.enabled = false; }
                    saved.Add(s);
                }
                cam.targetTexture = rt;
                // 판 중이면 전장 줌(CameraDirector)과 같은 배율로 찍는다. 판이 없으면 WorldViewHost가 UI 배율 1 크기를 준다.
                var runner = Object.FindFirstObjectByType<Wizard.Solo.App.GameRunner>();
                cam.orthographicSize = runner != null && runner.Host != null
                    ? runner.Host.OrthographicSizeFor(Height)
                    : UiScale.OrthographicSize(Height, 1f);
                for (int i = 0; i < saved.Count; i++)
                {
                    var s = saved[i];
                    s.Canvas.renderMode = RenderMode.ScreenSpaceCamera;
                    s.Canvas.worldCamera = cam;
                    s.Canvas.planeDistance = 1f;
                    s.Canvas.sortingOrder = s.Order + CanvasOrderLift;
                    if (s.Scaler != null) s.Scaler.scaleFactor = 1f;
                }
                Rebuild(saved);
                cam.Render();

                RenderTexture.active = rt;
                tex = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
                tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                tex.Apply(false);
                File.WriteAllBytes(path, tex.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = prevActive;
                cam.targetTexture = prevTarget;
                cam.orthographicSize = prevSize;
                for (int i = 0; i < saved.Count; i++)
                {
                    var s = saved[i];
                    if (s.Canvas == null) continue;
                    s.Canvas.renderMode = s.Mode;
                    s.Canvas.worldCamera = s.Camera;
                    s.Canvas.planeDistance = s.PlaneDistance;
                    s.Canvas.sortingOrder = s.Order;
                    if (s.Scaler != null) s.Scaler.scaleFactor = s.Scale;
                    if (s.Driver != null) s.Driver.enabled = s.DriverEnabled;
                }
                Rebuild(saved);
                RenderTexture.ReleaseTemporary(rt);
                if (tex != null) Object.Destroy(tex);
            }
        }

        /// <summary>배율이 바뀌면 글자 메시를 다시 만들어야 한다(동적 글꼴의 글리프 크기가 배율을 따른다).</summary>
        static void Rebuild(List<Saved> saved)
        {
            for (int i = 0; i < saved.Count; i++)
            {
                if (saved[i].Canvas == null) continue;
                var graphics = saved[i].Canvas.GetComponentsInChildren<Graphic>(false);
                for (int g = 0; g < graphics.Length; g++) graphics[g].SetAllDirty();
            }
            Canvas.ForceUpdateCanvases();
        }
    }
}
