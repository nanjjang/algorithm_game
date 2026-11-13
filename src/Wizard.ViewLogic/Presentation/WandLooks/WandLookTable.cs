using System;
using System.Collections.Generic;

namespace Wizard.ViewLogic.Presentation.WandLooks
{
    // 다섯 지팡이의 겉모습 자료. 겉모습 id 로 그림 묶음을 고르는 곳은 Resolve 하나뿐이다(spec 04 12.3절 4).
    // 없는 겉모습 id(소유를 확인하지 못한 것 포함)는 기본 겉모습이다(12.3절 5)
    public sealed class WandLookTable
    {
        private readonly List<WandLookSet> _sets;

        public WandLookTable(IReadOnlyList<WandLookSet> sets)
        {
            _sets = new List<WandLookSet>(sets.Count);
            foreach (WandLookSet s in sets)
            {
                foreach (WandLookSet e in _sets)
                {
                    if (e.WandKind == s.WandKind)
                    {
                        throw new ArgumentException("duplicate wand kind " + s.WandId, nameof(sets));
                    }
                }
                int at = 0;
                while (at < _sets.Count && _sets[at].WandKind < s.WandKind)
                {
                    at++;
                }
                _sets.Insert(at, s);
            }
        }

        // 종류 번호 오름차순
        public IReadOnlyList<WandLookSet> Sets => _sets;

        public bool TryGet(int wandKind, out WandLookSet set)
        {
            foreach (WandLookSet s in _sets)
            {
                if (s.WandKind == wandKind)
                {
                    set = s;
                    return true;
                }
            }
            set = null!;
            return false;
        }

        public WandLookSet Get(int wandKind)
        {
            if (!TryGet(wandKind, out WandLookSet set))
            {
                throw new ArgumentOutOfRangeException(nameof(wandKind));
            }
            return set;
        }

        public WandLookResolved Resolve(WandLookKey key)
        {
            WandLookSet set = Get(key.WandKind);
            WandSkin? skin = set.Skin(key.SkinId);
            return new WandLookResolved(key, set, skin);
        }
    }
}
