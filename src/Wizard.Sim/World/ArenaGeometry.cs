using System;
using Wizard.Content;
using Wizard.Core;

namespace Wizard.Sim.World
{
    // spec 01 5.6, DECISIONS 2.3, world-characters 2.6. 전장의 벽과 기둥에 대한 몸(원) 판정. 불변이다.
    // 세계를 만들 때 ArenaDef 하나로 한 번 만든다(그때만 할당). ResolveBody 는 할당하지 않는다.
    //
    // 좌표는 칸 단위 Fix64, 안쪽 영역은 [0, Width] × [0, Height](ArenaDef). y 는 아래가 +.
    // 벽: 몸의 중심을 [r, Width − r] × [r, Height − r] 안으로 축별로 따로 자른다(벽을 따라 미끄러진다) [임시].
    // 기둥: 1×1칸 사각형. 원-사각형 밀어내기, 최소 이동 방향 [임시].
    //
    // 좌우·상하 거울 대칭: 거울 입력(x → Width − x 또는 y → Height − y, 기둥도 거울 배치)이면 결과도 정확히 거울이다.
    // 그래서 반올림이 들어가는 계산(모서리 밀어내기)은 절댓값으로 하고 부호를 나중에 붙인다(Fix64 곱의 내림은 음수 쪽으로 기운다).
    // 예외: 몸의 중심이 기둥 안에 있고 두 변까지의 깊이가 정확히 같을 때만 정해진 순서(왼, 오른, 위, 아래)로 고른다.
    public sealed class ArenaGeometry
    {
        // 기둥 한 변(칸). ArenaDef.Pillars 는 1×1칸 기둥의 작은 쪽 모서리다
        private const long PillarSizeRaw = Fix64.OneRaw;

        private readonly long _widthRaw;
        private readonly long _heightRaw;

        // 기둥 사각형. 색인 = ArenaDef.Pillars 의 순서. 처리도 이 순서다
        private readonly long[] _pillarMinX;
        private readonly long[] _pillarMinY;
        private readonly long[] _pillarMaxX;
        private readonly long[] _pillarMaxY;

        public ArenaGeometry(ArenaDef def)
        {
            if (def == null)
            {
                throw new ArgumentNullException(nameof(def));
            }
            if (def.Width.Raw <= 0L || def.Height.Raw <= 0L)
            {
                throw new ArgumentException("ArenaDef must have a positive size", nameof(def));
            }
            _widthRaw = def.Width.Raw;
            _heightRaw = def.Height.Raw;
            int n = def.Pillars.Length;
            _pillarMinX = new long[n];
            _pillarMinY = new long[n];
            _pillarMaxX = new long[n];
            _pillarMaxY = new long[n];
            for (int i = 0; i < n; i++)
            {
                FixVec2 p = def.Pillars[i];
                _pillarMinX[i] = p.X.Raw;
                _pillarMinY[i] = p.Y.Raw;
                _pillarMaxX[i] = p.X.Raw + PillarSizeRaw;
                _pillarMaxY[i] = p.Y.Raw + PillarSizeRaw;
            }
        }

        public Fix64 Width => Fix64.FromRaw(_widthRaw);

        public Fix64 Height => Fix64.FromRaw(_heightRaw);

        public int PillarCount => _pillarMinX.Length;

        // 이동을 마친 몸의 중심을 벽과 기둥 밖으로 낸다(spec 01 5.6). 순서: 벽 자르기 → 기둥(색인 순) → 벽 자르기.
        // 마지막 벽 자르기는 "Pos 는 언제나 벽 안"(spec 01 3.9의 범위)을 지키는 마무리다. 기둥이 벽에서 2r 넘게 떨어져 있으면
        // 결과를 바꾸지 않는다. bodyRadius 는 0 이상이고 전장 폭·높이의 절반보다 작아야 한다.
        public FixVec2 ResolveBody(FixVec2 pos, Fix64 bodyRadius)
        {
            long r = bodyRadius.Raw;
            long x = ClampAxis(pos.X.Raw, r, _widthRaw);
            long y = ClampAxis(pos.Y.Raw, r, _heightRaw);
            for (int i = 0; i < _pillarMinX.Length; i++)
            {
                PushOutOfPillar(i, r, ref x, ref y);
            }
            x = ClampAxis(x, r, _widthRaw);
            y = ClampAxis(y, r, _heightRaw);
            return new FixVec2(Fix64.FromRaw(x), Fix64.FromRaw(y));
        }

        // 점이 기둥 안(경계 포함)에 있는가. 탄·적의 판정이 쓸 수 있다
        public bool IsInsidePillar(FixVec2 point)
        {
            long x = point.X.Raw, y = point.Y.Raw;
            for (int i = 0; i < _pillarMinX.Length; i++)
            {
                if (x >= _pillarMinX[i] && x <= _pillarMaxX[i] && y >= _pillarMinY[i] && y <= _pillarMaxY[i])
                {
                    return true;
                }
            }
            return false;
        }

        private static long ClampAxis(long v, long r, long size)
        {
            long lo = r;
            long hi = size - r;
            if (v < lo)
            {
                return lo;
            }
            return v > hi ? hi : v;
        }

        private void PushOutOfPillar(int i, long r, ref long x, ref long y)
        {
            long minX = _pillarMinX[i], maxX = _pillarMaxX[i];
            long minY = _pillarMinY[i], maxY = _pillarMaxY[i];
            // 사각형 위의 가장 가까운 점
            long qx = x < minX ? minX : (x > maxX ? maxX : x);
            long qy = y < minY ? minY : (y > maxY ? maxY : y);
            long dx = x - qx;
            long dy = y - qy;

            if (dx == 0L && dy == 0L)
            {
                // 중심이 사각형 안(경계 포함): 가장 얕은 변으로 내보낸다
                long left = x - minX, right = maxX - x, top = y - minY, bottom = maxY - y;
                long best = left;
                int side = 0;
                if (right < best) { best = right; side = 1; }
                if (top < best) { best = top; side = 2; }
                if (bottom < best) { side = 3; }
                switch (side)
                {
                    case 0: x = minX - r; break;
                    case 1: x = maxX + r; break;
                    case 2: y = minY - r; break;
                    default: y = maxY + r; break;
                }
                return;
            }

            long ax = dx < 0L ? -dx : dx;
            long ay = dy < 0L ? -dy : dy;
            if (ax >= r || ay >= r)
            {
                return;                                   // 한 축만으로도 r 이상 떨어져 있다: 겹치지 않는다
            }
            if (dy == 0L)
            {
                x = dx > 0L ? maxX + r : minX - r;        // 왼쪽·오른쪽 변
                return;
            }
            if (dx == 0L)
            {
                y = dy > 0L ? maxY + r : minY - r;        // 위·아래 변
                return;
            }

            // 모서리: |d|² < r² 이면 모서리에서 d 방향으로 거리 r 까지 민다. 128비트 정수로 비교한다
            Wide.Mul64(ax, ax, out long axHi, out ulong axLo);
            Wide.Mul64(ay, ay, out long ayHi, out ulong ayLo);
            Add128(axHi, axLo, ayHi, ayLo, out long dHi, out ulong dLo);
            Wide.Mul64(r, r, out long rHi, out ulong rLo);
            if (Wide.Compare128(dHi, dLo, rHi, rLo) >= 0)
            {
                return;
            }
            long len = Sqrt128(dHi, dLo, ax + ay);       // floor(|d|) ≥ max(ax, ay) ≥ 1
            long ox = FixMath.MulDiv(ax, r, len);
            long oy = FixMath.MulDiv(ay, r, len);
            x = dx > 0L ? qx + ox : qx - ox;
            y = dy > 0L ? qy + oy : qy - oy;
        }

        private static void Add128(long aHi, ulong aLo, long bHi, ulong bLo, out long hi, out ulong lo)
        {
            lo = unchecked(aLo + bLo);
            long carry = lo < aLo ? 1L : 0L;
            hi = unchecked(aHi + bHi + carry);
        }

        // floor(sqrt(n)), n = (hi, lo) ≥ 0. 답은 [0, upper] 안에 있다(호출자가 보장)
        private static long Sqrt128(long hi, ulong lo, long upper)
        {
            long a = 0L;
            long b = upper;
            while (a < b)
            {
                long mid = a + ((b - a + 1L) >> 1);
                Wide.Mul64(mid, mid, out long mHi, out ulong mLo);
                if (Wide.Compare128(mHi, mLo, hi, lo) <= 0)
                {
                    a = mid;
                }
                else
                {
                    b = mid - 1L;
                }
            }
            return a;
        }
    }
}
