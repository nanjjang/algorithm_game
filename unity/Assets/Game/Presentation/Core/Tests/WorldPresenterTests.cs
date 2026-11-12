#nullable enable
using System;
using NUnit.Framework;
using UnityEngine;
using Wizard.Content;
using Wizard.Core;
using Wizard.Sim.Events;
using Wizard.ViewLogic.Convert;
using Wizard.ViewLogic.Presentation;
using Vector2 = UnityEngine.Vector2;

namespace Wizard.Unity.Presentation.Core.Tests
{
    // M1-UE-02 수락 조건 2·3: 플레이어·적·탄·포탑이 도형으로 보이고 위치는 틱 사이를 보간한다. 열 링·시야 화살표·잠금이 ISimView 값으로
    // 그려진다. 섬 간격이 밀리초로 찍힌다. 표시 계층은 뷰와 이벤트만 읽는다(적과 탄도 ISimView 의 목록에서 읽는다, CONTRACTS 3.3, T-54).
    public sealed class WorldPresenterTests
    {
        private const float Eps = 1e-4f;
        private WorldPresenter? _presenter;
        private FakeSimView _view = new FakeSimView();

        [SetUp]
        public void SetUp()
        {
            _view = new FakeSimView { Tick = 10 };
            var setup = new WorldPresenterSetup(_ => 0.3f)
            {
                EnsureCamera = false,
                ShowDebugText = true,
            };
            _presenter = new WorldPresenter(setup);
        }

        [TearDown]
        public void TearDown()
        {
            _presenter?.Dispose();
            _presenter = null;
        }

        private WorldPresenter P => _presenter!;

        [Test]
        public void PlayerPosition_IsInterpolatedBetweenTicks()
        {
            _view.Players = new[] { FakeSimView.MakePlayer(12f, 7f) };
            P.Render(_view, 0.0);
            _view.Tick = 11;
            _view.Players = new[] { FakeSimView.MakePlayer(12.5f, 7.25f) };
            P.Render(_view, 0.5);
            // 표시 좌표는 y 를 뒤집는다: (12, −7) → (12.5, −7.25) 의 가운데
            Vector2 at = P.PlayerDrawnAt(0);
            Assert.That(at.x, Is.EqualTo(12.25f).Within(Eps));
            Assert.That(at.y, Is.EqualTo(-7.125f).Within(Eps));
            P.Render(_view, 1.0);
            Assert.That(P.PlayerDrawnAt(0).x, Is.EqualTo(12.5f).Within(Eps));
        }

        [Test]
        public void SameTick_IsSampledOnce_EvenOverManyFrames()
        {
            _view.Players = new[] { FakeSimView.MakePlayer(1f, 1f) };
            P.Render(_view, 0.1);
            P.Render(_view, 0.4);
            P.Render(_view, 0.9);
            Assert.That(_view.EnemyReads, Is.EqualTo(1));
            Assert.That(_view.ProjectileReads, Is.EqualTo(1));
            _view.Tick = 11;
            P.Render(_view, 0.0);
            Assert.That(_view.EnemyReads, Is.EqualTo(2));
            Assert.That(_view.ProjectileReads, Is.EqualTo(2));
        }

        [Test]
        public void IsAFramePresenter_TheDriverCanHoldItWithoutAnAdapter()
        {
            IFramePresenter presenter = P;
            _view.Players = new[] { FakeSimView.MakePlayer(1f, 1f) };
            presenter.OnEvents(ReadOnlySpan<SimEvent>.Empty, _view);
            presenter.Render(_view, 0.0);
            Assert.That(P.PlayerDrawn(0), Is.True);
        }

        [Test]
        public void PlayerNotSpawnedYet_IsNotDrawn()
        {
            // 첫 틱이 돌기 전: 세계가 플레이어를 세우지 않았다(MaxHpPoints 0). 전장의 (0, 0) 에 몸을 그리지 않는다
            _view.Tick = 0;
            _view.Players = new[] { FakeSimView.NotSpawned() };
            P.Render(_view, 0.0);
            Assert.That(P.PlayerDrawn(0), Is.False);
            Assert.That(Count(ShapePalette.PlayerBody), Is.EqualTo(0));
            Assert.That(Count(ShapePalette.HeatMarkFaint), Is.EqualTo(0));
            StringAssert.DoesNotContain("P1", P.DebugText);
            _view.Tick = 1;
            _view.Players = new[] { FakeSimView.MakePlayer(12f, 7f) };
            P.Render(_view, 0.5);
            Assert.That(P.PlayerDrawn(0), Is.True);
            Assert.That(P.PlayerDrawnAt(0).x, Is.EqualTo(12f).Within(Eps));     // 새로 선 플레이어는 보간 없이 제자리에
            Assert.That(P.PlayerDrawnAt(0).y, Is.EqualTo(-7f).Within(Eps));
        }

        [Test]
        public void EntityPositions_AreInterpolatedBetweenTicksById()
        {
            _view.Players = Array.Empty<PlayerView>();
            _view.EnemyList = new[] { FakeSimView.MakeEnemy(5, EnemyTypeId.SlimeMoss, 3f, 3f, 0.35f) };
            _view.ProjectileList = new[] { FakeSimView.MakeEnemyShot(8, 12f, 3f, 0.12f) };
            P.Render(_view, 0.0);
            _view.Tick = 11;
            // 목록의 순서가 바뀌어도(새 탄이 앞에 끼어도) 짝은 id 로 찾는다
            _view.EnemyList = new[] { FakeSimView.MakeEnemy(5, EnemyTypeId.SlimeMoss, 3f, 3.05f, 0.35f) };
            _view.ProjectileList = new[] { FakeSimView.MakePlayerBullet(9, 1f, 1f, 0.15f), FakeSimView.MakeEnemyShot(8, 12f, 3.1f, 0.12f) };
            P.Render(_view, 0.5);
            Assert.That(Single(ShapePalette.Slime).transform.localPosition.y, Is.EqualTo(-3.025f).Within(Eps));
            Assert.That(Single(ShapePalette.EnemyShot).transform.localPosition.y, Is.EqualTo(-3.05f).Within(Eps));
            Assert.That(Single(ShapePalette.PlayerBullet).transform.localPosition.x, Is.EqualTo(1f).Within(Eps));   // 새 개체는 제자리
        }

        [Test]
        public void ColdRing_ShowsOnlyAFaintCritMark()
        {
            _view.Players = new[] { FakeSimView.MakePlayer(5f, 5f) };
            P.Render(_view, 0.0);
            Assert.That(Count(ShapePalette.HeatRising) + Count(ShapePalette.HeatImminent) + Count(ShapePalette.HeatLocked), Is.EqualTo(0));
            Assert.That(Count(ShapePalette.HeatMarkFaint), Is.EqualTo(1));
        }

        [Test]
        public void RisingRing_FillsByHeat()
        {
            // 열 40/100 → 48막대 중 ceil(0.4 × 48) = 20
            _view.Players = new[] { FakeSimView.MakePlayer(5f, 5f, heatHu: 4800, ring: HeatRingState.Rising) };
            P.Render(_view, 0.0);
            Assert.That(Count(ShapePalette.HeatRising), Is.EqualTo(20));
            Assert.That(Count(ShapePalette.HeatMark), Is.EqualTo(1));
        }

        [Test]
        public void CriticalRing_IsWhiteWithABlackOutline()
        {
            _view.Players = new[] { FakeSimView.MakePlayer(5f, 5f, heatHu: 10800, ring: HeatRingState.Critical) };
            P.Render(_view, 0.0);
            int white = Count(ShapePalette.HeatCritical);
            Assert.That(white, Is.EqualTo(Mathf.CeilToInt(0.9f * ShapeStyle.RingSegments)));
            Assert.That(Count(ShapePalette.Outline), Is.EqualTo(white));
        }

        [Test]
        public void ExemptArmed_AddsAThinFullRing()
        {
            _view.Players = new[] { FakeSimView.MakePlayer(5f, 5f, heatHu: 10200, ring: HeatRingState.Critical, exempt: true) };
            P.Render(_view, 0.0);
            // 흰 링 = 임계 채움(ceil(0.85 × 48) = 41) + 열 면제 한 바퀴(48)
            Assert.That(Count(ShapePalette.HeatCritical), Is.EqualTo(41 + ShapeStyle.RingSegments));
        }

        [Test]
        public void LockedRing_ShowsTheRemainingLockAndAnX()
        {
            // 잠금 180틱 중 45틱 남음 → 48막대 중 12, 잠금 X 막대 둘
            _view.Players = new[] { FakeSimView.MakePlayer(5f, 5f, heatHu: 12000, ring: HeatRingState.Locked, lockRemain: 45, lockTotal: 180) };
            P.Render(_view, 0.0);
            Assert.That(CountAt(ShapePalette.HeatLocked, ShapeStyle.OrderHeatRing + 1), Is.EqualTo(12));
            Assert.That(CountAt(ShapePalette.LockCross, ShapeStyle.OrderLockCross), Is.EqualTo(2));
            _view.Tick = 11;
            _view.Players = new[] { FakeSimView.MakePlayer(5f, 5f, heatHu: 12000, ring: HeatRingState.Locked, lockRemain: 0, lockTotal: 180) };
            P.Render(_view, 0.0);
            Assert.That(CountAt(ShapePalette.HeatLocked, ShapeStyle.OrderHeatRing + 1), Is.EqualTo(0));
            Assert.That(CountAt(ShapePalette.LockCross, ShapeStyle.OrderLockCross), Is.EqualTo(2));
        }

        [TestCase(StanceMode.Normal)]
        [TestCase(StanceMode.Stance)]
        [TestCase(StanceMode.Reverse)]
        public void FacingArrow_FollowsFacingAndStance(StanceMode mode)
        {
            _view.Players = new[] { FakeSimView.MakePlayer(5f, 5f, facing: Dir8.N, stance: mode) };
            P.Render(_view, 0.0);
            Color expected = mode == StanceMode.Reverse ? ShapePalette.FacingReverse
                : (mode == StanceMode.Stance ? ShapePalette.FacingStance : ShapePalette.FacingNormal);
            SpriteRenderer? head = null;
            for (int i = 0; i < P.Canvas.UsedCount; i++)
            {
                SpriteRenderer r = P.Canvas.Piece(i);
                if (r.sortingOrder == ShapeStyle.OrderFacing && r.sprite == P.Canvas.Sprites.Triangle)
                {
                    head = r;
                }
            }
            Assert.That(head, Is.Not.Null);
            Assert.That(Same(head!.color, expected), Is.True);
            Assert.That(head.transform.localEulerAngles.z, Is.EqualTo(90f).Within(Eps));     // N = 화면 위
            Assert.That(head.transform.localPosition.y, Is.GreaterThan(-5f));                // 몸(y = −5)보다 위
        }

        [Test]
        public void Entities_AreDrawnAsShapes_WithTheDangerColorOnlyForEnemyAttacks()
        {
            _view.Players = new[] { FakeSimView.MakePlayer(12f, 7f) };
            _view.EnemyList = new[]
            {
                FakeSimView.MakeEnemy(5, EnemyTypeId.SlimeMoss, 3f, 3f, 0.35f, hp: 10),
                FakeSimView.MakeEnemy(6, EnemyTypeId.ShamanCap, 12f, 2f, 0.3f, windupStart: 9U, windupTicks: 24),
                FakeSimView.MakeEnemy(4, EnemyTypeId.WolfBramble, 20f, 10f, 0.4f),
            };
            _view.ProjectileList = new[]
            {
                FakeSimView.MakePlayerBullet(7, 12f, 6f, 0.15f),
                FakeSimView.MakeEnemyShot(8, 12f, 3f, 0.12f),
            };
            P.Render(_view, 0.5);                                                            // 시전 예고는 시작 틱부터 찬다
            SpriteRenderer slime = Single(ShapePalette.Slime);
            Assert.That(slime.transform.localPosition.x, Is.EqualTo(3f).Within(Eps));
            Assert.That(slime.transform.localPosition.y, Is.EqualTo(-3f).Within(Eps));     // 표시 좌표는 y 를 뒤집는다
            Assert.That(slime.transform.localScale.x, Is.EqualTo(0.7f).Within(Eps));
            Assert.That(Count(ShapePalette.EnemyHpFill), Is.EqualTo(1));                    // 체력 절반: 막대(가득 찬 둘은 없다)
            SpriteRenderer turret = Single(ShapePalette.Turret);
            Assert.That(turret.sprite, Is.EqualTo(P.Canvas.Sprites.Square));
            Assert.That(Count(ShapePalette.EnemyTelegraph), Is.GreaterThan(0));             // 시전 중: 예고 링
            SpriteRenderer other = Single(ShapePalette.Enemy);                              // 그 밖의 종: 원판
            Assert.That(other.sprite, Is.EqualTo(P.Canvas.Sprites.Disk));
            Assert.That(other.transform.localScale.x, Is.EqualTo(0.8f).Within(Eps));
            Assert.That(Single(ShapePalette.PlayerBullet).transform.localPosition.y, Is.EqualTo(-6f).Within(Eps));
            SpriteRenderer shot = Single(ShapePalette.EnemyShot);
            Assert.That(shot.transform.localScale.x, Is.EqualTo(0.24f).Within(Eps));
            Assert.That(shot.sortingOrder, Is.GreaterThan(Single(ShapePalette.PlayerBody).sortingOrder));   // 적 탄은 가려지지 않는다
        }

        [Test]
        public void TurretTelegraph_FillsFromTheStartTickAndLengthTheWorldGives()
        {
            // 시전이 틱 9 에 시작해 24틱(공격은 틱 33). 채움 = (뷰 틱 − 1 − 9 + alpha) ÷ 24. 48막대 중 ceil(채움 × 48)
            _view.Players = Array.Empty<PlayerView>();
            _view.EnemyList = new[] { FakeSimView.MakeEnemy(6, EnemyTypeId.ShamanCap, 12f, 2f, 0.3f, windupStart: 9U, windupTicks: 24) };
            _view.Tick = 10;
            P.Render(_view, 0.0);
            Assert.That(Count(ShapePalette.EnemyTelegraph), Is.EqualTo(0));                 // 시작한 틱의 상태를 처음 본 순간: 0
            P.Render(_view, 0.5);
            Assert.That(Count(ShapePalette.EnemyTelegraph), Is.EqualTo(1));                 // 0.5 ÷ 24 × 48 = 1
            _view.Tick = 22;                                                                 // 처음 본 틱이 아니어도 값이 같다(기억하지 않는다)
            P.Render(_view, 0.0);
            Assert.That(Count(ShapePalette.EnemyTelegraph), Is.EqualTo(24));                // 12 ÷ 24 = 절반
            _view.Tick = 33;
            P.Render(_view, 1.0);
            Assert.That(Count(ShapePalette.EnemyTelegraph), Is.EqualTo(ShapeStyle.RingSegments));   // 공격 직전: 가득
            _view.Tick = 34;                                                                 // 시전이 끝나고 탄이 나갔다
            _view.EnemyList = new[] { FakeSimView.MakeEnemy(6, EnemyTypeId.ShamanCap, 12f, 2f, 0.3f) };
            P.Render(_view, 0.0);
            Assert.That(Count(ShapePalette.EnemyTelegraph), Is.EqualTo(0));
        }

        [Test]
        public void Reset_DropsInterpolationAndGapReports()
        {
            _view.Players = new[] { FakeSimView.MakePlayer(5f, 5f) };
            var e = new SimEvent { Type = SimEventType.PracticeGapReport, Slot = new PlayerSlot(0), Tick = 10, A = 384, V = 384, B = 6, C = 1 };
            P.OnEvents(new[] { e }, _view);
            P.Render(_view, 0.0);
            StringAssert.Contains("gap 25 ms", P.DebugText);
            P.Reset();
            _view.Tick = 11;
            _view.Players = new[] { FakeSimView.MakePlayer(5.5f, 5f) };
            P.Render(_view, 0.0);
            Assert.That(P.PlayerDrawnAt(0).x, Is.EqualTo(5.5f).Within(Eps));                // 앞 묶음이 없다: 보간하지 않는다
            StringAssert.Contains("P1 gap -", P.DebugText);
        }

        [Test]
        public void UnusedPieces_AreTurnedOffNextFrame()
        {
            _view.Players = new[] { FakeSimView.MakePlayer(5f, 5f, heatHu: 12000, ring: HeatRingState.Critical) };
            P.Render(_view, 0.0);
            int many = P.Canvas.UsedCount;
            _view.Tick = 11;
            _view.Players = new[] { FakeSimView.MakePlayer(5f, 5f) };
            P.Render(_view, 0.0);
            Assert.That(P.Canvas.UsedCount, Is.LessThan(many));
            int enabled = 0;
            foreach (SpriteRenderer r in P.Canvas.Root.GetComponentsInChildren<SpriteRenderer>(true))
            {
                enabled += r.enabled ? 1 : 0;
            }
            Assert.That(enabled, Is.EqualTo(P.Canvas.UsedCount));
            Assert.That(P.Canvas.PieceCount, Is.EqualTo(many));                             // 조각은 버리지 않고 돌려 쓴다
        }

        [Test]
        public void GapReport_IsPrintedInMilliseconds()
        {
            _view.Players = new[] { FakeSimView.MakePlayer(5f, 5f) };
            P.Render(_view, 0.0);
            StringAssert.Contains("P1 gap -", P.DebugText);
            // 마지막 방향 입력에서 K 까지 384 단위 = 25ms(spec 01 2.1: 15,360 단위 = 1초)
            var e = new SimEvent { Type = SimEventType.PracticeGapReport, Slot = new PlayerSlot(0), Tick = 10, A = 384, V = 384, B = 6, C = 1 };
            P.OnEvents(new[] { e }, _view);
            P.Render(_view, 0.5);
            StringAssert.Contains("P1 gap 25 ms", P.DebugText);
            StringAssert.Contains("art", P.DebugText);
            // 섬 창 1,075 단위 = 69.99ms → 70
            var just = new SimEvent { Type = SimEventType.PracticeGapReport, Slot = new PlayerSlot(0), Tick = 11, A = 1075, V = 1075, B = 6, C = 1 };
            var cast = new SimEvent { Type = SimEventType.ArtCast, Slot = new PlayerSlot(0), Tick = 11, A = 6, C = 1 };
            _view.Tick = 11;
            P.OnEvents(new[] { just, cast }, _view);
            P.Render(_view, 0.0);
            StringAssert.Contains("P1 gap 70 ms", P.DebugText);
            StringAssert.Contains("just", P.DebugText);
        }

        [Test]
        public void DebugText_ShowsHeatAndLockFromTheView()
        {
            _view.Players = new[] { FakeSimView.MakePlayer(5f, 5f, heatHu: 12000, ring: HeatRingState.Locked, lockRemain: 45, lockTotal: 180) };
            P.Render(_view, 0.0);
            StringAssert.Contains("tick 10", P.DebugText);
            StringAssert.Contains("heat 100/85 Locked lock 45/180", P.DebugText);
        }

        [Test]
        public void ArenaAndCamera_FrameTheField()
        {
            var setup = new WorldPresenterSetup(_ => 0.3f)
            {
                Arena = ArenaOutlineOf(24, 14),
                EnsureCamera = true,
                ShowDebugText = false,
            };
            using (var p = new WorldPresenter(setup))
            {
                Assert.That(p.Camera, Is.Not.Null);
                WorldCameraRig rig = p.Camera!;
                Camera cam = rig.Camera;
                Assert.That(cam.orthographic, Is.True);
                Assert.That(cam.transform.position.x, Is.EqualTo(12f).Within(Eps));
                Assert.That(cam.transform.position.y, Is.EqualTo(-7f).Within(Eps));
                Assert.That(cam.orthographicSize, Is.GreaterThanOrEqualTo(7f + ShapeStyle.CameraMarginCells - Eps));
                p.Render(new FakeSimView { Tick = 1 }, 0.0);
                SpriteRenderer floor = p.Canvas.Piece(0);
                Assert.That(floor.sortingOrder, Is.EqualTo(ShapeStyle.OrderFloor));
                Assert.That(floor.transform.localScale.x, Is.EqualTo(24f).Within(Eps));
                Assert.That(floor.transform.localScale.y, Is.EqualTo(14f).Within(Eps));
                bool owned = rig.Owned;
                GameObject camGo = cam.gameObject;
                p.Dispose();
                if (owned)
                {
                    Assert.That(camGo == null, Is.True);
                }
            }
        }

        [Test]
        public void Dispose_RemovesEverythingItMade()
        {
            GameObject root = P.Root;
            _view.Players = new[] { FakeSimView.MakePlayer(5f, 5f) };
            P.Render(_view, 0.0);
            P.Dispose();
            Assert.That(root == null, Is.True);
            Assert.Throws<ObjectDisposedException>(() => P.Render(_view, 0.0));
            Assert.Throws<ObjectDisposedException>(() => P.OnEvents(ReadOnlySpan<SimEvent>.Empty, _view));
            P.Dispose();                                                                     // 두 번 치워도 된다
        }

        [Test]
        public void Parent_HoldsTheRootAndTheCamera()
        {
            var holder = new GameObject("Holder");
            try
            {
                var setup = new WorldPresenterSetup(_ => 0.3f) { Parent = holder.transform, EnsureCamera = true, ShowDebugText = false };
                using (var p = new WorldPresenter(setup))
                {
                    Assert.That(p.Root.transform.parent, Is.SameAs(holder.transform));
                    if (p.Camera!.Owned)
                    {
                        Assert.That(p.Camera.Camera.transform.parent, Is.SameAs(p.Root.transform));
                    }
                }
                Assert.That(holder.transform.childCount, Is.EqualTo(0));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(holder);
            }
        }

        [Test]
        public void Rendering_OnlyReadsTheView()
        {
            _view.Players = new[] { FakeSimView.MakePlayer(5f, 5f), FakeSimView.MakePlayer(6f, 6f) };
            P.Render(_view, 0.0);
            // 묶음 채우기 2번(슬롯마다) + 그리기 2번 + 디버그 글자 2번. 세계 쪽 다른 호출은 없다(가짜 뷰는 읽기 칸만 있다)
            Assert.That(_view.PlayerReads, Is.EqualTo(6));
            P.Render(_view, 0.5);                                                            // 같은 틱: 다시 읽는 것은 그리기뿐
            Assert.That(_view.PlayerReads, Is.EqualTo(8));
        }

        private static ArenaOutline ArenaOutlineOf(int w, int h) =>
            ArenaOutline.From(new Wizard.Content.ArenaDef { Width = Fix64.FromInt(w), Height = Fix64.FromInt(h) });

        private int Count(Color c)
        {
            int n = 0;
            for (int i = 0; i < P.Canvas.UsedCount; i++)
            {
                n += Same(P.Canvas.Piece(i).color, c) ? 1 : 0;
            }
            return n;
        }

        private int CountAt(Color c, int order)
        {
            int n = 0;
            for (int i = 0; i < P.Canvas.UsedCount; i++)
            {
                SpriteRenderer r = P.Canvas.Piece(i);
                n += Same(r.color, c) && r.sortingOrder == order ? 1 : 0;
            }
            return n;
        }

        private SpriteRenderer Single(Color c)
        {
            SpriteRenderer? found = null;
            for (int i = 0; i < P.Canvas.UsedCount; i++)
            {
                SpriteRenderer r = P.Canvas.Piece(i);
                if (Same(r.color, c))
                {
                    Assert.That(found, Is.Null, "more than one piece of that color");
                    found = r;
                }
            }
            Assert.That(found, Is.Not.Null);
            return found!;
        }

        private static bool Same(Color a, Color b) =>
            Mathf.Abs(a.r - b.r) < Eps && Mathf.Abs(a.g - b.g) < Eps && Mathf.Abs(a.b - b.b) < Eps && Mathf.Abs(a.a - b.a) < Eps;
    }
}
