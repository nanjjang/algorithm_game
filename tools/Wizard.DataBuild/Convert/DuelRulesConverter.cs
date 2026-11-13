using System;
using System.Collections.Generic;
using System.Text.Json;
using Wizard.Content;
using Wizard.Core;
using Wizard.DataBuild.Core;

// 네임스페이스는 폴더의 앞머리(Wizard.DataBuild)다. Wizard.DataBuild.Convert 로 두면 이 어셈블리 안의 System.Convert 호출이 가려진다
namespace Wizard.DataBuild
{
    // data/sim/duel_rules.json → 팩 절 13 duel_rules(DuelRuleSet 하나 + 캐릭터마다 DuelCharacterOverride).
    // 읽는 쪽과 절 바이트의 꼴: src/Wizard.Content/Pack/Sections/ContentDb.DuelRules.cs. 스키마: data/schema/duel_rules.schema.json.
    // spec 10 2.1~2.4·3.4·3.5·7.2~7.3·9.2, spec 04 8절, world-characters 5.4.
    // 단위: 초 → 틱(× 60), 칸 → Raw(× 2^32), % → 천분율(× 10), 배율 → 천분율(× 1000), 도 → 센티도(× 100), 열 → HU(× 120).
    // 교차 검사: 방의 기본 판 수·라운드 길이가 match 와 같다(선승 = (판 + 1) / 2), round_seconds − lead_seconds = start_seconds,
    // 선택지가 겹치지 않고 기본값이 선택지·범위 안, 판 수는 홀수, 바닥 색은 낮·해질녘·밤, 야생 자리는 돌마당 안(24×14),
    // 캐릭터는 char.1~char.5 가 한 번씩.
    public sealed class DuelRulesConverter : ISectionConverter
    {
        // 돌마당 크기(칸, spec 10 3.1). 야생 자리 검사에만 쓴다. 정본은 arena.json 이고 이 값은 검사용 경계다
        private const int StoneyardWidthCells = 24;
        private const int StoneyardHeightCells = 14;

        public ContentSection Section => ContentSection.DuelRules;

        public void Convert(DataFile file, ByteWriter output, Diagnostics diagnostics)
        {
            int before = diagnostics.Count;
            string path = file.RepoPath;
            JsonElement root = file.Root;
            var w = new ByteWriter();

            // ── match ──
            JsonElement m = root.GetProperty("match");
            int players = Int(m, "players");
            int roundsToWin = Int(m, "rounds_to_win");
            int roundLength = Ticks(m, "round_seconds");
            int countdown = Ticks(m, "countdown_seconds");
            int intermission = Ticks(m, "intermission_seconds");
            int hpPerCell = Int(m, "hp_per_cell");
            int startHeat = Units.HeatToHu(m.GetProperty("round_start_heat").GetDecimal());
            int startCrystals = Int(m, "round_start_crystals");
            int overtime = Ticks(m, "overtime_seconds");
            int overtimeMult = Ratio(m, "overtime_damage_multiplier");
            if (roundLength <= 0 || overtime <= 0)
            {
                diagnostics.Error(path, roundLength <= 0 ? "/match/round_seconds" : "/match/overtime_seconds", "0 틱이 된다");
            }
            w.I32(players);
            w.I32(roundsToWin);
            w.I32(roundLength);
            w.I32(countdown);
            w.I32(intermission);
            w.I32(hpPerCell);
            w.I32(startHeat);
            w.I32(startCrystals);
            w.I32(overtime);
            w.I32(overtimeMult);

            // ── thorns ──
            JsonElement t = root.GetProperty("thorns");
            int thornStart = Ticks(t, "start_seconds");
            int thornLead = Ticks(t, "lead_seconds");
            int thornStep = Ticks(t, "step_seconds");
            int thornInterval = Ticks(t, "damage_interval_seconds");
            if (thornStep <= 0 || thornInterval <= 0)
            {
                diagnostics.Error(path, thornStep <= 0 ? "/thorns/step_seconds" : "/thorns/damage_interval_seconds", "0 틱이 된다");
            }
            if (roundLength - thornLead != thornStart)
            {
                diagnostics.Error(path, "/thorns/lead_seconds", "라운드 길이 − lead_seconds 가 start_seconds 와 다르다(spec 10 3.4, U-02)");
            }
            w.I32(thornStart);
            w.I32(thornLead);
            w.I32(thornStep);
            w.I32(thornInterval);
            w.I32(Units.PercentToPermille(t.GetProperty("damage_percent").GetDecimal()));
            w.I32(Int(t, "ring_max"));

            // ── wild ──
            JsonElement wild = root.GetProperty("wild");
            string enemyId = wild.GetProperty("enemy").GetString() ?? "";
            EnemyTypeId enemy = default;
            if (!ContentIds.TryParse(enemyId, out enemy))
            {
                diagnostics.Error(path, "/wild/enemy", "'" + enemyId + "' 는 적 id(spec 14 3.6)가 아니다");
            }
            int wildInterval = Ticks(wild, "spawn_interval_seconds");
            int wildCooldown = Ticks(wild, "contact_cooldown_seconds");
            if (wildInterval <= 0 || wildCooldown <= 0)
            {
                diagnostics.Error(path, wildInterval <= 0 ? "/wild/spawn_interval_seconds" : "/wild/contact_cooldown_seconds", "0 틱이 된다");
            }
            w.U16((ushort)enemy);
            w.I32(wildInterval);
            w.I32(Int(wild, "spawn_count"));
            w.I32(Int(wild, "contact_damage"));
            w.I32(wildCooldown);
            JsonElement points = wild.GetProperty("spawn_points");
            w.U16((ushort)points.GetArrayLength());
            int pi = 0;
            foreach (JsonElement p in points.EnumerateArray())
            {
                decimal x = p.GetProperty("x").GetDecimal();
                decimal y = p.GetProperty("y").GetDecimal();
                if (x >= StoneyardWidthCells || y >= StoneyardHeightCells)
                {
                    diagnostics.Error(path, "/wild/spawn_points/" + pi.ToString(System.Globalization.CultureInfo.InvariantCulture), "돌마당(24×14칸) 밖이다");
                }
                w.I64(Units.CellsToRaw(x));
                w.I64(Units.CellsToRaw(y));
                pi++;
            }

            // ── combat ──
            JsonElement c = root.GetProperty("combat");
            w.I32(Units.PercentToPermille(c.GetProperty("enhance_per_stage_percent").GetDecimal()));
            w.I32(Ratio(c, "element_multiplier"));
            int burstWindow = Ticks(c, "burst_window_seconds");
            if (burstWindow <= 0)
            {
                diagnostics.Error(path, "/combat/burst_window_seconds", "0 틱이 된다");
            }
            w.I32(burstWindow);
            w.I32(Units.PercentToPermille(c.GetProperty("burst_cap_percent").GetDecimal()));
            w.I32(Ticks(c, "post_hit_invuln_seconds"));
            w.I64(Units.CellsToRaw(c.GetProperty("hit_radius").GetDecimal()));
            w.I64(Units.CellsToRaw(c.GetProperty("hit_center_offset_y").GetDecimal()));
            w.I32(Ticks(c, "chain_lightning_aim_seconds"));
            w.I32(Int(c, "chain_lightning_jumps"));
            w.I32(Ratio(c, "homing_turn_multiplier"));
            w.I32(Ticks(c, "homing_seconds"));
            w.I32(Units.PercentToPermille(c.GetProperty("ice_spike_slow_percent").GetDecimal()));
            w.I32(Ticks(c, "ice_spike_slow_seconds"));
            w.I32(Units.PercentToPermille(c.GetProperty("frost_slow_percent").GetDecimal()));
            w.I32(Ticks(c, "frost_slow_seconds"));
            w.I32(Units.PercentToPermille(c.GetProperty("erosion_bonus_percent").GetDecimal()));
            w.I32(Ticks(c, "erosion_seconds"));
            w.I32(Int(c, "status_apply_delay_ticks"));
            w.I32(Int(c, "art_prep_ticks"));
            w.I32(Int(c, "bullet_cap_per_player"));
            w.I32(Ratio(c, "unstable_core_radius_multiplier"));
            w.I32(Ticks(c, "glass_wand_off_seconds"));

            // ── graze ──
            JsonElement g = root.GetProperty("graze");
            w.I32(Ticks(g, "accept_interval_seconds"));
            w.I32(Int(g, "combo"));
            w.I32(Ticks(g, "penalty_interval_seconds"));
            w.I32(Ticks(g, "penalty_window_seconds"));

            // ── aim ──
            JsonElement a = root.GetProperty("aim");
            w.I32(Units.DegreesToCenti(a.GetProperty("assist_max_degrees").GetDecimal()));
            w.I32(Units.DegreesToCenti(a.GetProperty("assist_fallback_degrees").GetDecimal()));
            w.I64(Units.CellsToRaw(a.GetProperty("aim_line_length").GetDecimal()));
            w.I32(Units.DegreesToCenti(a.GetProperty("twisted_barrel_jitter_degrees").GetDecimal()));

            // ── room ──
            JsonElement room = root.GetProperty("room");
            int[] rounds = IntChoices(room.GetProperty("rounds").GetProperty("choices"), path, "/room/rounds/choices", diagnostics);
            int defRounds = Int(room.GetProperty("rounds"), "default");
            foreach (int r in rounds)
            {
                if (r % 2 == 0)
                {
                    diagnostics.Error(path, "/room/rounds/choices", "판 수는 홀수다(선승 = (판 + 1) / 2, spec 10 2.5)");
                    break;
                }
            }
            RequireIn(rounds, defRounds, path, "/room/rounds/default", diagnostics);
            if ((defRounds + 1) / 2 != roundsToWin)
            {
                diagnostics.Error(path, "/room/rounds/default", "기본 판 수의 선승이 match.rounds_to_win 과 다르다");
            }
            int[] seconds = IntChoices(room.GetProperty("round_seconds").GetProperty("choices"), path, "/room/round_seconds/choices", diagnostics);
            int defSeconds = Int(room.GetProperty("round_seconds"), "default");
            RequireIn(seconds, defSeconds, path, "/room/round_seconds/default", diagnostics);
            if (Units.SecondsToTicks(defSeconds) != roundLength)
            {
                diagnostics.Error(path, "/room/round_seconds/default", "기본 라운드 길이가 match.round_seconds 와 다르다");
            }
            foreach (int s in seconds)
            {
                if (Units.SecondsToTicks(s) <= thornLead)
                {
                    diagnostics.Error(path, "/room/round_seconds/choices", "라운드 길이가 가시밭 lead_seconds 보다 짧다");
                    break;
                }
            }
            JsonElement hp = room.GetProperty("hp_multiplier");
            int[] hpChoices = DecimalChoices(hp.GetProperty("choices"), path, "/room/hp_multiplier/choices", diagnostics);
            int defHp = Units.RatioToPermille(hp.GetProperty("default").GetDecimal());
            RequireIn(hpChoices, defHp, path, "/room/hp_multiplier/default", diagnostics);
            int[] enhance = Range(room.GetProperty("enhance_cap"), false, path, "/room/enhance_cap", diagnostics);
            int[] il = Range(room.GetProperty("il_cap"), true, path, "/room/il_cap", diagnostics);
            bool allowMut = room.GetProperty("allow_mutations").GetProperty("default").GetBoolean();
            int[] hero = Range(room.GetProperty("hero_block_cap"), true, path, "/room/hero_block_cap", diagnostics);
            bool equalize = room.GetProperty("equalize").GetProperty("default").GetBoolean();
            bool wildOn = room.GetProperty("wild_intrusion").GetProperty("default").GetBoolean();
            bool thornsOff = room.GetProperty("thorns_off").GetProperty("default").GetBoolean();
            JsonElement floor = room.GetProperty("floor_color");
            var floors = new List<TimeOfDay>();
            int fi = 0;
            foreach (JsonElement f in floor.GetProperty("choices").EnumerateArray())
            {
                TimeOfDay tod = Floor(f.GetString() ?? "", path, "/room/floor_color/choices/" + fi.ToString(System.Globalization.CultureInfo.InvariantCulture), diagnostics);
                if (floors.Contains(tod))
                {
                    diagnostics.Error(path, "/room/floor_color/choices", "선택지가 겹친다");
                }
                floors.Add(tod);
                fi++;
            }
            TimeOfDay defFloor = Floor(floor.GetProperty("default").GetString() ?? "", path, "/room/floor_color/default", diagnostics);
            if (defFloor != TimeOfDay.None && !floors.Contains(defFloor))
            {
                diagnostics.Error(path, "/room/floor_color/default", "기본값이 선택지에 없다");
            }
            JsonElement spec = room.GetProperty("spectate");

            WriteList(w, rounds);
            w.I32(defRounds);
            WriteList(w, seconds);
            w.I32(defSeconds);
            WriteList(w, hpChoices);
            w.I32(defHp);
            WriteRange(w, enhance);
            WriteRange(w, il);
            w.U8(allowMut ? (byte)1 : (byte)0);
            WriteRange(w, hero);
            w.U8(equalize ? (byte)1 : (byte)0);
            w.U8(wildOn ? (byte)1 : (byte)0);
            w.U8(thornsOff ? (byte)1 : (byte)0);
            floors.Sort();          // 열거형 값 오름차순(정규 순서). 겹침은 위에서 오류다
            w.U16((ushort)floors.Count);
            foreach (TimeOfDay f in floors)
            {
                w.U8((byte)f);
            }
            w.U8((byte)defFloor);
            w.U8(spec.GetProperty("default").GetBoolean() ? (byte)1 : (byte)0);
            w.I32(Int(spec, "max_spectators"));

            // ── characters: CharacterId 오름차순, char.1~char.5 한 번씩 ──
            var rows = new SortedDictionary<byte, JsonElement>();
            int ci = 0;
            foreach (JsonElement row in root.GetProperty("characters").EnumerateArray())
            {
                string at = "/characters/" + ci.ToString(System.Globalization.CultureInfo.InvariantCulture);
                string cid = row.GetProperty("character").GetString() ?? "";
                if (!ContentIds.TryParse(cid, out CharacterId ch) || ch == CharacterId.Baseline)
                {
                    diagnostics.Error(path, at + "/character", "'" + cid + "' 는 캐릭터 id(char.1~char.5)가 아니다");
                }
                else if (rows.ContainsKey((byte)ch))
                {
                    diagnostics.Error(path, at + "/character", "'" + cid + "' 가 두 번 있다");
                }
                else
                {
                    rows.Add((byte)ch, row);
                }
                ci++;
            }
            foreach (CharacterId ch in (CharacterId[])Enum.GetValues(typeof(CharacterId)))
            {
                if (ch != CharacterId.Baseline && !rows.ContainsKey((byte)ch))
                {
                    diagnostics.Error(path, "/characters", "'" + ContentIds.ToId(ch) + "' 의 결투 값이 없다(spec 10 7.2)");
                }
            }
            w.U16((ushort)rows.Count);
            foreach (KeyValuePair<byte, JsonElement> kv in rows)
            {
                JsonElement row = kv.Value;
                w.U8(kv.Key);
                w.I32(Int(row, "penalty_crystal_loss"));
                w.I32(Int(row, "penalties_per_crystal_loss"));
                bool has = row.TryGetProperty("overflow", out JsonElement o);
                w.I32(has ? Ratio(o, "damage") : 0);
                w.I64(has ? Units.CellsToRaw(o.GetProperty("radius").GetDecimal()) : 0);
                w.I64(has ? Units.CellsToRaw(o.GetProperty("knockback").GetDecimal()) : 0);
                w.U8(has && o.GetProperty("clears_bullets").GetBoolean() ? (byte)1 : (byte)0);
                has = row.TryGetProperty("lightning_rod", out o);
                w.I32(has ? Ratio(o, "damage") : 0);
                w.I32(has ? Ticks(o, "cooldown_seconds") : 0);
                w.I32(has ? Ticks(o, "aim_seconds") : 0);
                w.I64(has ? Units.CellsToRaw(o.GetProperty("range").GetDecimal()) : 0);
                w.I64(has ? Units.CellsToRaw(o.GetProperty("marker_radius").GetDecimal()) : 0);
                has = row.TryGetProperty("thin_ice", out o);
                w.I32(has ? Int(o, "hits") : 0);
                w.I32(has ? Units.PercentToPermille(o.GetProperty("slow_percent").GetDecimal()) : 0);
                w.I32(has ? Ticks(o, "slow_seconds") : 0);
                w.I32(has ? Ticks(o, "immune_seconds") : 0);
                w.U8(has && o.GetProperty("freezes").GetBoolean() ? (byte)1 : (byte)0);
                has = row.TryGetProperty("shadow_contract", out o);
                w.I32(has ? Units.PercentToPermille(o.GetProperty("per_crystal_percent").GetDecimal()) : 0);
                has = row.TryGetProperty("on_credit", out o);
                w.I32(has ? Int(o, "penalty_extra_cells") : 0);
            }

            if (diagnostics.Count != before)
            {
                return;
            }
            output.Bytes(w.Written);
        }

        // 스키마의 "integer"는 6.0 도 받으므로 decimal 로 읽어 정수인지 본다
        private static int Int(JsonElement o, string name)
        {
            decimal v = o.GetProperty(name).GetDecimal();
            if (v != decimal.Truncate(v) || v < int.MinValue || v > int.MaxValue)
            {
                throw new FormatException(name + " 은 정수가 아니다");
            }
            return (int)v;
        }

        private static int Ticks(JsonElement o, string name) => Units.SecondsToTicks(o.GetProperty(name).GetDecimal());

        private static int Ratio(JsonElement o, string name) => Units.RatioToPermille(o.GetProperty(name).GetDecimal());

        private static int[] IntChoices(JsonElement arr, string path, string at, Diagnostics d)
        {
            var list = new List<int>();
            foreach (JsonElement e in arr.EnumerateArray())
            {
                decimal v = e.GetDecimal();
                list.Add(v == decimal.Truncate(v) && v >= 0 && v <= int.MaxValue ? (int)v : int.MinValue);
            }
            return Ascending(list, path, at, d);
        }

        private static int[] DecimalChoices(JsonElement arr, string path, string at, Diagnostics d)
        {
            var list = new List<int>();
            foreach (JsonElement e in arr.EnumerateArray())
            {
                list.Add(Units.RatioToPermille(e.GetDecimal()));
            }
            return Ascending(list, path, at, d);
        }

        // 선택지는 원본 그대로 오름차순이어야 한다(겹침 없음). 정렬하지 않는다: 화면에 보이는 순서가 이 순서다
        private static int[] Ascending(List<int> list, string path, string at, Diagnostics d)
        {
            for (int i = 1; i < list.Count; i++)
            {
                if (list[i] <= list[i - 1])
                {
                    d.Error(path, at, "선택지는 겹치지 않는 오름차순이다");
                    break;
                }
            }
            return list.ToArray();
        }

        private static void RequireIn(int[] choices, int value, string path, string at, Diagnostics d)
        {
            if (Array.IndexOf(choices, value) < 0)
            {
                d.Error(path, at, "기본값이 선택지에 없다");
            }
        }

        // [min, max, default]. default 가 null 이면 NoCap(nullable 일 때만)
        private static int[] Range(JsonElement o, bool nullable, string path, string at, Diagnostics d)
        {
            int min = Int(o, "min");
            int max = Int(o, "max");
            JsonElement def = o.GetProperty("default");
            int value;
            if (def.ValueKind == JsonValueKind.Null)
            {
                if (!nullable)
                {
                    d.Error(path, at + "/default", "'없음'을 고를 수 없는 옵션이다");
                }
                value = DuelRuleSet.RoomRules.NoCap;
            }
            else
            {
                value = Int(o, "default");
                if (value < min || value > max)
                {
                    d.Error(path, at + "/default", "기본값이 min~max 밖이다");
                }
            }
            if (min > max)
            {
                d.Error(path, at + "/min", "min 이 max 보다 크다");
            }
            return new[] { min, max, value };
        }

        private static TimeOfDay Floor(string id, string path, string at, Diagnostics d)
        {
            if (!ContentIds.TryParse(id, out TimeOfDay tod) || tod == TimeOfDay.None || tod == TimeOfDay.BossWave)
            {
                d.Error(path, at, "'" + id + "' 는 바닥 색(tod.day / tod.dusk / tod.night)이 아니다");
                return TimeOfDay.None;
            }
            return tod;
        }

        private static void WriteList(ByteWriter w, int[] values)
        {
            w.U16((ushort)values.Length);
            foreach (int v in values)
            {
                w.I32(v);
            }
        }

        private static void WriteRange(ByteWriter w, int[] r)
        {
            w.I32(r[0]);
            w.I32(r[1]);
            w.I32(r[2]);
        }
    }
}
