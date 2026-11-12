#nullable enable
using System;
using System.Collections.Generic;

namespace Wizard.Unity.UI.Common
{
    // 방향키로 갈 다음 칸을 자리로 찾는다(spec 13 "방향키 칸 이동"). 게임 규칙이 아니라 배치의 기하다.
    // [임시: 명세가 고르는 법을 정하지 않았다]
    //   후보: 가운데가 그 방향으로 엄밀히 앞에 있는 것
    //   점수: 진행 방향의 가운데 거리 + 3 × 옆 방향의 어긋남(두 사각형의 옆 범위가 겹치면 0, 아니면 범위 사이 거리)
    //   가장 작은 점수. 같으면 목록에서 앞선 것(완전 순서)
    public static class UiNavigator
    {
        public const int CrossAxisWeight = 3;

        // 없으면 -1. candidates 안의 from 자신은 가운데 거리가 0 이라 뽑히지 않는다
        public static int FindNeighbor(PixelRect from, UiDirection direction, IReadOnlyList<PixelRect> candidates)
        {
            if (candidates == null)
            {
                throw new ArgumentNullException(nameof(candidates));
            }
            int best = -1;
            long bestScore = long.MaxValue;
            for (int i = 0; i < candidates.Count; i++)
            {
                long score = Score(from, candidates[i], direction);
                if (score >= 0 && score < bestScore)
                {
                    best = i;
                    bestScore = score;
                }
            }
            return best;
        }

        // 그 방향의 후보가 아니면 -1
        public static long Score(PixelRect from, PixelRect to, UiDirection direction)
        {
            long along;
            long cross;
            switch (direction)
            {
                case UiDirection.Up:
                    along = (long)from.CenterY2 - to.CenterY2;
                    cross = Gap(from.X, from.Right, to.X, to.Right);
                    break;
                case UiDirection.Down:
                    along = (long)to.CenterY2 - from.CenterY2;
                    cross = Gap(from.X, from.Right, to.X, to.Right);
                    break;
                case UiDirection.Left:
                    along = (long)from.CenterX2 - to.CenterX2;
                    cross = Gap(from.Y, from.Bottom, to.Y, to.Bottom);
                    break;
                case UiDirection.Right:
                    along = (long)to.CenterX2 - from.CenterX2;
                    cross = Gap(from.Y, from.Bottom, to.Y, to.Bottom);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(direction), direction, null);
            }
            if (along <= 0)
            {
                return -1;
            }
            const long Two = 2;   // 가운데는 두 배로 세었다
            return along + CrossAxisWeight * Two * cross;
        }

        // 반열린 범위 [a0, a1) 와 [b0, b1) 사이의 거리. 겹치면 0
        private static long Gap(int a0, int a1, int b0, int b1)
        {
            if (b0 >= a1)
            {
                return (long)b0 - a1;
            }
            if (a0 >= b1)
            {
                return (long)a0 - b1;
            }
            return 0;
        }
    }
}
