using System;
using Wizard.Content;
using Wizard.Core;

namespace Wizard.Sim.Arts
{
    // 판정기가 읽는 비기 하나의 데이터(spec 03 5.2 경로, 12.1 비용·외상·종류). 불변이다.
    // ArtDef(Wizard.Content)의 필드는 비기 데이터 작업(M5-DAT-01)이 채운다. 그때까지 트라이와 판정기는 이 값으로 만든다.
    public sealed class ParserArt
    {
        public readonly ArtId Id;
        public readonly bool IsUnique;          // 고유 비기(부모 대체가 있다, spec 03 7.3)
        public readonly CharacterId Owner;      // 고유 비기의 주인. 공용이면 Baseline
        public readonly bool IsJust;            // 섬 슬롯에 붙는다(뇌광창, spec 03 5.2)
        public readonly bool TakesAll;          // 결정을 전부 쓴다(최소 MinCost. 만개, spec 03 7.3)
        public readonly int Cost;               // 고정 비용(TakesAll 이면 쓰지 않는다)
        public readonly int MinCost;            // TakesAll 의 최소 결정 수
        public readonly bool CreditAllowed;     // 외상(char.5)을 쓸 수 있다(만개만 아니다)

        private readonly CmdDir[][] _paths;

        public ParserArt(ArtId id, bool isUnique, CharacterId owner, bool isJust, bool takesAll, int cost, int minCost,
                         bool creditAllowed, CmdDir[][] paths)
        {
            if (id == ArtId.None)
            {
                throw new SimInvariantException("arts: parser art needs an id");
            }
            if (paths == null || paths.Length == 0)
            {
                throw new SimInvariantException("arts: parser art needs at least one path");
            }
            if (cost < 0 || minCost < 0)
            {
                throw new SimInvariantException("arts: parser art cost must not be negative");
            }
            if (isUnique == (owner == CharacterId.Baseline))
            {
                throw new SimInvariantException("arts: a unique art has an owner and a common art has none");
            }
            if (isUnique && isJust)
            {
                throw new SimInvariantException("arts: a unique art cannot sit on the just slot");
            }
            var copy = new CmdDir[paths.Length][];
            for (int i = 0; i < paths.Length; i++)
            {
                CmdDir[] p = paths[i] ?? throw new SimInvariantException("arts: parser art path is null");
                var c = new CmdDir[p.Length];
                for (int j = 0; j < p.Length; j++)
                {
                    if (p[j] > CmdDir.Right)
                    {
                        throw new SimInvariantException("arts: parser art path has an unknown direction");
                    }
                    c[j] = p[j];
                }
                copy[i] = c;
            }
            Id = id;
            IsUnique = isUnique;
            Owner = owner;
            IsJust = isJust;
            TakesAll = takesAll;
            Cost = cost;
            MinCost = minCost;
            CreditAllowed = creditAllowed;
            _paths = copy;
        }

        public int PathCount => _paths.Length;

        public ReadOnlySpan<CmdDir> Path(int index) => _paths[index];
    }
}
