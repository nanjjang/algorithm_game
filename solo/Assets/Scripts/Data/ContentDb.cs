using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using UnityEngine;

namespace Wizard.Solo.Data
{
    /// <summary>
    /// 콘텐츠 창고(00 문서 9절). Resources/Data의 JSON(data/sim 사본 + 솔로 보충 solo/*.json)을 Newtonsoft.Json으로 읽는다.
    /// Def 필드는 PascalCase, JSON은 snake_case — SnakeCaseNamingStrategy로 잇는다. 조회는 없는 id에 null(예외 없음).
    /// 읽기 실패(파일 없음, 파싱 오류)는 <see cref="LoadErrors"/>에 모으고 Debug.LogError로 알린 뒤 빈 값으로 둔다.
    /// </summary>
    public sealed class ContentDb : IContentDb
    {
        /// <summary>Resources 안의 경로(확장자 없이). LoadFromDirectory는 같은 상대 경로에 ".json"을 붙여 읽는다.</summary>
        public static class Paths
        {
            public const string Blocks = "sim/blocks", Elements = "sim/elements", Characters = "sim/characters",
                Arts = "sim/arts", Tries = "sim/tries", Enemies = "sim/enemies", Elites = "sim/elites", Bosses = "sim/bosses",
                Waves = "sim/waves", Frames = "sim/frames", Modifiers = "sim/modifiers", Tods = "sim/tod",
                Scoring = "sim/scoring", Starfall = "sim/starfall", WeatherEvents = "sim/weather_events", Arena = "sim/arena";
            public const string Enhance = "solo/enhance", Economy = "solo/economy", Mutations = "solo/mutations",
                Drops = "solo/drops", Camp = "solo/camp", Const = "solo/const", Practice = "solo/practice";

            /// <summary>읽는 순서(데이터 판 글자의 해시 순서이기도 하다).</summary>
            public static readonly string[] All =
            {
                Blocks, Elements, Characters, Arts, Tries, Enemies, Elites, Bosses, Waves, Frames, Modifiers, Tods,
                Scoring, Starfall, WeatherEvents, Arena, Enhance, Economy, Mutations, Drops, Camp, Const, Practice
            };
        }

        /// <summary>Resources 아래 콘텐츠 뿌리 폴더와 보충 수치 조각 폴더.</summary>
        public const string ResourcesDataRoot = "Data";
        public const string ResourcesSoloExtraRoot = "DataSolo";
        /// <summary>데이터 판 글자의 머리와 해시 글자 수(16진).</summary>
        const string DataVersionPrefix = "solo-";
        const int DataVersionHexChars = 12;

        readonly Func<string, string> _readSoloExtra;
        readonly Action<string> _logError;
        readonly Dictionary<string, JObject> _soloExtraCache = new Dictionary<string, JObject>();
        readonly JsonSerializer _serializer;
        readonly List<string> _errors = new List<string>();

        List<BlockDef> _blocks = new List<BlockDef>();
        readonly Dictionary<string, BlockDef> _blockById = new Dictionary<string, BlockDef>();
        readonly Dictionary<string, List<string>> _blocksOfRarity = new Dictionary<string, List<string>>();
        List<CharacterDef> _characters = new List<CharacterDef>();
        readonly Dictionary<string, CharacterDef> _charById = new Dictionary<string, CharacterDef>();
        List<ArtDef> _arts = new List<ArtDef>();
        readonly Dictionary<string, ArtDef> _artById = new Dictionary<string, ArtDef>();
        readonly Dictionary<string, List<string>> _trieArts = new Dictionary<string, List<string>>();
        List<EnemyDef> _enemies = new List<EnemyDef>();
        readonly Dictionary<string, EnemyDef> _enemyById = new Dictionary<string, EnemyDef>();
        List<EliteDef> _elites = new List<EliteDef>();
        readonly Dictionary<string, EliteDef> _eliteById = new Dictionary<string, EliteDef>();
        List<BossDef> _bosses = new List<BossDef>();
        readonly Dictionary<string, BossDef> _bossById = new Dictionary<string, BossDef>();
        List<FrameDef> _frames = new List<FrameDef>();
        readonly Dictionary<string, FrameDef> _frameById = new Dictionary<string, FrameDef>();
        List<ModifierDef> _modifiers = new List<ModifierDef>();
        readonly Dictionary<string, ModifierDef> _modifierById = new Dictionary<string, ModifierDef>();
        List<TodDef> _tods = new List<TodDef>();
        readonly Dictionary<string, TodDef> _todById = new Dictionary<string, TodDef>();
        List<WeatherEventDef> _weatherEvents = new List<WeatherEventDef>();
        List<ArenaDef> _arenas = new List<ArenaDef>();

        ContentDb(Func<string, string> readSoloExtra, Action<string> logError)
        {
            _readSoloExtra = readSoloExtra;
            _logError = logError ?? (m => Debug.LogError(m));
            _serializer = JsonSerializer.Create(CreateSettings());
        }

        /// <summary>Def 읽기 설정. 다른 작업이 같은 규칙으로 JSON을 Def로 읽을 때 쓴다(DataSolo 조각 등).</summary>
        public static JsonSerializerSettings CreateSettings()
        {
            return new JsonSerializerSettings
            {
                ContractResolver = new DefaultContractResolver { NamingStrategy = new SnakeCaseNamingStrategy() },
                MissingMemberHandling = MissingMemberHandling.Ignore,
                NullValueHandling = NullValueHandling.Include,
                FloatParseHandling = FloatParseHandling.Double,
            };
        }

        // ------------------------------------------------------------------ 읽기

        /// <summary>Resources/Data의 TextAsset으로 읽는다(부팅 경로).</summary>
        public static ContentDb LoadFromResources(Action<string> logError = null)
        {
            return Load(
                rel =>
                {
                    var ta = Resources.Load<TextAsset>(ResourcesDataRoot + "/" + rel);
                    return ta != null ? ta.text : null;
                },
                name =>
                {
                    var ta = Resources.Load<TextAsset>(ResourcesSoloExtraRoot + "/" + name);
                    return ta != null ? ta.text : null;
                },
                logError);
        }

        /// <summary>
        /// 폴더에서 File IO로 읽는다(테스트·도구용). dataDir = …/Assets/Resources/Data. 보충 조각 폴더를 주지 않으면
        /// dataDir 옆의 DataSolo(…/Assets/Resources/DataSolo)를 쓴다.
        /// </summary>
        public static ContentDb LoadFromDirectory(string dataDir, string soloExtraDir = null, Action<string> logError = null)
        {
            if (soloExtraDir == null)
            {
                string parent = Path.GetDirectoryName(Path.GetFullPath(dataDir).TrimEnd('/', '\\'));
                soloExtraDir = parent != null ? Path.Combine(parent, ResourcesSoloExtraRoot) : ResourcesSoloExtraRoot;
            }
            return Load(
                rel =>
                {
                    string p = Path.Combine(dataDir, rel + ".json");
                    return File.Exists(p) ? File.ReadAllText(p, Encoding.UTF8) : null;
                },
                name =>
                {
                    string p = Path.Combine(soloExtraDir, name + ".json");
                    return File.Exists(p) ? File.ReadAllText(p, Encoding.UTF8) : null;
                },
                logError);
        }

        /// <summary>
        /// 읽는 함수를 직접 준다(rel = Paths.* 상대 경로, 없으면 null을 돌려준다). logError = 오류 알림(null이면 Debug.LogError).
        /// </summary>
        public static ContentDb Load(Func<string, string> readData, Func<string, string> readSoloExtra, Action<string> logError = null)
        {
            if (readData == null) throw new ArgumentNullException(nameof(readData));
            var db = new ContentDb(readSoloExtra, logError);
            db.LoadAll(readData);
            for (int i = 0; i < db._errors.Count; i++) db._logError(db._errors[i]);
            return db;
        }

        /// <summary>읽는 동안 생긴 오류(파일 없음·파싱 실패·중복 id). 비어 있어야 정상.</summary>
        public IReadOnlyList<string> LoadErrors => _errors;

        void LoadAll(Func<string, string> read)
        {
            var texts = new Dictionary<string, string>();
            for (int i = 0; i < Paths.All.Length; i++)
            {
                string rel = Paths.All[i];
                string text = null;
                bool threw = false;
                try { text = read(rel); }
                catch (Exception e) { threw = true; _errors.Add("ContentDb: cannot read '" + rel + "': " + e.Message); }
                if (text == null && !threw) _errors.Add("ContentDb: missing data file '" + rel + "'");
                texts[rel] = text;
            }

            DataVersion = ComputeDataVersion(texts);

            var blocks = Parse<BlocksFile>(texts, Paths.Blocks);
            Elements = Parse<ElementsDef>(texts, Paths.Elements) ?? new ElementsDef();
            var chars = Parse<CharactersFile>(texts, Paths.Characters);
            var arts = Parse<ArtsFile>(texts, Paths.Arts);
            Tries = Parse<TriesDef>(texts, Paths.Tries) ?? new TriesDef();
            var enemies = Parse<EnemiesFile>(texts, Paths.Enemies);
            var elites = Parse<ElitesFile>(texts, Paths.Elites);
            var bosses = Parse<BossesFile>(texts, Paths.Bosses);
            Waves = Parse<WavesDef>(texts, Paths.Waves) ?? new WavesDef();
            var frames = Parse<FramesFile>(texts, Paths.Frames);
            var mods = Parse<ModifiersFile>(texts, Paths.Modifiers);
            var tods = Parse<TodsFile>(texts, Paths.Tods);
            Scoring = Parse<ScoringDef>(texts, Paths.Scoring) ?? new ScoringDef();
            Starfall = Parse<StarfallDef>(texts, Paths.Starfall) ?? new StarfallDef();
            var weather = Parse<WeatherEventsFile>(texts, Paths.WeatherEvents);
            var arenas = Parse<ArenasFile>(texts, Paths.Arena);
            Enhance = Parse<EnhanceConfig>(texts, Paths.Enhance) ?? new EnhanceConfig();
            Economy = Parse<EconomyConfig>(texts, Paths.Economy) ?? new EconomyConfig();
            Mutations = Parse<MutationsConfig>(texts, Paths.Mutations) ?? new MutationsConfig();
            Drops = Parse<DropsConfig>(texts, Paths.Drops) ?? new DropsConfig();
            Camp = Parse<CampConfig>(texts, Paths.Camp) ?? new CampConfig();
            Const = Parse<SoloConstDef>(texts, Paths.Const) ?? new SoloConstDef();
            Practice = Parse<PracticeConfig>(texts, Paths.Practice) ?? new PracticeConfig();

            _blocks = blocks != null && blocks.Blocks != null ? blocks.Blocks : new List<BlockDef>();
            Index(_blocks, b => b.Id, _blockById, "block");

            BaselineCharacter = chars != null && chars.Baseline != null ? chars.Baseline : new CharacterDef();
            _characters = chars != null && chars.Characters != null ? chars.Characters : new List<CharacterDef>();
            Index(_characters, c => c.Id, _charById, "character");

            _arts = arts != null && arts.Arts != null ? arts.Arts : new List<ArtDef>();
            Index(_arts, a => a.Id, _artById, "art");
            if (Tries.Characters != null)
                for (int i = 0; i < Tries.Characters.Count; i++)
                {
                    var tc = Tries.Characters[i];
                    if (tc == null || tc.Character == null) continue;
                    if (_trieArts.ContainsKey(tc.Character)) _errors.Add("ContentDb: duplicate trie for '" + tc.Character + "'");
                    _trieArts[tc.Character] = tc.Arts ?? new List<string>();
                }

            _enemies = enemies != null && enemies.Enemies != null ? enemies.Enemies : new List<EnemyDef>();
            Index(_enemies, e => e.Id, _enemyById, "enemy");
            if (Practice.Dummy != null)
            {
                if (Practice.Dummy.Id != Ids.Enemy.Dummy)
                    _errors.Add("ContentDb: practice.json dummy id must be '" + Ids.Enemy.Dummy + "'");
                if (_enemyById.ContainsKey(Ids.Enemy.Dummy))
                    _errors.Add("ContentDb: '" + Ids.Enemy.Dummy + "' must not be in enemies.json");
                _enemyById[Ids.Enemy.Dummy] = Practice.Dummy;
            }

            _elites = elites != null && elites.Elites != null ? elites.Elites : new List<EliteDef>();
            Index(_elites, e => e.Id, _eliteById, "elite");

            _bosses = bosses != null && bosses.Bosses != null ? bosses.Bosses : new List<BossDef>();
            StableSortByRotation(_bosses);
            Index(_bosses, b => b.Id, _bossById, "boss");
            BossCommon = bosses != null && bosses.Common != null ? bosses.Common : new BossCommonDef();

            _frames = frames != null && frames.Frames != null ? frames.Frames : new List<FrameDef>();
            Index(_frames, f => f.Id, _frameById, "frame");
            _modifiers = mods != null && mods.Modifiers != null ? mods.Modifiers : new List<ModifierDef>();
            Index(_modifiers, m => m.Id, _modifierById, "modifier");
            _tods = tods != null && tods.Tods != null ? tods.Tods : new List<TodDef>();
            Index(_tods, t => t.Id, _todById, "tod");
            _weatherEvents = weather != null && weather.Events != null ? weather.Events : new List<WeatherEventDef>();
            _arenas = arenas != null && arenas.Arenas != null ? arenas.Arenas : new List<ArenaDef>();
            Field = null;
            for (int i = 0; i < _arenas.Count; i++)
                if (_arenas[i] != null && _arenas[i].Id == Ids.Place.Field) { Field = _arenas[i]; break; }
            if (Field == null) _errors.Add("ContentDb: arena '" + Ids.Place.Field + "' not found");

            if (Drops.Materials != null)
            {
                try { Drops.MaterialTable = Drops.Materials.ToObject<MaterialsDef>(_serializer) ?? new MaterialsDef(); }
                catch (Exception e) { _errors.Add("ContentDb: drops.json materials: " + e.Message); }
            }

            BuildRarityOrder();
        }

        T Parse<T>(Dictionary<string, string> texts, string rel) where T : class
        {
            string text;
            if (!texts.TryGetValue(rel, out text) || text == null) return null;
            try
            {
                using (var sr = new StringReader(text))
                using (var jr = new JsonTextReader(sr))
                    return _serializer.Deserialize<T>(jr);
            }
            catch (Exception e)
            {
                _errors.Add("ContentDb: cannot parse '" + rel + "': " + e.Message);
                return null;
            }
        }

        void Index<T>(List<T> list, Func<T, string> id, Dictionary<string, T> into, string what) where T : class
        {
            for (int i = 0; i < list.Count; i++)
            {
                var item = list[i];
                string key = item != null ? id(item) : null;
                if (key == null) { _errors.Add("ContentDb: " + what + " without id at index " + i); continue; }
                if (into.ContainsKey(key)) { _errors.Add("ContentDb: duplicate " + what + " id '" + key + "'"); continue; }
                into[key] = item;
            }
        }

        static void StableSortByRotation(List<BossDef> list)
        {
            // 삽입 정렬: 같은 rotation_index면 JSON 순서를 지킨다.
            for (int i = 1; i < list.Count; i++)
            {
                var x = list[i];
                int j = i - 1;
                while (j >= 0 && list[j].RotationIndex > x.RotationIndex) { list[j + 1] = list[j]; j--; }
                list[j + 1] = x;
            }
        }

        void BuildRarityOrder()
        {
            string[] rarities = { Ids.Rarity.Common, Ids.Rarity.Rare, Ids.Rarity.Hero };
            for (int r = 0; r < rarities.Length; r++)
            {
                string rarity = rarities[r];
                var order = new List<string>();
                List<string> fixedOrder;
                if (Drops.RarityOrder != null && Drops.RarityOrder.TryGetValue(rarity, out fixedOrder) && fixedOrder != null)
                {
                    for (int i = 0; i < fixedOrder.Count; i++)
                    {
                        BlockDef b;
                        if (!_blockById.TryGetValue(fixedOrder[i], out b))
                            _errors.Add("ContentDb: drops.json rarity_order has unknown block '" + fixedOrder[i] + "'");
                        else if (b.Rarity != rarity)
                            _errors.Add("ContentDb: drops.json rarity_order '" + rarity + "' has block '" + b.Id + "' of rarity '" + b.Rarity + "'");
                        else order.Add(b.Id);
                    }
                    // 순서 표에 빠진 같은 등급 블록이 있으면 오류로 알리고 blocks.json 순서대로 뒤에 붙인다.
                    for (int i = 0; i < _blocks.Count; i++)
                        if (_blocks[i].Rarity == rarity && !order.Contains(_blocks[i].Id))
                        {
                            _errors.Add("ContentDb: drops.json rarity_order '" + rarity + "' misses '" + _blocks[i].Id + "'");
                            order.Add(_blocks[i].Id);
                        }
                }
                else
                {
                    _errors.Add("ContentDb: drops.json rarity_order has no '" + rarity + "' list");
                    for (int i = 0; i < _blocks.Count; i++)
                        if (_blocks[i].Rarity == rarity) order.Add(_blocks[i].Id);
                }
                _blocksOfRarity[rarity] = order;
            }
        }

        static string ComputeDataVersion(Dictionary<string, string> texts)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < Paths.All.Length; i++)
            {
                string t;
                texts.TryGetValue(Paths.All[i], out t);
                sb.Append(Paths.All[i]).Append('\n').Append(t ?? string.Empty).Append('\n');
            }
            byte[] hash;
            using (var sha = SHA256.Create()) hash = sha.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));
            var hex = new StringBuilder(DataVersionPrefix);
            for (int i = 0; i < hash.Length && hex.Length < DataVersionPrefix.Length + DataVersionHexChars; i++)
                hex.Append(hash[i].ToString("x2"));
            return hex.ToString(0, DataVersionPrefix.Length + DataVersionHexChars);
        }

        static T Find<T>(Dictionary<string, T> map, string id) where T : class
        {
            T v;
            return id != null && map.TryGetValue(id, out v) ? v : null;
        }

        // ------------------------------------------------------------------ IContentDb

        public string DataVersion { get; private set; }

        public IReadOnlyList<BlockDef> Blocks => _blocks;
        public BlockDef Block(string blockId) => Find(_blockById, blockId);
        public IReadOnlyList<string> BlocksOfRarity(string rarity)
        {
            List<string> list;
            return rarity != null && _blocksOfRarity.TryGetValue(rarity, out list) ? list : (IReadOnlyList<string>)Array.Empty<string>();
        }
        public ElementsDef Elements { get; private set; }

        public IReadOnlyList<CharacterDef> Characters => _characters;
        public CharacterDef Character(string charId) => Find(_charById, charId);
        public CharacterDef BaselineCharacter { get; private set; }

        public IReadOnlyList<ArtDef> Arts => _arts;
        public ArtDef Art(string artId) => Find(_artById, artId);
        public TriesDef Tries { get; private set; }
        public IReadOnlyList<string> TrieArts(string charId)
        {
            List<string> list;
            return charId != null && _trieArts.TryGetValue(charId, out list) ? list : null;
        }

        public IReadOnlyList<EnemyDef> Enemies => _enemies;
        public EnemyDef Enemy(string enemyId) => Find(_enemyById, enemyId);
        public EliteDef Elite(string eliteOrEnemyId)
        {
            if (eliteOrEnemyId == null) return null;
            var e = Find(_eliteById, eliteOrEnemyId);
            if (e != null) return e;
            const string enemyPrefix = "enemy.";
            if (eliteOrEnemyId.StartsWith(enemyPrefix, StringComparison.Ordinal) && eliteOrEnemyId.Length > enemyPrefix.Length)
                return Find(_eliteById, Ids.Enemy.EliteOf(eliteOrEnemyId));
            return null;
        }
        /// <summary>elites.json 목록(JSON 순서).</summary>
        public IReadOnlyList<EliteDef> Elites => _elites;
        public BossCommonDef BossCommon { get; private set; }
        public IReadOnlyList<BossDef> Bosses => _bosses;
        public BossDef Boss(string bossId) => Find(_bossById, bossId);

        public WavesDef Waves { get; private set; }
        public IReadOnlyList<FrameDef> Frames => _frames;
        public FrameDef Frame(string frameId) => Find(_frameById, frameId);
        public IReadOnlyList<ModifierDef> Modifiers => _modifiers;
        public ModifierDef Modifier(string whimId) => Find(_modifierById, whimId);
        public IReadOnlyList<TodDef> Tods => _tods;
        public TodDef Tod(string todId) => Find(_todById, todId);
        public ScoringDef Scoring { get; private set; }
        public StarfallDef Starfall { get; private set; }
        public IReadOnlyList<WeatherEventDef> WeatherEvents => _weatherEvents;
        public ArenaDef Field { get; private set; }
        /// <summary>arena.json 전부(결투 전장 place.arena 포함. 솔로는 Field만 쓴다).</summary>
        public IReadOnlyList<ArenaDef> Arenas => _arenas;

        public EnhanceConfig Enhance { get; private set; }
        public EconomyConfig Economy { get; private set; }
        public MutationsConfig Mutations { get; private set; }
        public DropsConfig Drops { get; private set; }
        public CampConfig Camp { get; private set; }
        public SoloConstDef Const { get; private set; }
        public PracticeConfig Practice { get; private set; }

        public JObject SoloExtra(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            JObject cached;
            if (_soloExtraCache.TryGetValue(name, out cached)) return cached;
            JObject result = null;
            if (_readSoloExtra != null)
            {
                try
                {
                    string text = _readSoloExtra(name);
                    if (text != null) result = JObject.Parse(text);
                }
                catch (Exception e)
                {
                    _logError("ContentDb: cannot read solo extra '" + name + "': " + e.Message);
                    result = null;
                }
            }
            _soloExtraCache[name] = result;
            return result;
        }

        /// <summary>보충 조각을 Def 꼴로 읽는다(같은 snake_case 규칙). 없으면 null.</summary>
        public T SoloExtraAs<T>(string name) where T : class
        {
            var jo = SoloExtra(name);
            return jo != null ? jo.ToObject<T>(_serializer) : null;
        }
    }
}
