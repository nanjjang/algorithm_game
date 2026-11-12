using System.Globalization;
using System.Text.Json;
using Wizard.Content;
using Wizard.DataBuild.Core;

namespace Wizard.DataBuild.Validate.Evasion
{
    // data/sim/enemies.json(spec 05 2.2·2.3, 스키마 enemies.schema.json)의 적과 공격을 검산 대상으로 옮긴다.
    // 꼴은 스키마가 이미 보았다. 값이 틀린 줄(알 수 없는 id 등)은 변환기(EnemiesConverter)가 알리므로 여기서는 읽을 수 있는 것만 옮긴다.
    // 시간은 변환기와 같이 Units.SecondsToTicks 로 틱이 된 뒤의 값을 본다(실제로 돌 예고 길이).
    public sealed class EnemiesEvasionSource : IEvasionSource
    {
        public const string EnemiesFile = "sim/enemies.json";

        public void Collect(DataSet data, EvasionInput input, Diagnostics diagnostics)
        {
            DataFile? file = null;
            foreach (DataFile f in data.Files)
            {
                if (f.DataPath == EnemiesFile)
                {
                    file = f;
                }
            }
            if (file == null || !file.Root.TryGetProperty("enemies", out JsonElement enemies) || enemies.ValueKind != JsonValueKind.Array)
            {
                return;
            }
            int i = 0;
            foreach (JsonElement row in enemies.EnumerateArray())
            {
                string at = "/enemies/" + i.ToString(CultureInfo.InvariantCulture);
                string id = Str(row, "id");
                input.Movers.Add(new EvasionMover
                {
                    File = file.RepoPath,
                    Pointer = at,
                    Owner = id,
                    ContactDamage = Int(row, "contact_damage"),
                    MoveSpeedMilli = EvasionMath.CeilMilli(Dec(row, "move_speed")),
                    IsBoss = false,
                });
                if (row.TryGetProperty("attacks", out JsonElement attacks) && attacks.ValueKind == JsonValueKind.Array)
                {
                    int j = 0;
                    foreach (JsonElement ar in attacks.EnumerateArray())
                    {
                        EvasionAttack? a = ReadAttack(ar);
                        if (a != null)
                        {
                            a.File = file.RepoPath;
                            a.Pointer = at + "/attacks/" + j.ToString(CultureInfo.InvariantCulture);
                            a.Owner = id;
                            input.Attacks.Add(a);
                        }
                        j++;
                    }
                }
                i++;
            }
        }

        public static EvasionAttack? ReadAttack(JsonElement ar)
        {
            if (!TryKind(Str(ar, "kind"), out AttackDef.AttackKind kind))
            {
                return null;
            }
            var a = new EvasionAttack
            {
                Key = Str(ar, "key"),
                Kind = kind,
                Radius = Dec(ar, "radius"),
                InnerRadius = Dec(ar, "inner_radius"),
                Width = Dec(ar, "width"),
                FanDegrees = Dec(ar, "fan_degrees"),
                Lock = Str(ar, "lock") == "last_n_ticks" ? AttackDef.LockMode.LastNTicks : AttackDef.LockMode.AtStart,
            };
            string shape = Str(ar, "shape");
            if (shape.Length > 0)
            {
                if (!TryShape(shape, out AttackDef.TelegraphShape s))
                {
                    return null;
                }
                a.Shape = s;
            }
            try
            {
                a.TelegraphTicks = Units.SecondsToTicks(Dec(ar, "telegraph_seconds"));
                a.LockTicks = Units.SecondsToTicks(Dec(ar, "lock_seconds"));
            }
            catch (DataBuildException)
            {
                return null;                          // 변환기가 같은 값을 알린다
            }
            if (ar.TryGetProperty("escape_override_milli", out JsonElement eo) && eo.ValueKind == JsonValueKind.Number)
            {
                a.EscapeOverrideMilli = eo.GetInt32();
            }
            if (ar.TryGetProperty("proj", out JsonElement pj) && pj.ValueKind == JsonValueKind.Object)
            {
                a.Proj = new EvasionProj
                {
                    SpeedMilli = EvasionMath.CeilMilli(Dec(pj, "speed")),
                    Count = Int(pj, "count"),
                    SpreadDegrees = Dec(pj, "spread_degrees"),
                };
            }
            return a;
        }

        public static bool TryKind(string s, out AttackDef.AttackKind kind)
        {
            switch (s)
            {
                case "contact": kind = AttackDef.AttackKind.Contact; return true;
                case "dash": kind = AttackDef.AttackKind.Dash; return true;
                case "zone": kind = AttackDef.AttackKind.Zone; return true;
                case "aimed_shot": kind = AttackDef.AttackKind.AimedShot; return true;
                case "spread": kind = AttackDef.AttackKind.Spread; return true;
                case "ring_shot": kind = AttackDef.AttackKind.RingShot; return true;
                case "lob": kind = AttackDef.AttackKind.Lob; return true;
                case "self_destruct": kind = AttackDef.AttackKind.SelfDestruct; return true;
                case "tongue": kind = AttackDef.AttackKind.Tongue; return true;
                default: kind = default; return false;
            }
        }

        public static bool TryShape(string s, out AttackDef.TelegraphShape shape)
        {
            switch (s)
            {
                case "circle": shape = AttackDef.TelegraphShape.Circle; return true;
                case "band": shape = AttackDef.TelegraphShape.Band; return true;
                case "fan": shape = AttackDef.TelegraphShape.Fan; return true;
                case "aim_line": shape = AttackDef.TelegraphShape.AimLine; return true;
                case "ring": shape = AttackDef.TelegraphShape.Ring; return true;
                default: shape = default; return false;
            }
        }

        private static string Str(JsonElement o, string name) =>
            o.TryGetProperty(name, out JsonElement e) && e.ValueKind == JsonValueKind.String ? e.GetString() ?? "" : "";

        private static decimal Dec(JsonElement o, string name) =>
            o.TryGetProperty(name, out JsonElement e) && e.ValueKind == JsonValueKind.Number ? e.GetDecimal() : 0m;

        private static int Int(JsonElement o, string name) =>
            o.TryGetProperty(name, out JsonElement e) && e.ValueKind == JsonValueKind.Number && e.TryGetInt32(out int v) ? v : 0;
    }
}
