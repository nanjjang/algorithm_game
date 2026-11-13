using System;
using System.Collections.Generic;
using Wizard.ViewLogic.Common;
using Wizard.ViewLogic.Presentation;

namespace Wizard.ViewLogic.Assets
{
    // 통합 에셋 레지스트리(spec 14 5.7절 [임시], ARCHITECTURE 5.x ViewLogic.Assets): AssetKey → 기록.
    // 게임 코드는 매니페스트 형식 다섯 가지를 보지 않고 이것만 본다. 엔진을 모른다(텍스처는 IAssetCatalog 구현이 쥔다).
    //
    // 한 열쇠에 기록이 여럿일 수 있다: UI 는 같은 (Id, 변형, 상태)를 크기마다 따로 그린다
    // (K-40 의 16·24·32, I-L4 의 24·48·128 …, spec 14 4.4절 "크기마다 따로 그린다"). 같은 크기는 두 번 넣을 수 없다.
    // 크기는 열쇠의 일부가 아니다(DECISIONS-TECH T-58). 크기 없이 찾으면 가장 작은 기록을 준다
    // (높이, 그다음 폭이 작은 것. 매니페스트의 줄 순서에 기대지 않는다. 화면 그림은 정수 배로 키우기만 한다, spec 14 4.3절).
    //
    // 없는 열쇠는 대체 기록(AssetPlaceholder)을 돌려주고 열쇠마다 한 번만 IAssetLog 에 알린다.
    // 레지스트리는 표시 계층의 것이라 스레드 하나에서 쓴다.
    public sealed class AssetRegistry
    {
        private static readonly IReadOnlyList<AssetRecord> NoRecords = new AssetRecord[0];
        private static readonly IReadOnlyList<string> NoIds = new string[0];

        private readonly Dictionary<AssetKey, List<AssetRecord>> _byKey = new Dictionary<AssetKey, List<AssetRecord>>(AssetKeyComparer.Instance);
        private readonly List<AssetRecord> _records = new List<AssetRecord>();
        private readonly Dictionary<AssetPack, List<string>> _ids = new Dictionary<AssetPack, List<string>>();
        private readonly Dictionary<AssetPack, Dictionary<string, List<AssetRecord>>> _byId = new Dictionary<AssetPack, Dictionary<string, List<AssetRecord>>>();
        private readonly HashSet<AssetKey> _markedMissing = new HashSet<AssetKey>(AssetKeyComparer.Instance);
        private readonly List<AssetKey> _markedMissingOrder = new List<AssetKey>();
        private readonly HashSet<AssetKey> _logged = new HashSet<AssetKey>(AssetKeyComparer.Instance);
        private readonly Dictionary<string, WandSheet> _wands = new Dictionary<string, WandSheet>(StringComparer.Ordinal);
        private readonly List<string> _wandOrder = new List<string>();
        private readonly Dictionary<AssetPack, PackTextureSettings> _settings = new Dictionary<AssetPack, PackTextureSettings>();
        private IAssetLog? _log;

        public AssetRegistry(IAssetLog? log = null)
        {
            _log = log;
        }

        // 전체 기록(넣은 순서 = 매니페스트 순서)
        public IReadOnlyList<AssetRecord> Records => _records;

        public int Count => _records.Count;

        // 매니페스트에 있지만 파일이 없다고 표시한 열쇠(UI external 의 S-2405·S-2407, C-13)
        public IReadOnlyList<AssetKey> MarkedMissing => _markedMissingOrder;

        // 대체 기록을 알린 열쇠의 수
        public int PlaceholderLogCount => _logged.Count;

        public void SetLog(IAssetLog? log) => _log = log;

        // 같은 열쇠·같은 크기가 이미 있으면 false(로더가 문제로 적는다)
        public bool Add(AssetRecord record)
        {
            if (record.IsPlaceholder)
            {
                throw new ArgumentException("placeholder records are not stored", nameof(record));
            }
            if (!_byKey.TryGetValue(record.Key, out List<AssetRecord>? list))
            {
                list = new List<AssetRecord>();
                _byKey.Add(record.Key, list);
            }
            foreach (AssetRecord r in list)
            {
                if (r.FrameSize.Width == record.FrameSize.Width && r.FrameSize.Height == record.FrameSize.Height)
                {
                    return false;
                }
            }
            list.Add(record);
            _records.Add(record);
            if (!_byId.TryGetValue(record.Key.Pack, out Dictionary<string, List<AssetRecord>>? ids))
            {
                ids = new Dictionary<string, List<AssetRecord>>(StringComparer.Ordinal);
                _byId.Add(record.Key.Pack, ids);
                _ids.Add(record.Key.Pack, new List<string>());
            }
            if (!ids.TryGetValue(record.Key.Id, out List<AssetRecord>? sameId))
            {
                sameId = new List<AssetRecord>();
                ids.Add(record.Key.Id, sameId);
                _ids[record.Key.Pack].Add(record.Key.Id);
            }
            sameId.Add(record);
            return true;
        }

        public void MarkMissing(AssetKey key)
        {
            if (_markedMissing.Add(key))
            {
                _markedMissingOrder.Add(key);
            }
        }

        public bool IsMarkedMissing(AssetKey key) => _markedMissing.Contains(key);

        public bool Contains(AssetKey key) => _byKey.ContainsKey(key);

        // 크기를 주지 않은 조회: 그 열쇠의 가장 작은 기록(T-58). 크기가 하나뿐이면 그 기록이다
        public bool TryGet(AssetKey key, out AssetRecord record)
        {
            if (_byKey.TryGetValue(key, out List<AssetRecord>? list))
            {
                AssetRecord smallest = list[0];
                for (int i = 1; i < list.Count; i++)
                {
                    PixelSize s = list[i].FrameSize;
                    PixelSize m = smallest.FrameSize;
                    if (s.Height < m.Height || (s.Height == m.Height && s.Width < m.Width))
                    {
                        smallest = list[i];
                    }
                }
                record = smallest;
                return true;
            }
            record = null!;
            return false;
        }

        // 크기(프레임 하나)까지 맞는 기록
        public bool TryGet(AssetKey key, PixelSize frameSize, out AssetRecord record)
        {
            if (_byKey.TryGetValue(key, out List<AssetRecord>? list))
            {
                foreach (AssetRecord r in list)
                {
                    if (r.FrameSize.Width == frameSize.Width && r.FrameSize.Height == frameSize.Height)
                    {
                        record = r;
                        return true;
                    }
                }
            }
            record = null!;
            return false;
        }

        // 한 열쇠의 모든 크기(매니페스트 순서)
        public IReadOnlyList<AssetRecord> GetAll(AssetKey key) =>
            _byKey.TryGetValue(key, out List<AssetRecord>? list) ? list : NoRecords;

        // 있으면 그 기록(크기가 여럿이면 가장 작은 것), 없으면 대체 기록(크기 = 1칸). 대체는 열쇠마다 한 번 기록한다
        public AssetRecord Resolve(AssetKey key)
        {
            if (TryGet(key, out AssetRecord r))
            {
                return r;
            }
            return Placeholder(key, AssetPlaceholder.DefaultSize);
        }

        // 크기까지 맞는 기록, 없으면 그 크기의 대체 기록
        public AssetRecord Resolve(AssetKey key, PixelSize frameSize)
        {
            if (TryGet(key, frameSize, out AssetRecord r))
            {
                return r;
            }
            return Placeholder(key, frameSize);
        }

        // 한 묶음의 Id(처음 넣은 순서). UI 의 고유 ID 수가 TV-180 이다
        public IReadOnlyList<string> IdsOf(AssetPack pack) => _ids.TryGetValue(pack, out List<string>? ids) ? ids : NoIds;

        // 한 Id 의 모든 기록(변형·상태·크기, 매니페스트 순서)
        public IReadOnlyList<AssetRecord> RecordsOf(AssetPack pack, string id) =>
            _byId.TryGetValue(pack, out Dictionary<string, List<AssetRecord>>? ids) && ids.TryGetValue(id, out List<AssetRecord>? list) ? list : NoRecords;

        public void AddWand(WandSheet sheet)
        {
            if (_wands.ContainsKey(sheet.WandId))
            {
                throw new ArgumentException("wand sheet already added: " + sheet.WandId, nameof(sheet));
            }
            _wands.Add(sheet.WandId, sheet);
            _wandOrder.Add(sheet.WandId);
        }

        public bool TryGetWand(string wandId, out WandSheet sheet)
        {
            if (_wands.TryGetValue(wandId, out WandSheet? s))
            {
                sheet = s;
                return true;
            }
            sheet = null!;
            return false;
        }

        public IReadOnlyList<string> WandIds => _wandOrder;

        public void SetSettings(AssetPack pack, PackTextureSettings settings) => _settings[pack] = settings;

        public bool TryGetSettings(AssetPack pack, out PackTextureSettings settings) => _settings.TryGetValue(pack, out settings);

        private AssetRecord Placeholder(AssetKey key, PixelSize size)
        {
            if (_logged.Add(key))
            {
                _log?.PlaceholderUsed(key);
            }
            return AssetPlaceholder.RecordFor(key, size);
        }
    }
}
