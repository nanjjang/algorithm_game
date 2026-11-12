#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Wizard.Unity.Presentation.Core
{
    // 회색 도형을 프레임마다 처음부터 그리는 판(즉시 그리기). 그림 조각은 SpriteRenderer 묶음을 돌려 쓴다:
    // Begin → Disk/Box/Bar/Triangle/Arc … → End(이번 프레임에 안 쓴 조각은 끈다). 조각 수가 늘면 그때 하나씩 만든다(할당은 그때뿐).
    // 좌표는 표시 좌표(1칸 = 1유닛, y 위로)의 UnityEngine.Vector2 다. 실수 변환(Fix64 → float)은 하지 않는다(FixConvert 가 한다).
    // 물리와 Time.timeScale 을 쓰지 않는다. 장면 파일·프리팹을 쓰지 않고 개체를 코드로 만든다(T-32).
    public sealed class ShapeCanvas : IDisposable
    {
        // URP 2D 의 빛 없는 스프라이트 셰이더. 없으면(셰이더가 빌드에 빠진 경우) SpriteRenderer 의 기본 재질을 쓴다
        private const string UnlitShaderName = "Universal Render Pipeline/2D/Sprite-Unlit-Default";
        private const float TurnDegrees = 360f;           // 단위 환산
        private const float SegmentSlack = 1e-4f;         // 막대 수를 셀 때 실수 오차로 한 개 더 생기지 않게

        private readonly GameObject _root;
        private readonly ShapeSprites _sprites;
        private readonly Material? _material;
        private readonly List<SpriteRenderer> _pieces = new List<SpriteRenderer>();
        private int _used;
        private bool _disposed;

        public ShapeCanvas(string name, Transform? parent)
        {
            _root = new GameObject(name);
            if (parent != null)
            {
                _root.transform.SetParent(parent, false);
            }
            _sprites = new ShapeSprites();
            Shader? shader = Shader.Find(UnlitShaderName);
            if (shader != null)
            {
                _material = new Material(shader) { name = name + ".unlit", hideFlags = HideFlags.DontSave };
            }
        }

        public GameObject Root => _root;

        public ShapeSprites Sprites => _sprites;

        // 이번 프레임에 쓴 조각 수
        public int UsedCount => _used;

        // 지금까지 만든 조각 수(돌려 쓰는 묶음의 크기)
        public int PieceCount => _pieces.Count;

        // 이번 프레임의 index 번째 조각(시험과 디버그용)
        public SpriteRenderer Piece(int index)
        {
            if ((uint)index >= (uint)_used)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }
            return _pieces[index];
        }

        public void Begin()
        {
            _used = 0;
        }

        public void End()
        {
            for (int i = _used; i < _pieces.Count; i++)
            {
                if (_pieces[i].enabled)
                {
                    _pieces[i].enabled = false;
                }
            }
        }

        // 원판: 중심과 반지름(칸)
        public SpriteRenderer Disk(Vector2 center, float radius, Color color, int order) =>
            Place(_sprites.Disk, center, new Vector2(radius * 2f, radius * 2f), 0f, color, order);

        // 사각: 중심, 크기(칸), 반시계 + 각도(도)
        public SpriteRenderer Box(Vector2 center, Vector2 size, float angleDeg, Color color, int order) =>
            Place(_sprites.Square, center, size, angleDeg, color, order);

        // 막대: 두 점 사이, 굵기(칸)
        public SpriteRenderer Bar(Vector2 from, Vector2 to, float width, Color color, int order)
        {
            Vector2 d = to - from;
            float len = d.magnitude;
            float angle = len > 0f ? Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg : 0f;
            return Place(_sprites.Square, (from + to) * 0.5f, new Vector2(len, width), angle, color, order);
        }

        // 삼각형: 중심, 한 변(칸), 꼭짓점이 가리키는 각도(도)
        public SpriteRenderer Triangle(Vector2 center, float size, float angleDeg, Color color, int order) =>
            Place(_sprites.Triangle, center, new Vector2(size, size), angleDeg, color, order);

        // 원호: 시작 각(반시계 + 도, 0 = 오른쪽)에서 sweepDeg 만큼(음수면 시계 방향). 한 바퀴를 segmentsPerTurn 막대로 그린다.
        // 돌려주는 값 = 그린 막대 수
        public int Arc(Vector2 center, float radius, float width, float startDeg, float sweepDeg, Color color, int order, int segmentsPerTurn)
        {
            if (segmentsPerTurn < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(segmentsPerTurn));
            }
            float turns = Mathf.Abs(sweepDeg) / TurnDegrees;
            if (turns <= 0f || radius <= 0f || width <= 0f)
            {
                return 0;
            }
            int n = Mathf.Max(1, Mathf.CeilToInt(turns * segmentsPerTurn - SegmentSlack));
            Vector2 prev = OnCircle(center, radius, startDeg);
            for (int i = 1; i <= n; i++)
            {
                Vector2 next = OnCircle(center, radius, startDeg + sweepDeg * i / n);
                Bar(prev, next, width, color, order);
                prev = next;
            }
            return n;
        }

        public static Vector2 OnCircle(Vector2 center, float radius, float deg)
        {
            float rad = deg * Mathf.Deg2Rad;
            return center + new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * radius;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            _pieces.Clear();
            ShapeSprites.Destroy(_root);
            ShapeSprites.Destroy(_material);
            _sprites.Dispose();
        }

        private SpriteRenderer Place(Sprite sprite, Vector2 center, Vector2 scale, float angleDeg, Color color, int order)
        {
            SpriteRenderer r = NextPiece();
            if (r.sprite != sprite)
            {
                r.sprite = sprite;
            }
            Transform t = r.transform;
            t.localPosition = new Vector3(center.x, center.y, 0f);
            t.localRotation = Quaternion.Euler(0f, 0f, angleDeg);
            t.localScale = new Vector3(scale.x, scale.y, 1f);
            r.color = color;
            r.sortingOrder = order;
            if (!r.enabled)
            {
                r.enabled = true;
            }
            return r;
        }

        private SpriteRenderer NextPiece()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(ShapeCanvas));
            }
            if (_used < _pieces.Count)
            {
                return _pieces[_used++];
            }
            var go = new GameObject("piece");
            go.transform.SetParent(_root.transform, false);
            SpriteRenderer r = go.AddComponent<SpriteRenderer>();
            if (_material != null)
            {
                r.sharedMaterial = _material;
            }
            _pieces.Add(r);
            _used++;
            return r;
        }
    }
}
