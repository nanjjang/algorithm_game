using System.Text.Json;
using Wizard.Content;
using Wizard.Core;
using Wizard.DataBuild.Core;

// 네임스페이스는 폴더의 앞머리(Wizard.DataBuild)다. Wizard.DataBuild.Convert 로 두면 이 어셈블리 안의 System.Convert 호출(Names/CSharpLexer.cs)이 가려진다
namespace Wizard.DataBuild
{
    // data/sim/scoring.json → 팩 절 11 scoring(ScoringDef 하나). 읽는 쪽과 절 바이트의 꼴: src/Wizard.Content/Pack/Sections/ContentDb.Scoring.cs.
    // 스키마: data/schema/scoring.schema.json. spec 07 12.1~12.2.
    // 단위: 웨이브 계수 S(w)의 두 값 → 10배 정수(spec 07 0.2 "S10(w) = 9 + w", 정확해야 한다), 초 → 틱(× 60). 나머지는 정수 원점수 그대로.
    public sealed class ScoringConverter : ISectionConverter
    {
        public ContentSection Section => ContentSection.Scoring;

        private const long TenthsPerUnit = 10;              // S10 = S × 10

        public void Convert(DataFile file, ByteWriter output, Diagnostics diagnostics)
        {
            int before = diagnostics.Count;
            JsonElement root = file.Root;
            JsonElement coef = root.GetProperty("wave_coef");
            int atWave1 = (int)WavesConverter.Exact(coef, "at_wave_1", "/wave_coef", TenthsPerUnit, file, diagnostics);
            int perWave = (int)WavesConverter.Exact(coef, "per_wave", "/wave_coef", TenthsPerUnit, file, diagnostics);
            JsonElement fast = root.GetProperty("fast_clear");
            JsonElement graze = root.GetProperty("graze");
            if (diagnostics.Count != before)
            {
                return;
            }
            output.I32(atWave1);
            output.I32(perWave);
            output.I32(WavesConverter.Int(root, "kill_per_cost"));
            output.I32(WavesConverter.Int(root, "boss_kill"));
            output.I32(WavesConverter.Int(root, "wave_clear"));
            output.I32(WavesConverter.Int(fast, "per_second"));
            output.I32(Units.SecondsToTicks(fast.GetProperty("target_seconds").GetDecimal()));
            output.I32(Units.SecondsToTicks(fast.GetProperty("boss_target_seconds").GetDecimal()));
            output.I32(WavesConverter.Int(root, "no_hit"));
            output.I32(WavesConverter.Int(graze, "per_graze"));
            output.I32(WavesConverter.Int(graze, "cap"));
            output.I32(WavesConverter.Int(graze, "boss_cap"));
            output.I32(WavesConverter.Int(root, "phantom_first"));
        }
    }
}
