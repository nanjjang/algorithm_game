using System.Text.Json;
using Wizard.Content;
using Wizard.Core;
using Wizard.DataBuild.Core;

// 네임스페이스는 폴더의 앞머리(Wizard.DataBuild)다. Wizard.DataBuild.Convert 로 두면 이 어셈블리 안의 System.Convert 호출(Names/CSharpLexer.cs)이 가려진다
namespace Wizard.DataBuild
{
    // data/sim/starfall.json → 팩 절 15 starfall(StarfallDef 하나). 읽는 쪽과 절 바이트의 꼴: src/Wizard.Content/Pack/Sections/ContentDb.Starfall.cs.
    // 스키마: data/schema/starfall.schema.json. world-characters 2.3, spec 01 10.5(원문 + [임시] 표), spec 01 2.3.
    // 단위: % → 만분율(× 100), 초 → 틱(× 60), 칸 → Raw(× 2^32).
    // 교차 검사: 시간대는 tod.boss 가 아니다(보스 웨이브는 하루 속 순번 10, 별똥은 밤 8~9), 떨어지는 때 from ≤ to, 깜빡임 ≤ 놓여 있는 시간.
    public sealed class StarfallConverter : ISectionConverter
    {
        public ContentSection Section => ContentSection.Starfall;

        public void Convert(DataFile file, ByteWriter output, Diagnostics diagnostics)
        {
            int before = diagnostics.Count;
            JsonElement root = file.Root;

            string t = root.GetProperty("only_tod").GetString() ?? "";
            if (!ContentIds.TryParse(t, out TimeOfDay tod) || tod == TimeOfDay.None)
            {
                diagnostics.Error(file.RepoPath, "/only_tod", "'" + t + "' 는 시간대 id(spec 14 3.9)가 아니다");
            }
            else if (tod == TimeOfDay.BossWave)
            {
                diagnostics.Error(file.RepoPath, "/only_tod", "별똥은 보스 웨이브에 두지 않는다(spec 01 10.5: 밤 웨이브 8~9)");
            }

            int chanceBp = Units.PercentToBp(root.GetProperty("chance_percent").GetDecimal());
            JsonElement window = root.GetProperty("drop_window_seconds");
            int dropFrom = Units.SecondsToTicks(window.GetProperty("from").GetDecimal());
            int dropTo = Units.SecondsToTicks(window.GetProperty("to").GetDecimal());
            if (dropFrom > dropTo)
            {
                diagnostics.Error(file.RepoPath, "/drop_window_seconds/from", "떨어지는 때의 from 이 to 보다 늦다");
            }
            int telegraph = Units.SecondsToTicks(root.GetProperty("telegraph_seconds").GetDecimal());
            int landed = Units.SecondsToTicks(root.GetProperty("landed_seconds").GetDecimal());
            int blink = Units.SecondsToTicks(root.GetProperty("blink_seconds").GetDecimal());
            if (telegraph <= 0 || landed <= 0)
            {
                diagnostics.Error(file.RepoPath, telegraph <= 0 ? "/telegraph_seconds" : "/landed_seconds", "0 틱이 된다");
            }
            if (blink > landed)
            {
                diagnostics.Error(file.RepoPath, "/blink_seconds", "깜빡이는 시간이 놓여 있는 시간보다 길다");
            }
            JsonElement area = root.GetProperty("area");
            long width = Units.CellsToRaw(area.GetProperty("width").GetDecimal());
            long height = Units.CellsToRaw(area.GetProperty("height").GetDecimal());
            long step = Units.CellsToRaw(root.GetProperty("grid_step").GetDecimal());
            long hazard = Units.CellsToRaw(root.GetProperty("hazard_clearance").GetDecimal());
            long player = Units.CellsToRaw(root.GetProperty("player_clearance").GetDecimal());
            int retry = Units.SecondsToTicks(root.GetProperty("retry_seconds").GetDecimal());
            if (retry <= 0)
            {
                diagnostics.Error(file.RepoPath, "/retry_seconds", "0 틱이 된다");
            }
            long pick = Units.CellsToRaw(root.GetProperty("pick_radius").GetDecimal());
            int crystals = WavesConverter.Int(root, "crystals");
            if (diagnostics.Count != before)
            {
                return;
            }

            output.U8((byte)tod);
            output.I32(chanceBp);
            output.I32(dropFrom);
            output.I32(dropTo);
            output.I32(telegraph);
            output.I32(landed);
            output.I32(blink);
            output.I64(width);
            output.I64(height);
            output.I64(step);
            output.I64(hazard);
            output.I64(player);
            output.I32(retry);
            output.I64(pick);
            output.I32(crystals);
        }
    }
}
