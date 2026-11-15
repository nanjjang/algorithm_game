using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Wizard.Solo.Data;

namespace Wizard.Solo.Sim.Arts
{
    /// <summary>파이널 어빌리티 한 번의 예약된 단계 하나(판 안의 상태). 주인: Arts.</summary>
    public sealed class FinalStep
    {
        public enum StepKind { Volley, Area, Warn, Clear, VortexEnd }

        public StepKind Kind;
        public int Tick;
        public Vector2 Pos;
        public Vector2 Dir;
        /// <summary>일제 사격: 시야 기준 돌림(도), 갈래 수, 갈래 간격(도), 정면·나머지 배율.</summary>
        public float AngleOffset;
        public int Branches = 1;
        public float BranchAngle;
        public float SideDamage;
        /// <summary>T 배수(일제 사격은 트리 배율).</summary>
        public float Damage;
        public float Radius;
        public float Inner;
        public float Pull;
        public int StunTicks, FreezeTicks;
        /// <summary>이 단계가 속한 묶음의 "적마다 한 번" 셈(null = 매번 때린다).</summary>
        public List<int> HitSet;
        /// <summary>그림: 빌려 쓰는 고유 비기 id(ArtStage) — null이면 ArtPulse.</summary>
        public string Fx;
        public ArtStageKind FxKind;
        public int FxB;
        public bool Done;
    }

    /// <summary>발동한 파이널 어빌리티 하나(ArtPlayerState.Finals).</summary>
    public sealed class FinalRun
    {
        public string Id;
        public ArtCast Cast;
        public readonly List<FinalStep> Steps = new List<FinalStep>();
        public bool Done;
    }

    // 파이널 어빌리티(사용자 지시 2026-11-15: "게이지 차면 K 누르면 나가는 방출 말고, 캐릭터마다 다른 파이널 어빌리티, 조합에 따라 다르게").
    // 결정이 min_crystals(2)개 이상일 때 K를 빠르게 두 번(double_tap_seconds 안) → 결정 전부를 쓰고 파이널 어빌리티.
    // 무엇이 나가는지는 캐릭터 × 고른 기본 공격 유형(핵 블록) 15가지(AttackStyles.json final_abilities). 일제 사격 단계는 지금 지팡이 회로(코어 + 연결 블록)를
    // 그대로 쏘므로 회로 조합에 따라서도 달라진다. 범위 단계의 그림·소리는 그 캐릭터 고유 비기 강화의 것을 빌려 쓴다(새 그림·소리 없음).
    // 결정이 충분할 때 커맨드 없는 K는 두 번째 K를 잠깐 기다렸다가(오지 않으면 그 첫 K 시각으로 마력 방출) 판정한다. 명세 트라이는 그대로. 손잡이 = final_abilities.enabled(명세 값 기본 꺼짐, FunPass가 켠다).
    public sealed partial class ArtSystem
    {
        /// <summary>K 두 번 사이 최대 간격(final_abilities.double_tap_seconds, 없으면 0.25초)을 입력 시각 단위로.</summary>
        static long DoubleTapU(SimWorld w) =>
            (long)(F(AttackStyles.FinalConfig(w.Content), "double_tap_seconds", 0.25f) * SimConst.UnitsPerSecond);

        /// <summary>파이널을 쓸 수 있나: 손잡이 켬, 결정 min_crystals개 이상, 지금 코어에 파이널이 있다.</summary>
        static bool FinalReady(SimWorld w, PlayerState p)
        {
            int need = AttackStyles.FinalMinCrystals(w.Content);
            return need > 0 && p.Crystals >= need && FinalIdFor(w, p) != null;
        }

        /// <summary>지금 K를 누르면 커맨드 없는 마력 방출인가(방향 입력이 걸려 있지 않다). 커맨드 비기는 두 번 누르기를 기다리지 않는다.</summary>
        static bool PlainKIsBurst(ArtPlayerState s, PlayerState p, long timeU)
        {
            if (s.Parser.HasBuffered) return false;
            var pv = s.Parser.Preview(timeU, s.Wallet, p.UnlockedArts);
            return pv.Main.IsFire && pv.Main.Art == Ids.Art.Burst;
        }

        /// <summary>결정 전부를 쓰고 파이널 어빌리티를 시작한다(K 두 번).</summary>
        bool TryStartFinal(SimWorld w, PlayerState p, ArtPlayerState s)
        {
            if (w.Content == null || !FinalReady(w, p)) return false;
            string core = p.Spell != null && p.Spell.Core != null ? p.Spell.Core.Id : null;
            var spec = AttackStyles.FinalAbility(w.Content, core);
            if (spec == null || !(spec["stages"] is JArray stages)) return false;
            string id = Str(spec, "id");
            var def = w.Content.Art(Ids.Art.Burst);
            if (string.IsNullOrEmpty(id) || def == null) return false;

            int spent = w.Mods.Player.SpendAllCrystals(w, p);
            w.Emit(SimEventType.CommandReset, p.Slot, a: (int)CommandResetReason.Fired);
            s.Parser.Reset(CommandResetReason.Fired);
            s.HasPhysical = false;
            w.Emit(SimEventType.ArtFired, p.Slot, a: 0, b: 0, f: spent, id: id, pos: p.Pos);

            var run = new FinalRun
            {
                Id = id,
                Cast = new ArtCast
                {
                    Def = def,
                    JudgeTick = w.Tick,
                    StartTick = w.Tick,
                    CrystalsAfter = 0,
                    CharMul = w.Mods.Traits.CharacterMul(w, p, 0),
                    HasPhysical = false,
                    Physical = p.Facing
                }
            };
            int cap = (int)F(spec, "shot_cap", w.Content.Const != null ? w.Content.Const.ArtShotCap : 0);
            run.Cast.Capped = cap > 0;
            run.Cast.BudgetLeft = cap;

            float invuln = F(spec, "invuln_seconds");
            if (invuln > 0f) w.Mods.Player.GrantInvuln(w, p, SimConst.Ticks(invuln), false);

            for (int i = 0; i < stages.Count; i++)
                if (stages[i] is JObject st) BuildStage(w, p, run, st);
            s.Finals.Add(run);
            w.Mods.Run.OnArtFired(w, p, id);
            return true;
        }

        /// <summary>지금 코어로 나갈 파이널 어빌리티 id(꺼졌거나 없으면 null).</summary>
        static string FinalIdFor(SimWorld w, PlayerState p)
        {
            string core = p.Spell != null && p.Spell.Core != null ? p.Spell.Core.Id : null;
            return Str(AttackStyles.FinalAbility(w.Content, core), "id");
        }

        static string Str(JObject o, string key) =>
            o != null && o[key] != null && o[key].Type == JTokenType.String ? (string)o[key] : null;

        static int StartOf(SimWorld w, JObject st) => w.Tick + (F(st, "at") > 0f ? UpTicks(F(st, "at")) : 0);

        static ArtStageKind FxKindOf(string fx, bool warnHead)
        {
            switch (fx)
            {
                case Ids.Art.Kiln: return ArtStageKind.Rise;
                case Ids.Art.BoltDash: return warnHead ? ArtStageKind.Warn : ArtStageKind.Land;
                case Ids.Art.IceScreen: return ArtStageKind.Rise;
                case Ids.Art.ShadowHand: return ArtStageKind.Crush;
                case Ids.Art.CopyCircle: return ArtStageKind.Pulse;
                default: return ArtStageKind.Blast;
            }
        }

        void BuildStage(SimWorld w, PlayerState p, FinalRun run, JObject st)
        {
            string kind = Str(st, "kind");
            string fx = Str(st, "fx");
            int start = StartOf(w, st);
            int gap = UpTicks(F(st, "gap_seconds"));
            float dmg = F(st, "damage");
            int stun = UpTicks(F(st, "stun_seconds"));
            int freeze = UpTicks(F(st, "freeze_seconds"));
            Vector2 facing = Dir8Util.ToSimVector(p.Facing);
            switch (kind)
            {
                case "volley":
                {
                    // 지금 지팡이 회로를 그대로 쏜다(시야 기준). repeats번, 매번 spin도씩 돌린다.
                    int repeats = Mathf.Max(1, (int)F(st, "repeats", 1f));
                    for (int r = 0; r < repeats; r++)
                        run.Steps.Add(new FinalStep
                        {
                            Kind = FinalStep.StepKind.Volley, Tick = start + r * gap, AngleOffset = r * F(st, "spin_degrees"),
                            Branches = Mathf.Max(1, (int)F(st, "branches", 1f)), BranchAngle = F(st, "branch_angle_degrees"),
                            Damage = dmg, SideDamage = F(st, "side_damage", dmg)
                        });
                    break;
                }
                case "rings":
                {
                    // 시전 자리 둘레로 고리가 바깥으로 연달아(적마다 한 번).
                    if (!(st["radii"] is JArray radii)) break;
                    var hit = new List<int>();
                    for (int i = 0; i < radii.Count; i++)
                    {
                        float outer = (float)radii[i];
                        float inner = i > 0 ? (float)radii[i - 1] : 0f;
                        run.Steps.Add(new FinalStep
                        {
                            Kind = FinalStep.StepKind.Area, Tick = start + i * gap, Pos = p.Pos, Dir = facing, Radius = outer, Inner = inner,
                            Damage = dmg, StunTicks = stun, FreezeTicks = freeze, HitSet = hit, Fx = fx, FxKind = FxKindOf(fx, false), FxB = i
                        });
                    }
                    break;
                }
                case "strikes":
                {
                    // 가까운 적부터 차례로 내리꽂는다(적이 없으면 시야 앞 부채꼴 자리). 예고가 있으면 미리 보여 준다.
                    int count = Mathf.Max(1, (int)F(st, "count", 1f));
                    float range = F(st, "range", 7f);
                    float radius = F(st, "radius", 1f);
                    int warn = UpTicks(F(st, "warn_seconds"));
                    var targets = NearestTargets(w, p.Pos, range);
                    for (int i = 0; i < count; i++)
                    {
                        Vector2 at;
                        if (targets.Count > 0) at = targets[i % targets.Count].Pos;
                        else
                        {
                            float ang = Dir8Util.ToDegrees(p.Facing) + ((i * 137.5f) % 90f - 45f);
                            float dist = range * (0.35f + 0.6f * ((i * 0.618f) % 1f));
                            at = p.Pos + (Vector2)(Quaternion.Euler(0f, 0f, ang) * Vector3.right) * dist;
                        }
                        at = w.ClampToArena(at, 0f);
                        int tick = start + warn + i * gap;
                        if (warn > 0)
                            run.Steps.Add(new FinalStep
                            {
                                Kind = FinalStep.StepKind.Warn, Tick = start + i * gap, Pos = at, Radius = radius, Fx = fx,
                                FxKind = FxKindOf(fx, true), FxB = i, Inner = tick - (start + i * gap)
                            });
                        run.Steps.Add(new FinalStep
                        {
                            Kind = FinalStep.StepKind.Area, Tick = tick, Pos = at, Dir = facing, Radius = radius, Damage = dmg,
                            StunTicks = stun, FreezeTicks = freeze, Fx = fx, FxKind = FxKindOf(fx, false), FxB = i == count - 1 ? 4 : Mathf.Min(i, 3)
                        });
                    }
                    break;
                }
                case "line":
                {
                    // 시야(와 둘레 directions 갈래)로 줄지어 솟는다(적마다 한 번).
                    int dirs = Mathf.Max(1, (int)F(st, "directions", 1f));
                    int count = Mathf.Max(1, (int)F(st, "count", 1f));
                    float first = F(st, "start", 0.9f), step = F(st, "step", 0.9f), radius = F(st, "radius", 0.8f);
                    var hit = new List<int>();
                    for (int k = 0; k < dirs; k++)
                    {
                        Vector2 dir = (Vector2)(Quaternion.Euler(0f, 0f, 360f * k / dirs) * (Vector3)facing);
                        for (int i = 0; i < count; i++)
                            run.Steps.Add(new FinalStep
                            {
                                Kind = FinalStep.StepKind.Area, Tick = start + i * gap, Pos = w.ClampToArena(p.Pos + dir * (first + step * i), 0f), Dir = dir,
                                Radius = radius, Damage = dmg, StunTicks = stun, FreezeTicks = freeze, HitSet = hit, Fx = fx,
                                FxKind = FxKindOf(fx, false), FxB = k == 0 ? i : i + 1
                            });
                    }
                    break;
                }
                case "vortex":
                {
                    // 시야 앞 offset 자리에서 맥박마다 끌어당기며 벤다. 마지막에 그림 닫기.
                    Vector2 center = w.ClampToArena(p.Pos + facing * F(st, "offset"), 0f);
                    int pulses = Mathf.Max(1, (int)F(st, "pulses", 1f));
                    float radius = F(st, "radius", 3f);
                    for (int i = 0; i < pulses; i++)
                        run.Steps.Add(new FinalStep
                        {
                            Kind = FinalStep.StepKind.Area, Tick = start + i * gap, Pos = center, Dir = facing, Radius = radius, Damage = dmg,
                            Pull = F(st, "pull"), StunTicks = stun, FreezeTicks = freeze, Fx = fx, FxKind = FxKindOf(fx, false), FxB = i
                        });
                    if (fx == Ids.Art.CopyCircle)
                        run.Steps.Add(new FinalStep { Kind = FinalStep.StepKind.VortexEnd, Tick = start + pulses * gap, Pos = center, Radius = radius, Fx = fx });
                    break;
                }
                case "crush":
                {
                    // 움켜쥐어 끌어모은 뒤(grip) 짓누른다(crush). 그림자 손 그림.
                    Vector2 center = w.ClampToArena(p.Pos + facing * F(st, "offset"), 0f);
                    float radius = F(st, "radius", 3f);
                    run.Steps.Add(new FinalStep
                    {
                        Kind = FinalStep.StepKind.Area, Tick = start, Pos = center, Dir = facing, Radius = F(st, "grip_radius", radius),
                        Damage = F(st, "grip_damage"), Pull = F(st, "pull"), Fx = fx, FxKind = ArtStageKind.Grip
                    });
                    run.Steps.Add(new FinalStep
                    {
                        Kind = FinalStep.StepKind.Area, Tick = start + UpTicks(F(st, "crush_delay_seconds", 0.45f)), Pos = center, Dir = facing,
                        Radius = radius, Damage = dmg, StunTicks = stun, FreezeTicks = freeze, Fx = fx, FxKind = ArtStageKind.Crush
                    });
                    break;
                }
                case "clear":
                    run.Steps.Add(new FinalStep { Kind = FinalStep.StepKind.Clear, Tick = start, Pos = p.Pos, Radius = F(st, "radius", 3f) });
                    break;
            }
        }

        List<EnemyState> NearestTargets(SimWorld w, Vector2 from, float range)
        {
            var list = new List<EnemyState>();
            for (int i = 0; i < w.Enemies.Count; i++)
            {
                var en = w.Enemies[i];
                if (en.Targetable && (en.Pos - from).magnitude <= range) list.Add(en);
            }
            list.Sort((a, b) =>
            {
                int c = (a.Pos - from).sqrMagnitude.CompareTo((b.Pos - from).sqrMagnitude);
                return c != 0 ? c : a.Id.CompareTo(b.Id);
            });
            return list;
        }

        // ------------------------------------------------------------ 5단계 진행

        void RunFinals(SimWorld w, PlayerState p, ArtPlayerState s)
        {
            for (int i = 0; i < s.Finals.Count; i++)
            {
                var run = s.Finals[i];
                bool left = false;
                for (int k = 0; k < run.Steps.Count; k++)
                {
                    var step = run.Steps[k];
                    if (step.Done) continue;
                    if (w.Tick < step.Tick) { left = true; continue; }
                    RunFinalStep(w, p, run, step);
                    step.Done = true;
                }
                if (!left) run.Done = true;
            }
            s.Finals.RemoveAll(r => r.Done);
        }

        void RunFinalStep(SimWorld w, PlayerState p, FinalRun run, FinalStep step)
        {
            switch (step.Kind)
            {
                case FinalStep.StepKind.Volley:
                {
                    int shot = NextShot(StateOf(p));
                    Exec(w, p, run.Cast, shot, p.Pos, step.AngleOffset, step.Damage, ShotSource.ArtOther, ShotHitEffect.None, false);
                    for (int j = 1; j < step.Branches; j++)
                    {
                        int k = (j + 1) / 2;
                        float sign = (j & 1) == 1 ? 1f : -1f;
                        Exec(w, p, run.Cast, shot, p.Pos, step.AngleOffset + sign * k * step.BranchAngle, step.SideDamage, ShotSource.ArtOther,
                            ShotHitEffect.None, true);
                    }
                    break;
                }
                case FinalStep.StepKind.Warn:
                    if (step.Fx == Ids.Art.BoltDash)
                        w.Emit(SimEventType.ArtStage, p.Slot, id: step.Fx, a: (int)ArtStageKind.Warn, b: step.FxB, pos: step.Pos, f: step.Radius,
                            pos2: new Vector2(step.Inner, 0f));
                    break;
                case FinalStep.StepKind.Clear:
                    w.Emit(SimEventType.ArtPulse, p.Slot, id: run.Id, pos: step.Pos, f: step.Radius);
                    w.Mods.Threats.ClearShots(w, step.Pos, step.Radius);
                    break;
                case FinalStep.StepKind.VortexEnd:
                    w.Emit(SimEventType.ArtStage, p.Slot, id: step.Fx, a: (int)ArtStageKind.End, b: 0, pos: step.Pos, f: step.Radius);
                    break;
                case FinalStep.StepKind.Area:
                    EmitFinalFx(w, p, run, step);
                    HitFinalArea(w, p, run, step);
                    break;
            }
        }

        void EmitFinalFx(SimWorld w, PlayerState p, FinalRun run, FinalStep step)
        {
            if (string.IsNullOrEmpty(step.Fx))
            {
                w.Emit(SimEventType.ArtPulse, p.Slot, id: run.Id, pos: step.Pos, f: step.Radius);
                return;
            }
            Vector2 pos2 = step.Fx == Ids.Art.Kiln ? new Vector2(step.Inner, 0f) : step.Dir;
            w.Emit(SimEventType.ArtStage, p.Slot, id: step.Fx, a: (int)step.FxKind, b: step.FxB, pos: step.Pos, f: step.Radius, pos2: pos2);
        }

        void HitFinalArea(SimWorld w, PlayerState p, FinalRun run, FinalStep step)
        {
            float amount = step.Damage * w.Mods.Spells.StandardT(w, p) * run.Cast.CharMul;
            string element = CoreElement(p);
            int n = w.Enemies.Count;
            for (int i = 0; i < n; i++)
            {
                var en = w.Enemies[i];
                if (!en.Targetable) continue;
                if (step.HitSet != null && step.HitSet.Contains(en.Id)) continue;
                Vector2 to = step.Pos - en.Pos;
                float d = to.magnitude;
                if (d - en.BodyRadius > step.Radius) continue;
                if (step.Inner > 0f && d + en.BodyRadius < step.Inner) continue;
                if (step.HitSet != null) step.HitSet.Add(en.Id);
                if (step.Pull > 0f && d > 0f) w.Mods.Enemies.ApplyCc(w, en, CcKind.Pull, Mathf.Min(step.Pull, d), CcSource.ArtStage, to / d);
                if (amount > 0f && en.Targetable)
                {
                    var res = Direct(w, p, en, amount, element, DamageOrigin.Art, false, run.Id);
                    if (res.Killed) continue;
                }
                if (!en.Targetable) continue;
                if (step.StunTicks > 0)
                {
                    if (en.IsBoss || en.Has(EnemyTags.Boss)) w.Mods.Bosses.ActionHalt(w, en, step.StunTicks);
                    else w.Mods.Enemies.ApplyCc(w, en, CcKind.Stun, step.StunTicks, CcSource.ArtStage, step.Dir);
                }
                if (step.FreezeTicks > 0) w.Mods.Enemies.ApplyCc(w, en, CcKind.Freeze, step.FreezeTicks, CcSource.ArtStage, step.Dir);
            }
        }
    }
}
