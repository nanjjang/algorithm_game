using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Wizard.TestKit.Shared;

namespace Wizard.ReplayCli
{
    // 두 실행을 틱마다 비교한 결과(PIPELINE 7.3 "해시가 갈라지면 처음 갈라진 틱과 상태 절을 알려 준다").
    // Tick = 처음으로 해시가 다른 world.Tick(그 앞 Step 인 Tick − 1 의 처리에서 갈라졌다)
    public sealed class DivergenceReport
    {
        public DivergenceReport(uint ticksCompared, bool diverged, uint tick, ulong hashA, ulong hashB, IReadOnlyList<SectionDifference> sections,
                                IReadOnlyList<string> sectionNames, string? note)
        {
            TicksCompared = ticksCompared;
            Diverged = diverged;
            Tick = tick;
            HashA = hashA;
            HashB = hashB;
            Sections = sections;
            SectionNames = sectionNames;
            Note = note;
        }

        // 비교한 틱 경계의 수(world.Tick == 0 포함)
        public uint TicksCompared { get; }

        public bool Diverged { get; }

        public uint Tick { get; }

        public ulong HashA { get; }

        public ulong HashB { get; }

        // 두 세계를 다 가진 비교(기록 대 기록)에서만 바이트 자리까지 있다
        public IReadOnlyList<SectionDifference> Sections { get; }

        // 다른 절의 이름(기록 대 기록, 추적 파일 대 추적 파일 모두)
        public IReadOnlyList<string> SectionNames { get; }

        // 그 밖의 사정(길이가 다르다, 그 틱에 넣은 입력이 다르다)
        public string? Note { get; }

        public override string ToString()
        {
            var sb = new StringBuilder();
            if (!Diverged)
            {
                sb.Append("same: ").Append(TicksCompared.ToString(CultureInfo.InvariantCulture)).Append(" tick boundaries compared");
            }
            else
            {
                sb.Append("diverged at tick ").Append(Tick.ToString(CultureInfo.InvariantCulture))
                  .Append(" (after the Step of tick ").Append(Tick == 0 ? "-" : (Tick - 1).ToString(CultureInfo.InvariantCulture)).Append("): ")
                  .Append(HashText.Format(HashA)).Append(" vs ").Append(HashText.Format(HashB));
                sb.Append("\nsections: ").Append(SectionNames.Count == 0 ? "(none identified)" : string.Join(", ", SectionNames));
                if (Sections.Count > 0)
                {
                    sb.Append("\ndetail: ").Append(StateImage.Describe(Sections));
                }
            }
            if (Note != null)
            {
                sb.Append("\nnote: ").Append(Note);
            }
            return sb.ToString();
        }
    }
}
