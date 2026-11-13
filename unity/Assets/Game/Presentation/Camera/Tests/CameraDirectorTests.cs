#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Wizard.Core;
using EntityId = Wizard.Core.EntityId;
using Wizard.ViewLogic.Convert;
using Wizard.ViewLogic.Presentation;
using Wizard.ViewLogic.Presentation.Limits;

namespace Wizard.Unity.Presentation.Cameras.Tests
{
    // 수락 3: 히트스톱은 그림만 멈춘다(맞은 대상·맞힌 것의 열쇠만, 표시 시계로). HUD 와 메뉴는 흔들림을 받지 않는다(전장 카메라만 움직인다).
    // 흔들림 폭과 길이의 한도(16px, 0.6초)는 연출 한도(M9-PRS-02)의 규칙이고 여기서는 카메라에 그대로 옮겨지는지만 본다.
    public sealed class CameraDirectorTests
    {
        private const float Eps = 1e-5f;
        private const int FrameMs = 16;

        private readonly List<Object> _made = new List<Object>();
        private long _now;

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in _made)
            {
                if (o != null)
                {
                    Object.DestroyImmediate(o);
                }
            }
            _made.Clear();
            _now = 0;
        }

        private UnityEngine.Camera MakeCamera(string name, Vector3 pos)
        {
            var go = new GameObject(name);
            _made.Add(go);
            go.transform.position = pos;
            UnityEngine.Camera cam = go.AddComponent<UnityEngine.Camera>();
            cam.orthographic = true;
            return cam;
        }

        private void Frames(CameraDirector d, int count, List<Vector2>? offsets = null)
        {
            for (int i = 0; i < count; i++)
            {
                _now += FrameMs;
                d.Advance();
                offsets?.Add(d.OffsetCells);
            }
        }

        [Test]
        public void Shake_MovesOnlyTheWorldCamera_HudAndMenuStayStill()
        {
            UnityEngine.Camera world = MakeCamera("World", new Vector3(3f, 4f, -10f));
            UnityEngine.Camera hudCam = MakeCamera("HudCamera", new Vector3(0f, 0f, -10f));
            var hudGo = new GameObject("Hud");
            _made.Add(hudGo);
            Canvas canvas = hudGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var menuGo = new GameObject("Menu", typeof(RectTransform));
            _made.Add(menuGo);
            menuGo.transform.SetParent(hudGo.transform, false);
            Vector3 menuBefore = menuGo.transform.position;

            var d = new CameraDirector(world.transform, () => _now, 7);
            d.Shake(BindingShake.Large);
            var offsets = new List<Vector2>();
            Frames(d, 10, offsets);

            bool moved = false;
            float maxCells = PhotosensitivityLimits.MaxShakePx / (float)DisplayUnits.PixelsPerCell;
            foreach (Vector2 o in offsets)
            {
                moved |= o != Vector2.zero;
                Assert.That(o.magnitude, Is.LessThanOrEqualTo(maxCells + Eps), "at most 16px (0.25 cells)");
            }
            Assert.That(moved, Is.True, "world camera shakes");
            Vector3 wp = world.transform.position;
            Assert.That(wp.x, Is.EqualTo(3f + d.OffsetCells.x).Within(Eps));
            Assert.That(wp.y, Is.EqualTo(4f + d.OffsetCells.y).Within(Eps));
            Assert.That(wp.z, Is.EqualTo(-10f));

            Assert.That(hudCam.transform.position, Is.EqualTo(new Vector3(0f, 0f, -10f)), "HUD camera does not shake");
            Assert.That(menuGo.transform.position, Is.EqualTo(menuBefore), "menu (screen-space canvas) does not shake");
            Assert.That(Time.timeScale, Is.EqualTo(1f));
        }

        [Test]
        public void Shake_DecaysAndTheCameraReturnsExactlyToItsPlace()
        {
            UnityEngine.Camera world = MakeCamera("World", new Vector3(1f, 2f, -10f));
            var d = new CameraDirector(world.transform, () => _now, 3);
            d.Shake(BindingShake.XLarge);
            Frames(d, 3);
            // 누적값은 초당 2.5씩 준다: 1에서 0까지 0.4초. 길이 한도 0.6초 안에서 끝난다
            Frames(d, 600 / FrameMs);
            Assert.That(d.Accumulated, Is.EqualTo(0f));
            Assert.That(d.OffsetCells, Is.EqualTo(Vector2.zero));
            Assert.That(world.transform.position.x, Is.EqualTo(1f).Within(Eps));
            Assert.That(world.transform.position.y, Is.EqualTo(2f).Within(Eps));
        }

        [Test]
        public void Shake_DoesNotDriftWhenSomethingElseMovesTheCamera()
        {
            UnityEngine.Camera world = MakeCamera("World", Vector3.zero);
            var d = new CameraDirector(world.transform, () => _now, 5);
            d.Shake(BindingShake.Medium);
            Frames(d, 2);
            world.transform.position += new Vector3(10f, 0f, 0f);       // 다른 코드(카메라 따라가기)가 옮긴다
            Frames(d, 600 / FrameMs);
            Assert.That(world.transform.position.x, Is.EqualTo(10f).Within(Eps));
            Assert.That(world.transform.position.y, Is.EqualTo(0f).Within(Eps));
        }

        [Test]
        public void Shake_MinimalPresetOrZeroScale_DoesNotMoveTheCamera()
        {
            UnityEngine.Camera world = MakeCamera("World", Vector3.zero);
            var d = new CameraDirector(world.transform, () => _now, 5) { Settings = MotionSettings.Of(MotionPreset.Minimal) };
            d.Shake(BindingShake.XLarge);
            Frames(d, 5);
            Assert.That(world.transform.position, Is.EqualTo(Vector3.zero));

            d.Settings = new MotionSettings(MotionPreset.Full, 0, 100, true);
            d.Shake(BindingShake.XLarge);
            Frames(d, 5);
            Assert.That(world.transform.position, Is.EqualTo(Vector3.zero));
            Assert.That(d.ShakeCount, Is.EqualTo(2));
        }

        [Test]
        public void ShakeNone_IsIgnored()
        {
            var d = new CameraDirector(null, () => _now, 1);
            d.Shake(BindingShake.None);
            Assert.That(d.ShakeCount, Is.EqualTo(0));
            Assert.That(d.Accumulated, Is.EqualTo(0f));
        }

        [Test]
        public void Hitstop_FreezesOnlyTheHitTargetAndTheSourceForItsLength()
        {
            var d = new CameraDirector(null, () => _now, 1);
            _now = 500;
            d.Hitstop(120, new EntityId(3), new EntityId(4));
            Assert.That(d.IsFrozen(new EntityId(4)), Is.True);
            Assert.That(d.IsFrozen(new EntityId(3)), Is.True);
            Assert.That(d.IsFrozen(new EntityId(5)), Is.False, "other entities keep animating");
            Assert.That(d.IsScreenFrozen, Is.False);
            Assert.That(d.VisualDeltaSeconds(new EntityId(4), 0.016f), Is.EqualTo(0f));
            Assert.That(d.VisualDeltaSeconds(new EntityId(5), 0.016f), Is.EqualTo(0.016f));

            _now = 619;
            Assert.That(d.IsFrozen(new EntityId(4)), Is.True);
            _now = 620;
            Assert.That(d.IsFrozen(new EntityId(4)), Is.False);
            d.Advance();
            Assert.That(d.FrozenCount, Is.EqualTo(0));
            Assert.That(Time.timeScale, Is.EqualTo(1f), "timeScale untouched");
        }

        [Test]
        public void Hitstop_FollowsTheMotionSettings()
        {
            var d = new CameraDirector(null, () => _now, 1) { Settings = MotionSettings.Of(MotionPreset.Reduced) };
            d.Hitstop(120, EntityId.None, new EntityId(9));
            _now = 59;
            Assert.That(d.IsFrozen(new EntityId(9)), Is.True);
            _now = 60;
            Assert.That(d.IsFrozen(new EntityId(9)), Is.False, "Reduced: half length");

            d.Settings = new MotionSettings(MotionPreset.Full, 100, 100, false);
            d.Hitstop(120, EntityId.None, new EntityId(9));
            Assert.That(d.IsFrozen(new EntityId(9)), Is.False, "hitstop disabled");
        }

        [Test]
        public void Hitstop_WithoutEntities_FreezesTheScreenKey()
        {
            var d = new CameraDirector(null, () => _now, 1);
            d.Hitstop(50, EntityId.None, EntityId.None);
            Assert.That(d.IsScreenFrozen, Is.True);
            Assert.That(d.IsFrozen(EntityId.None), Is.False);
        }

        [Test]
        public void Reset_PutsTheCameraBackAndClearsEverything()
        {
            UnityEngine.Camera world = MakeCamera("World", new Vector3(1f, 1f, -10f));
            var d = new CameraDirector(world.transform, () => _now, 11);
            d.Shake(BindingShake.XLarge);
            d.Hitstop(200, EntityId.None, new EntityId(2));
            Frames(d, 2);
            d.Reset();
            Assert.That(world.transform.position, Is.EqualTo(new Vector3(1f, 1f, -10f)));
            Assert.That(d.Accumulated, Is.EqualTo(0f));
            Assert.That(d.IsFrozen(new EntityId(2)), Is.False);
        }

        [Test]
        public void ToShakeSize_MapsEveryBindingSize()
        {
            Assert.That(CameraDirector.ToShakeSize(BindingShake.Small), Is.EqualTo(ShakeSize.Small));
            Assert.That(CameraDirector.ToShakeSize(BindingShake.Medium), Is.EqualTo(ShakeSize.Medium));
            Assert.That(CameraDirector.ToShakeSize(BindingShake.Large), Is.EqualTo(ShakeSize.Large));
            Assert.That(CameraDirector.ToShakeSize(BindingShake.XLarge), Is.EqualTo(ShakeSize.XLarge));
        }
    }
}
