using System.Globalization;
using System.Text;

namespace Wizard.Solo.Sim.Spells
{
    // 주문 트리의 디버그 글자 출력. 10 요약서 9절(S02 14.5) 형식, id는 정본 id.
    public sealed partial class SpellSystem
    {
        /// <summary>
        /// 형식(소수 둘째 자리 반올림, 줄 사이는 '\n'):
        /// <code>
        /// SPELL interval=9t heat=26.80 count=6/64 mirror_count=12/64 T=30.00
        /// S0 depth=0 decay=1.00 split=3@1 heat=8.80 inst=3
        ///   [0] P block.magic_bolt *owner
        /// S1 depth=1 decay=0.85 trigger@2 split=1 heat=18.00 inst=3
        ///   [3] P block.fireball *owner
        /// DIAG none
        /// </code>
        /// count = 가지치기 전 투사체 수 / 트리 1회 상한, T = 표준 T의 바탕(강화·변이 전). 변형은 "M", 무시된 분열·트리거는 "C … ignored".
        /// 상한은 이 트리를 컴파일한 콘텐츠의 값이다.
        /// </summary>
        public override string DebugDump(CompiledSpell spell)
        {
            var sb = new StringBuilder();
            if (spell == null) return "SPELL none";
            if (!spell.IsValid || spell.Scopes.Count == 0)
            {
                sb.Append("SPELL invalid");
                AppendDiagnostics(sb, spell);
                return sb.ToString();
            }

            string cap = spell.TreeShotCap > 0 && spell.TreeShotCap != int.MaxValue
                ? spell.TreeShotCap.ToString(CultureInfo.InvariantCulture) : "?";
            sb.Append("SPELL interval=").Append(spell.FireIntervalTicks.ToString(CultureInfo.InvariantCulture)).Append('t');
            sb.Append(" heat=").Append(F2(spell.HeatPerShotHu / (float)SimConst.HuPerHeat));
            sb.Append(" count=").Append(spell.InstancesTotal.ToString(CultureInfo.InvariantCulture)).Append('/').Append(cap);
            sb.Append(" mirror_count=").Append(spell.InstancesTotalMirrored.ToString(CultureInfo.InvariantCulture)).Append('/').Append(cap);
            sb.Append(" T=").Append(F2(spell.StandardTBase));

            var ids = spell.SlotBlockIds;
            for (int d = 0; d < spell.Scopes.Count; d++)
            {
                var s = spell.Scopes[d];
                sb.Append('\n').Append('S').Append(d.ToString(CultureInfo.InvariantCulture));
                sb.Append(" depth=").Append(s.Depth.ToString(CultureInfo.InvariantCulture));
                sb.Append(" decay=").Append(F2(s.DepthMultiplier));
                if (s.TriggerSlot >= 0) sb.Append(" trigger@").Append(s.TriggerSlot.ToString(CultureInfo.InvariantCulture));
                sb.Append(" split=").Append(s.SplitCount.ToString(CultureInfo.InvariantCulture));
                if (s.SplitSlot >= 0) sb.Append('@').Append(s.SplitSlot.ToString(CultureInfo.InvariantCulture));
                sb.Append(" heat=").Append(F2(s.HeatHu / (float)SimConst.HuPerHeat));
                sb.Append(" inst=").Append(s.InstancesTotal.ToString(CultureInfo.InvariantCulture));

                // 이 스코프의 슬롯 범위: 연 트리거 다음 칸부터 다음 스코프의 트리거 앞 칸까지.
                int firstSlot = s.TriggerSlot < 0 ? 0 : s.TriggerSlot + 1;
                int lastSlot = d + 1 < spell.Scopes.Count ? spell.Scopes[d + 1].TriggerSlot - 1 : (ids != null ? ids.Length - 1 : -1);
                for (int slot = firstSlot; ids != null && slot <= lastSlot && slot < ids.Length; slot++)
                {
                    string id = ids[slot];
                    if (string.IsNullOrEmpty(id) || slot == s.SplitSlot) continue;
                    sb.Append('\n').Append("  [").Append(slot.ToString(CultureInfo.InvariantCulture)).Append("] ");
                    if (IsProjectileSlot(s, slot))
                    {
                        sb.Append("P ").Append(id);
                        if (slot == s.OwnerSlot) sb.Append(" *owner");
                    }
                    else if (HasDiagnostic(spell, SpellDiagCode.W_SPLIT_DUPLICATE, slot) || HasDiagnostic(spell, SpellDiagCode.W_TRIGGER_DEPTH, slot))
                    {
                        sb.Append("C ").Append(id).Append(" ignored");
                    }
                    else
                    {
                        sb.Append("M ").Append(id);
                    }
                }
            }
            AppendDiagnostics(sb, spell);
            return sb.ToString();
        }

        static void AppendDiagnostics(StringBuilder sb, CompiledSpell spell)
        {
            if (spell.Diagnostics.Count == 0)
            {
                sb.Append('\n').Append("DIAG none");
                return;
            }
            for (int i = 0; i < spell.Diagnostics.Count; i++)
            {
                var g = spell.Diagnostics[i];
                sb.Append('\n').Append("DIAG ").Append(g.Code.ToString()).Append('@').Append(g.Slot.ToString(CultureInfo.InvariantCulture));
            }
        }

        static bool IsProjectileSlot(SpellScope s, int slot)
        {
            for (int i = 0; i < s.Projectiles.Count; i++) if (s.Projectiles[i].Slot == slot) return true;
            return false;
        }

        static bool HasDiagnostic(CompiledSpell spell, SpellDiagCode code, int slot)
        {
            for (int i = 0; i < spell.Diagnostics.Count; i++)
                if (spell.Diagnostics[i].Code == code && spell.Diagnostics[i].Slot == slot) return true;
            return false;
        }

        static string F2(float v) => v.ToString("0.00", CultureInfo.InvariantCulture);
    }
}
