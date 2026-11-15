using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Wizard.Solo.Art;
using Wizard.Solo.Meta;
using Wizard.Solo.Sim;
using Wizard.Solo.UI;

namespace Wizard.Solo.View.World
{
    /// <summary>
    /// 플레이어 한 명의 그림(09 2·3절). 합성 순서(09 2.3절): 몸 → 야생 물 → 꾸미기 → 지팡이, 위를 볼 때는 지팡이가 맨 아래
    /// (char.2는 위를 볼 때 지팡이를 split_y에서 둘로 나눠 아래쪽은 맨 아래, 위쪽은 맨 위, 둘 다 x + offset).
    /// 같은 띠(Bodies + YOrder) 안의 순서는 z로 가른다(카메라에 가까울수록 위).
    /// </summary>
    public sealed class PlayerFigure
    {
        public readonly int Slot;
        public readonly string CharId;

        readonly ViewContext _vc;
        readonly WorldTuning _t;
        readonly IArtLibrary _art;
        readonly ISpritePool _pool;
        readonly MaterialPropertyBlock _block = new MaterialPropertyBlock();
        readonly Color _elementColor;

        SpriteRenderer _body, _wand, _wandUpper, _shadow, _glow, _detached, _kilnLine, _cosmetic, _mutationRing;
        readonly List<SpriteRenderer> _ghosts = new List<SpriteRenderer>();
        readonly List<SpriteRenderer> _stains = new List<SpriteRenderer>();
        readonly List<string> _stainKeys = new List<string>();
        readonly List<SpriteRenderer> _particles = new List<SpriteRenderer>();
        readonly List<float> _particleX = new List<float>();
        readonly List<SpriteRenderer> _marks = new List<SpriteRenderer>();
        readonly List<SpriteRenderer> _debtRings = new List<SpriteRenderer>();
        readonly List<SpriteRenderer> _iceCrown = new List<SpriteRenderer>();
        readonly SpriteRenderer[] _frostTracks = new SpriteRenderer[3];
        readonly float[] _frostTrackTimes = { Never, Never, Never };
        readonly SpriteRenderer[] _iceRingFront = new SpriteRenderer[3];
        readonly SpriteRenderer[] _iceRingBack = new SpriteRenderer[3];
        static readonly string[] IceRingFrontKeys = { "wand-ring-26-front", "wand-ring-30-front", "wand-ring-36-front" };
        static readonly string[] IceRingBackKeys = { "wand-ring-26-back", "wand-ring-30-back", "wand-ring-36-back" };
        static readonly int[] IceRingDepthPx = { 10, 20, 31 };
        SpriteRenderer _pillarGrip;
        float _lastFrostTrack = Never;
        int _nextFrostTrack;
        readonly Dictionary<Sprite, Sprite[]> _splitCache = new Dictionary<Sprite, Sprite[]>();

        // 사건 시각(지역 시계, 초). 없으면 아주 이른 값.
        const float Never = -1e6f;
        float _fireTime = Never, _artTime = Never, _hitTime = Never, _downTime = Never, _riseTime = Never;
        bool _artSignature;

        // 스침 잔상용 판정 중심 기록(틱별 고리).
        const int HistorySize = 64;
        readonly Vector2[] _history = new Vector2[HistorySize];
        readonly int[] _historyTick = new int[HistorySize];

        /// <summary>시험용: 지금 고른 클립과 그 장.</summary>
        public ClipChoice CurrentClip { get; private set; }
        public int CurrentFrame { get; private set; }
        public SpriteRenderer Body => _body;
        public SpriteRenderer Wand => _wand;
        public SpriteRenderer Shadow => _shadow;
        public int GhostCount { get { int n = 0; foreach (var g in _ghosts) if (g.enabled) n++; return n; } }
        public int ParticleCount { get { int n = 0; foreach (var g in _particles) if (g.enabled) n++; return n; } }
        public int MarkCount { get { int n = 0; foreach (var g in _marks) if (g.enabled) n++; return n; } }
        public int StainCount => _stains.Count;
        public bool CosmeticShown => _cosmetic != null && _cosmetic.enabled;
        public float BodyAlpha => _body != null ? _body.color.a : 0f;
        public WandHeatLook HeatLook { get; private set; }

        public PlayerFigure(ViewContext vc, WorldTuning t, PlayerState p, SimWorld w)
        {
            _vc = vc;
            _t = t;
            Slot = p.Slot;
            CharId = p.CharId;
            _art = vc != null && vc.Game != null ? vc.Game.Art : null;
            _pool = vc != null ? vc.Pool : null;
            _elementColor = UiTheme.ElementColor(p.Def != null ? p.Def.Element : null);
            for (int i = 0; i < HistorySize; i++) _historyTick[i] = int.MinValue;
            // Build 때 이미 쓰러져 있으면(보조 판에서 돌아옴) 쓰러짐 클립의 마지막 장을 보인다.
            if (p.Downed) _downTime = Never;
            if (_art == null || _pool == null) return;
            _shadow = _pool.Rent(_art.Circle(16), SortBands.Shadows);
            _body = _pool.Rent(null, SortBands.Bodies);
            _wand = _pool.Rent(null, SortBands.Bodies);
            BuildOverlays();
        }

        // ------------------------------------------------------------ 사건

        public void OnEvent(SimWorld w, in SimEvent e, float now)
        {
            switch (e.Type)
            {
                case SimEventType.ShotFired: _fireTime = now; break;
                case SimEventType.ArtFired:
                    _artTime = now;
                    var p = w != null ? w.Players[Slot] : null;
                    _artSignature = p != null && p.Def != null && (e.Id == p.Def.UniqueArt || (e.Id != null && e.Id.StartsWith("final.", System.StringComparison.Ordinal)));
                    break;
                case SimEventType.PlayerHit: _hitTime = now; break;
                case SimEventType.PlayerDowned: _downTime = now; _riseTime = Never; break;
                case SimEventType.PlayerRose: _riseTime = now; break;
            }
        }

        // ------------------------------------------------------------ 그리기

        public void Render(SimWorld w, PlayerState p, float alpha, float now)
        {
            if (p == null) return;
            RecordHistory(w.Tick, p.Pos);
            if (_art == null || _pool == null || _body == null) return;

            // 1) 클립 고르기(상태 + 사건 창).
            var input = new BodyInput
            {
                Facing = p.Facing,
                Moving = p.Moving,
                InStance = p.InStance,
                Downed = p.Downed,
                Rising = !p.Downed && now - _riseTime < ClipSeconds(_t.ClipDeath, Facing3.Down),
                HitActive = now - _hitTime < ClipSeconds(_t.ClipHit, Facing3.Down),
                ArtActive = now - _artTime < ClipSeconds(_artSignature ? _t.ClipSignature : _t.ClipSpecial, Facing3.Down),
                ArtIsSignature = _artSignature,
                FireActive = now - _fireTime < ClipSeconds(_t.ClipFire, Facing3.Down)
            };
            var choice = WorldViewRules.SelectClip(input, _t);
            CurrentClip = choice;
            var anim = _art.CharacterClip(CharId, choice.Clip, choice.Facing);
            var info = _art.CharacterClipInfo(CharId, choice.Clip, choice.Facing);
            float started = choice.Play == ClipPlay.Loop ? 0f
                : choice.Play == ClipPlay.Reverse ? _riseTime
                : input.Downed ? _downTime
                : input.HitActive ? _hitTime
                : input.ArtActive ? _artTime
                : _fireTime;
            int frames = anim != null && anim.Frames != null ? anim.Frames.Length : 0;
            int idx = WorldViewRules.FrameIndex(choice.Play, frames, anim != null ? anim.Fps : 0f, anim != null && anim.Loop, now - started);
            CurrentFrame = idx;
            PlayerFrame frame = info != null && idx < info.Frames.Count ? info.Frames[idx] : null;

            // 2) 위치: 판정 중심에서 0.4칸 아래가 발(02 1절), 보간은 ViewMath.Lerp.
            var center = WorldViewRules.InterpolatedCenter(p, alpha);
            var feetSim = WorldViewRules.FeetSim(center, _t.FeetBelowCenterCells);
            var feet = _vc.ToView(feetSim);
            // 플레이어는 적 몸 띠 위(적 사이에 묻혀도 보이게 — View/Effects/FxLayers.PlayerBody)
            int order = Wizard.Solo.View.Effects.FxLayers.PlayerBody;
            float z = _t.LayerZStep;

            _body.sprite = WorldDraw.FrameAt(anim, idx);
            if (CharId == Ids.Char.C4 && !choice.HideWand && frame != null)
            {
                var body = _art.Effect(ArtLibrary.LayersGroup, "reactive/char.4/body");
                int cell = frame.Rect.Y / 64 * 12 + frame.Rect.X / 64;
                if (body != null && body.Frames.Length == 147)
                    _body.sprite = WorldDraw.FrameAt(body, cell);
            }
            _body.flipX = choice.FlipX;
            _body.sortingOrder = order;
            WorldDraw.Place(_body, feet, 0f);

            // 그림자(발밑 타원).
            WorldDraw.Place(_shadow, feet);
            float circleD = 32f / SimConst.PixelsPerUnit;
            _shadow.transform.localScale = new Vector3(_t.ShadowSizeCells.x / circleD, _t.ShadowSizeCells.y / circleD, 1f);
            WorldDraw.SetAlpha(_shadow, _t.ShadowColor, _t.ShadowAlpha);

            // 3) 무적 깜빡임·피격 번쩍·림(시간대 세기).
            int effects = _vc.Options != null ? _vc.Options.EffectsLevel : 0;
            float bodyAlpha = 1f, invulnOutline = 0f;
            if (p.Invulnerable(w.Tick) && !p.Downed)
                bodyAlpha = WorldViewRules.InvulnAlpha(effects, (w.Tick + alpha) / SimConst.TicksPerSecond, _t, out invulnOutline);
            float flash = WorldViewRules.HitFlashAmount(now - _hitTime, _t);
            float rimStrength = RimStrength(w);
            Color outline = invulnOutline > 0f ? Color.white : new Color(_t.RimColor.r, _t.RimColor.g, _t.RimColor.b, rimStrength);
            float outlineSize = invulnOutline > 0f ? invulnOutline : (rimStrength > 0f ? _t.RimPx : 0f);
            _body.color = new Color(1f, 1f, 1f, bodyAlpha);
            WorldDraw.SetShaderProps(_body, _block, _t.HitFlashColor, flash, outline, outlineSize);

            // 4) 덧그림(야생 물, 금 간 거울 조각).
            RenderOverlays(frame, feet, choice, order, bodyAlpha, flash, outline, outlineSize, now);

            // 5) 지팡이.
            RenderWand(w, p, frame, feet, choice, order, bodyAlpha, flash, outline, outlineSize, now);
            if (CharId == Ids.Char.C4)
                RenderIceDetails(p, frame, feet, choice, order, bodyAlpha, flash, outline, outlineSize, now);

            // 6) 스침 가속 잔상 3장(속성색 30%, 10 3.5절).
            RenderGhosts(w, p, choice, order);
        }

        float ClipSeconds(string clip, Facing3 f)
        {
            if (_art == null || clip == null) return 0f;
            var a = _art.CharacterClip(CharId, clip, f);
            return a != null ? a.Duration : 0f;
        }

        void RenderIceDetails(PlayerState player, PlayerFrame frame, Vector3 feet, ClipChoice choice, int order,
            float alpha, float flash, Color outline, float outlineSize, float now)
        {
            bool show = !choice.HideWand && frame != null;
            var crown = _art.Effect(ArtLibrary.LayersGroup, "reactive/char.4/crown_shard");
            var info = _art.LayerInfo("reactive/char.4/crown_shard");
            var offsets = info?.Raw?["offsets"] as JArray;
            if (show && crown.Frames.Length == 15 && offsets?.Count == 5)
            {
                var heat = WorldViewRules.HeatLook(player);
                int size = heat == WandHeatLook.Locked ? 0 : heat == WandHeatLook.Critical ? 1 : 2;
                while (_iceCrown.Count < 5) _iceCrown.Add(_pool.Rent(null, order));
                for (int i = 0; i < _iceCrown.Count; i++)
                {
                    var shard = _iceCrown[i];
                    shard.sprite = crown.Frames[size * 5 + i];
                    int bob = Mathf.RoundToInt(Mathf.Sin(now * 4f + i * 1.26f));
                    var offset = ManifestParser.Point(offsets[i]);
                    var anchor = new PixelPoint(frame.Head.X + offset.X, frame.Head.Y + offset.Y + bob);
                    PlaceIceDetail(shard, frame, anchor, feet, choice, order, -0.5f * _t.LayerZStep,
                        alpha, flash, outline, outlineSize);
                }
            }
            else foreach (var shard in _iceCrown) shard.enabled = false;

            var track = _art.Effect(ArtLibrary.LayersGroup, "reactive/char.4/frost_footprint");
            const float lifetime = 0.5f;
            if (show && player.Moving && now - _lastFrostTrack >= lifetime / _frostTracks.Length && track?.Frames.Length == 3)
            {
                int i = _nextFrostTrack;
                if (_frostTracks[i] == null) _frostTracks[i] = _pool.Rent(null, SortBands.Shadows);
                WorldDraw.Place(_frostTracks[i], feet);
                _frostTrackTimes[i] = _lastFrostTrack = now;
                _nextFrostTrack = (i + 1) % _frostTracks.Length;
            }
            for (int i = 0; i < _frostTracks.Length; i++)
            {
                var sr = _frostTracks[i];
                if (sr == null) continue;
                float age = now - _frostTrackTimes[i];
                sr.enabled = age >= 0f && age < lifetime && track?.Frames.Length == 3;
                if (sr.enabled) sr.sprite = track.Frames[Mathf.Min(2, (int)(age * 3f / lifetime))];
            }

            bool planted = show && WandSheet.ClampStage(player.Wand.EffectiveStage) == WandSheet.MaxStage
                && (choice.Clip == _t.ClipIdle || choice.Clip == _t.ClipWalk || choice.Clip == _t.ClipStance);
            if (planted)
            {
                var grip = _art.Effect(ArtLibrary.LayersGroup, "equipment/char.4/pillar-grip:" + ArtAliases.FacingSuffix(choice.Facing));
                if (_pillarGrip == null) _pillarGrip = _pool.Rent(null, order);
                _pillarGrip.sprite = WorldDraw.FrameAt(grip, 0);
                PlaceIceDetail(_pillarGrip, frame, frame.Hand, feet, choice, order, -3.5f * _t.LayerZStep,
                    alpha, flash, outline, outlineSize);
            }
            else if (_pillarGrip != null) _pillarGrip.enabled = false;
        }

        void PlaceIceDetail(SpriteRenderer renderer, PlayerFrame frame, PixelPoint anchor, Vector3 feet, ClipChoice choice,
            int order, float depth, float alpha, float flash, Color outline, float outlineSize)
        {
            var offset = WorldViewRules.PixelDelta(frame.Feet, anchor, choice.FlipX);
            WorldDraw.Place(renderer, feet + new Vector3(offset.x, offset.y, 0f), depth);
            renderer.enabled = true;
            renderer.flipX = choice.FlipX;
            renderer.sortingOrder = order;
            renderer.color = new Color(1f, 1f, 1f, alpha);
            WorldDraw.SetShaderProps(renderer, _block, _t.HitFlashColor, flash, outline, outlineSize);
        }

        float RimStrength(SimWorld w)
        {
            var todId = WorldViewRules.TargetTod(w);
            var tod = w.Content != null ? w.Content.Tod(todId) : null;
            return tod != null ? tod.RimStrengthPercent / 100f : 0f;
        }

        void RecordHistory(int tick, Vector2 pos)
        {
            int i = ((tick % HistorySize) + HistorySize) % HistorySize;
            _history[i] = pos;
            _historyTick[i] = tick;
        }

        bool TryHistory(int tick, out Vector2 pos)
        {
            int i = ((tick % HistorySize) + HistorySize) % HistorySize;
            pos = _history[i];
            return _historyTick[i] == tick;
        }

        // ------------------------------------------------------------ 덧그림

        void BuildOverlays()
        {
            var profile = _vc.Game != null ? _vc.Game.Profile : null;
            if (profile == null) return;
            var rec = profile.Character(Ids.Char.NumberOf(CharId));
            if (rec != null)
            {
                int worn = Mathf.Min(rec.WildStainWorn, Mathf.Max(1, rec.WildStainStageMax));
                var stages = WorldViewRules.WildStainStages(worn, _t);
                if (_t.WildStainParts.TryGetValue(CharId, out var byStage))
                    for (int s = 0; s < stages.Count; s++)
                        if (byStage.TryGetValue(stages[s], out var parts))
                            for (int k = 0; k < parts.Count; k++)
                            {
                                string key = WorldViewRules.WildStainKey(CharId, stages[s], parts[k]);
                                if (_art.LayerInfo(key) == null) continue;
                                _stainKeys.Add(key);
                                _stains.Add(_pool.Rent(null, SortBands.Bodies));
                            }
            }
            if (!string.IsNullOrEmpty(_t.CrackedMirrorLayer) && profile.Cosmetics != null && profile.Cosmetics.Equipped != null
                && profile.Cosmetics.Equipped.ContainsValue(Ids.Cosmetic.CrackedMirrorShard) && _art.LayerInfo(_t.CrackedMirrorLayer) != null)
                _cosmetic = _pool.Rent(null, SortBands.Bodies);
        }

        void RenderOverlays(PlayerFrame frame, Vector3 feet, ClipChoice choice, int order, float alpha, float flash,
            Color outline, float outlineSize, float now)
        {
            bool hide = choice.HideWand; // 쓰러짐(빈 몸) 동안은 덧그림도 숨긴다.
            for (int i = 0; i < _stains.Count; i++)
                PlaceLayer(_stains[i], _stainKeys[i], frame, feet, choice, order, -(1 + i * 0.1f) * _t.LayerZStep, alpha, flash, outline, outlineSize, now, hide);
            if (_cosmetic != null)
                PlaceLayer(_cosmetic, _t.CrackedMirrorLayer, frame, feet, choice, order, -2f * _t.LayerZStep, alpha, flash, outline, outlineSize, now, hide);
        }

        void PlaceLayer(SpriteRenderer sr, string key, PlayerFrame frame, Vector3 feet, ClipChoice choice, int order, float z,
            float alpha, float flash, Color outline, float outlineSize, float now, bool hide)
        {
            var e = _art.LayerInfo(key);
            if (hide || frame == null || e == null) { sr.enabled = false; return; }
            string dir = ArtAliases.FacingSuffix(choice.Facing);
            var raw = e.Raw;
            string attach = raw != null && raw["attachment"] != null ? (string)raw["attachment"] : "head";
            PixelPoint anchor = attach == "feet" ? frame.Feet : attach == "shoulder" ? frame.Shoulder : frame.Head;
            var off = raw != null ? raw["offsetByDirection"] as JObject : null;
            var arr = off != null ? off[dir] as JArray : null;
            if (arr != null && arr.Count >= 2) anchor = new PixelPoint(anchor.X + (int)arr[0], anchor.Y + (int)arr[1]);
            var anim = _art.Effect(ArtLibrary.LayersGroup, key + ":" + dir);
            sr.enabled = true;
            sr.sprite = WorldDraw.Frame(anim, now);
            sr.flipX = choice.FlipX;
            sr.sortingOrder = order;
            var d = WorldViewRules.PixelDelta(frame.Feet, anchor, choice.FlipX);
            WorldDraw.Place(sr, feet + new Vector3(d.x, d.y, 0f), z);
            sr.color = new Color(1f, 1f, 1f, alpha);
            WorldDraw.SetShaderProps(sr, _block, _t.HitFlashColor, flash, outline, outlineSize);
        }

        // ------------------------------------------------------------ 지팡이

        void RenderWand(SimWorld w, PlayerState p, PlayerFrame frame, Vector3 feet, ClipChoice choice, int order,
            float alpha, float flash, Color outline, float outlineSize, float now)
        {
            int stage = WandSheet.ClampStage(p.Wand.EffectiveStage);
            bool hide = choice.HideWand || frame == null;
            HeatLook = WorldViewRules.HeatLook(p);
            if (hide)
            {
                _wand.enabled = false;
                if (_wandUpper != null) _wandUpper.enabled = false;
                HideWandExtras();
                return;
            }
            var sprite = _art.WandHand(CharId, stage, choice.Facing, choice.WandAim);
            var hand = WorldViewRules.PixelDelta(frame.Feet, frame.Hand, choice.FlipX);
            var wandPos = feet + new Vector3(hand.x, hand.y, 0f);
            bool up = choice.Facing == Facing3.Up;
            float wandZ = up ? _t.LayerZStep : -3f * _t.LayerZStep;

            // 열 반응: 임계 = 흰빛(빨강·금색 금지), 잠금 = 무쇠색으로 꺼졌다가 0.5초마다 한 칸씩 다시 밝아짐.
            Color tint = _t.WandCritGlowColor;
            float tintAmount = 0f;
            if (HeatLook == WandHeatLook.Critical) tintAmount = _t.WandCritTintAmount;
            else if (HeatLook == WandHeatLook.Locked)
            {
                tint = _t.WandLockColor;
                tintAmount = _t.WandLockTintAmount * (1f - WorldViewRules.LockBrightness(w.Tick, p.LockStartTick, p.UnlockTick, _t.WandLockRebrightenSeconds));
            }
            if (flash > 0f) { tint = _t.HitFlashColor; tintAmount = Mathf.Max(tintAmount, flash); }

            Vector2Int split;
            bool splitUp = up && _t.WandUpSplit.TryGetValue(CharId, out split) && sprite != null;
            if (splitUp)
            {
                _t.WandUpSplit.TryGetValue(CharId, out split);
                var parts = Split(sprite, split.x);
                var dx = new Vector3((choice.FlipX ? -split.y : split.y) / (float)SimConst.PixelsPerUnit, 0f, 0f);
                _wand.sprite = parts[0]; // 아래쪽(맨 아래)
                if (_wandUpper == null) _wandUpper = _pool.Rent(null, order);
                _wandUpper.enabled = true;
                _wandUpper.sprite = parts[1]; // 위쪽(맨 위)
                _wandUpper.flipX = choice.FlipX;
                _wandUpper.sortingOrder = order;
                WorldDraw.Place(_wandUpper, wandPos + dx, -4f * _t.LayerZStep);
                _wandUpper.color = new Color(1f, 1f, 1f, alpha);
                WorldDraw.SetShaderProps(_wandUpper, _block, tint, tintAmount, outline, outlineSize);
                wandPos += dx;
            }
            else
            {
                _wand.sprite = sprite;
                if (_wandUpper != null) _wandUpper.enabled = false;
            }
            _wand.enabled = true;
            _wand.flipX = choice.FlipX;
            _wand.sortingOrder = order;
            WorldDraw.Place(_wand, wandPos, wandZ);
            _wand.color = new Color(1f, 1f, 1f, alpha);
            WorldDraw.SetShaderProps(_wand, _block, tint, tintAmount, outline, outlineSize);

            var stageInfo = _art.WandStageInfo(CharId, stage);
            var grip = _art.WandGrip;
            var headOff = stageInfo != null ? WorldViewRules.WandPointOffset(stageInfo.Head, grip, choice.FlipX) : Vector2.zero;
            var head = wandPos + new Vector3(headOff.x, headOff.y, 0f);
            float fxZ = wandZ - 0.5f * _t.LayerZStep;
            var opts = _vc.Options;
            string level = opts != null ? opts.WandFxLevel : OptionValues.All;

            // 임계 빛(지팡이 효과 투명도를 곱한다).
            if (HeatLook == WandHeatLook.Critical && _t.WandCritGlowRadiusPx > 0)
            {
                if (_glow == null) _glow = _pool.Rent(null, order);
                _glow.enabled = true;
                _glow.sprite = _art.Circle(_t.WandCritGlowRadiusPx);
                _glow.sortingOrder = order;
                WorldDraw.Place(_glow, head, fxZ);
                WorldDraw.SetAlpha(_glow, _t.WandCritGlowColor, WorldViewRules.WandFxAlpha(opts, _t.WandCritGlowAlpha) * alpha);
            }
            else if (_glow != null) _glow.enabled = false;

            // 대기 입자(김·불티 등): 열 상태별 요청 수를 지팡이 효과 단계로 줄인다.
            int want = WorldViewRules.WandParticleBudget(level, WorldViewRules.IdleParticleRequest(HeatLook, _t));
            RenderParticles(want, head, order, fxZ, alpha, now);

            // 불가마 지대 안: 지팡이 밑동 주황 빛 한 줄(wands/dalgu 4.4절).
            if (p.InKilnZone && _t.WandKilnLinePx.y > 0)
            {
                if (_kilnLine == null) _kilnLine = _pool.Rent(_art.WhitePixel, order);
                _kilnLine.enabled = true;
                _kilnLine.sortingOrder = order;
                float lw = _t.WandKilnLinePx.x / (float)SimConst.PixelsPerUnit, lh = _t.WandKilnLinePx.y / (float)SimConst.PixelsPerUnit;
                WorldDraw.Rect(_kilnLine, wandPos + new Vector3(0f, lh * 0.5f, 0f), lw, lh,
                    new Color(_t.WandKilnLineColor.r, _t.WandKilnLineColor.g, _t.WandKilnLineColor.b, WorldViewRules.WandFxAlpha(opts, 1f) * alpha), fxZ);
            }
            else if (_kilnLine != null) _kilnLine.enabled = false;

            // 분리 조각(단계에 따라). 성에는 열이 오르면 작아진다.
            if (WorldViewRules.DetachedVisible(CharId, stage, _t))
            {
                var part = _art.WandDetachedPart(CharId);
                if (part != null)
                {
                    if (_detached == null) _detached = _pool.Rent(null, order);
                    _detached.enabled = true;
                    _detached.sprite = WorldDraw.Frame(part, now);
                    _detached.flipX = choice.FlipX;
                    _detached.sortingOrder = order;
                    float scale = 1f;
                    if (_t.WandDetachedHeatShrink.TryGetValue(CharId, out var shrink))
                        scale = Mathf.Lerp(1f, shrink, Mathf.Clamp01(p.HeatHu / (100f * SimConst.HuPerHeat)));
                    _detached.transform.localScale = new Vector3(scale, scale, 1f);
                    WorldDraw.Place(_detached, head, fxZ);
                    _detached.color = new Color(1f, 1f, 1f, alpha);
                    WorldDraw.SetShaderProps(_detached, _block, tint, tintAmount, outline, outlineSize);
                }
            }
            else if (_detached != null) _detached.enabled = false;

            if (CharId == Ids.Char.C4)
                RenderIceRings(stage, head, order, wandZ, alpha, now);

            // 빚 고리(속 빈 연보라, 최대 2 — 02 8.3절).
            int debt = Mathf.Clamp(p.Debt, 0, 2);
            while (_debtRings.Count < debt) _debtRings.Add(_pool.Rent(null, order));
            for (int i = 0; i < _debtRings.Count; i++)
            {
                var r = _debtRings[i];
                r.enabled = i < debt;
                if (!r.enabled) continue;
                r.sprite = _art.Circle(Mathf.Max(1, _t.WandDebtRingRadiusPx), true, 1);
                r.sortingOrder = order;
                float step = (_t.WandDebtRingRadiusPx * 2 + 1) / (float)SimConst.PixelsPerUnit;
                WorldDraw.Place(r, head + new Vector3((i - (debt - 1) * 0.5f) * step, step, 0f), fxZ);
                WorldDraw.SetAlpha(r, _t.WandDebtRingColor, alpha);
            }

            // 표식: 변이(연두 점), 확장, 흔적 또는 유리 지팡이 흰 금.
            RenderMarks(p, stageInfo, grip, wandPos, choice.FlipX, order, wandZ - 0.25f * _t.LayerZStep, alpha);
            RenderMutationRing(p, stageInfo, grip, wandPos, choice.FlipX, order, wandZ - 0.3f * _t.LayerZStep, alpha, now);
        }

        Sprite[] Split(Sprite s, int splitY)
        {
            if (_splitCache.TryGetValue(s, out var parts)) return parts;
            var r = s.rect;
            int h = Mathf.RoundToInt(r.height), wpx = Mathf.RoundToInt(r.width);
            var grip = _art.WandGrip;
            int topH = Mathf.Clamp(splitY, 1, h - 1), lowH = h - topH;
            // 아래쪽 = 위에서 split_y 줄 아래(텍스처 좌표로는 아래쪽 lowH 줄), 위쪽 = 위 split_y 줄.
            var low = Sprite.Create(s.texture, new Rect(r.x, r.y, wpx, lowH),
                new Vector2(grip.X / (float)wpx, (lowH - (grip.Y - topH)) / (float)lowH), SimConst.PixelsPerUnit, 0, SpriteMeshType.FullRect);
            var top = Sprite.Create(s.texture, new Rect(r.x, r.y + lowH, wpx, topH),
                new Vector2(grip.X / (float)wpx, (topH - grip.Y) / (float)topH), SimConst.PixelsPerUnit, 0, SpriteMeshType.FullRect);
            parts = new[] { low, top };
            _splitCache[s] = parts;
            return parts;
        }

        void RenderParticles(int count, Vector3 head, int order, float z, float alpha, float now)
        {
            while (_particles.Count < count)
            {
                _particles.Add(_pool.Rent(_art.Circle(Mathf.Max(1, _t.WandParticleRadiusPx)), order));
                _particleX.Add(Random.Range(-1f, 1f));
            }
            Color baseColor = _t.WandParticleColors.TryGetValue(CharId, out var c) ? c : Color.white;
            float life = _t.WandParticleLifetimeSeconds > 0f ? _t.WandParticleLifetimeSeconds : 1f;
            for (int i = 0; i < _particles.Count; i++)
            {
                var sr = _particles[i];
                sr.enabled = i < count;
                if (!sr.enabled) continue;
                float phase = (now / life + i / (float)Mathf.Max(1, count)) % 1f;
                if (phase < 0.05f) _particleX[i] = Random.Range(-1f, 1f); // 새로 피어오를 때 옆 자리를 다시 고른다(표시용 난수).
                float px = 3f / SimConst.PixelsPerUnit;
                var pos = head + new Vector3(_particleX[i] * px, phase * _t.WandParticleRiseCells, 0f);
                sr.sortingOrder = order;
                WorldDraw.Place(sr, pos, z);
                WorldDraw.SetAlpha(sr, baseColor, WorldViewRules.WandFxAlpha(_vc.Options, 1f - phase) * alpha);
            }
        }

        void RenderMarks(PlayerState p, WandStage info, PixelPoint grip, Vector3 wandPos, bool flip, int order, float z, float alpha)
        {
            int used = 0;
            if (info != null)
            {
                int muts = Mathf.Min(p.Wand.Mutations.Count, info.Mutations.Count);
                for (int i = 0; i < muts; i++)
                    Mark(ref used, wandPos, WorldViewRules.WandPointOffset(info.Mutations[i], grip, flip), _t.MarkMutationPx, _t.MarkMutationPx, _t.MarkMutationColor, order, z, alpha);

                int baseSlots = p.Def != null ? p.Def.BaseLinkSlots : p.Wand.LinkSlotCount;
                int exp = WorldViewRules.ExpansionCount(p.Wand.LinkSlotCount, baseSlots);
                Color ec = _t.MarkExpansionColors.TryGetValue(CharId, out var c) ? c : Color.white;
                for (int i = 0; i < exp; i++)
                {
                    var pt = new PixelPoint(info.Expansion.X, info.Expansion.Y + i * _t.MarkExpansionStepPx);
                    Mark(ref used, wandPos, WorldViewRules.WandPointOffset(pt, grip, flip), _t.MarkExpansionPx, _t.MarkExpansionPx, ec, order, z, alpha);
                }

                int cracks = WorldViewRules.CrackCount(p.Wand.TempStageDrop, _t.MarkCrackMax);
                if (cracks > 0)
                {
                    // 유리 지팡이 임시 하락: 흔적 대신 흰 금(흔적 공개와 무관하게 항상, 07 10절).
                    for (int i = 0; i < cracks && i < _t.MarkCrackOffsetsPx.Count; i++)
                    {
                        var o = _t.MarkCrackOffsetsPx[i];
                        var pt = new PixelPoint(info.Head.X + o.x, info.Head.Y + o.y);
                        Mark(ref used, wandPos, WorldViewRules.WandPointOffset(pt, grip, flip), _t.MarkCrackSizePx.x, _t.MarkCrackSizePx.y, _t.MarkCrackColor, order, z, alpha);
                    }
                }
                else
                {
                    // 하락 흔적(내 화면에는 늘 보인다, 07 4.9절): 2×2 점 최대 2개.
                    var profile = _vc.Game != null ? _vc.Game.Profile : null;
                    var wd = profile != null ? profile.Wand(p.Wand.WandId) : null;
                    int scars = wd != null ? WorldViewRules.ScarCount(wd.Stage, wd.RecordPeak, _t.MarkScarMax) : 0;
                    for (int i = 0; i < scars && i < _t.MarkScarOffsetsPx.Count; i++)
                    {
                        var o = _t.MarkScarOffsetsPx[i];
                        var pt = new PixelPoint(info.Head.X + o.x, info.Head.Y + o.y);
                        Mark(ref used, wandPos, WorldViewRules.WandPointOffset(pt, grip, flip), _t.MarkScarPx, _t.MarkScarPx, _t.MarkScarColor, order, z, alpha);
                    }
                }
            }
            for (int i = used; i < _marks.Count; i++) _marks[i].enabled = false;
        }

        /// <summary>변이가 있으면 첫 변이 점에 덩굴 고리(wand/mutation_ring)를 감는다(FX 20.1 "지팡이 그림에 변이 표식 고리가 생긴다").</summary>
        void RenderMutationRing(PlayerState p, WandStage info, PixelPoint grip, Vector3 wandPos, bool flip, int order, float z, float alpha, float now)
        {
            bool show = info != null && info.Mutations.Count > 0 && p.Wand.Mutations.Count > 0 && !string.IsNullOrEmpty(_t.MarkMutationRingKey);
            if (!show)
            {
                if (_mutationRing != null) _mutationRing.enabled = false;
                return;
            }
            var anim = _art.Effect(_t.MarkMutationRingGroup, _t.MarkMutationRingKey);
            if (_mutationRing == null) _mutationRing = _pool.Rent(null, order);
            var off = WorldViewRules.WandPointOffset(info.Mutations[0], grip, flip);
            _mutationRing.enabled = true;
            _mutationRing.sprite = WorldDraw.Frame(anim, now);
            _mutationRing.flipX = flip;
            _mutationRing.sortingOrder = order;
            WorldDraw.Place(_mutationRing, wandPos + new Vector3(off.x, off.y, 0f), z);
            _mutationRing.color = new Color(1f, 1f, 1f, alpha);
        }

        void Mark(ref int used, Vector3 wandPos, Vector2 offset, int wPx, int hPx, Color color, int order, float z, float alpha)
        {
            if (used >= _marks.Count) _marks.Add(_pool.Rent(_art.WhitePixel, order));
            var sr = _marks[used++];
            sr.enabled = true;
            sr.sprite = _art.WhitePixel;
            sr.sortingOrder = order;
            float ppu = SimConst.PixelsPerUnit;
            WorldDraw.Rect(sr, wandPos + new Vector3(offset.x, offset.y, 0f), wPx / ppu, hPx / ppu,
                new Color(color.r, color.g, color.b, color.a * alpha), z);
        }

        void HideWandExtras()
        {
            if (_glow != null) _glow.enabled = false;
            if (_kilnLine != null) _kilnLine.enabled = false;
            if (_mutationRing != null) _mutationRing.enabled = false;
            if (_detached != null) _detached.enabled = false;
            foreach (var r in _particles) r.enabled = false;
            foreach (var r in _debtRings) r.enabled = false;
            foreach (var r in _marks) r.enabled = false;
            if (CharId == Ids.Char.C4) RenderIceRings(0, Vector3.zero, 0, 0f, 0f, 0f);
        }

        void RenderIceRings(int stage, Vector3 head, int order, float depth, float alpha, float now)
        {
            int visible = Mathf.Clamp(stage - 9, 0, stage == 12 ? 2 : 3);
            for (int i = 0; i < _iceRingFront.Length; i++)
            {
                if (i >= visible)
                {
                    if (_iceRingFront[i] != null) _iceRingFront[i].enabled = false;
                    if (_iceRingBack[i] != null) _iceRingBack[i].enabled = false;
                    continue;
                }
                var center = head - Vector3.up * (IceRingDepthPx[i] / (float)SimConst.PixelsPerUnit);
                float phase = i % 2 == 0 ? now : -now;
                RenderIceRingPart(ref _iceRingBack[i], IceRingBackKeys[i], center, order,
                    depth + 0.3f * _t.LayerZStep, alpha, phase, stage >= 12);
                RenderIceRingPart(ref _iceRingFront[i], IceRingFrontKeys[i], center, order,
                    depth - 0.3f * _t.LayerZStep, alpha, phase, stage >= 12);
            }
        }

        void RenderIceRingPart(ref SpriteRenderer renderer, string key, Vector3 center, int order, float depth,
            float alpha, float phase, bool aurora)
        {
            var animation = _art.Effect("wand", key);
            if (renderer == null) renderer = _pool.Rent(null, order);
            renderer.sprite = WorldDraw.Frame(animation, Mathf.Repeat(phase, animation.Duration));
            renderer.sortingOrder = order;
            renderer.enabled = true;
            WorldDraw.Place(renderer, center, depth);
            WorldDraw.SetAlpha(renderer, aurora ? Color.white : new Color(0.87f, 0.97f, 1f), alpha);
        }

        // ------------------------------------------------------------ 잔상

        void RenderGhosts(SimWorld w, PlayerState p, ClipChoice choice, int order)
        {
            bool on = w.Tick < p.GrazeBuffUntilTick && !p.Downed && _t.AfterimageCount > 0;
            int n = on ? _t.AfterimageCount : 0;
            while (_ghosts.Count < n) _ghosts.Add(_pool.Rent(null, order));
            for (int i = 0; i < _ghosts.Count; i++)
            {
                var g = _ghosts[i];
                Vector2 pos = Vector2.zero;
                if (i >= n || !TryHistory(w.Tick - (i + 1) * Mathf.Max(1, _t.AfterimageGapTicks), out pos)) { g.enabled = false; continue; }
                g.enabled = true;
                g.sprite = _body.sprite;
                g.flipX = choice.FlipX;
                var feetSim = WorldViewRules.FeetSim(pos, _t.FeetBelowCenterCells);
                g.sortingOrder = order;
                WorldDraw.Place(g, _vc.ToView(feetSim), (1 + i) * _t.LayerZStep);
                g.color = new Color(1f, 1f, 1f, _t.AfterimageAlpha);
                // 속성 보조색으로 덮는다(단색 덮기 1).
                WorldDraw.SetShaderProps(g, _block, _elementColor, 1f, Color.clear, 0f);
            }
        }

        // ------------------------------------------------------------ 정리

        public void Dispose()
        {
            if (_pool != null)
            {
                Return(ref _body); Return(ref _wand); Return(ref _wandUpper); Return(ref _shadow); Return(ref _glow);
                Return(ref _detached); Return(ref _kilnLine); Return(ref _cosmetic); Return(ref _mutationRing);
                Return(ref _pillarGrip);
                ReturnList(_iceCrown);
                for (int i = 0; i < _frostTracks.Length; i++) Return(ref _frostTracks[i]);
                for (int i = 0; i < _iceRingFront.Length; i++)
                {
                    Return(ref _iceRingFront[i]);
                    Return(ref _iceRingBack[i]);
                }
                ReturnList(_ghosts); ReturnList(_stains); ReturnList(_particles); ReturnList(_marks); ReturnList(_debtRings);
            }
            _stainKeys.Clear();
            _particleX.Clear();
            foreach (var kv in _splitCache)
                foreach (var s in kv.Value)
                {
                    if (s == null) continue;
                    if (Application.isPlaying) Object.Destroy(s); else Object.DestroyImmediate(s);
                }
            _splitCache.Clear();
        }

        void Return(ref SpriteRenderer sr)
        {
            if (sr != null) _pool.Return(sr);
            sr = null;
        }

        void ReturnList(List<SpriteRenderer> list)
        {
            for (int i = 0; i < list.Count; i++) if (list[i] != null) _pool.Return(list[i]);
            list.Clear();
        }
    }
}
