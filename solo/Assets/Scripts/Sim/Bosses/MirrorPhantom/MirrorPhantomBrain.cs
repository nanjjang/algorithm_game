using System;
using System.Collections.Generic;
using UnityEngine;
using Wizard.Solo.Data;

namespace Wizard.Solo.Sim.Bosses
{
    /// <summary>
    /// 스승의 허상(boss.mirror_phantom, 05 요약서 9.2절).
    ///  - copy_fire(상시): Waiting에서만, 플레이어 지팡이의 컴파일 결과를 phantom_copy 변환으로 적 탄(Threats.SpawnShot)으로 쏜다.
    ///  - summon_slimes(상시): fightTicks가 30초의 배수가 될 때마다 이끼 슬라임 6(졸개). 첫 만남엔 없다.
    ///  - mimic_art(2단계~): 해금한 ③④⑧ 중 하나(없으면 ①, 첫 만남은 셋 다)를 0.8초 예고 뒤 따라 쓴다.
    ///  - bloom_mimic(3단계): 1.2초 예고 → 팔방 전개 0·30·60틱 → 빈틈 2.0초(예고·반복·빈틈은 뼈대가 데이터대로).
    /// 첫 만남의 시계(되비침·쓰러짐)와 종료 조건은 뼈대(BossSystem.Phases)가 맡는다.
    /// 복사 탄은 Threats의 적 탄이라 "맞았다"를 직접 알 수 없다 → 쏜 탄을 지켜보다 사라진 자리에서 뒷일을 한다:
    /// 폭발(0.8초 예고 원), 부메랑의 돌아오는 길, 플레이어를 맞힌 탄의 하위 스코프.
    /// </summary>
    public sealed class MirrorPhantomBrain : BossBrainBase
    {
        public const string PatternCopyFire = "copy_fire", PatternSummon = "summon_slimes", PatternMimicArt = "mimic_art",
            PatternBloomMimic = "bloom_mimic";
        // 그림 클립 이름(monsters manifest의 boss.mirror_phantom).
        const string ClipHover = "hover", ClipFire = "fire", ClipCast = "cast", ClipBurst = "burst", ClipPhase = "phase";

        sealed class Volley
        {
            public int Remaining;
            public int CastId;
        }

        sealed class Track
        {
            public EnemyShotEntity Shot;
            public PhantomShot Info;
            public Volley Volley;
            public CompiledSpell Tree;
            public Vector2 Origin;
            public Vector2 Dir;
            public bool Returning;
        }

        PhantomCopyParams _prm = new PhantomCopyParams();
        float _minGap;
        int _nextCopyTick;
        string _currentArt;
        string _lastArt;
        readonly List<Track> _tracks = new List<Track>();
        readonly List<PhantomShot> _shotBuf = new List<PhantomShot>();
        readonly List<float> _offsetBuf = new List<float>();
        readonly List<float> _branchBuf = new List<float>();
        readonly List<string> _artBuf = new List<string>();

        /// <summary>이번 패턴에서 따라 쓸 비기(테스트·표시용).</summary>
        public string CurrentArt => _currentArt;
        public PhantomCopyParams Params => _prm;

        public override void Init(BossContext c)
        {
            _prm = PhantomCopyParams.From(c.Def.PhantomCopy);
            _minGap = PhantomCopyRules.MinSplitGapDegrees(_prm, c.Tuning);
            c.SetAnim(ClipHover);
        }

        public override void OnStateChanged(BossContext c, BossFightState oldState, BossFightState newState)
        {
            switch (newState)
            {
                case BossFightState.Waiting:
                case BossFightState.R5Blocked:
                case BossFightState.Recovery:
                    c.SetAnim(ClipHover);
                    break;
                case BossFightState.Telegraph:
                    c.SetAnim(ClipCast);
                    break;
                case BossFightState.Execute:
                    c.SetAnim(ClipBurst);
                    break;
                case BossFightState.PhaseTransition:
                    c.SetAnim(ClipPhase);
                    break;
            }
        }

        // ------------------------------------------------------------ 이동: 플레이어와 거리 5 유지, 벽에서 1.5 안쪽

        public override void Move(BossContext c, PlayerState target)
        {
            if (c.Boss.State == BossFightState.Recovery) return;
            var body = c.Body;
            float keep = BossTuning.Float(c.Def.Movement, "keep_distance", 0f);
            float margin = BossTuning.Float(c.Def.Movement, "wall_margin", 0f);
            float step = c.Def.MoveSpeed * (1f - body.SlowStrength(c.W.Tick)) * SimConst.TickSeconds;
            if (step <= 0f) return;
            Vector2 away = body.Pos - target.Pos;
            float dist = away.magnitude;
            Vector2 dir = dist > 0f ? away / dist : -body.Facing;
            Vector2 goal = body.Pos;
            if (dist > keep + c.Tuning.KeepDistanceBand) goal = body.Pos - dir * Mathf.Min(step, dist - keep);
            else if (dist < keep - c.Tuning.KeepDistanceBand) goal = body.Pos + dir * Mathf.Min(step, keep - dist);
            // 벽에서 wall_margin 안쪽에 머문다(밖에 있으면 안으로 돌아온다).
            Vector2 clamped = new Vector2(
                Mathf.Clamp(goal.x, margin, c.W.ArenaWidth - margin), Mathf.Clamp(goal.y, margin, c.W.ArenaHeight - margin));
            Vector2 delta = clamped - body.Pos;
            if (delta.sqrMagnitude <= 0f) return;
            if (delta.magnitude > step) delta = delta.normalized * step;
            Vector2 facing = body.Facing;
            c.MoveBody(delta);
            // 허상은 물러날 때도 플레이어를 본다.
            body.Facing = dist > 0f ? -dir : facing;
        }

        // ------------------------------------------------------------ 상시 행동

        public override void Always(BossContext c, PlayerState target, bool halted)
        {
            UpdateTracks(c);
            if (halted) return;
            Summon(c);
            CopyFire(c, target);
        }

        void Summon(BossContext c)
        {
            if (c.Boss.FirstEncounter || !HasPassive(c, PatternSummon)) return;
            var pattern = c.Pattern(PatternSummon);
            if (pattern == null || pattern.Steps.Count == 0) return;
            var step = pattern.Steps[0];
            int period = SimConst.Ticks(step.RepeatIntervalSeconds);
            if (step.Def.Summon == null || period <= 0) return;
            if (c.Boss.FightTicks <= 0 || c.Boss.FightTicks % period != 0) return;
            c.Summon(step.Def.Summon, null);
        }

        void CopyFire(BossContext c, PlayerState target)
        {
            // Waiting에서만 쏘고 패턴 진행 중엔 쉰다.
            if (c.Boss.State != BossFightState.Waiting || c.Run != null) return;
            if (!HasPassive(c, PatternCopyFire)) return;
            var spell = target.Spell;
            if (spell == null || !spell.IsValid) return;
            if (c.W.Tick < _nextCopyTick) return;
            var pattern = c.Pattern(PatternCopyFire);
            int min = pattern != null && pattern.Steps.Count > 0 ? pattern.Steps[0].Def.MinIntervalTicks : 0;
            _nextCopyTick = c.W.Tick + Math.Max(1,
                PhantomCopyRules.CopyIntervalTicks(spell.FireIntervalTicks, _prm.IntervalRatio, c.Common, c.Boss.RageStacks, min));
            c.SetAnim(ClipFire);
            FireTree(c, target, spell, c.Body.Pos, c.DirTo(target.Pos), c.W.NewCastId());
        }

        static bool HasPassive(BossContext c, string key)
        {
            var phase = c.PhaseDef;
            return phase != null && phase.Passives != null && phase.Passives.Contains(key);
        }

        // ------------------------------------------------------------ 따라 쓰기

        public override void OnPatternStart(BossContext c, BossPattern pattern)
        {
            var copy = pattern.Steps.Count > 0 ? pattern.Steps[0].Def.Copy : null;
            if (copy == null) return;
            var target = c.W.NearestPlayer(c.Body.Pos);
            var rng = c.Rng;
            bool mimic = pattern.Key == PatternMimicArt;
            _currentArt = PhantomCopyRules.PickMimicArt(copy.Arts, copy.Fallback, target != null ? target.UnlockedArts : null,
                c.Boss.FirstEncounter, mimic ? _lastArt : null, n => rng.Range(0, n), _artBuf);
            // 표시(머리 위 커맨드 화살표)가 읽을 값: A = 비기 번호(Ids.Art.All의 순서, 1부터), B = 예고 틱.
            c.PatternEventA = Array.IndexOf(Ids.Art.All, _currentArt) + 1;
            c.PatternEventB = SimConst.Ticks(copy.WarnSeconds);
        }

        public override BossActResult Act(BossContext c, BossStep step, BossStepInstance inst, PlayerState target)
        {
            if (step.Def.Copy == null) return BossActResult.Default;
            if (_currentArt != null) FireArt(c, target, _currentArt, inst.CastId);
            return BossActResult.Done;
        }

        public override void OnPatternEnd(BossContext c, BossPattern pattern, bool aborted)
        {
            if (pattern.Key == PatternMimicArt && !aborted) _lastArt = _currentArt;
        }

        /// <summary>비기 하나를 따라 쓴다: 갈래마다 트리 1회. 방향 = 쓰는 틱의 플레이어 방향. 탄 피해는 전부 1(비기 배율 무시).</summary>
        void FireArt(BossContext c, PlayerState target, string artId, int castId)
        {
            var spell = target.Spell;
            if (spell == null || !spell.IsValid) return;
            var art = c.W.Content != null ? c.W.Content.Art(artId) : null;
            int branches = art != null && art.Effect != null ? art.Effect.Branches : 1;
            float angle = art != null && art.Effect != null ? art.Effect.BranchAngleDegrees : 0f;
            PhantomCopyRules.ArtBranchOffsets(branches, angle, _minGap, _branchBuf);
            Vector2 aim = c.DirTo(target.Pos);
            for (int i = 0; i < _branchBuf.Count; i++)
                FireTree(c, target, spell, c.Body.Pos, BossRules.Rotate(aim, _branchBuf[i]), castId);
        }

        // ------------------------------------------------------------ 트리 1회 = 발사 1회(최대 12개)

        void FireTree(BossContext c, PlayerState owner, CompiledSpell spell, Vector2 pos, Vector2 dir, int castId)
        {
            // 변이는 2회차(웨이브 80 이상)부터 복사한다: 거울상이면 뒤로도, 뒤틀린 총열이면 흔들림.
            bool mutations = _prm.CopyMutationsFromCycle > 0 && c.Boss.Cycle >= _prm.CopyMutationsFromCycle;
            bool mirror = mutations && owner.Derived.MirrorImage;
            float spread = mutations && owner.Derived.TwistedBarrel && c.W.Content != null && c.W.Content.Mutations != null
                ? c.W.Content.Mutations.TwistedBarrelSpreadDegrees : 0f;
            var volley = new Volley { Remaining = _prm.MaxProjectiles, CastId = castId };
            _shotBuf.Clear();
            PhantomCopyRules.ScopeShots(c.W.Content, spell, 0, _prm, _minGap, mirror, volley.Remaining, _shotBuf, _offsetBuf);
            for (int i = 0; i < _shotBuf.Count; i++)
            {
                float jitter = spread > 0f ? c.Rng.Range(-spread, spread) : 0f;
                Launch(c, spell, _shotBuf[i], volley, pos, BossRules.Rotate(dir, _shotBuf[i].OffsetDegrees + jitter), false);
            }
        }

        void Launch(BossContext c, CompiledSpell tree, PhantomShot shot, Volley volley, Vector2 pos, Vector2 dir, bool returning)
        {
            if (volley.Remaining <= 0) return;
            volley.Remaining--;
            var w = c.W;
            if (shot.ChainLine)
            {
                // 연쇄 번개 → 조준 예고선 0.6초 뒤 직선 번개(폭 0.5, 길이 7). 탄으로 세는 공격이라 피해는 1칸 그대로다.
                var req = new TelegraphRequest { OwnerEnemyId = c.Body.Id, CastId = volley.CastId, CountAsOne = true };
                req.Zones.Add(new ZoneSpec
                {
                    Shape = ZoneShape.AimLine, Anchor = ZoneAnchor.Fixed, Pos = pos, Dir = dir, Width = _prm.ChainLineWidth,
                    Length = _prm.ChainLineLength, TelegraphTicks = SimConst.Ticks(_prm.ChainLineTelegraphSeconds),
                    BaseDamage = _prm.Damage, DamageSource = PlayerDamageSource.Projectile, Grazeable = true
                });
                w.Mods.Threats.RequestTelegraph(w, req);
                return;
            }
            var spec = new EnemyShotSpec
            {
                OwnerEnemyId = c.Body.Id, CastId = volley.CastId, Pos = pos, Dir = dir, Speed = _prm.Speed, Radius = shot.Radius,
                Range = shot.Range > 0f ? shot.Range : -1f, BaseDamage = _prm.Damage, Grazeable = true, Bounces = returning ? 0 : shot.Bounces
            };
            var e = w.Mods.Threats.SpawnShot(w, spec);
            if (e == null) return;
            bool hasChild = !returning && HasChildScope(tree, shot.ScopeDepth);
            if (returning || (!hasChild && shot.ExplosionRadius <= 0f && !shot.Returns)) return;
            _tracks.Add(new Track { Shot = e, Info = shot, Volley = volley, Tree = tree, Origin = pos, Dir = dir });
        }

        static bool HasChildScope(CompiledSpell tree, int depth)
        {
            int child = depth + 1;
            return tree != null && child < tree.Scopes.Count && child <= tree.MaxDepthNormal && tree.Scopes[child].Projectiles.Count > 0;
        }

        /// <summary>
        /// 지켜보던 탄이 사라졌으면 그 자리에서 뒷일을 한다. 자연스럽게 끝난 탄(플레이어 적중, 벽, 사거리)만 다룬다 —
        /// 소거(방열, 웨이브 끝)나 병풍에 막혀 사라진 탄은 아무 일도 없다.
        /// </summary>
        void UpdateTracks(BossContext c)
        {
            var w = c.W;
            for (int i = _tracks.Count - 1; i >= 0; i--)
            {
                var t = _tracks[i];
                var shot = t.Shot;
                if (!shot.Dead && w.EnemyShots.Contains(shot))
                {
                    if (shot.Vel.sqrMagnitude > 0f) t.Dir = shot.Vel.normalized;
                    continue;
                }
                _tracks.RemoveAt(i);
                if (shot.Blocked) continue;
                Vector2 end = shot.Pos;
                float stepLen = _prm.Speed * SimConst.TickSeconds + c.Tuning.CopyHitSlack;
                bool hitPlayer = false;
                for (int k = 0; k < w.Players.Length && !hitPlayer; k++)
                {
                    var p = w.Players[k];
                    if (!p.Active) continue;
                    hitPlayer = (p.Pos - end).magnitude <= shot.Radius + c.HitRadius(p) + stepLen;
                }
                bool atWall = !w.InArena(end, shot.Radius + stepLen);
                bool outOfRange = t.Info.Range > 0f && (end - t.Origin).magnitude >= t.Info.Range - stepLen;
                if (!hitPlayer && !atWall && !outOfRange) continue;

                if (t.Info.ExplosionRadius > 0f) Explode(c, t, end);
                if (hitPlayer) SpawnChildScope(c, t, end);
                else if (t.Info.Returns && c.Body != null) Launch(c, t.Tree, ReturnShot(c, t.Info, end), t.Volley, end, c.DirTo(end) * -1f, true);
            }
        }

        /// <summary>부메랑의 돌아오는 길: 허상 쪽으로, 거기까지의 거리만큼.</summary>
        static PhantomShot ReturnShot(BossContext c, PhantomShot info, Vector2 from)
        {
            var back = info;
            back.Range = (c.Body.Pos - from).magnitude;
            back.ExplosionRadius = 0f;
            back.Returns = false;
            return back;
        }

        /// <summary>폭발: 탄이 멈춘 자리에 0.8초 예고 원을 먼저 그린 뒤 터진다(반경 = 플레이어의 60%, 최대 1.5, 피해 1).</summary>
        void Explode(BossContext c, Track t, Vector2 at)
        {
            var req = new TelegraphRequest { OwnerEnemyId = c.Body.Id, CastId = t.Volley.CastId, CountAsOne = true };
            req.Zones.Add(new ZoneSpec
            {
                Shape = ZoneShape.Circle, Anchor = ZoneAnchor.Fixed, Pos = at, Dir = t.Dir, Radius = t.Info.ExplosionRadius,
                TelegraphTicks = SimConst.Ticks(_prm.ExplosionTelegraphSeconds), BaseDamage = _prm.ExplosionDamage,
                DamageSource = PlayerDamageSource.Projectile, Grazeable = true
            });
            c.W.Mods.Threats.RequestTelegraph(c.W, req);
        }

        /// <summary>플레이어를 처음 맞힌 탄의 하위 스코프: 맞힌 자리에서 부모가 날던 방향으로(03 요약서 6.1절). 발사 1회 12개 상한에 같이 센다.</summary>
        void SpawnChildScope(BossContext c, Track t, Vector2 at)
        {
            if (!HasChildScope(t.Tree, t.Info.ScopeDepth) || t.Volley.Remaining <= 0) return;
            _shotBuf.Clear();
            PhantomCopyRules.ScopeShots(c.W.Content, t.Tree, t.Info.ScopeDepth + 1, _prm, _minGap, false, t.Volley.Remaining, _shotBuf, _offsetBuf);
            int n = _shotBuf.Count;
            for (int i = 0; i < n; i++)
            {
                var shot = _shotBuf[i];
                Launch(c, t.Tree, shot, t.Volley, at, BossRules.Rotate(t.Dir, shot.OffsetDegrees), false);
            }
        }

        public override void OnFightEnd(BossContext c)
        {
            _tracks.Clear();
        }
    }
}
