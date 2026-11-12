using System;
using System.Collections.Generic;
using System.Globalization;
using Wizard.Content;

namespace Wizard.DataBuild.Validate.Evasion
{
    // 공격 하나와 몸 하나에 R1~R4 를 적용한다(spec 05 1.2, 10.1~10.6). 기준 속도 V 와 피격 반지름은 부르는 쪽이 준다(EvasionBasis).
    //   R1  예고 구역이 있는 공격: (예고 − 300ms) × V ≥ 벗어나는 거리 × 1000. 방향을 마지막 n틱에 고정하면 고정 뒤 n틱만 센다
    //   R2  몸 접촉이 있는 적: 북두꺼비 버프 뒤 속도 < V(보스는 버프 없이). 서리밭 위 걷는 속도가 있으면 < V_frost
    //   R3  한 번에 여러 발이나 이어 쏘는 줄기: 탄속 ≤ 9, 3칸 지점 이웃 탄 사이 ≥ 1.0칸
    //   R4  탄속 > 9 이거나 조준선으로 겨누는 탄: 조준선 예고가 있고, 방향 고정 창 ≥ 24틱(고정 창의 R1 은 R1 줄이 본다)
    public static class EvasionCheck
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        // 벗어나는 거리(milli 칸). 식이 없으면 null 과 이유
        public static int? EscapeMilli(EvasionAttack a, int hitMilli, out string? problem)
        {
            problem = null;
            if (a.EscapeOverrideMilli.HasValue)
            {
                return a.EscapeOverrideMilli.Value;
            }
            switch (a.Shape)
            {
                case AttackDef.TelegraphShape.Circle:
                    return EvasionMath.EscapeCircleMilli(EvasionMath.CeilMilli(a.Radius), hitMilli);
                case AttackDef.TelegraphShape.Band:
                case AttackDef.TelegraphShape.AimLine:
                    return EvasionMath.EscapeBandMilli(EvasionMath.CeilMilli(a.Width), hitMilli);
                case AttackDef.TelegraphShape.Fan:
                    if (a.FanDegrees <= 0m || a.FanDegrees > EvasionMath.HalfTurnDegrees)
                    {
                        problem = "부채꼴 전체각 " + a.FanDegrees.ToString(Inv) + "도는 spec 05 10.1 식(반각 0 초과 90° 이하)의 밖이다. escape_override_milli 를 적는다";
                        return null;
                    }
                    return EvasionMath.EscapeFanMilli(a.Radius, a.FanDegrees, hitMilli);
                case AttackDef.TelegraphShape.Ring:
                    problem = "고리(ring) 모양의 벗어나는 거리 식은 spec 05 10.1 에 없다. escape_override_milli 를 적는다";
                    return null;
                default:
                    problem = "예고 모양이 없다";
                    return null;
            }
        }

        // R1 이 세는 예고 틱: 마지막 n틱에 방향을 고정하면 고정 뒤 n틱(고슴도치 "고정 뒤", spec 05 10.3)
        public static int WindowTicks(EvasionAttack a) =>
            a.Lock == AttackDef.LockMode.LastNTicks ? Math.Min(a.LockTicks, a.TelegraphTicks) : a.TelegraphTicks;

        public static List<EvasionViolation> CheckAttack(EvasionAttack a, int speedMilli, int hitMilli) => Check(a, speedMilli, hitMilli);

        // 기준 속도·피격 반지름이 없을 때(캐릭터 데이터가 없는 data/): V 에 기대지 않는 줄만 본다(R3 탄 사이·탄속, R4 조준선·고정 틱, 면제 표지)
        public static List<EvasionViolation> CheckAttackWithoutBasis(EvasionAttack a) => Check(a, null, null);

        private static List<EvasionViolation> Check(EvasionAttack a, int? speedMilli, int? hitMilli)
        {
            var list = new List<EvasionViolation>();
            if (a.Exempt)
            {
                if (!a.ExemptAllowed)
                {
                    list.Add(new EvasionViolation("exempt", "회피 규칙 면제(EvasionRuleExempt.PhantomFinisher)는 허상과의 첫 만남을 끝내는 '되비침'에만 쓴다(spec 05 1.2 보충 7)"));
                }
                return list;
            }
            if (a.Shape.HasValue && speedMilli.HasValue && hitMilli.HasValue)
            {
                CheckR1(a, speedMilli.Value, hitMilli.Value, list);
            }
            CheckShots(a, list);
            return list;
        }

        private static void CheckR1(EvasionAttack a, int speedMilli, int hitMilli, List<EvasionViolation> list)
        {
            int v = a.FrostVariant ? EvasionMath.FrostSpeedMilli(speedMilli) : speedMilli;
            string vName = a.FrostVariant ? "V_frost" : "V";
            int? esc = EscapeMilli(a, hitMilli, out string? problem);
            if (esc == null)
            {
                list.Add(new EvasionViolation("R1", problem ?? ""));
            }
            else
            {
                int ms = EvasionMath.TicksToMs(WindowTicks(a));
                if (!EvasionMath.R1(ms, esc.Value, v))
                {
                    list.Add(new EvasionViolation("R1",
                        (a.Lock == AttackDef.LockMode.LastNTicks ? "방향 고정 뒤 예고 " : "예고 ") + Seconds((long)ms * EvasionMath.MicroPerMilli)
                        + ", 벗어나는 거리 " + Cells(esc.Value) + ", " + vName + " " + Speed(v)
                        + " → 0.25 + d/" + vName + " = " + Seconds(EvasionMath.NeedMicros(esc.Value, v))
                        + ", 여유 " + Seconds(EvasionMath.MarginMicros(ms, esc.Value, v)) + " < 0.050초"
                        + " ((T − 300ms) × " + vName + " = " + ((long)(ms - EvasionMath.ReactionMs - EvasionMath.MarginMs) * v).ToString(Inv)
                        + " < d × 1000 = " + ((long)esc.Value * EvasionMath.MilliPerUnit).ToString(Inv) + ", spec 05 1.2 R1·10.2)"));
                }
            }
        }

        private static void CheckShots(EvasionAttack a, List<EvasionViolation> list)
        {
            EvasionProj? p = a.Proj;
            if (p != null)
            {
                bool stream = p.ShotsPerSecond > 0;
                bool scatter = p.Count >= 2 || stream;
                bool fast = p.SpeedMilli > EvasionMath.ScatterSpeedMaxMilli;
                bool aimLine = a.Shape == AttackDef.TelegraphShape.AimLine;
                if (scatter && fast)
                {
                    list.Add(new EvasionViolation("R3", "흩뿌리거나 이어 쏘는 탄의 속도 " + Speed(p.SpeedMilli) + " > 9칸/초(spec 05 1.2 R3)"));
                }
                if (p.Count >= 2)
                {
                    decimal gapDeg = EvasionMath.MinGapDegrees(p.SpreadDegrees, p.Count);
                    long gap = EvasionMath.DegreesToMicroRad(gapDeg);
                    if (!EvasionMath.R3Spacing(gap))
                    {
                        list.Add(new EvasionViolation("R3", p.Count.ToString(Inv) + "발의 이웃 탄 사이 " + gapDeg.ToString(Inv) + "도 → 3칸 지점 탄 사이 "
                            + Micro(EvasionMath.ArcMicro(gap, EvasionMath.SpacingMeasureMilli)) + "칸 < 1.0칸(3000 × Δθ_mrad = "
                            + ((long)EvasionMath.SpacingMeasureMilli * (gap / EvasionMath.MicroPerMilli)).ToString(Inv) + " < 1000000, spec 05 10.6)"));
                    }
                }
                if (stream)
                {
                    long s = EvasionMath.StreamSpacingMicro(p.SpeedMilli, p.ShotsPerSecond, p.SweepDegreesPerSecond, EvasionMath.SpacingMeasureMilli);
                    if (!EvasionMath.R3StreamSpacing(s))
                    {
                        list.Add(new EvasionViolation("R3", "이어 쏘는 줄기(초당 " + p.ShotsPerSecond.ToString(Inv) + "발, 초당 " + p.SweepDegreesPerSecond.ToString(Inv)
                            + "도)의 3칸 지점 탄 사이 " + Micro(s) + "칸 < 1.0칸(hypot(속도 ÷ 초당 발수, d × Δθ), spec 05 10.6)"));
                    }
                }
                if (fast || aimLine)
                {
                    if (!aimLine)
                    {
                        list.Add(new EvasionViolation("R4", "탄속 " + Speed(p.SpeedMilli) + " > 9칸/초인데 조준선(aim_line 예고)이 없다(spec 05 1.2 R4)"));
                    }
                    else
                    {
                        int window = WindowTicks(a);
                        if (window < EvasionMath.AimLockMinTicks)
                        {
                            list.Add(new EvasionViolation("R4", "조준 방향을 발사 " + window.ToString(Inv) + "틱 전에 고정한다 < "
                                + EvasionMath.AimLockMinTicks.ToString(Inv) + "틱(0.4초, spec 05 1.2 R4·10.2)"));
                        }
                    }
                }
            }
        }

        public static R2Status R2Of(EvasionMover m, int speedMilli)
        {
            if (m.ContactDamage <= 0)
            {
                return R2Status.Exempt;
            }
            return EvasionMath.R2(EvasionMath.ContactSpeedMilli(m.MoveSpeedMilli, m.IsBoss), speedMilli) ? R2Status.Pass : R2Status.Fail;
        }

        public static List<EvasionViolation> CheckMover(EvasionMover m, int speedMilli)
        {
            var list = new List<EvasionViolation>();
            if (R2Of(m, speedMilli) == R2Status.Fail)
            {
                int eff = EvasionMath.ContactSpeedMilli(m.MoveSpeedMilli, m.IsBoss);
                list.Add(new EvasionViolation("R2", "몸 접촉이 있는 적의 속도 " + Speed(m.MoveSpeedMilli)
                    + (m.IsBoss ? "" : "(북두꺼비 버프 뒤 " + Speed(eff) + ")") + " ≥ V " + Speed(speedMilli) + "(spec 05 1.2 R2·10.5)"));
            }
            if (m.ContactDamage > 0 && m.FrostMoveSpeedMilli.HasValue)
            {
                int vf = EvasionMath.FrostSpeedMilli(speedMilli);
                if (!EvasionMath.R2(m.FrostMoveSpeedMilli.Value, vf))
                {
                    list.Add(new EvasionViolation("R2", "서리밭 위 걷는 속도 " + Speed(m.FrostMoveSpeedMilli.Value) + " ≥ V_frost " + Speed(vf) + "(spec 05 10.4)"));
                }
            }
            return list;
        }

        private static string Cells(int milli) => ((decimal)milli / EvasionMath.MilliPerUnit).ToString("0.000", Inv) + "칸";

        private static string Speed(int milli) => ((decimal)milli / EvasionMath.MilliPerUnit).ToString("0.000", Inv) + "칸/초";

        private static string Seconds(long micros) => ((decimal)micros / (EvasionMath.MilliPerUnit * EvasionMath.MicroPerMilli)).ToString("0.000", Inv) + "초";

        private static string Micro(long micro) => ((decimal)micro / (EvasionMath.MilliPerUnit * EvasionMath.MicroPerMilli)).ToString("0.000", Inv);
    }
}
