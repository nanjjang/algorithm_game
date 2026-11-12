using System;
using Wizard.Core;

namespace Wizard.Content
{
    // 팩 절 19 elements(CONTRACTS 11.2)를 읽는다. 쓰는 쪽: tools/Wizard.DataBuild/Convert/ElementsConverter.cs. 원본: data/sim/elements.json.
    // 절 바이트(LE, 모든 수는 이미 정수). 항목 하나짜리 절이다:
    //   i64 weak_multiplier_raw   i64 resist_multiplier_raw        (Fix64 Raw. 1.25 → 5,368,709,120, 0.8 → 3,435,973,837)
    // 깨진 바이트(모자람, 남음, 약점 ≤ 1, 저항 ≥ 1 또는 ≤ 0)는 ContentException.
    public sealed partial class ContentDb
    {
        private ElementDef? _elementRules;

        partial void LoadElements(ReadOnlySpan<byte> section)
        {
            var r = new ByteReader(section);
            if (!r.TryI64(out long weak) || !r.TryI64(out long resist))
            {
                throw ElementsBroken("truncated entry");
            }
            if (r.Remaining != 0)
            {
                throw ElementsBroken("trailing bytes");
            }
            // 약점 > 1 > 저항 > 0 이 아니면 상성이 뒤집힌다
            if (weak <= Fix64.OneRaw || resist >= Fix64.OneRaw || resist <= 0)
            {
                throw ElementsBroken("multipliers out of range (need weak > 1 > resist > 0)");
            }
            _elementRules = new ElementDef { WeakMultiplier = Fix64.FromRaw(weak), ResistMultiplier = Fix64.FromRaw(resist) };
        }

        partial void FindElementRules(ref ElementDef? def)
        {
            def = _elementRules;
        }

        private static ContentException ElementsBroken(string what) => new ContentException("content section 'elements': " + what);
    }
}
