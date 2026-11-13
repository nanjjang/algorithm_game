using System;
using Wizard.Core;

namespace Wizard.Content
{
    // 팩 절 13 duel_rules(CONTRACTS 11.2)를 읽는다. 쓰는 쪽: tools/Wizard.DataBuild/Convert/DuelRulesConverter.cs. 원본: data/sim/duel_rules.json.
    // 절 바이트(LE, 모든 수는 이미 정수. 필드의 뜻은 DuelRuleSet·DuelCharacterOverride. 아래 순서가 그 필드 순서다):
    //   match   i32 × 10   players rounds_to_win round_length_ticks countdown_ticks intermission_ticks hp_per_cell
    //                      round_start_heat_hu round_start_crystals overtime_ticks overtime_damage_permille
    //   thorns  i32 × 6    start_ticks lead_ticks step_ticks damage_interval_ticks damage_permille ring_max
    //   wild    u16 enemy  i32 spawn_interval_ticks  i32 spawn_count  i32 contact_damage  i32 contact_cooldown_ticks
    //           u16 n + n × (i64 x_raw, i64 y_raw)   spawn_points
    //   combat  i32 enhance_per_stage_permille  i32 element_mult_permille  i32 burst_window_ticks  i32 burst_cap_permille
    //           i32 post_hit_invuln_ticks  i64 hit_radius_raw  i64 hit_center_offset_y_raw  i32 chain_aim_ticks  i32 chain_jumps
    //           i32 homing_turn_permille  i32 homing_ticks  i32 ice_spike_slow_permille  i32 ice_spike_slow_ticks
    //           i32 frost_slow_permille  i32 frost_slow_ticks  i32 erosion_bonus_permille  i32 erosion_ticks
    //           i32 status_apply_delay_ticks  i32 art_prep_ticks  i32 bullet_cap_per_player  i32 unstable_core_radius_permille
    //           i32 glass_wand_off_ticks
    //   graze   i32 × 4    accept_ticks combo penalty_interval_ticks penalty_window_ticks
    //   aim     i32 assist_max_centi  i32 assist_fallback_centi  i64 aim_line_length_raw  i32 twisted_barrel_jitter_centi
    //   room    u16 n + n × i32 rounds, i32 default         (오름차순, 홀수, 기본값은 선택지 안)
    //           u16 n + n × i32 round_seconds, i32 default  (오름차순, 기본값은 선택지 안)
    //           u16 n + n × i32 hp_mult_permille, i32 default
    //           i32 enhance_cap_min max default             (min ≤ default ≤ max)
    //           i32 il_cap_min max default                  (default 는 범위 안 또는 −1 = 없음)
    //           u8 allow_mutations
    //           i32 hero_block_cap_min max default          (il_cap 과 같다)
    //           u8 equalize  u8 wild_intrusion  u8 thorns_off
    //           u16 n + n × u8 floor_color, u8 default      (TimeOfDay Day/Dusk/Night, 오름차순, 기본값은 선택지 안)
    //           u8 spectate  i32 spectator_max
    //   characters  u16 n + n × (u8 character  i32 penalty_crystal_loss  i32 penalties_per_crystal_loss
    //           i32 overflow_damage_permille  i64 overflow_radius_raw  i64 overflow_knockback_raw  u8 overflow_clears_bullets
    //           i32 rod_damage_permille  i32 rod_cooldown_ticks  i32 rod_aim_ticks  i64 rod_range_raw  i64 rod_marker_radius_raw
    //           i32 thin_ice_hits  i32 thin_ice_slow_permille  i32 thin_ice_slow_ticks  i32 thin_ice_immune_ticks  u8 thin_ice_freezes
    //           i32 contract_per_crystal_permille  i32 debt_penalty_extra_cells)
    //           CharacterId 오름차순, 겹침 없음, Baseline 없음
    // bool 은 0/1. 깨진 바이트(모자람, 남음, 모르는 열거형 값, 순서 위반, 범위 밖 기본값)는 ContentException.
    // 캐릭터 절(1)이 먼저 읽히므로(ContentDb.Core 의 순서) 읽은 캐릭터의 CharacterDef.Duel 을 이 절의 같은 객체로 둔다.
    public sealed partial class ContentDb
    {
        private DuelRuleSet? _duelRules;

        partial void LoadDuelRules(ReadOnlySpan<byte> section)
        {
            var r = new ByteReader(section);
            var d = new DuelRuleSet();

            d.Players = DuelI32(ref r);
            d.RoundsToWin = DuelI32(ref r);
            d.RoundLengthTicks = DuelI32(ref r);
            d.CountdownTicks = DuelI32(ref r);
            d.IntermissionTicks = DuelI32(ref r);
            d.HpPerCell = DuelI32(ref r);
            d.RoundStartHeatHu = DuelI32(ref r);
            d.RoundStartCrystals = DuelI32(ref r);
            d.OvertimeTicks = DuelI32(ref r);
            d.OvertimeDamagePermille = DuelI32(ref r);

            d.ThornStartTicks = DuelI32(ref r);
            d.ThornLeadTicks = DuelI32(ref r);
            d.ThornStepTicks = DuelI32(ref r);
            d.ThornDamageIntervalTicks = DuelI32(ref r);
            d.ThornDamagePermille = DuelI32(ref r);
            d.ThornRingMax = DuelI32(ref r);

            ushort enemy = DuelU16(ref r);
            if (enemy == (ushort)EnemyTypeId.None || !Enum.IsDefined(typeof(EnemyTypeId), enemy))
            {
                throw DuelBroken("unknown wild enemy");
            }
            d.WildEnemy = (EnemyTypeId)enemy;
            d.WildSpawnIntervalTicks = DuelI32(ref r);
            d.WildSpawnCount = DuelI32(ref r);
            d.WildContactDamage = DuelI32(ref r);
            d.WildContactCooldownTicks = DuelI32(ref r);
            ushort np = DuelU16(ref r);
            var points = np == 0 ? Array.Empty<FixVec2>() : new FixVec2[np];
            for (int i = 0; i < np; i++)
            {
                Fix64 x = Fix64.FromRaw(DuelI64(ref r));
                Fix64 y = Fix64.FromRaw(DuelI64(ref r));
                points[i] = new FixVec2(x, y);
            }
            d.WildSpawnPoints = points;

            d.EnhancePerStagePermille = DuelI32(ref r);
            d.ElementMultPermille = DuelI32(ref r);
            d.BurstWindowTicks = DuelI32(ref r);
            d.BurstCapPermille = DuelI32(ref r);
            d.PostHitInvulnTicks = DuelI32(ref r);
            d.HitRadius = Fix64.FromRaw(DuelI64(ref r));
            d.HitCenterOffsetY = Fix64.FromRaw(DuelI64(ref r));
            d.ChainLightningAimTicks = DuelI32(ref r);
            d.ChainLightningJumps = DuelI32(ref r);
            d.HomingTurnPermille = DuelI32(ref r);
            d.HomingDurationTicks = DuelI32(ref r);
            d.IceSpikeSlowPermille = DuelI32(ref r);
            d.IceSpikeSlowTicks = DuelI32(ref r);
            d.FrostSlowPermille = DuelI32(ref r);
            d.FrostSlowTicks = DuelI32(ref r);
            d.ErosionBonusPermille = DuelI32(ref r);
            d.ErosionTicks = DuelI32(ref r);
            d.StatusApplyDelayTicks = DuelI32(ref r);
            d.ArtPrepTicks = DuelI32(ref r);
            d.BulletCapPerPlayer = DuelI32(ref r);
            d.UnstableCoreRadiusPermille = DuelI32(ref r);
            d.GlassWandOffTicks = DuelI32(ref r);

            d.GrazeAcceptTicks = DuelI32(ref r);
            d.GrazeCombo = DuelI32(ref r);
            d.PenaltyIntervalTicks = DuelI32(ref r);
            d.PenaltyWindowTicks = DuelI32(ref r);

            d.AimAssistMaxCenti = DuelI32(ref r);
            d.AimAssistFallbackCenti = DuelI32(ref r);
            d.AimLineLength = Fix64.FromRaw(DuelI64(ref r));
            d.TwistedBarrelJitterCenti = DuelI32(ref r);

            DuelRuleSet.RoomRules room = d.Room;
            room.RoundsChoices = DuelChoices(ref r);
            room.DefaultRounds = DuelI32(ref r);
            foreach (int n in room.RoundsChoices)
            {
                if (n <= 0 || (n & 1) == 0)
                {
                    throw DuelBroken("rounds must be odd and positive");
                }
            }
            DuelRequireIn(room.RoundsChoices, room.DefaultRounds);
            room.RoundSecondsChoices = DuelChoices(ref r);
            room.DefaultRoundSeconds = DuelI32(ref r);
            DuelRequireIn(room.RoundSecondsChoices, room.DefaultRoundSeconds);
            room.HpMultPermilleChoices = DuelChoices(ref r);
            room.DefaultHpMultPermille = DuelI32(ref r);
            DuelRequireIn(room.HpMultPermilleChoices, room.DefaultHpMultPermille);
            DuelRange(ref r, false, out room.EnhanceCapMin, out room.EnhanceCapMax, out room.DefaultEnhanceCap);
            DuelRange(ref r, true, out room.IlCapMin, out room.IlCapMax, out room.DefaultIlCap);
            room.DefaultAllowMutations = DuelBool(ref r);
            DuelRange(ref r, true, out room.HeroBlockCapMin, out room.HeroBlockCapMax, out room.DefaultHeroBlockCap);
            room.DefaultEqualize = DuelBool(ref r);
            room.DefaultWildIntrusion = DuelBool(ref r);
            room.DefaultThornsOff = DuelBool(ref r);
            ushort nf = DuelU16(ref r);
            var floors = new TimeOfDay[nf];
            for (int i = 0; i < nf; i++)
            {
                floors[i] = DuelFloor(DuelU8(ref r));
                if (i > 0 && floors[i] <= floors[i - 1])
                {
                    throw DuelBroken("floor colors out of order");
                }
            }
            room.FloorColorChoices = floors;
            room.DefaultFloorColor = DuelFloor(DuelU8(ref r));
            if (Array.IndexOf(floors, room.DefaultFloorColor) < 0)
            {
                throw DuelBroken("default floor color is not a choice");
            }
            room.DefaultSpectate = DuelBool(ref r);
            room.SpectatorMax = DuelI32(ref r);

            ushort nc = DuelU16(ref r);
            var overrides = new DuelCharacterOverride[nc];
            int max = 0;
            for (int i = 0; i < nc; i++)
            {
                byte id = DuelU8(ref r);
                if (id == (byte)CharacterId.Baseline || !Enum.IsDefined(typeof(CharacterId), id))
                {
                    throw DuelBroken("unknown character");
                }
                if (i > 0 && id <= (byte)overrides[i - 1].Character)
                {
                    throw DuelBroken("characters out of order");
                }
                var o = new DuelCharacterOverride { Character = (CharacterId)id };
                o.PenaltyCrystalLoss = DuelI32(ref r);
                o.PenaltiesPerCrystalLoss = DuelI32(ref r);
                o.OverflowDamagePermille = DuelI32(ref r);
                o.OverflowRadius = Fix64.FromRaw(DuelI64(ref r));
                o.OverflowKnockback = Fix64.FromRaw(DuelI64(ref r));
                o.OverflowClearsBullets = DuelBool(ref r);
                o.RodDamagePermille = DuelI32(ref r);
                o.RodCooldownTicks = DuelI32(ref r);
                o.RodAimTicks = DuelI32(ref r);
                o.RodRange = Fix64.FromRaw(DuelI64(ref r));
                o.RodMarkerRadius = Fix64.FromRaw(DuelI64(ref r));
                o.ThinIceHits = DuelI32(ref r);
                o.ThinIceSlowPermille = DuelI32(ref r);
                o.ThinIceSlowTicks = DuelI32(ref r);
                o.ThinIceImmuneTicks = DuelI32(ref r);
                o.ThinIceFreezes = DuelBool(ref r);
                o.ContractPerCrystalPermille = DuelI32(ref r);
                o.DebtPenaltyExtraCells = DuelI32(ref r);
                if (o.PenaltiesPerCrystalLoss <= 0)
                {
                    throw DuelBroken("penalties per crystal loss must be positive");
                }
                overrides[i] = o;
                max = id;
            }
            if (r.Remaining != 0)
            {
                throw DuelBroken("trailing bytes");
            }

            var table = new DuelCharacterOverride?[max + 1];
            foreach (DuelCharacterOverride o in overrides)
            {
                table[(int)o.Character] = o;
                // 캐릭터 절이 이미 읽혔으면 그 정의의 결투 값을 이 객체로 둔다(Build 안에서 한 번)
                int ci = (int)o.Character;
                if (ci < _characters.Length && _characters[ci] is CharacterDef def)
                {
                    def.Duel = o;
                }
            }
            d.Characters = table;
            _duelRules = d;
        }

        partial void FindDuelRules(ref DuelRuleSet? def)
        {
            if (_duelRules != null)
            {
                def = _duelRules;
            }
        }

        // 부분 클래스라 다른 절 파일과 이름이 겹치지 않게 절 이름을 붙인다
        private static int[] DuelChoices(ref ByteReader r)
        {
            ushort n = DuelU16(ref r);
            if (n == 0)
            {
                throw DuelBroken("empty choices");
            }
            var v = new int[n];
            for (int i = 0; i < n; i++)
            {
                v[i] = DuelI32(ref r);
                if (i > 0 && v[i] <= v[i - 1])
                {
                    throw DuelBroken("choices out of order");
                }
            }
            return v;
        }

        private static void DuelRequireIn(int[] choices, int value)
        {
            if (Array.IndexOf(choices, value) < 0)
            {
                throw DuelBroken("default is not a choice");
            }
        }

        private static void DuelRange(ref ByteReader r, bool nullable, out int min, out int max, out int def)
        {
            min = DuelI32(ref r);
            max = DuelI32(ref r);
            def = DuelI32(ref r);
            if (min > max)
            {
                throw DuelBroken("reversed range");
            }
            bool none = nullable && def == DuelRuleSet.RoomRules.NoCap;
            if (!none && (def < min || def > max))
            {
                throw DuelBroken("default out of range");
            }
        }

        private static TimeOfDay DuelFloor(byte v)
        {
            if (v == (byte)Wizard.Content.TimeOfDay.None || v == (byte)Wizard.Content.TimeOfDay.BossWave || !Enum.IsDefined(typeof(Wizard.Content.TimeOfDay), v))
            {
                throw DuelBroken("unknown floor color");
            }
            return (Wizard.Content.TimeOfDay)v;
        }

        private static bool DuelBool(ref ByteReader r)
        {
            byte v = DuelU8(ref r);
            if (v > 1)
            {
                throw DuelBroken("bool is not 0 or 1");
            }
            return v == 1;
        }

        private static byte DuelU8(ref ByteReader r)
        {
            if (!r.TryU8(out byte v))
            {
                throw DuelBroken("truncated");
            }
            return v;
        }

        private static ushort DuelU16(ref ByteReader r)
        {
            if (!r.TryU16(out ushort v))
            {
                throw DuelBroken("truncated");
            }
            return v;
        }

        private static int DuelI32(ref ByteReader r)
        {
            if (!r.TryI32(out int v))
            {
                throw DuelBroken("truncated");
            }
            return v;
        }

        private static long DuelI64(ref ByteReader r)
        {
            if (!r.TryI64(out long v))
            {
                throw DuelBroken("truncated");
            }
            return v;
        }

        private static ContentException DuelBroken(string what) => new ContentException("content section 'duel_rules': " + what);
    }
}
