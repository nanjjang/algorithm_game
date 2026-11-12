using System;
using Wizard.Content;
using Wizard.Core;

namespace Wizard.Sim.Arts
{
    // 트라이와 판정기를 만드는 데이터 한 벌: 판정기 상수 + 비기들(공용 + 다섯 캐릭터의 고유). 불변이다.
    // 콘텐츠 팩의 arts·tries 절(M5-DAT-01)이 생기면 그 절에서 이 값을 만든다. 지금은 부르는 쪽(시험, 시제품)이 만든다.
    public sealed class ParserSource
    {
        public readonly ParserTimings Timings;

        private readonly ParserArt?[] _byId;     // (int)ArtId 로 찾는다
        private readonly ParserArt[] _arts;      // 넣은 순서

        public ParserSource(in ParserTimings timings, ParserArt[] arts)
        {
            if (arts == null || arts.Length == 0)
            {
                throw new SimInvariantException("arts: parser source needs arts");
            }
            int maxId = 0;
            foreach (ParserArt a in arts)
            {
                if (a == null)
                {
                    throw new SimInvariantException("arts: parser source has a null art");
                }
                if ((int)a.Id > maxId)
                {
                    maxId = (int)a.Id;
                }
            }
            var byId = new ParserArt?[maxId + 1];
            var list = new ParserArt[arts.Length];
            for (int i = 0; i < arts.Length; i++)
            {
                ParserArt a = arts[i];
                if (byId[(int)a.Id] != null)
                {
                    throw new SimInvariantException("arts: parser source has the same art twice");
                }
                byId[(int)a.Id] = a;
                list[i] = a;
            }
            Timings = timings;
            _byId = byId;
            _arts = list;
        }

        public int Count => _arts.Length;

        public ParserArt At(int index) => _arts[index];

        // 없으면 null
        public ParserArt? Find(ArtId id)
        {
            int i = (int)id;
            return i < _byId.Length ? _byId[i] : null;
        }

        public ParserArt Get(ArtId id) => Find(id) ?? throw new SimInvariantException("arts: art is not in the parser source");

        // 이 캐릭터의 트라이에 들어가는 비기인가(공용 전부 + 그 캐릭터의 고유 하나, spec 03 5.1)
        public bool IsInTrieOf(ParserArt art, CharacterId character) => !art.IsUnique || art.Owner == character;

        internal ReadOnlySpan<ParserArt> All => _arts;
    }
}
