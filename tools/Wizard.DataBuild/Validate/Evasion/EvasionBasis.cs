using System.Text.Json;
using Wizard.DataBuild.Core;

namespace Wizard.DataBuild.Validate.Evasion
{
    // 회피 검산의 기준: V = 가장 느린 캐릭터의 자세 속도, 피격 반지름(spec 05 1.2 "캐릭터 데이터에서 min(stance_speed)로 계산하고 하드코딩하지 않는다").
    // data/sim/characters.json 의 characters 배열(캐릭터 다섯)에서 구한다. 그 배열이 아직 없으면(캐릭터 줄은 M5-DAT-01 이 더한다) 기준값(baseline) 줄로 구한다.
    // V 는 내림, 피격 반지름은 여러 줄 가운데 가장 큰 값을 올림한다(검사를 엄하게).
    public sealed class EvasionBasis
    {
        public const string CharactersFile = "sim/characters.json";

        public EvasionBasis(int speedMilli, int hitMilli, string source)
        {
            SpeedMilli = speedMilli;
            HitMilli = hitMilli;
            Source = source;
        }

        public int SpeedMilli { get; }

        public int HitMilli { get; }

        // 어디서 구했는가(진단용. "characters" 또는 "baseline")
        public string Source { get; }

        public static bool HasCharactersFile(DataSet data)
        {
            foreach (DataFile f in data.Files)
            {
                if (f.DataPath == CharactersFile)
                {
                    return true;
                }
            }
            return false;
        }

        // 캐릭터 파일이 없거나 V 를 구할 줄이 없으면 null
        public static EvasionBasis? Read(DataSet data)
        {
            foreach (DataFile f in data.Files)
            {
                if (f.DataPath == CharactersFile)
                {
                    return FromJson(f.Root);
                }
            }
            return null;
        }

        public static EvasionBasis? FromJson(JsonElement root)
        {
            if (root.ValueKind != JsonValueKind.Object)
            {
                return null;
            }
            if (root.TryGetProperty("characters", out JsonElement chars) && chars.ValueKind == JsonValueKind.Array && chars.GetArrayLength() > 0)
            {
                int v = int.MaxValue;
                int hit = 0;
                foreach (JsonElement c in chars.EnumerateArray())
                {
                    if (!TryRow(c, out int cv, out int ch))
                    {
                        return null;
                    }
                    v = cv < v ? cv : v;
                    hit = ch > hit ? ch : hit;
                }
                return new EvasionBasis(v, hit, "characters");
            }
            if (root.TryGetProperty("baseline", out JsonElement b) && TryRow(b, out int bv, out int bh))
            {
                return new EvasionBasis(bv, bh, "baseline");
            }
            return null;
        }

        private static bool TryRow(JsonElement row, out int speedMilli, out int hitMilli)
        {
            speedMilli = 0;
            hitMilli = 0;
            if (row.ValueKind != JsonValueKind.Object
                || !row.TryGetProperty("stance_speed", out JsonElement s) || s.ValueKind != JsonValueKind.Number
                || !row.TryGetProperty("hit_radius", out JsonElement h) || h.ValueKind != JsonValueKind.Number)
            {
                return false;
            }
            speedMilli = EvasionMath.FloorMilli(s.GetDecimal());
            hitMilli = EvasionMath.CeilMilli(h.GetDecimal());
            return speedMilli > 0;
        }
    }
}
