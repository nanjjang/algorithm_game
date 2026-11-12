#nullable enable
using System;
using UnityEngine;

namespace Wizard.Unity.Presentation.Core
{
    // 전장을 비추는 직교 카메라. 장면에는 시작 개체 하나뿐이라(T-32) 카메라가 없으면 코드로 만든다. 이미 있는 주 카메라는 그대로 쓴다.
    // 흔들림·히트스톱·카메라 연출은 여기가 아니다(Presentation/Camera 의 CameraDirector, M9-PRS-03). 여기는 전장 전체가 보이게 맞추기만 한다.
    public sealed class WorldCameraRig : IDisposable
    {
        public const string ObjectName = "WorldCamera";
        private const string MainCameraTag = "MainCamera";
        private const float CameraZ = -10f;               // 2D: 그림(z = 0) 앞에 둔다

        private readonly bool _owned;

        private WorldCameraRig(Camera camera, bool owned)
        {
            Camera = camera;
            _owned = owned;
        }

        public Camera Camera { get; }

        public bool Owned => _owned;

        public static WorldCameraRig Ensure(Transform? parent)
        {
            Camera? main = Camera.main;
            if (main != null)
            {
                return new WorldCameraRig(main, false);
            }
            var go = new GameObject(ObjectName) { tag = MainCameraTag };
            if (parent != null)
            {
                go.transform.SetParent(parent, false);
            }
            Camera cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = ShapePalette.Background;
            return new WorldCameraRig(cam, true);
        }

        // 표시 좌표의 사각(가운데, 크기)이 여백까지 다 보이게 맞춘다. 화면 비가 좁으면 가로에 맞춘다
        public void Frame(Vector2 center, Vector2 size, float marginCells)
        {
            Camera cam = Camera;
            cam.orthographic = true;
            cam.transform.position = new Vector3(center.x, center.y, CameraZ);
            cam.transform.rotation = Quaternion.identity;
            float aspect = cam.aspect > 0f ? cam.aspect : 1f;
            float halfH = size.y / 2f + marginCells;
            float halfWAsH = (size.x / 2f + marginCells) / aspect;
            cam.orthographicSize = Mathf.Max(halfH, halfWAsH);
        }

        public void Dispose()
        {
            if (_owned && Camera != null)
            {
                ShapeSprites.Destroy(Camera.gameObject);
            }
        }
    }
}
