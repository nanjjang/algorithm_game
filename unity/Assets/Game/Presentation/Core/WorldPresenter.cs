#nullable enable
using System;
using UnityEngine;
using Wizard.Core;
using Wizard.Sim.Events;
using Wizard.Unity.Presentation.World.Basic;
using Wizard.ViewLogic.Convert;
using Wizard.ViewLogic.Presentation;

namespace Wizard.Unity.Presentation.Core
{
    // 회색 도형 표시의 입구(M1-UE-02, ARCHITECTURE 6.1·9절). 프레임의 끝에서 고정 틱 구동기가 부르는 두 단계를 받는다(IFramePresenter):
    //   OnEvents(이번 프레임의 표시 이벤트, 뷰)  — 섬 간격 보고(PracticeGapReport·ArtCast)를 모은다
    //   Render(뷰, alpha)                        — 새 틱이면 도형 묶음을 한 번 읽고(플레이어·적·투사체 전부 ISimView), 틱 사이를 보간해 그린다.
    //                                              열 링·시야 화살표·잠금은 그 틱의 PlayerView 값이다
    // 표시 계층은 코어 상태를 바꾸지 않는다: ISimView 와 표시 이벤트만 본다. 세계를 쥐지 않는다. Fix64 → float 는 FixConvert 만 한다
    // (ViewLogic.Convert 의 표본들이 부른다). 여기서는 System.Numerics.Vector2 를 UnityConvert 로 엔진 타입에 옮기기만 한다.
    // Time.timeScale·Time.deltaTime·물리를 쓰지 않는다. 히트스톱과 카메라 연출은 M9-PRS-03(Presentation/Bus·Camera)의 일이다.
    //
    // 만들고 FixedTickDriver.AddPresenter 에 꽂고 치우는 것은 조립 루트(Wizard.Unity.App)다(CONTRACTS 13, T-54).
    public sealed class WorldPresenter : IFramePresenter, IDisposable
    {
        public const string RootName = "World";

        private readonly GameObject _root;
        private readonly ShapeCanvas _canvas;
        private readonly ShapeInterpolator _shapes;
        private readonly PlayerShapeSampler _players;
        private readonly GapReadout _gaps = new GapReadout();
        private readonly DebugTextBuilder _debug = new DebugTextBuilder();
        private readonly DebugTextOverlay? _overlay;
        private readonly WorldCameraRig? _camera;
        private readonly ArenaOutline? _arena;
        private readonly UnityEngine.Vector2[] _playerPos;
        private readonly bool[] _playerDrawn;
        private bool _disposed;

        public WorldPresenter(WorldPresenterSetup setup)
        {
            if (setup == null)
            {
                throw new ArgumentNullException(nameof(setup));
            }
            _root = new GameObject(RootName);
            if (setup.Parent != null)
            {
                _root.transform.SetParent(setup.Parent, false);
            }
            _canvas = new ShapeCanvas("Shapes", _root.transform);
            _shapes = new ShapeInterpolator(setup.PlayerCapacity, setup.EntityCapacity);
            _players = new PlayerShapeSampler(setup.BodyRadiusCells);
            _playerPos = new UnityEngine.Vector2[setup.PlayerCapacity];
            _playerDrawn = new bool[setup.PlayerCapacity];
            _arena = setup.Arena;
            if (setup.ShowDebugText)
            {
                _overlay = _root.AddComponent<DebugTextOverlay>();
            }
            if (setup.EnsureCamera)
            {
                _camera = WorldCameraRig.Ensure(_root.transform);
                if (_arena != null)
                {
                    _camera.Frame(UnityConvert.ToUnity(_arena.Center), UnityConvert.ToUnity(_arena.Size), ShapeStyle.CameraMarginCells);
                }
            }
        }

        public GameObject Root => _root;

        public ShapeCanvas Canvas => _canvas;

        public ShapeInterpolator Shapes => _shapes;

        public GapReadout Gaps => _gaps;

        public WorldCameraRig? Camera => _camera;

        public string DebugText => _debug.Text;

        // 보간과 모아 둔 보고를 버린다(세션이 바뀔 때). 다음 Render 는 새 세계의 첫 틱을 끊어 그린다
        public void Reset()
        {
            RequireOpen();
            _shapes.Clear();
            _gaps.Clear();
        }

        public void OnEvents(ReadOnlySpan<SimEvent> events, ISimView view)
        {
            RequireOpen();
            _gaps.Observe(events);
        }

        public void Render(ISimView view, double alpha)
        {
            RequireOpen();
            if (view == null)
            {
                throw new ArgumentNullException(nameof(view));
            }
            uint tick = view.Tick;
            if (!_shapes.Has(tick))
            {
                ShapeFrame f = _shapes.Begin(tick);
                _players.Fill(view, f);
                EntityShapeSampler.Fill(view, f);
                _shapes.End();
            }

            _canvas.Begin();
            if (_arena != null)
            {
                ArenaShapes.Draw(_canvas, _arena);
            }
            ShapeFrame cur = _shapes.Current;
            for (int i = 0; i < cur.EntityCount; i++)
            {
                ShapeSample s = _shapes.Entity(i, alpha);
                EntityShapes.Draw(_canvas, s, tick, alpha);
            }
            Array.Clear(_playerDrawn, 0, _playerDrawn.Length);
            for (int i = 0; i < cur.PlayerCount; i++)
            {
                ShapeSample s = _shapes.Player(i, alpha);
                int slot = s.Id;                                               // 플레이어 도형의 Id 는 슬롯이다
                PlayerView p = view.Player(new PlayerSlot((byte)slot));
                PlayerShapes.Draw(_canvas, s, p, tick);
                if ((uint)slot < (uint)_playerPos.Length)
                {
                    _playerPos[slot] = UnityConvert.ToUnity(s.Pos);
                    _playerDrawn[slot] = true;
                }
            }
            _canvas.End();

            if (_debug.Update(view, _shapes, _gaps) && _overlay != null)
            {
                _overlay.Text = _debug.Text;
            }
        }

        // 마지막 Render 에서 그 슬롯의 몸을 그렸는가(시작 상태가 서기 전의 플레이어는 그리지 않는다)
        public bool PlayerDrawn(int slot) => (uint)slot < (uint)_playerDrawn.Length && _playerDrawn[slot];

        // 마지막 Render 에서 그 슬롯의 몸을 그린 자리(보간 뒤)
        public UnityEngine.Vector2 PlayerDrawnAt(int slot)
        {
            if (!PlayerDrawn(slot))
            {
                throw new ArgumentOutOfRangeException(nameof(slot));
            }
            return _playerPos[slot];
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            _canvas.Dispose();
            _camera?.Dispose();
            ShapeSprites.Destroy(_root);
        }

        private void RequireOpen()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(WorldPresenter));
            }
        }
    }
}
