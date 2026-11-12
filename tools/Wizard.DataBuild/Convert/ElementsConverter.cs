using System.Globalization;
using Wizard.Content;
using Wizard.Core;
using Wizard.DataBuild.Core;

// 네임스페이스는 폴더의 앞머리(Wizard.DataBuild)다. Wizard.DataBuild.Convert 로 두면 이 어셈블리 안의 System.Convert 호출(Names/CSharpLexer.cs)이 가려진다
namespace Wizard.DataBuild
{
    // data/sim/elements.json → 팩 절 19 elements. 읽는 쪽과 절 바이트의 꼴: src/Wizard.Content/Pack/Sections/ContentDb.Elements.cs.
    // 스키마: data/schema/elements.schema.json(꼴과 범위는 스키마가 이미 보았다).
    //   weak_multiplier → WeakMultiplier(Fix64 Raw, × 2^32), resist_multiplier → ResistMultiplier. 반올림은 가장 가까운 정수, 같으면 짝수(CONTRACTS 0.3).
    // 교차 검사: 약점 배율 > 1 > 저항 배율(DECISIONS 2.5, spec 02 3.3 AF-1).
    public sealed class ElementsConverter : ISectionConverter
    {
        public ContentSection Section => ContentSection.Elements;

        public void Convert(DataFile file, ByteWriter output, Diagnostics diagnostics)
        {
            long weak = FixRaw(file.Root.GetProperty("weak_multiplier").GetDecimal());
            long resist = FixRaw(file.Root.GetProperty("resist_multiplier").GetDecimal());
            int before = diagnostics.Count;
            if (weak <= Fix64.OneRaw)
            {
                diagnostics.Error(file.RepoPath, "/weak_multiplier", "약점 배율은 1 보다 커야 한다(spec 02 3.3 AF-1)");
            }
            if (resist >= Fix64.OneRaw || resist <= 0)
            {
                diagnostics.Error(file.RepoPath, "/resist_multiplier", "저항 배율은 0 보다 크고 1 보다 작아야 한다(spec 02 3.3 AF-1)");
            }
            if (diagnostics.Count != before)
            {
                return;
            }
            output.I64(weak);
            output.I64(resist);
        }

        private static long FixRaw(decimal v)
        {
            decimal raw = Units.RoundHalfEven(v, Fix64.OneRaw, 1);
            if (raw < long.MinValue || raw > long.MaxValue)
            {
                throw new DataBuildException("값 " + v.ToString(CultureInfo.InvariantCulture) + " 을 바꾼 Raw 가 64비트 범위를 넘는다");
            }
            return (long)raw;
        }
    }
}
