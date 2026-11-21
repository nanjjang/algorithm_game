using System.Collections.Generic;
using UnityEngine;
using Wizard.Solo.Art;
using Wizard.Solo.Data;
using Wizard.Solo.Sim;

namespace Wizard.Solo.View.World
{
    /// <summary>
    /// 전장 층(00 문서 7절 AddArena): 24×14 바닥 타일(시간대별 시트, 날마다 맵 6벌), 가장자리 벽·등장구 덤불·판정 없는 소품,
    /// 탑, 모닥불과 출발 표식(야영 중, RunState의 자리·반경만 읽는다), 출발 누름 고리, 시간대 전환(야영 중 4초),
    /// 해질녘 호박 테두리·밤 비네트·달빛 테두리, 보스 웨이브 달과 달빛 원, 날씨(소나기 빗줄기·물웅덩이, 안개 가장자리, 버린 물건).
    /// 상태를 읽기만 한다. Build는 이벤트 없이 지금 상태만 보고 그린다(시간대·맵·날씨·야영 여부를 상태에서 고른다).
    /// 그리는 층: 바닥(Floor)·바닥 흔적(FloorMarks) 띠와 날씨(Weather) 띠 — 예고·탄·열 링보다 아래, 날씨 덧칠만 위(10 3.1절).
    /// </summary>
    public sealed class ArenaLayer : IWorldViewLayer
    {
        // 바닥 띠 안의 순서(띠 안 0~99).
        const int OrderTiles = SortBands.Floor, OrderTilesBlend = SortBands.Floor + 1, OrderMoon = SortBands.Floor + 3,
            OrderTower = SortBands.Floor + 5, OrderWindow = SortBands.Floor + 6, OrderProps = SortBands.Floor + 10,
            OrderWalls = SortBands.Floor + 20, OrderSpawns = SortBands.Floor + 21, OrderRims = SortBands.Floor + 30;
        const int OrderMoonlight = SortBands.FloorMarks + 5, OrderPuddles = SortBands.FloorMarks + 10,
            OrderFireRing = SortBands.FloorMarks + 20, OrderMarker = SortBands.FloorMarks + 21, OrderMarkerInner = SortBands.FloorMarks + 22,
            OrderVignette = SortBands.FloorMarks + 90, OrderMoonEdge = SortBands.FloorMarks + 91;
        const int OrderHoldRing = SortBands.PlayerMarks + 10;
        const int OrderRain = SortBands.Weather, OrderFog = SortBands.Weather + 1;

        ViewContext _vc;
        SimWorld _w;
        IArtLibrary _art;
        ISpritePool _pool;
        IContentDb _db;
        WorldTuning _t;
        float _time;

        readonly TodTransition _tod = new TodTransition();
        ArenaScene _scene;
        ArenaMap _map;
        string _mapId;
        string _tilesTod, _blendTod;

        readonly List<SpriteRenderer> _tiles = new List<SpriteRenderer>();
        readonly List<SpriteRenderer> _blendTiles = new List<SpriteRenderer>();
        readonly List<SpriteRenderer> _walls = new List<SpriteRenderer>();
        readonly List<SpriteRenderer> _rims = new List<SpriteRenderer>();
        readonly List<SpriteRenderer> _spawns = new List<SpriteRenderer>();
        readonly List<float> _spawnShakeUntil = new List<float>();
        readonly List<Vector3> _spawnBase = new List<Vector3>();
        readonly List<SpriteRenderer> _props = new List<SpriteRenderer>();
        readonly List<SpriteRenderer> _puddles = new List<SpriteRenderer>();
        readonly SpriteRenderer[] _rain = new SpriteRenderer[4];
        readonly SpriteRenderer[] _fog = new SpriteRenderer[4];
        readonly List<SpriteRenderer> _fireflies = new List<SpriteRenderer>();
        SpriteRenderer _tower, _window, _moon, _moonlight, _campfire, _fireRing, _marker, _markerInner, _discarded;
        int _discardedVariant = -2;
        MeshRenderer _holdRing, _vignette, _moonEdge;
        Mesh _holdMesh, _vignetteMesh, _moonEdgeMesh;
        float _windowThrowStart = float.MinValue;
        float _holdCancelStart = float.MinValue;
        float _holdCancelRatio;
        float _lastHoldRatio = -1f;

        // ---- 시험·다른 층이 읽는 지금 상태 ----
        public ArenaScene Scene => _scene;
        public TodTransition Tod => _tod;
        public string MapId => _mapId;
        public SpriteRenderer Campfire => _campfire;
        public SpriteRenderer FireRing => _fireRing;
        public SpriteRenderer DepartMarker => _marker;
        public MeshRenderer HoldRing => _holdRing;
        public int TileCount => _tiles.Count;
        public int ActiveRainCount { get { int n = 0; foreach (var r in _rain) if (r != null) n++; return n; } }
        public int ActiveFireflyCount => _fireflies.Count;
        public int ActiveFogCount { get { int n = 0; foreach (var r in _fog) if (r != null) n++; return n; } }
        public int PuddleCount => _puddles.Count;
        public bool MoonlightShown => _moonlight != null;
        public bool VignetteShown => _vignette != null;

        public void Build(ViewContext vc, SimWorld w)
        {
            DisposeAll();
            _vc = vc;
            _w = w;
            _art = vc != null && vc.Game != null ? vc.Game.Art : null;
            _pool = vc != null ? vc.Pool : null;
            _db = w != null ? w.Content : vc != null && vc.Game != null ? vc.Game.Content : null;
            _t = WorldTuning.From(_db);
            _time = 0f;
            // 이벤트 없이 상태만 보고 고른다: 시간대는 목표를 그대로(전환 없이), 맵은 지금 소품 배치 번호.
            _scene = ArenaScene.From(w, vc != null ? vc.Options : null, _t, _db);
            _tod.Reset(_scene.TodId);
            if (_art == null || _pool == null) return;
            BuildStatic();
            Render(w, 1f, 0f);
        }

        public void OnEvent(SimWorld w, in SimEvent e)
        {
            switch (e.Type)
            {
                case SimEventType.EnemySpawnNotice:
                    if (_map != null)
                    {
                        int i = _map.NearestSpawn(e.Pos);
                        if (i >= 0 && i < _spawnShakeUntil.Count) _spawnShakeUntil[i] = _time + _t.SpawnShakeSeconds;
                    }
                    break;
                case SimEventType.DayChanged:
                    // 새 날: 맵(소품 배치)은 상태(PropVariant)에서 Render가 다시 고른다. 탑 창에서 버린 물건이 떨어진다.
                    _windowThrowStart = _time;
                    break;
                case SimEventType.DepartHold:
                    if (e.Slot >= 0 && w != null && e.Slot != w.Me.Slot) break; // 협동: 동료의 출발 누름은 내 고리가 아니다
                    if (e.A == 0 && _lastHoldRatio > 0f)
                    {
                        _holdCancelStart = _time;
                        _holdCancelRatio = _lastHoldRatio;
                    }
                    else if (e.A != 0) _holdCancelStart = float.MinValue;
                    break;
                case SimEventType.TodChanged:
                    // 전환의 시작점. 목표는 이벤트 글자가 아니라 상태(다음 웨이브 구성)에서 고른다 — Build·Render와 같은 한 곳이라
                    // 이벤트를 놓쳐도(보조 판에서 돌아옴) 같은 그림이 된다.
                    if (w != null)
                        _tod.Update(WorldViewRules.TargetTod(w), w.Tick, w.InCamp,
                            _db != null && _db.Camp != null ? SimConst.Ticks(_db.Camp.TodTransitionSeconds) : 0);
                    break;
            }
        }

        public void Render(SimWorld w, float alpha, float dt)
        {
            if (w == null) return;
            _w = w;
            _time += dt;
            _scene = ArenaScene.From(w, _vc != null ? _vc.Options : null, _t, _db);
            int transitionTicks = _db != null && _db.Camp != null ? SimConst.Ticks(_db.Camp.TodTransitionSeconds) : 0;
            _tod.Update(_scene.TodId, w.Tick, _scene.InCamp, transitionTicks);
            if (_art == null || _pool == null) return;

            if (_scene.MapId != _mapId) RebuildMap();
            RenderTiles(w.Tick);
            RenderTodOverlays(w.Tick);
            RenderSpawns();
            RenderTower();
            RenderCamp();
            RenderWeather();
        }

        public void Dispose() => DisposeAll();

        // ------------------------------------------------------------ 짓기

        void BuildStatic()
        {
            RebuildMap();
            // 탑(위쪽 가장자리 너머, 09 7.3절)과 창.
            if (!string.IsNullOrEmpty(_t.TowerId))
            {
                var anim = _art.World(_t.TowerId);
                _tower = _pool.Rent(WorldDraw.FrameAt(anim, 0), OrderTower);
                WorldDraw.Place(_tower, View(_t.TowerPos));
            }
            if (!string.IsNullOrEmpty(_t.TowerWindowId))
            {
                var anim = _art.World(_t.TowerWindowId);
                _window = _pool.Rent(WorldDraw.FrameAt(anim, _t.TowerWindowClosedFrame), OrderWindow);
                var off = new Vector2(_t.TowerWindowOffsetPx.x, _t.TowerWindowOffsetPx.y) / SimConst.PixelsPerUnit;
                WorldDraw.Place(_window, View(_t.TowerPos + off));
            }
        }

        void RebuildMap()
        {
            ReturnList(_tiles);
            ReturnList(_blendTiles);
            ReturnList(_walls);
            ReturnList(_rims);
            ReturnList(_spawns);
            ReturnList(_props);
            _spawnShakeUntil.Clear();
            _spawnBase.Clear();
            _tilesTod = _blendTod = null;
            _mapId = _scene.MapId;
            _map = _mapId != null ? ArenaMap.Parse(_art.WorldMapJson(_mapId)) : null;

            int W = Mathf.RoundToInt(_w != null ? _w.ArenaWidth : 24f), H = Mathf.RoundToInt(_w != null ? _w.ArenaHeight : 14f);
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    var sr = _pool.Rent(null, OrderTiles);
                    WorldDraw.Place(sr, View(new Vector2(x + 0.5f, y + 0.5f)));
                    _tiles.Add(sr);
                }
            if (_map == null) return;

            for (int i = 0; i < _map.Walls.Count; i++)
            {
                var p = _map.Walls[i];
                _walls.Add(RentPiece(p, OrderWalls, 0));
                var rim = _pool.Rent(null, OrderRims);
                WorldDraw.Place(rim, View(PieceAnchor(p)));
                rim.enabled = false;
                _rims.Add(rim);
            }
            for (int i = 0; i < _map.Spawns.Count; i++)
            {
                var p = _map.Spawns[i];
                var sr = RentPiece(p, OrderSpawns, _t.SpawnOpenFrame);
                _spawns.Add(sr);
                _spawnBase.Add(sr.transform.localPosition);
                _spawnShakeUntil.Add(float.MinValue);
            }
            for (int i = 0; i < _map.Props.Count; i++)
            {
                var p = _map.Props[i];
                int wTiles = PieceWidthTiles(p.Sprite);
                // 판정 없는 소품은 가장자리 2칸 안쪽에만(가운데 20×10칸 비움, 01 4.3절). 맵이 어기면 그리지 않는다.
                if (WorldViewRules.OverlapsCenter(p.Tile.x, p.Tile.y, wTiles, 1, W, H, _t.CenterClearCells)) continue;
                _props.Add(RentPiece(p, OrderProps, 0));
            }
            _discardedVariant = -2;
        }

        SpriteRenderer RentPiece(in MapPiece p, int order, int frame)
        {
            var anim = _art.World(p.Sprite);
            var sr = _pool.Rent(WorldDraw.FrameAt(anim, frame), order);
            sr.flipX = p.MirrorX;
            WorldDraw.Place(sr, View(PieceAnchor(p)));
            return sr;
        }

        Vector2 PieceAnchor(in MapPiece p)
        {
            var info = _art.WorldInfo(p.Sprite);
            var origin = info != null ? info.Origin : new PixelPoint(SimConst.PixelsPerUnit / 2, SimConst.PixelsPerUnit / 2);
            if (p.MirrorX && info != null)
            {
                // 뒤집은 그림은 기준점 둘레로 뒤집힌다 → 칸을 그대로 덮도록 기준점 x를 반대편으로 옮긴다.
                origin = new PixelPoint(info.FrameSize.X - origin.X, origin.Y);
            }
            return WorldViewRules.SpriteAnchorSim(p.Tile, origin);
        }

        int PieceWidthTiles(string spriteId)
        {
            var info = _art.WorldInfo(spriteId);
            if (info == null || info.FrameSize.X <= 0) return 1;
            return Mathf.Max(1, Mathf.CeilToInt(info.FrameSize.X / (float)SimConst.PixelsPerUnit));
        }

        // ------------------------------------------------------------ 바닥과 시간대

        void RenderTiles(int tick)
        {
            string from = _tod.From, to = _tod.To;
            bool blending = _tod.Blending(tick) && ArtAliases.TodVariant(from) != ArtAliases.TodVariant(to);
            string baseTod = blending ? from : to;
            if (_tilesTod != baseTod)
            {
                SetTileSprites(_tiles, baseTod);
                _tilesTod = baseTod;
            }
            if (blending)
            {
                if (_blendTiles.Count == 0)
                    for (int i = 0; i < _tiles.Count; i++)
                    {
                        var sr = _pool.Rent(null, OrderTilesBlend);
                        sr.transform.localPosition = _tiles[i].transform.localPosition;
                        _blendTiles.Add(sr);
                    }
                if (_blendTod != to)
                {
                    SetTileSprites(_blendTiles, to);
                    _blendTod = to;
                }
                float p = _tod.Progress(tick);
                for (int i = 0; i < _blendTiles.Count; i++) WorldDraw.SetAlpha(_blendTiles[i], Color.white, p);
            }
            else if (_blendTiles.Count > 0)
            {
                ReturnList(_blendTiles);
                _blendTod = null;
            }
        }

        void SetTileSprites(List<SpriteRenderer> list, string tod)
        {
            int W = Mathf.RoundToInt(_w.ArenaWidth);
            for (int i = 0; i < list.Count; i++)
            {
                int x = i % W, y = i / W;
                string id = null;
                if (_map != null && _map.Ground != null && y < _map.Ground.Length && x < _map.Ground[y].Length) id = _map.Ground[y][x];
                if (string.IsNullOrEmpty(id)) id = _t.FallbackTileId;
                list[i].sprite = WorldDraw.FrameAt(_art.World(id, tod), 0);
            }
        }

        /// <summary>시간대의 밤다움(0 낮·해질녘, 1 밤·보스)과 해질녘다움을 전환 비율로 섞는다.</summary>
        float Blend(int tick, System.Func<string, float> f)
        {
            float p = _tod.Progress(tick);
            return f(_tod.From) * (1f - p) + f(_tod.To) * p;
        }

        void RenderTodOverlays(int tick)
        {
            float night = Blend(tick, s => WorldViewRules.IsNightLike(s) ? 1f : 0f);
            float dusk = Blend(tick, s => s == Ids.Tod.Dusk ? 1f : 0f);
            float boss = Blend(tick, s => s == Ids.Tod.Boss ? 1f : 0f);

            // 가장자리 윗면 빛: 해질녘 호박색, 밤 달빛(09 7.3 field.wall.rim.*).
            for (int i = 0; i < _rims.Count; i++)
            {
                var rim = _rims[i];
                string id = night >= dusk ? _t.RimWallNightId : _t.RimWallDuskId;
                float a = Mathf.Max(night, dusk);
                if (a <= 0f || string.IsNullOrEmpty(id)) { rim.enabled = false; continue; }
                rim.enabled = true;
                var p = _map.Walls[i];
                rim.sprite = WorldDraw.FrameAt(_art.World(id), (p.Tile.x + p.Tile.y) % 3);
                rim.flipX = p.MirrorX;
                WorldDraw.SetAlpha(rim, Color.white, a);
            }

            float W = _w.ArenaWidth, H = _w.ArenaHeight;
            // 밤 비네트(바닥 층에만 — 예고·탄·열 링은 이보다 위, 01 4.1절).
            if (night > 0f && _t.VignetteAlpha > 0f)
            {
                if (_vignette == null)
                {
                    _vignette = _pool.RentMesh(OrderVignette);
                    _vignetteMesh = new Mesh { name = "arena_vignette" };
                    _vignette.GetComponent<MeshFilter>().sharedMesh = _vignetteMesh;
                    _vignette.transform.localPosition = Vector3.zero;
                }
                var c = _t.VignetteColor;
                c.a = _t.VignetteAlpha * night;
                ShapeMeshes.Vignette(_vignetteMesh, W, H, _t.VignetteInnerFraction, c);

                if (_moonEdge == null)
                {
                    _moonEdge = _pool.RentMesh(OrderMoonEdge);
                    _moonEdgeMesh = new Mesh { name = "arena_moon_edge" };
                    _moonEdge.GetComponent<MeshFilter>().sharedMesh = _moonEdgeMesh;
                    _moonEdge.transform.localPosition = Vector3.zero;
                }
                var e = _t.MoonlightEdgeColor;
                e.a = _t.MoonlightEdgeAlpha * night;
                // 벽(두께 1칸) 안쪽 경계에 2px 달빛 테두리.
                float px = 2f / SimConst.PixelsPerUnit;
                ShapeMeshes.Frame(_moonEdgeMesh, W - 2f, H - 2f, px, e);
                _moonEdge.transform.localPosition = new Vector3(1f, 1f, 0f);
            }
            else
            {
                ReturnMesh(ref _vignette, ref _vignetteMesh);
                ReturnMesh(ref _moonEdge, ref _moonEdgeMesh);
            }

            // 보스 웨이브: 바닥 달빛 원(판정 없음)과 위쪽 밖의 달.
            if (boss > 0f)
            {
                if (_moonlight == null && !string.IsNullOrEmpty(_t.MoonlightId))
                {
                    _moonlight = _pool.Rent(WorldDraw.FrameAt(_art.World(_t.MoonlightId), 0), OrderMoonlight);
                    WorldDraw.Place(_moonlight, View(new Vector2(W * 0.5f, H * 0.5f)));
                }
                if (_moon == null && !string.IsNullOrEmpty(_t.MoonId))
                {
                    _moon = _pool.Rent(WorldDraw.FrameAt(_art.World(_t.MoonId), 0), OrderMoon);
                    WorldDraw.Place(_moon, View(_t.MoonPos));
                }
                WorldDraw.SetAlpha(_moonlight, Color.white, _t.MoonlightCircleAlpha * boss);
                WorldDraw.SetAlpha(_moon, Color.white, boss);
            }
            else
            {
                ReturnSprite(ref _moonlight);
                ReturnSprite(ref _moon);
            }
        }

        // ------------------------------------------------------------ 등장구·탑

        void RenderSpawns()
        {
            if (_map == null) return;
            float jitterPx = _scene.Fog ? _t.SpawnShakeFogJitterPx : _t.SpawnShakeJitterPx;
            for (int i = 0; i < _spawns.Count; i++)
            {
                var sr = _spawns[i];
                var p = _map.Spawns[i];
                var anim = _art.World(p.Sprite);
                bool shaking = _time < _spawnShakeUntil[i];
                if (shaking && _t.SpawnShakeFrames.Length > 0 && anim != null)
                {
                    int k = anim.Fps > 0f ? (int)(_time * anim.Fps) % _t.SpawnShakeFrames.Length : 0;
                    sr.sprite = WorldDraw.FrameAt(anim, _t.SpawnShakeFrames[k]);
                    var o = Random.insideUnitCircle * (jitterPx / SimConst.PixelsPerUnit);
                    sr.transform.localPosition = _spawnBase[i] + new Vector3(o.x, o.y, 0f);
                }
                else
                {
                    sr.sprite = WorldDraw.FrameAt(anim, _t.SpawnOpenFrame);
                    sr.transform.localPosition = _spawnBase[i];
                }
            }
        }

        void RenderTower()
        {
            if (_tower != null)
            {
                // 탑: idle 0 / 빨래 1-4 반복(09 7.3절 fps 6). 빨래는 낮·해질녘에만 넌다.
                var anim = _art.World(_t.TowerId);
                bool laundry = !WorldViewRules.IsNightLike(_tod.To) && anim != null && anim.Frames != null && anim.Frames.Length > 1;
                if (laundry)
                {
                    int n = anim.Frames.Length - 1;
                    int k = anim.Fps > 0f ? (int)(_time * anim.Fps) % n : 0;
                    _tower.sprite = anim.Frames[1 + k];
                }
                else _tower.sprite = WorldDraw.FrameAt(anim, 0);
            }
            if (_window != null)
            {
                var anim = _art.World(_t.TowerWindowId);
                float since = _time - _windowThrowStart;
                if (since >= 0f && since < _t.TowerWindowThrowSeconds && _t.TowerWindowThrowFrames.Length > 0)
                {
                    int k = Mathf.Min(_t.TowerWindowThrowFrames.Length - 1,
                        (int)(since / _t.TowerWindowThrowSeconds * _t.TowerWindowThrowFrames.Length));
                    _window.sprite = WorldDraw.FrameAt(anim, _t.TowerWindowThrowFrames[k]);
                }
                else _window.sprite = WorldDraw.FrameAt(anim, _t.TowerWindowClosedFrame);
            }
        }

        // ------------------------------------------------------------ 야영

        void RenderCamp()
        {
            if (_scene.ShowCampfire)
            {
                // 모닥불(StartCamp 포함)과 상호작용 반경: Run이 camp.json으로 정한 자리·반경만 읽는다.
                if (_campfire == null) _campfire = _pool.Rent(null, SortBands.Bodies);
                var fireAnim = _art.World(_t.CampfireId);
                _campfire.sprite = WorldDraw.Frame(fireAnim, _time);
                _campfire.sortingOrder = SortBands.Bodies + SortBands.YOrder(_scene.FirePos.y);
                WorldDraw.Place(_campfire, View(_scene.FirePos));

                _fireRing = Ring(_fireRing, OrderFireRing, _scene.FirePos, _scene.FireRadius, _t.InteractRingColor, _t.InteractRingAlpha);
                _marker = Ring(_marker, OrderMarker, _scene.MarkerPos, _scene.MarkerRadius, _t.DepartMarkerColor, _t.DepartMarkerAlpha);
                if (_markerInner == null) _markerInner = _pool.Rent(null, OrderMarkerInner);
                int innerPx = Mathf.Max(1, Mathf.RoundToInt(_scene.MarkerRadius * _t.DepartMarkerInnerFraction * SimConst.PixelsPerUnit));
                _markerInner.sprite = _art.Circle(innerPx);
                WorldDraw.Place(_markerInner, View(_scene.MarkerPos));
                WorldDraw.SetAlpha(_markerInner, _t.DepartMarkerColor, _t.DepartMarkerAlpha);
            }
            else
            {
                ReturnSprite(ref _campfire);
                ReturnSprite(ref _fireRing);
                ReturnSprite(ref _marker);
                ReturnSprite(ref _markerInner);
            }
            RenderHoldRing();
        }

        SpriteRenderer Ring(SpriteRenderer sr, int order, Vector2 sim, float radiusCells, Color color, float alpha)
        {
            if (radiusCells <= 0f) { ReturnSprite(ref sr); return null; }
            if (sr == null) sr = _pool.Rent(null, order);
            int px = Mathf.Max(1, Mathf.RoundToInt(radiusCells * SimConst.PixelsPerUnit));
            sr.sprite = _art.Circle(px, true, Mathf.Max(1, _t.InteractRingWidthPx));
            WorldDraw.Place(sr, View(sim));
            WorldDraw.SetAlpha(sr, color, alpha);
            return sr;
        }

        void RenderHoldRing()
        {
            float ratio = _scene.DepartRatio;
            float cancelT = _t.DepartHoldCancelSeconds > 0f ? (_time - _holdCancelStart) / _t.DepartHoldCancelSeconds : 1f;
            bool cancelling = ratio < 0f && cancelT >= 0f && cancelT < 1f && _scene.InCamp;
            if (ratio < 0f && !cancelling)
            {
                ReturnMesh(ref _holdRing, ref _holdMesh);
                _lastHoldRatio = -1f;
                return;
            }
            if (_holdRing == null)
            {
                _holdRing = _pool.RentMesh(OrderHoldRing);
                _holdMesh = new Mesh { name = "depart_hold_ring" };
                _holdRing.GetComponent<MeshFilter>().sharedMesh = _holdMesh;
            }
            var c = _t.DepartHoldRingColor;
            float shown;
            if (cancelling)
            {
                // 취소: 차 있던 고리가 잠깐 사라진다(색 바꾸지 않음 — 위험색 아님).
                shown = _holdCancelRatio;
                c.a = _t.DepartHoldRingAlpha * (1f - cancelT);
            }
            else
            {
                shown = ratio;
                c.a = _t.DepartHoldRingAlpha;
                _lastHoldRatio = ratio;
            }
            ShapeMeshes.FillArc(_holdMesh, _t.DepartHoldRingRadiusCells, _t.DepartHoldRingWidthCells, shown, _t.DepartHoldRingSegments, c);
            var v = View(_scene.MarkerPos);
            _holdRing.transform.localPosition = new Vector3(v.x, v.y, 0f);
        }

        // ------------------------------------------------------------ 날씨

        void RenderWeather()
        {
            float W = _w.ArenaWidth, H = _w.ArenaHeight;
            // 빗줄기(입자 옵션): 불투명도 12% 이하, 가운데 20×10칸 위에는 그리지 않는다(10 3.9절).
            if (_scene.DrawRain && !string.IsNullOrEmpty(_t.RainId))
            {
                var anim = _art.World(_t.RainId);
                float cx = _t.CenterClearCells.x, cy = _t.CenterClearCells.y;
                float x0 = (W - cx) * 0.5f, y0 = (H - cy) * 0.5f;
                float a = Mathf.Min(_t.RainOpacity, _t.RainMaxOpacity);
                Band(_rain, 0, anim, OrderRain, 0f, 0f, W, y0, a);
                Band(_rain, 1, anim, OrderRain, 0f, y0 + cy, W, H - (y0 + cy), a);
                Band(_rain, 2, anim, OrderRain, 0f, y0, x0, cy, a);
                Band(_rain, 3, anim, OrderRain, x0 + cx, y0, W - (x0 + cx), cy, a);
            }
            else ReturnAll(_rain);

            // 안개(입자 옵션): 가장자리 edge_width칸.
            float e = _scene.FogEdgeCells;
            if (_scene.DrawFog && e > 0f && !string.IsNullOrEmpty(_t.FogId))
            {
                var anim = _art.World(_t.FogId);
                Band(_fog, 0, anim, OrderFog, 0f, 0f, W, e, _t.FogOpacity);
                Band(_fog, 1, anim, OrderFog, 0f, H - e, W, e, _t.FogOpacity);
                Band(_fog, 2, anim, OrderFog, 0f, e, e, H - 2f * e, _t.FogOpacity);
                Band(_fog, 3, anim, OrderFog, W - e, e, e, H - 2f * e, _t.FogOpacity);
            }
            else ReturnAll(_fog);

            // 물웅덩이 반사(바닥 흔적 — 입자 옵션과 무관하게 소나기 동안 남는다).
            if (_scene.DrawPuddles && !string.IsNullOrEmpty(_t.PuddleId))
            {
                var anim = _art.World(_t.PuddleId);
                if (_puddles.Count == 0)
                    for (int i = 0; i < _t.PuddleTiles.Count; i++)
                    {
                        var tile = _t.PuddleTiles[i];
                        var sr = _pool.Rent(null, OrderPuddles);
                        WorldDraw.Place(sr, View(new Vector2(tile.x + 0.5f, tile.y + 0.5f)));
                        _puddles.Add(sr);
                    }
                for (int i = 0; i < _puddles.Count; i++) _puddles[i].sprite = WorldDraw.Frame(anim, _time + i * 0.37f);
            }
            else ReturnList(_puddles);

            RenderFireflies(W, H);

            // 버린 물건(날이 바뀔 때 탑 창에서 떨어진 새 소품).
            int v = _scene.DiscardedItemVariant;
            if (v != _discardedVariant)
            {
                ReturnSprite(ref _discarded);
                _discardedVariant = v;
                if (v >= 0 && v < _t.DiscardedItemProps.Count && v < _t.DiscardedItemTiles.Count)
                {
                    string id = _t.DiscardedItemProps[v];
                    var tile = _t.DiscardedItemTiles[v];
                    int Wt = Mathf.RoundToInt(W), Ht = Mathf.RoundToInt(H);
                    if (!WorldViewRules.OverlapsCenter(tile.x, tile.y, PieceWidthTiles(id), 1, Wt, Ht, _t.CenterClearCells))
                    {
                        var piece = new MapPiece { Tile = tile, Sprite = id, Extent = new Vector2Int(1, 1) };
                        _discarded = RentPiece(piece, OrderProps);
                    }
                }
            }
        }

        SpriteRenderer RentPiece(in MapPiece p, int order) => RentPiece(p, order, 0);

        /// <summary>시뮬레이션 사각형(왼쪽 위 x, y, 폭, 높이)을 그림으로 타일처럼 채운다(그림 기준점 = 왼쪽 위).</summary>
        void Band(SpriteRenderer[] slots, int i, SpriteAnim anim, int order, float x, float y, float w, float h, float alpha)
        {
            if (w <= 0f || h <= 0f) { ReturnSprite(ref slots[i]); return; }
            if (slots[i] == null)
            {
                slots[i] = _pool.Rent(null, order);
                slots[i].drawMode = SpriteDrawMode.Tiled;
            }
            var sr = slots[i];
            sr.sprite = WorldDraw.Frame(anim, _time);
            sr.drawMode = SpriteDrawMode.Tiled;
            sr.size = new Vector2(w, h);
            WorldDraw.Place(sr, View(new Vector2(x, y)));
            WorldDraw.SetAlpha(sr, Color.white, alpha);
        }

        // ------------------------------------------------------------ 정리

        Vector3 View(Vector2 sim) => _vc != null ? _vc.ToView(sim) : SimConst.ToView(sim, _w != null ? _w.ArenaHeight : 14f);

        void ReturnSprite(ref SpriteRenderer sr)
        {
            if (sr != null && _pool != null) _pool.Return(sr);
            sr = null;
        }

        void ReturnMesh(ref MeshRenderer mr, ref Mesh mesh)
        {
            if (mr != null && _pool != null)
            {
                var mf = mr.GetComponent<MeshFilter>();
                if (mf != null) mf.sharedMesh = null;
                _pool.Return(mr);
            }
            ShapeMeshes.DestroyMesh(mesh);
            mr = null;
            mesh = null;
        }

        void ReturnList(List<SpriteRenderer> list)
        {
            if (_pool != null) for (int i = 0; i < list.Count; i++) if (list[i] != null) _pool.Return(list[i]);
            list.Clear();
        }

        /// <summary>
        /// 반딧불: 밤 시간대(야영 포함)에 가장자리 띠(firefly_edge_cells칸) 안에서만 천천히 흔들린다. 날씨 입자 옵션이 꺼지면 없다.
        /// 자리는 번호로 정해지는 고정 배치(화면용, 시뮬레이션 난수를 쓰지 않는다). 가운데 빈 칸(center_clear_cells) 위에는 오지 않는다.
        /// </summary>
        void RenderFireflies(float W, float H)
        {
            bool particles = _vc == null || _vc.Options == null || _vc.Options.WeatherParticles;
            if (_scene.TodId != Ids.Tod.Night || !particles || _t.FireflyCount <= 0 || string.IsNullOrEmpty(_t.FireflyKey))
            {
                ReturnList(_fireflies);
                return;
            }
            var anim = _art.Effect(_t.FireflyGroup, _t.FireflyKey);
            float e = Mathf.Max(0.5f, _t.FireflyEdgeCells);
            while (_fireflies.Count < _t.FireflyCount) _fireflies.Add(_pool.Rent(null, OrderRain));
            for (int i = 0; i < _fireflies.Count; i++)
            {
                // 둘레를 따라 고르게 놓고(둘레 위 거리 = (i + 0.5) / 수 × 둘레), 띠 안 깊이는 번호마다 다르게.
                float per = 2f * (W + H);
                float d = (i + 0.5f) / _fireflies.Count * per;
                float depth = e * (0.25f + 0.5f * Mathf.Repeat(i * 0.618f, 1f));
                Vector2 basePos = d < W ? new Vector2(d, depth)
                    : d < W + H ? new Vector2(W - depth, d - W)
                    : d < 2f * W + H ? new Vector2(2f * W + H - d, H - depth)
                    : new Vector2(depth, per - d);
                float ph = i * 1.7f;
                float k = 2f * Mathf.PI * _t.FireflyDriftHz;
                var drift = new Vector2(Mathf.Sin(_time * k + ph), Mathf.Cos(_time * k * 0.8f + ph * 0.5f)) * (_t.FireflyDriftCells * 0.5f);
                var pos = basePos + drift;
                pos.x = Mathf.Clamp(pos.x, 0f, W);
                pos.y = Mathf.Clamp(pos.y, 0f, H);
                var sr = _fireflies[i];
                sr.sprite = WorldDraw.Frame(anim, _time + i * 0.21f);
                sr.sortingOrder = OrderRain;
                WorldDraw.Place(sr, View(pos));
            }
        }

        void ReturnAll(SpriteRenderer[] arr)
        {
            for (int i = 0; i < arr.Length; i++) ReturnSprite(ref arr[i]);
        }

        void DisposeAll()
        {
            ReturnList(_tiles);
            ReturnList(_blendTiles);
            ReturnList(_walls);
            ReturnList(_rims);
            ReturnList(_spawns);
            ReturnList(_props);
            ReturnList(_puddles);
            ReturnList(_fireflies);
            ReturnAll(_rain);
            ReturnAll(_fog);
            ReturnSprite(ref _tower);
            ReturnSprite(ref _window);
            ReturnSprite(ref _moon);
            ReturnSprite(ref _moonlight);
            ReturnSprite(ref _campfire);
            ReturnSprite(ref _fireRing);
            ReturnSprite(ref _marker);
            ReturnSprite(ref _markerInner);
            ReturnSprite(ref _discarded);
            ReturnMesh(ref _holdRing, ref _holdMesh);
            ReturnMesh(ref _vignette, ref _vignetteMesh);
            ReturnMesh(ref _moonEdge, ref _moonEdgeMesh);
            _spawnShakeUntil.Clear();
            _spawnBase.Clear();
            _map = null;
            _mapId = null;
            _tilesTod = _blendTod = null;
            _discardedVariant = -2;
        }
    }
}
