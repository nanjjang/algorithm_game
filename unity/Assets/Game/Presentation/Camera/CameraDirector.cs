#nullable enable
using System;
using UnityEngine;
using Wizard.Core;
using EntityId = Wizard.Core.EntityId;
using Wizard.ViewLogic.Convert;
using Wizard.ViewLogic.Presentation;
using Wizard.ViewLogic.Presentation.Limits;

// 네임스페이스가 폴더 이름(Camera)과 다르다: Wizard.Unity.Presentation.Camera 라는 네임스페이스가 생기면 같은 어셈블리의
// Wizard.Unity.Presentation.Core 안에서 'Camera' 가 UnityEngine.Camera 가 아니라 이 네임스페이스로 풀려 WorldCameraRig 가 컴파일되지 않는다.
// 그래서 복수형(Cameras)을 쓴다. 결과 보고에 적었다.
namespace Wizard.Unity.Presentation.Cameras
{
    // 카메라 연출(ARCHITECTURE 9 "CameraDirector(흔들림 누적기, 히트스톱 시계)", effects.md 0.2, spec 13 14.3·14.4).
    // 라우터가 바인딩 줄의 shake·hitstop_ms 를 ICameraCueSink 로 넘기면 연출 한도(M9-PRS-02)의 ShakeAccumulator·HitstopClock 에 잇는다.
    //  - 흔들림: 전장 카메라(생성자에 준 Transform) 하나만 움직인다. HUD·메뉴는 화면 공간이라 흔들리지 않는다. 예고 판정·입력은 이 값을 읽지 않는다.
    //    폭(px, 기준 화면) → 칸은 DisplayUnits(1칸 = 64px). 매 프레임 지난 프레임의 어긋남을 빼고 새 어긋남을 더한다(다른 코드가 카메라를 옮겨도 쌓이지 않는다).
    //    방향은 표시 전용 난수(DisplayRandom)로 고른다. 코어 난수기를 쓰지 않는다.
    //  - 그림 정지: 게임 시간을 멈추지 않는다. 맞은 대상(target)과 맞힌 것(source)의 열쇠만 표시 시계로 얼린다. Time.timeScale 을 건드리지 않고
    //    세계·틱·세션을 쥐지 않으므로 틱은 그대로 간다. 그리는 쪽이 IsFrozen·VisualDeltaSeconds 로 그 개체의 그림만 멈춘다.
    //    코어 밖 사건(둘 다 None)은 화면 열쇠 하나(ScreenKey)를 얼린다(강화 화면처럼 화면 전체를 멈춰도 되는 곳, effects.md 0.2).
    //  - 세기·줄이기·광과민 한도는 MotionSettings 와 연출 한도가 집행한다(옵션과 무관하게 16px·0.6초 이하).
    public sealed class CameraDirector : ICameraCueSink, IDisposable
    {
        public const long ScreenKey = -1;                 // 개체 id 는 양수다. 화면 전체 그림 정지의 열쇠

        private readonly Transform? _target;
        private readonly Func<long> _nowMs;
        private readonly ShakeAccumulator _shake = new ShakeAccumulator();
        private readonly HitstopClock _hitstop = new HitstopClock();
        private readonly DisplayRandom _random;
        private UnityEngine.Vector2 _applied;            // 지금 카메라에 더해 둔 어긋남(칸)
        private long _lastMs = -1;

        public CameraDirector(Transform? worldCamera, Func<long> nowMs, uint shakeSeed)
        {
            _target = worldCamera;
            _nowMs = nowMs ?? throw new ArgumentNullException(nameof(nowMs));
            _random = new DisplayRandom(shakeSeed);
        }

        public MotionSettings Settings { get; set; } = MotionSettings.Default;

        public Transform? Target => _target;

        public float Accumulated => _shake.Accumulated;

        public float AmplitudePx => _shake.AmplitudePx(Settings);

        // 지금 카메라에 더해 둔 어긋남(칸)
        public UnityEngine.Vector2 OffsetCells => _applied;

        public int ShakeCount { get; private set; }

        public int HitstopCount { get; private set; }

        public int FrozenCount => _hitstop.ActiveCount;

        public void Shake(BindingShake size)
        {
            if (size == BindingShake.None)
            {
                return;
            }
            _shake.Add(ToShakeSize(size));
            ShakeCount++;
        }

        public void Hitstop(int durationMs, EntityId source, EntityId target)
        {
            if (durationMs <= 0)
            {
                return;
            }
            long now = _nowMs();
            MotionSettings s = Settings;
            HitstopCount++;
            if (source.IsNone && target.IsNone)
            {
                _hitstop.Request(ScreenKey, durationMs, now, s);
                return;
            }
            if (!target.IsNone)
            {
                _hitstop.Request(target.Value, durationMs, now, s);
            }
            if (!source.IsNone)
            {
                _hitstop.Request(source.Value, durationMs, now, s);
            }
        }

        public bool IsFrozen(EntityId id) => !id.IsNone && _hitstop.IsFrozen(id.Value, _nowMs());

        public bool IsScreenFrozen => _hitstop.IsFrozen(ScreenKey, _nowMs());

        // 그 개체의 그림이 이번 프레임에 나아갈 시간(초). 얼어 있으면 0. 세계의 틱과 무관하다
        public float VisualDeltaSeconds(EntityId id, float frameDeltaSeconds) =>
            id.IsNone ? frameDeltaSeconds : _hitstop.VisualDeltaSeconds(id.Value, _nowMs(), frameDeltaSeconds);

        // 프레임마다 한 번(버스의 Render). 누적값을 줄이고, 끝난 그림 정지를 지우고, 카메라 어긋남을 다시 정한다
        public void Advance()
        {
            long now = _nowMs();
            float dt = _lastMs < 0 || now <= _lastMs ? 0f : (now - _lastMs) / (float)DisplayUnits.MillisecondsPerSecond;
            _lastMs = now;
            _shake.Tick(dt);
            _hitstop.Prune(now);

            System.Numerics.Vector2 px = _shake.SampleOffset(Settings, _random);
            UnityEngine.Vector2 cells = UnityConvert.ToUnity(px) / DisplayUnits.PixelsPerCell;
            Apply(cells);
        }

        // 새 판·경기·재동기화: 흔들림과 그림 정지를 비우고 카메라를 제자리로 돌린다
        public void Reset()
        {
            _shake.Reset();
            _hitstop.Clear();
            _lastMs = -1;
            Apply(UnityEngine.Vector2.zero);
        }

        public void Dispose() => Reset();

        public static ShakeSize ToShakeSize(BindingShake size)
        {
            switch (size)
            {
                case BindingShake.Small: return ShakeSize.Small;
                case BindingShake.Medium: return ShakeSize.Medium;
                case BindingShake.Large: return ShakeSize.Large;
                case BindingShake.XLarge: return ShakeSize.XLarge;
                default: throw new ArgumentOutOfRangeException(nameof(size), size, null);
            }
        }

        private void Apply(UnityEngine.Vector2 cells)
        {
            if (_target != null)
            {
                Vector3 p = _target.localPosition;
                p.x += cells.x - _applied.x;
                p.y += cells.y - _applied.y;
                _target.localPosition = p;
            }
            _applied = cells;
        }
    }
}
