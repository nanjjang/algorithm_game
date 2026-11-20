using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Wizard.Solo.Art
{
    /// <summary>
    /// 그림 창고(IArtLibrary 구현, 00 문서 10절). Resources/Art 아래 원본 manifest를 부팅 때 파싱만 하고,
    /// 텍스처는 처음 요청될 때 한 장씩 읽으며(LoadAll 금지), 스프라이트는 요청된 칸만 Sprite.Create로 만들어 캐시한다.
    /// meta에 슬라이스를 굽지 않는다. 그림이 없거나 rect가 시트 밖이면 null 대신 대체 그림(회청·나무 어두움 바둑판)을 준다.
    /// 효과 묶음(group)의 텍스처·스프라이트는 Release(group)로 내릴 수 있다(강화 화면이 "ritual"을 내린다).
    ///
    /// manifest에 없는 보강 묶음(Effect의 group으로 부른다):
    /// - "enhance" = sprite-pack/native-64/enhancement의 new(변이 아이콘, 지켰다 도장, 작업대 촛불, 느루, 조합 패널 …). 열쇠 = new의 열쇠.
    /// - "layers"  = sprite-pack/characters/players/layers의 groups(반응 부품, 야생 물, 금 간 거울, 성에 기둥 손). 열쇠 = 그룹 경로이고
    ///   폴더 자리에 "char.N"을 써도 된다(예 "reactive/char.1/steam"). "열쇠:접두사"로 프레임 열쇠가 그 접두사로 시작하는 것만 고른다
    ///   (예 "reactive/char.1/steam:threshold"). 붙는 자리·속도 같은 나머지 값은 LayerInfo(열쇠).Raw.
    /// </summary>
    public sealed partial class ArtLibrary : IArtLibrary, IDisposable
    {
        public const string EnhanceGroup = "enhance";
        public const string LayersGroup = "layers";

        const string PlayersDir = "players/", WandsDir = "wands/", MonstersDir = "monsters/", EffectsDir = "effects/",
            UiDir = "ui/", WorldDir = "world/", EnhanceDir = "supplement/enhancement/", LayersDir = "supplement/layers/";

        readonly IArtAssetSource _src;
        readonly PlayerManifest _players;
        readonly WandManifest _wands;
        readonly MonsterManifest _monsters;
        readonly EffectManifest _effects;
        readonly UiManifest _ui;
        readonly WorldManifest _world;
        readonly ArtPresentation _presentation;
        readonly JObject _textureOverrides;
        readonly string _enhanceBackground;

        // 캐시: 열쇠 → 만든 것. 텍스처 열쇠는 Art 기준 경로(확장자 포함).
        readonly Dictionary<string, Texture2D> _textures = new Dictionary<string, Texture2D>();
        readonly HashSet<string> _missingTextures = new HashSet<string>();
        readonly Dictionary<string, Sprite> _sprites = new Dictionary<string, Sprite>();
        readonly Dictionary<string, SpriteAnim> _anims = new Dictionary<string, SpriteAnim>();
        // Release용: 효과 묶음 → 그 묶음이 만든 텍스처 경로·스프라이트 열쇠·애니 열쇠
        readonly Dictionary<string, GroupCache> _groupCaches = new Dictionary<string, GroupCache>();
        readonly List<string> _warnings = new List<string>();

        sealed class GroupCache
        {
            public readonly HashSet<string> Textures = new HashSet<string>();
            public readonly List<string> Sprites = new List<string>();
            public readonly List<string> Anims = new List<string>();
        }

        /// <summary>실행용: Resources에서 읽는다.</summary>
        public ArtLibrary() : this(new ResourcesArtSource()) { }

        /// <summary>manifest JSON 파싱만 한다(텍스처는 읽지 않는다).</summary>
        public ArtLibrary(IArtAssetSource source)
        {
            _src = source ?? new ResourcesArtSource();
            var textureOverrides = _src.LoadText(ArtAliases.ResourceRoot + "solo_texture_overrides");
            if (textureOverrides != null)
            {
                try { _textureOverrides = JObject.Parse(textureOverrides); }
                catch (Exception e) { Warn("solo_texture_overrides: " + e.Message); }
            }
            _players = Parse(PlayersDir + "manifest", ManifestParser.ParsePlayers) ?? new PlayerManifest();
            var soloPlayers = _src.LoadText(ArtAliases.ResourceRoot + PlayersDir + "solo_overrides");
            if (soloPlayers != null)
            {
                try
                {
                    foreach (var character in ManifestParser.ParsePlayers(soloPlayers).Characters)
                        _players.Characters[character.Key] = character.Value;
                }
                catch (Exception e)
                {
                    Warn("players/solo_overrides: " + e.Message);
                }
            }
            _wands = Parse(WandsDir + "manifest", ManifestParser.ParseWands) ?? new WandManifest();
            var soloWands = Parse(WandsDir + "solo_overrides", ManifestParser.ParseWands, optional: true);
            if (soloWands != null)
            {
                foreach (var sprite in soloWands.Sprites) _wands.Sprites[sprite.Key] = sprite.Value;
                foreach (var detached in soloWands.Detached) _wands.Detached[detached.Key] = detached.Value;
            }
            _monsters = Parse(MonstersDir + "manifest", ManifestParser.ParseMonsters) ?? new MonsterManifest();
            _effects = Parse(EffectsDir + "manifest", j => ManifestParser.ParseEffects(j, EffectsDir)) ?? new EffectManifest();
            var soloEffects = Parse(EffectsDir + "solo_overrides", j => ManifestParser.ParseEffects(j, EffectsDir), optional: true);
            if (soloEffects != null)
                foreach (var group in soloEffects.Groups)
                {
                    if (_effects.ByName.TryGetValue(group.Name, out var original)) ApplyEffectOverrides(original, group);
                    else AddGroup(group);
                }
            LoadSoloSupplement();
            _ui = Parse(UiDir + "manifest", ManifestParser.ParseUi) ?? new UiManifest();
            _world = Parse(WorldDir + "manifest", ManifestParser.ParseWorld) ?? new WorldManifest();

            string bg = null;
            var enhance = Parse(EnhanceDir + "manifest", j => ManifestParser.ParseEnhancement(j, EnhanceGroup, EnhanceDir, out bg))
                          ?? new EffectGroup { Name = EnhanceGroup };
            _enhanceBackground = bg;
            AddGroup(enhance);
            var layers = Parse(LayersDir + "manifest", j => ManifestParser.ParseLayers(j, LayersGroup, LayersDir))
                         ?? new EffectGroup { Name = LayersGroup };
            var soloLayers = _src.LoadText(ArtAliases.ResourceRoot + LayersDir + "solo_overrides");
            if (soloLayers != null)
            {
                try
                {
                    var overrides = ManifestParser.ParseLayers(soloLayers, LayersGroup, LayersDir);
                    ApplyEffectOverrides(layers, overrides);
                }
                catch (Exception e)
                {
                    Warn("supplement/layers/solo_overrides: " + e.Message);
                }
            }
            AddGroup(layers);

            _presentation = ArtPresentation.Parse(_src.LoadText(ArtPresentation.ResourcePath));
            UnityEngine.Font.textureRebuilt += OnFontTextureRebuilt;
        }

        void AddGroup(EffectGroup g)
        {
            if (_effects.ByName.ContainsKey(g.Name)) return;
            _effects.Groups.Add(g);
            _effects.ByName[g.Name] = g;
        }

        static void ApplyEffectOverrides(EffectGroup original, EffectGroup overrides)
        {
            foreach (var entry in overrides.Entries)
            {
                if (original.ByKey.TryGetValue(entry.Key, out var previous))
                {
                    original.Entries[original.Entries.IndexOf(previous)] = entry;
                    original.ByKey[entry.Key] = entry;
                }
                else original.Add(entry);
            }
        }

        T Parse<T>(string artPathNoExt, Func<string, T> parse, bool optional = false) where T : class
        {
            string json = _src.LoadText(ArtAliases.ResourceRoot + artPathNoExt);
            if (json == null)
            {
                if (!optional) Warn("manifest 없음: " + artPathNoExt);
                return null;
            }
            try { return parse(json); }
            catch (Exception e)
            {
                Warn("manifest 읽기 실패: " + artPathNoExt + " — " + e.Message);
                return null;
            }
        }

        readonly HashSet<string> _misses = new HashSet<string>();

        /// <summary>찾지 못해 대체 그림을 준 요청(같은 것은 한 번). 로그는 남기지 않는다 — 그림이 아직 없는 것(09 요약서 9절)도 여기 든다.
        /// 통합 확인용: 열쇠를 잘못 적은 곳(변형·상태 이름)을 찾는 데 쓴다.</summary>
        public IReadOnlyCollection<string> Misses => _misses;

        void Miss(string what) { _misses.Add(what); }

        void Warn(string msg)
        {
            _warnings.Add(msg);
            Debug.LogWarning("[ArtLibrary] " + msg);
        }

        // ---------- 읽기 전용 정보(표시 작업·테스트용) ----------

        /// <summary>읽으며 생긴 경고(빠진 manifest·그림). 테스트와 통합 확인용.</summary>
        public IReadOnlyList<string> Warnings => _warnings;
        public PlayerManifest Players => _players;
        public WandManifest Wands => _wands;
        public MonsterManifest Monsters => _monsters;
        public EffectManifest Effects => _effects;
        public UiManifest UiAssets => _ui;
        public WorldManifest WorldAssets => _world;
        public ArtPresentation Presentation => _presentation;
        /// <summary>지금 메모리에 올라온 텍스처 수(늦게 읽기 확인용).</summary>
        public int LoadedTextureCount => _textures.Count;
        public int CachedSpriteCount => _sprites.Count;
        public bool IsTextureLoaded(string artPath) => _textures.ContainsKey(artPath);

        // ---------- 텍스처·자르기 공용 ----------

        Texture2D Tex(string artPath, string group, bool optional = false)
        {
            if (artPath == null) return null;
            if (_textures.TryGetValue(artPath, out var t) && t != null)
            {
                if (group != null) GroupOf(group).Textures.Add(artPath);
                return t;
            }
            if (_missingTextures.Contains(artPath)) return null;
            var texturePath = (string)_textureOverrides?[artPath] ?? artPath;
            t = _src.LoadTexture(ArtAliases.ToResourcePath(texturePath));
            if (t == null)
            {
                _missingTextures.Add(artPath);
                if (!optional) Warn("그림 없음: " + artPath);
                return null;
            }
            t.filterMode = FilterMode.Point;
            _textures[artPath] = t;
            if (group != null) GroupOf(group).Textures.Add(artPath);
            return t;
        }

        GroupCache GroupOf(string group)
        {
            if (!_groupCaches.TryGetValue(group, out var g)) _groupCaches[group] = g = new GroupCache();
            return g;
        }

        /// <summary>시트의 한 칸을 잘라 캐시한다. 그림이 없거나 rect가 시트 밖이면 대체 그림(캐시하지 않는다 — 대체 그림은 따로 캐시된다).</summary>
        Sprite Cut(string key, string artPath, PixelRect r, Vector2 pivot, Vector4 border, string group = null,
            float pixelsPerUnit = RectMath.PixelsPerUnit, float pixelScale = 1f)
        {
            if (_sprites.TryGetValue(key, out var s) && s != null) return s;
            if (pixelScale > 1f)
            {
                // 논리 rect(64px 기준) → 시트 rect. PPU도 같은 배수라 화면 크기는 그대로다. pivot은 정규화 값이라 그대로.
                r = new PixelRect(Mathf.RoundToInt(r.X * pixelScale), Mathf.RoundToInt(r.Y * pixelScale),
                    Mathf.RoundToInt(r.W * pixelScale), Mathf.RoundToInt(r.H * pixelScale));
                pixelsPerUnit *= pixelScale;
            }
            var tex = Tex(artPath, group);
            if (tex == null) return Placeholder(r.W, r.H);
            if (!RectMath.Fits(r, tex.width, tex.height))
            {
                Warn("rect가 시트 밖: " + artPath + " " + r + " (시트 " + tex.width + "x" + tex.height + ")");
                return Placeholder(r.W, r.H);
            }
            s = Sprite.Create(tex, RectMath.ToUnityRect(r, tex.height), pivot, pixelsPerUnit, 0, SpriteMeshType.FullRect, border, false);
            s.name = key;
            _sprites[key] = s;
            if (group != null) GroupOf(group).Sprites.Add(key);
            return s;
        }

        Sprite CutWhole(string key, string artPath, string group = null)
        {
            if (_sprites.TryGetValue(key, out var s) && s != null) return s;
            var tex = Tex(artPath, group);
            if (tex == null) return Placeholder(RectMath.PixelsPerUnitInt, RectMath.PixelsPerUnitInt);
            return Cut(key, artPath, new PixelRect(0, 0, tex.width, tex.height), Center, Vector4.zero, group);
        }

        static readonly Vector2 Center = new Vector2(0.5f, 0.5f);

        SpriteAnim CacheAnim(string key, SpriteAnim a, string group = null)
        {
            a.Key = key;
            _anims[key] = a;
            if (group != null) GroupOf(group).Anims.Add(key);
            return a;
        }

        SpriteAnim PlaceholderAnim(string key, int w, int h)
        {
            return new SpriteAnim { Key = key, Frames = new[] { Placeholder(w, h) }, Fps = 0f, Loop = true };
        }

        // ---------- 캐릭터 ----------

        PlayerCharacter CharacterOf(string charId)
        {
            var folder = ArtAliases.FolderOf(charId);
            return folder != null && _players.Characters.TryGetValue(folder, out var pc) ? pc : null;
        }

        /// <summary>그 방향에 실제로 쓰는 클립 정보(프레임별 feet·hand·head·shoulder 점). 지팡이 붙이기·머리 위 표시용. 없으면 null.</summary>
        public PlayerClip CharacterClipInfo(string charId, string clip, Facing3 facing)
        {
            var pc = CharacterOf(charId);
            if (pc == null) return null;
            var name = ArtAliases.ResolveCharacterClip(pc.ClipByName.Keys, clip, facing);
            return name != null ? pc.ClipByName[name] : null;
        }

        // ---------- 조회 기억 ----------
        // 표시 층은 프레임마다 개체마다 같은 (id, 클립)으로 묻는다. 묻는 글 그대로를 열쇠로 결과를 기억해 두면
        // 매번 열쇠 글 잇기("mo|"+id+"|"+clip 등)·별칭 풀기·람다·목록 만들기를 하지 않는다(GC 쓰레기가 프레임마다 쌓이던 곳).
        // _anims에 든 진짜 그림만 기억한다(자리 표시 그림은 예전처럼 매번 새로). 효과는 Release가 기억을 비운다.

        const string NullKey = "\u0000";
        readonly Dictionary<string, Dictionary<string, SpriteAnim[]>> _charClipMemo = new Dictionary<string, Dictionary<string, SpriteAnim[]>>(StringComparer.Ordinal);
        readonly Dictionary<string, Dictionary<string, SpriteAnim>> _monsterClipMemo = new Dictionary<string, Dictionary<string, SpriteAnim>>(StringComparer.Ordinal);
        readonly Dictionary<string, IReadOnlyList<string>> _monsterClipNamesMemo = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        readonly Dictionary<string, Dictionary<string, SpriteAnim>> _effectMemo = new Dictionary<string, Dictionary<string, SpriteAnim>>(StringComparer.Ordinal);
        Func<string, bool> _monsterExists;

        Func<string, bool> MonsterExists => _monsterExists ?? (_monsterExists = k => _monsters.ById.ContainsKey(k));

        /// <summary>_anims에 들어 있는 바로 그 그림인가(기억해도 되는 결과인가).</summary>
        bool IsCachedAnim(SpriteAnim a) => a != null && a.Key != null && _anims.TryGetValue(a.Key, out var c) && ReferenceEquals(c, a);

        static bool TryMemo<T>(Dictionary<string, Dictionary<string, T>> memo, string a, string b, out T value)
        {
            value = default(T);
            return a != null && memo.TryGetValue(a, out var inner) && inner.TryGetValue(b ?? NullKey, out value);
        }

        static void PutMemo<T>(Dictionary<string, Dictionary<string, T>> memo, string a, string b, T value)
        {
            if (a == null) return;
            if (!memo.TryGetValue(a, out var inner)) memo[a] = inner = new Dictionary<string, T>(StringComparer.Ordinal);
            inner[b ?? NullKey] = value;
        }

        public SpriteAnim CharacterClip(string charId, string clip, Facing3 facing)
        {
            int fi = (int)facing;
            bool memoable = fi >= 0 && fi < 8;
            if (memoable && TryMemo(_charClipMemo, charId, clip, out var byFacing) && byFacing[fi] != null) return byFacing[fi];
            var r = CharacterClipUncached(charId, clip, facing);
            if (memoable && IsCachedAnim(r))
            {
                if (!TryMemo(_charClipMemo, charId, clip, out byFacing)) PutMemo(_charClipMemo, charId, clip, byFacing = new SpriteAnim[8]);
                byFacing[fi] = r;
            }
            return r;
        }

        SpriteAnim CharacterClipUncached(string charId, string clip, Facing3 facing)
        {
            var pc = CharacterOf(charId);
            int canvas = _players.Canvas.X > 0 ? _players.Canvas.X : RectMath.PixelsPerUnitInt;
            if (pc == null) return PlaceholderAnim("ch|" + charId + "|" + clip, canvas, canvas);
            var name = ArtAliases.ResolveCharacterClip(pc.ClipByName.Keys, clip, facing);
            if (name == null) return PlaceholderAnim("ch|" + pc.Folder + "|" + clip, canvas, canvas);
            string key = "ch|" + pc.Folder + "|" + name;
            if (_anims.TryGetValue(key, out var cached)) return cached;
            var c = pc.ClipByName[name];
            var frames = new Sprite[c.Frames.Count];
            for (int i = 0; i < frames.Length; i++)
            {
                var f = c.Frames[i];
                frames[i] = Cut(key + "|" + i, PlayersDir + pc.Body, f.Rect, RectMath.ToPivot(f.Feet, f.Rect.W, f.Rect.H), Vector4.zero,
                    null, RectMath.PixelsPerUnit, pc.PixelScale);
            }
            if (frames.Length == 0) return PlaceholderAnim(key, canvas, canvas);
            return CacheAnim(key, new SpriteAnim { Frames = frames, Fps = c.Fps, Loop = c.Loop });
        }

        public Sprite CharacterPortrait(string charId, int expression)
        {
            var pc = CharacterOf(charId);
            if (pc == null || pc.Portrait == null) return Placeholder(RectMath.PixelsPerUnitInt, RectMath.PixelsPerUnitInt);
            var tex = Tex(PlayersDir + pc.Portrait, null);
            if (tex == null) return Placeholder(RectMath.PixelsPerUnitInt, RectMath.PixelsPerUnitInt);
            // 초상 시트는 정사각 칸의 가로 띠(64×64 × 4, asset_aliases character_sheets.portrait)
            int cell = tex.height;
            int count = Math.Max(1, tex.width / Math.Max(1, cell));
            int k = expression < 0 ? 0 : expression >= count ? count - 1 : expression;
            // 해상도 배수가 있는 캐릭터는 초상도 같은 배수(PPU 64×배수)로 만든다(칸 크기는 시트 높이 그대로).
            return Cut("chp|" + pc.Folder + "|" + k, PlayersDir + pc.Portrait, RectMath.StripFrame(k, cell, cell), Center, Vector4.zero,
                null, RectMath.PixelsPerUnit * pc.PixelScale);
        }

        public Sprite CharacterSelection(string charId)
        {
            var pc = CharacterOf(charId);
            if (pc == null || pc.Selection == null) return Placeholder(192, 192);
            return CutWhole("chs|" + pc.Folder, PlayersDir + pc.Selection);
        }

        /// <summary>실루엣 32×32(선택 화면의 잠긴 칸 등).</summary>
        public Sprite CharacterSilhouette(string charId)
        {
            var pc = CharacterOf(charId);
            if (pc == null || pc.Silhouette == null) return Placeholder(32, 32);
            return CutWhole("chsil|" + pc.Folder, PlayersDir + pc.Silhouette);
        }

        // ---------- 지팡이 ----------

        WandSprites WandOf(string charId)
        {
            var folder = ArtAliases.FolderOf(charId);
            return folder != null && _wands.Sprites.TryGetValue(folder, out var w) ? w : null;
        }

        public Sprite WandHand(string charId, int stage, Facing3 facing, bool aim)
        {
            var w = WandOf(charId);
            if (w == null || w.HandFile == null) return Placeholder(32, 64);
            stage = WandSheet.ClampStage(stage);
            int cw = w.HandCell.X > 0 ? w.HandCell.X : 32, ch = w.HandCell.Y > 0 ? w.HandCell.Y : 64;
            // 행 순서는 manifest의 rows 이름으로 찾는다(없으면 공식: 방향 × 2 + 자세).
            int row = w.HandRows.IndexOf(ArtAliases.FacingSuffix(facing) + (aim ? "_aim" : "_hold"));
            if (row < 0) row = WandSheet.HandRow(facing, aim);
            var r = new PixelRect(stage * cw, row * ch, cw, ch);
            var grip = _wands.Grip.X > 0 || _wands.Grip.Y > 0 ? _wands.Grip : new PixelPoint(16, 48);
            return Cut("wh|" + w.Folder + "|" + stage + "|" + row, WandsDir + w.HandFile, r, RectMath.ToPivot(grip, cw, ch), Vector4.zero);
        }

        public Sprite WandInspection(string charId, int stage)
        {
            var w = WandOf(charId);
            if (w == null || w.InspectionFile == null) return Placeholder(192, 192);
            stage = WandSheet.ClampStage(stage);
            int cell = w.InspectionCell.X > 0 ? w.InspectionCell.X : 192;
            var r = WandSheet.InspectionRect(stage, w.InspectionColumns > 0 ? w.InspectionColumns : 7, cell);
            return Cut("wi|" + w.Folder + "|" + stage, WandsDir + w.InspectionFile, r, Center, Vector4.zero);
        }

        public Sprite WandIcon(string charId, int stage)
        {
            var w = WandOf(charId);
            if (w == null || w.IconFile == null) return Placeholder(64, 64);
            stage = WandSheet.ClampStage(stage);
            int cell = w.IconCell.X > 0 ? w.IconCell.X : 64;
            return Cut("wc|" + w.Folder + "|" + stage, WandsDir + w.IconFile, WandSheet.IconRect(stage, cell), Center, Vector4.zero);
        }

        /// <summary>단계별 점(머리, 변이 2~3점, 확장 칸). 32×64 칸 안 px, 모든 방향 공통 [임시 U-04]. 없으면 null.</summary>
        public WandStage WandStageInfo(string charId, int stage)
        {
            var w = WandOf(charId);
            if (w == null) return null;
            stage = WandSheet.ClampStage(stage);
            for (int i = 0; i < w.Stages.Count; i++) if (w.Stages[i].Stage == stage) return w.Stages[i];
            return null;
        }

        /// <summary>손잡이 기준점(지팡이 칸 안 px). 지팡이 칸 왼쪽 위 = 캐릭터 hand − 이 점(WandSheet.AttachTopLeft).</summary>
        public PixelPoint WandGrip => _wands.Grip.X > 0 || _wands.Grip.Y > 0 ? _wands.Grip : new PixelPoint(16, 48);

        /// <summary>
        /// 그 캐릭터 지팡이의 분리 조각(초롱 최고 단계 번개, 성에 둘레 조각, 그믐 떠 있는 눈. wands/parts). 없는 캐릭터면 null.
        /// pivot = manifest anchor. 속도는 manifest에 없어 효과 기본 12fps(09 5절 "모두 fps 12")로 반복한다.
        /// </summary>
        public SpriteAnim WandDetachedPart(string charId)
        {
            var folder = ArtAliases.FolderOf(charId);
            if (folder == null) return null;
            foreach (var d in _wands.Detached.Values)
            {
                if (!d.Key.StartsWith(folder + "_", StringComparison.Ordinal)) continue;
                string key = "wd|" + d.Key;
                if (_anims.TryGetValue(key, out var cached)) return cached;
                int n = Math.Max(1, d.Frames);
                var frames = new Sprite[n];
                for (int i = 0; i < n; i++)
                    frames[i] = Cut(key + "|" + i, WandsDir + d.File, RectMath.StripFrame(i, d.Cell.X, d.Cell.Y),
                        RectMath.ToPivot(d.Anchor, d.Cell.X, d.Cell.Y), Vector4.zero);
                return CacheAnim(key, new SpriteAnim { Frames = frames, Fps = DetachedFps, Loop = true });
            }
            return null;
        }

        const float DetachedFps = 12f;

        // ---------- 몬스터 ----------

        public SpriteAnim MonsterClip(string monsterId, string clip)
        {
            if (TryMemo(_monsterClipMemo, monsterId, clip, out var hit)) return hit;
            var r = MonsterClipUncached(monsterId, clip);
            if (IsCachedAnim(r)) PutMemo(_monsterClipMemo, monsterId, clip, r);
            return r;
        }

        SpriteAnim MonsterClipUncached(string monsterId, string clip)
        {
            var id = ArtAliases.MonsterEntryKey(monsterId, MonsterExists);
            if (id == null) return PlaceholderAnim("mo|" + monsterId, RectMath.PixelsPerUnitInt, RectMath.PixelsPerUnitInt);
            var e = _monsters.ById[id];
            if (e.Clips.Count == 0) return PlaceholderAnim("mo|" + id, Math.Max(1, e.Size.X), Math.Max(1, e.Size.Y));
            var c = (clip != null ? e.Clip(clip) : null) ?? e.Clips[0];
            string key = "mo|" + id + "|" + c.Name;
            if (_anims.TryGetValue(key, out var cached)) return cached;
            var frames = new Sprite[c.Frames.Count];
            for (int i = 0; i < frames.Length; i++)
            {
                var f = c.Frames[i];
                frames[i] = Cut(key + "|" + i, MonstersDir + e.Path, f.Rect, RectMath.ToPivot(f.Origin, f.Rect.W, f.Rect.H), Vector4.zero);
            }
            if (frames.Length == 0) return PlaceholderAnim(key, Math.Max(1, e.Size.X), Math.Max(1, e.Size.Y));
            return CacheAnim(key, new SpriteAnim { Frames = frames, Fps = _presentation.MonsterClipFps, Loop = _presentation.MonsterClipLoops(c.Name) });
        }

        /// <summary>그 몬스터 그림의 클립 이름들(manifest 순서). 없으면 빈 목록. 같은 id면 같은 (읽기 전용) 목록을 돌려준다.</summary>
        public IReadOnlyList<string> MonsterClipNames(string monsterId)
        {
            if (monsterId != null && _monsterClipNamesMemo.TryGetValue(monsterId, out var memo)) return memo;
            var id = ArtAliases.MonsterEntryKey(monsterId, MonsterExists);
            var list = new List<string>();
            if (id != null) foreach (var c in _monsters.ById[id].Clips) list.Add(c.Name);
            var ro = list.AsReadOnly();
            if (monsterId != null) _monsterClipNamesMemo[monsterId] = ro;
            return ro;
        }

        public Sprite BossPortrait(string bossId)
        {
            if (bossId == null || !_monsters.BossUi.TryGetValue(bossId, out var b) || b.Portrait == null) return Placeholder(96, 96);
            return CutWhole("bp|" + bossId, MonstersDir + b.Portrait);
        }

        public Sprite BossHudIcon(string bossId)
        {
            if (bossId == null || !_monsters.BossUi.TryGetValue(bossId, out var b) || b.Hud == null) return Placeholder(32, 32);
            return CutWhole("bh|" + bossId, MonstersDir + b.Hud);
        }

        // ---------- 효과 ----------

        public SpriteAnim Effect(string group, string key)
        {
            if (TryMemo(_effectMemo, group, key, out var hit)) return hit;
            var r = EffectUncached(group, key);
            if (IsCachedAnim(r)) PutMemo(_effectMemo, group, key, r);
            return r;
        }

        SpriteAnim EffectUncached(string group, string key)
        {
            string filter = null;
            if (key != null && group == LayersGroup)
            {
                int colon = key.IndexOf(':');
                if (colon >= 0) { filter = key.Substring(colon + 1); key = key.Substring(0, colon); }
                key = ArtAliases.ReplaceCharSegments(key);
            }
            string animKey = "fx|" + group + "|" + key + (filter != null ? ":" + filter : "");
            if (_anims.TryGetValue(animKey, out var cached)) return cached;
            EffectEntry e = null;
            if (group != null && key != null && _effects.ByName.TryGetValue(group, out var g)) g.ByKey.TryGetValue(key, out e);
            if (e == null || e.Frames.Count == 0)
            {
                Miss("fx " + group + "/" + key);
                int w = e != null && e.Size.X > 0 ? e.Size.X : 32, h = e != null && e.Size.Y > 0 ? e.Size.Y : 32;
                return PlaceholderAnim(animKey, w, h);
            }
            var list = new List<Sprite>(e.Frames.Count);
            for (int i = 0; i < e.Frames.Count; i++)
            {
                if (filter != null && (e.FrameKeys == null || i >= e.FrameKeys.Count || !e.FrameKeys[i].StartsWith(filter, StringComparison.Ordinal))) continue;
                var r = e.Frames[i];
                var pivot = e.HasPivot ? RectMath.ToPivot(e.Pivot, r.W, r.H) : Center;
                list.Add(Cut(animKey + "|" + i, e.Path, r, pivot, Vector4.zero, group, RectMath.PixelsPerUnit, e.PixelScale));
            }
            if (list.Count == 0) return PlaceholderAnim(animKey, Math.Max(1, e.Size.X), Math.Max(1, e.Size.Y));
            return CacheAnim(animKey, new SpriteAnim { Frames = list.ToArray(), Fps = e.Fps, Loop = e.Loop }, group);
        }

        /// <summary>효과 항목 정보(크기, 프레임 rect, 원본 JSON). 없으면 null.</summary>
        public EffectEntry EffectInfo(string group, string key)
        {
            if (group == LayersGroup && key != null)
            {
                int colon = key.IndexOf(':');
                if (colon >= 0) key = key.Substring(0, colon);
                key = ArtAliases.ReplaceCharSegments(key);
            }
            if (group != null && key != null && _effects.ByName.TryGetValue(group, out var g) && g.ByKey.TryGetValue(key, out var e)) return e;
            return null;
        }

        /// <summary>보강 층 정보(EffectInfo(LayersGroup, key)와 같다).</summary>
        public EffectEntry LayerInfo(string key) => EffectInfo(LayersGroup, key);

        public void Release(string effectGroup)
        {
            if (effectGroup == null || !_groupCaches.TryGetValue(effectGroup, out var g)) return;
            // 기억해 둔 효과 결과가 지울 그림을 가리킬 수 있다(묻는 묶음 이름과 그림의 묶음이 다를 수 있어 통째로 비운다 — 드물다)
            _effectMemo.Clear();
            foreach (var k in g.Anims) _anims.Remove(k);
            foreach (var k in g.Sprites)
            {
                if (_sprites.TryGetValue(k, out var s)) { DestroyObject(s); _sprites.Remove(k); }
            }
            // 다른 묶음도 쓰는 텍스처는 남긴다.
            foreach (var path in g.Textures)
            {
                bool shared = false;
                foreach (var kv in _groupCaches)
                    if (kv.Key != effectGroup && kv.Value.Textures.Contains(path)) { shared = true; break; }
                if (shared) continue;
                if (_textures.TryGetValue(path, out var t))
                {
                    _textures.Remove(path);
                    _src.UnloadTexture(t);
                }
            }
            _groupCaches.Remove(effectGroup);
        }

        static void DestroyObject(UnityEngine.Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(o);
            else UnityEngine.Object.DestroyImmediate(o);
        }

        // ---------- 월드 ----------

        /// <summary>
        /// 전장 타일·소품. assetId = world/manifest.json 열쇠(shared 별칭 포함). "열쇠:자세"면 poseFrames의 그 자세만(예 "facility.master_chair:sleep").
        /// 타일형은 tod의 시간대 시트에서 1칸(pivot 가운데), 스프라이트형은 행 우선 칸들(pivot = origin), fps = manifest(없으면 0 = 멈춘 그림).
        /// </summary>
        public SpriteAnim World(string assetId, string todId = null)
        {
            if (assetId == null) return PlaceholderAnim("wo|null", RectMath.PixelsPerUnitInt, RectMath.PixelsPerUnitInt);
            string pose = null;
            int colon = assetId.IndexOf(':');
            if (colon >= 0) { pose = assetId.Substring(colon + 1); assetId = assetId.Substring(0, colon); }
            if (!_world.Assets.ContainsKey(assetId) && _world.Shared.TryGetValue(assetId, out var alias) && alias != null && _world.Assets.ContainsKey(alias))
                assetId = alias;
            if (!_world.Assets.TryGetValue(assetId, out var e))
                return PlaceholderAnim("wo|" + assetId, RectMath.PixelsPerUnitInt, RectMath.PixelsPerUnitInt);

            if (e.IsTile)
            {
                string v = ArtAliases.TodVariant(todId);
                if (!e.Variants.TryGetValue(v, out var tv) && !e.Variants.TryGetValue("day", out tv))
                    foreach (var any in e.Variants.Values) { tv = any; break; }
                string tkey = "wo|" + assetId + "|" + v;
                if (_anims.TryGetValue(tkey, out var tc)) return tc;
                if (tv == null) return PlaceholderAnim(tkey, Math.Max(1, e.FrameSize.X), Math.Max(1, e.FrameSize.Y));
                var sp = Cut(tkey, WorldDir + tv.File, tv.Rect, Center, Vector4.zero);
                return CacheAnim(tkey, new SpriteAnim { Frames = new[] { sp }, Fps = 0f, Loop = true });
            }

            string key = "wo|" + assetId + (pose != null ? ":" + pose : "");
            if (_anims.TryGetValue(key, out var cached)) return cached;
            int[] indices;
            if (pose == null || !e.PoseFrames.TryGetValue(pose, out indices))
            {
                indices = new int[Math.Max(1, e.FrameCount)];
                for (int i = 0; i < indices.Length; i++) indices[i] = i;
            }
            int fw = e.FrameSize.X, fh = e.FrameSize.Y;
            if (fw <= 0 || fh <= 0) return PlaceholderAnim(key, RectMath.PixelsPerUnitInt, RectMath.PixelsPerUnitInt);
            var frames = new Sprite[indices.Length];
            for (int i = 0; i < indices.Length; i++)
            {
                var r = RectMath.GridCell(indices[i], e.Columns, fw, fh);
                frames[i] = Cut("wo|" + assetId + "|" + indices[i], WorldDir + e.File, r, RectMath.ToPivot(e.Origin, fw, fh), Vector4.zero);
            }
            return CacheAnim(key, new SpriteAnim { Frames = frames, Fps = e.Fps, Loop = true });
        }

        /// <summary>월드 항목 정보(origin, mirrorFor, poseFrames …). 없으면 null.</summary>
        public WorldEntry WorldInfo(string assetId)
        {
            if (assetId == null) return null;
            int colon = assetId.IndexOf(':');
            if (colon >= 0) assetId = assetId.Substring(0, colon);
            if (_world.Assets.TryGetValue(assetId, out var e)) return e;
            if (_world.Shared.TryGetValue(assetId, out var alias) && alias != null && _world.Assets.TryGetValue(alias, out e)) return e;
            return null;
        }

        /// <summary>맵 배치 JSON 글(world/maps/&lt;mapId&gt;.json, 예 "field.01"). 없으면 null.</summary>
        public string WorldMapJson(string mapId)
        {
            return mapId == null ? null : _src.LoadText(ArtAliases.ResourceRoot + WorldDir + "maps/" + mapId);
        }

        public void Dispose()
        {
            UnityEngine.Font.textureRebuilt -= OnFontTextureRebuilt;
        }
    }
}
