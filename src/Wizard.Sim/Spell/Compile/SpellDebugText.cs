using System.Globalization;
using System.Text;
using Wizard.Content;
using Wizard.Core;
using Wizard.Sim.Heat;

namespace Wizard.Sim.Spell.Compile
{
    // spec 02 14.5 디버그 글자 출력(GDD 16 2단계 "트리를 글자로 출력하는 임시 조립기"). 형식은 [임시].
    // 개발용이라 화면 문자열이 아니다(이름 표를 거치지 않는다). 블록 이름은 spec 14 의 id 에서 "block." 을 뗀 것이다
    // (CONTRACTS 2.1 대응표: 명세 예시의 great_fireball → fireball, split_3 → split_three).
    //
    //   SPELL interval=<틱>t heat=<열> count=<가지치기 뒤 수>/<상한> mirror_count=<거울상 수>/<상한> T=<표준 T>
    //   S<d> depth=<d> decay=<감쇠> [trigger@<슬롯>] split=<갈래>[@<슬롯>] heat=<스코프 열> inst=<InstancesTotal>[ omitted]
    //     [<슬롯>] P <id>[ *owner]      투사체(슬롯 순)
    //     [<슬롯>] M <id>               변형
    //     [<슬롯>] C <id> ignored       무시된 분열(두 번째 이후)·트리거(넷째)
    //   DIAG none | DIAG <코드> slot=<슬롯> depth=<깊이>(정렬 순서대로 한 줄씩)
    // 명세 예시에 없는 줄(M, C, omitted, 진단 줄의 꼴, 무효일 때의 "SPELL invalid")은 이 작업이 정한 [임시] 꼴이다.
    // 소수는 둘째 자리까지 반올림(.5 는 올림). 줄 끝은 '\n', 마지막 줄 뒤에는 없다.
    internal static class SpellDebugText
    {
        private const string BlockPrefix = "block.";
        private const int Hundredths = 100;               // 소수 둘째 자리(단위 환산)

        // treeProjectileCap = 컴파일에 쓴 상한(출력의 "/64"). 컴파일러가 자기 규칙의 값을 준다
        public static string Print(CompiledSpell m, int treeProjectileCap)
        {
            var sb = new StringBuilder();
            if (!m.IsValid)
            {
                sb.Append("SPELL invalid");
                AppendDiagnostics(sb, m);
                return sb.ToString();
            }
            sb.Append("SPELL interval=").Append(Int(m.FireIntervalTicks)).Append('t');
            sb.Append(" heat=").Append(Hu(m.HeatPerShotHu));
            sb.Append(" count=").Append(Int(m.ExecutedCount(false))).Append('/').Append(Int(treeProjectileCap));
            sb.Append(" mirror_count=").Append(Int(m.ExecutedCount(true))).Append('/').Append(Int(treeProjectileCap));
            sb.Append(" T=").Append(Fix(m.StandardT));
            foreach (SpellScope s in m.Scopes)
            {
                sb.Append('\n').Append('S').Append(Int(s.Depth)).Append(" depth=").Append(Int(s.Depth));
                sb.Append(" decay=").Append(Fix(s.DepthMultiplier));
                if (s.Depth > 0)
                {
                    sb.Append(" trigger@").Append(Int(s.TriggerSlot));
                }
                sb.Append(" split=").Append(Int(s.SplitCount));
                if (s.SplitSlot != SpellCompilerCore.NoSlot)
                {
                    sb.Append('@').Append(Int(s.SplitSlot));
                }
                sb.Append(" heat=").Append(Hu(s.HeatHu)).Append(" inst=").Append(Int(s.InstancesTotal));
                if (s.Depth > m.MaxDepthNormal)
                {
                    sb.Append(" omitted");
                }
                foreach (CompiledSlot e in m.Slots)
                {
                    if (e.ScopeDepth != s.Depth)
                    {
                        continue;
                    }
                    switch (e.Role)
                    {
                        case SlotRole.Projectile:
                            SlotLine(sb, e, 'P');
                            if (e.Slot == s.OwnerSlot)
                            {
                                sb.Append(" *owner");
                            }
                            break;
                        case SlotRole.Modifier:
                            SlotLine(sb, e, 'M');
                            break;
                        case SlotRole.SplitIgnored:
                        case SlotRole.TriggerIgnored:
                            SlotLine(sb, e, 'C');
                            sb.Append(" ignored");
                            break;
                    }
                }
            }
            AppendDiagnostics(sb, m);
            return sb.ToString();
        }

        // spec 14 의 블록 id 에서 "block." 을 뗀 이름(글자 출력과 입력 문법이 같이 쓴다)
        public static string BlockName(BlockId id)
        {
            string full = ContentIds.ToId(id);
            return full.StartsWith(BlockPrefix, System.StringComparison.Ordinal) ? full.Substring(BlockPrefix.Length) : full;
        }

        public static string CodeName(DiagCode code)
        {
            switch (code)
            {
                case DiagCode.CoreEmpty: return "E_CORE_EMPTY";
                case DiagCode.CoreNotProjectile: return "E_CORE_NOT_PROJECTILE";
                case DiagCode.SlotCountMismatch: return "E_SLOT_COUNT_MISMATCH";
                case DiagCode.TriggerNoProjectile: return "W_TRIGGER_NO_PROJECTILE";
                case DiagCode.NoEffect: return "W_NO_EFFECT";
                case DiagCode.SplitDuplicate: return "W_SPLIT_DUPLICATE";
                case DiagCode.TriggerDepth: return "W_TRIGGER_DEPTH";
                case DiagCode.ProjectileCap: return "W_PROJECTILE_CAP";
                case DiagCode.UnknownBlock: return "E_UNKNOWN_BLOCK";
                default: return "NONE";
            }
        }

        private static void SlotLine(StringBuilder sb, CompiledSlot e, char kind)
        {
            sb.Append("\n  [").Append(Int(e.Slot)).Append("] ").Append(kind).Append(' ').Append(BlockName(e.Block));
        }

        private static void AppendDiagnostics(StringBuilder sb, CompiledSpell m)
        {
            if (m.Diagnostics.Length == 0)
            {
                sb.Append("\nDIAG none");
                return;
            }
            foreach (CompileDiagnostic d in m.Diagnostics)
            {
                sb.Append("\nDIAG ").Append(CodeName(d.Code)).Append(" slot=").Append(Int(d.Slot)).Append(" depth=").Append(Int(d.ScopeDepth));
            }
        }

        private static string Int(int v) => v.ToString(CultureInfo.InvariantCulture);

        // HU → 열, 소수 둘째 자리 반올림. 열은 0 이상이다
        private static string Hu(int hu)
        {
            long twice = (long)hu * Hundredths * 2 + HeatRules.HuPerHeat;
            return Hundredth(twice / (HeatRules.HuPerHeat * 2));
        }

        // Fix64 → 소수 둘째 자리 반올림(.5 는 올림)
        private static string Fix(Fix64 v) => Hundredth((v * Hundredths).RoundToInt());

        private static string Hundredth(long hundredths)
        {
            long whole = hundredths / Hundredths;
            long frac = hundredths % Hundredths;
            return whole.ToString(CultureInfo.InvariantCulture) + "." + frac.ToString(CultureInfo.InvariantCulture).PadLeft(2, '0');
        }
    }
}
