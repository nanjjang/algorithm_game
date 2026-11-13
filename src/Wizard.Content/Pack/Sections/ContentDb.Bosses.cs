using System;
using Wizard.Core;

namespace Wizard.Content
{
    // 팩 절 6 bosses(CONTRACTS 11.2)를 읽는다. 쓰는 쪽: tools/Wizard.DataBuild/Convert/BossesConverter.cs. 원본: data/sim/bosses.json.
    // 절 바이트(LE, 모든 수는 이미 정수. "opt" = u8 있음(0 | 1) 다음에 값, 없으면 값은 0):
    //   공통(BossDef.CommonDef 의 필드 순서): i32 intro_ticks  i32 phase_transition_ticks  i32 wait_ticks  i32 no_candidate_wait_ticks
    //     i32 r5_retry_ticks  i32 r5_repick_ticks  i32 rage_stack_ticks  i32 rage_max_stacks  i32 rage_step_permille
    //     i32 final_rage_ticks  i32 final_rage_damage_plus  i32 action_halt_ticks  i32 contact_cooldown_ticks
    //   u16 개수
    //   보스 × 개수(BossId 오름차순, 겹침 없음, None 없음):
    //     u16 id  i32 rotation_index  i32 base_hp  i32 hp_scale_anchor_wave  i32 move_speed_centi  opt i32 move_speed_on_frost_centi
    //     i32 contact_damage  i64 body_radius_raw  u8 element  u8 weak  u8 resist  i64 keep_distance_raw  i64 wall_margin_raw
    //     u8 단계 수(1 이상)
    //     단계 × 수: i32 index  i32 enter_at_hp_percent  u16 tags  i64 anchor_radius_raw
    //       u8 n  u8 pattern_key × n(고르는 패턴, 오름차순)  u8 n  u8 pattern_key × n(상시 행동, 오름차순)  u8 n  덮어쓰기 × n
    //     u8 패턴 수(1 이상)
    //     패턴 × 수(PatternKey 오름차순): u8 key  u8 passive  u8 graze  i32 recovery_ticks  i32 recovery_damage_permille  u8 단계 수(1 이상)  단계 × 수
    //     u8 n  덮어쓰기 × n(2회차)
    //     u8 has_phantom_copy  i32 interval_permille  i32 max_projectiles  i64 speed_per_tick_raw  i32 damage  i32 bounce_cap
    //       i32 explosion_radius_permille  i64 explosion_radius_max_raw  i32 explosion_telegraph_ticks  i32 explosion_damage
    //       i32 chain_line_telegraph_ticks  i64 chain_line_width_raw  i64 chain_line_length_raw  i32 split_min_angle_centi  i32 copy_mutations_from_cycle
    //     u8 has_first_encounter  i32 hp_floor_percent  i32 max_fight_ticks  i32 rage_stacks  i32 start_phase  i32 finisher_ticks
    //       i32 finisher_hp_zero_tick  i32 collapse_ticks
    //     u8 has_body  i64 head_radius_raw  i64 segment_radius_raw  i64 tail_radius_raw  i32 segment_count  i64 segment_spacing_raw
    //       i32 segment_damage_permille  i32 tail_damage_permille  i32 max_segment_hits
    //   덮어쓰기(목록 안에서 (pattern, step, key) 오름차순): u8 pattern_key  i32 step  u8 override_key  i64 value
    //   패턴의 단계: u8 kind
    //     kind 0 공격: 절 5 enemies 의 공격 꼴 그대로
    //     kind 1 복사: u8 n  u8 art × n(오름차순)  u8 fallback(0 = 없음)  i32 warn_ticks
    //     kind 2 부르기: u16 enemy  i32 count  i32 field_cap  i32 warn_ticks
    //     i32 count  i32 repeat  i32 repeat_interval_ticks  i32 repeat_angle_centi  u8 telegraph_each_repeat  i32 start_delay_ticks
    //     u8 rapid_fire  i32 min_interval_ticks  i32 frost_telegraph_ticks  i64 clear_hazard_margin_raw  i32 hazard_speed_permille  i64 hazard_drift_per_tick_raw
    //     u8 has_stream  i32 interval_ticks  i32 sweep_centi  i32 sweep_centi_per_tick
    //     u8 has_placement  i32 at_player  i64 min_player_distance_raw  opt i64 max_player_distance_raw  i64 min_spacing_raw  i64 wall_margin_raw  i32 max_tries
    //     u8 has_obstacle  u8 kind  i64 radius_raw  i32 duration_ticks  i32 hp  i32 max_count  u8 blocks_player_shots  u8 blocks_enemy_shots
    //       u8 has_shards  i64 shard_speed_per_tick_raw  i32 shard_count  i32 shard_spread_centi  i64 shard_range_raw  i64 shard_hit_radius_raw
    // 필드를 더하는 작업은 그 항목 끝에 더하고 변환기와 이 파일을 같은 커밋에서 고친다.
    // 깨진 바이트(모자람, 남음, 순서, 모르는 열거형 값·비트, 0/1 이 아닌 bool, 없음인데 0 이 아닌 값, 단계·패턴이 없는 보스, 없는 패턴을 가리키는 단계·덮어쓰기)는
    // ContentException. 공격 꼴의 깨짐은 절 5 의 읽기(ReadAttackEntry)가 같은 예외로 알린다.
    // 규칙 검사(단계 % 내림차순, 상시 행동의 짝, 연사형과 R3 하한, 덮어쓰기와 단계의 짝 …)는 변환기가 한다. 여기서는 바이트의 꼴만 믿지 않는다.
    public sealed partial class ContentDb
    {
        // BossId 값으로 찾는 표. 비어 있는 칸은 그 id 가 팩에 없다는 뜻이다
        private BossDef?[] _bosses = Array.Empty<BossDef?>();

        partial void LoadBosses(ReadOnlySpan<byte> section)
        {
            var r = new ByteReader(section);
            BossDef.CommonDef common = ReadBossCommon(ref r);
            if (!r.TryU16(out ushort count))
            {
                throw BossesBroken("truncated count");
            }
            var defs = new BossDef[count];
            int previous = (int)BossId.None;
            for (int i = 0; i < count; i++)
            {
                BossDef d = ReadBossEntry(ref r, common);
                if ((int)d.Id <= previous)
                {
                    throw BossesBroken("entries not in ascending id order (or id none)");
                }
                previous = (int)d.Id;
                defs[i] = d;
            }
            if (r.Remaining != 0)
            {
                throw BossesBroken("trailing bytes");
            }
            var table = new BossDef?[previous + 1];
            foreach (BossDef d in defs)
            {
                table[(int)d.Id] = d;
            }
            _bosses = table;
        }

        partial void FindBoss(BossId id, ref BossDef? def)
        {
            int i = (int)id;
            if (i < _bosses.Length)
            {
                def = _bosses[i];
            }
        }

        private static BossDef.CommonDef ReadBossCommon(ref ByteReader r) => new BossDef.CommonDef
        {
            IntroTicks = BossI32(ref r),
            PhaseTransitionTicks = BossI32(ref r),
            WaitTicks = BossI32(ref r),
            NoCandidateWaitTicks = BossI32(ref r),
            R5RetryTicks = BossI32(ref r),
            R5RepickTicks = BossI32(ref r),
            RageStackTicks = BossI32(ref r),
            RageMaxStacks = BossI32(ref r),
            RageStepPermille = BossI32(ref r),
            FinalRageTicks = BossI32(ref r),
            FinalRageDamagePlus = BossI32(ref r),
            ActionHaltTicks = BossI32(ref r),
            ContactCooldownTicks = BossI32(ref r),
        };

        private static BossDef ReadBossEntry(ref ByteReader r, BossDef.CommonDef common)
        {
            var d = new BossDef { Common = common };
            if (!r.TryU16(out ushort id))
            {
                throw BossesBroken("truncated entry");
            }
            if (!Enum.IsDefined(typeof(BossId), id))
            {
                throw BossesBroken("unknown boss id");
            }
            d.Id = (BossId)id;
            d.RotationIndex = BossI32(ref r);
            d.BaseHp = BossI32(ref r);
            d.HpScaleAnchorWave = BossI32(ref r);
            d.MoveSpeedCenti = BossI32(ref r);
            bool hasFrost = BossBool(ref r);
            int frost = BossI32(ref r);
            d.MoveSpeedOnFrostCenti = hasFrost ? frost : BossAbsent(frost);
            d.ContactDamage = BossI32(ref r);
            d.BodyRadius = Fix64.FromRaw(BossI64(ref r));
            d.Element = (Element)BossEnumByte(ref r, typeof(Element));
            d.Weak = (Element)BossEnumByte(ref r, typeof(Element));
            d.Resist = (Element)BossEnumByte(ref r, typeof(Element));
            d.Movement.KeepDistance = Fix64.FromRaw(BossI64(ref r));
            d.Movement.WallMargin = Fix64.FromRaw(BossI64(ref r));

            byte phases = BossU8(ref r);
            if (phases == 0)
            {
                throw BossesBroken("boss without phases");
            }
            d.Phases = new BossDef.PhaseDef[phases];
            for (int i = 0; i < phases; i++)
            {
                var p = new BossDef.PhaseDef
                {
                    Index = BossI32(ref r),
                    EnterAtHpPercent = BossI32(ref r),
                };
                if (!r.TryU16(out ushort tags))
                {
                    throw BossesBroken("truncated entry");
                }
                if ((tags & ~EnemyKnownTags()) != 0)
                {
                    throw BossesBroken("unknown tag bits");
                }
                p.Tags = (EnemyTag)tags;
                p.AnchorRadius = Fix64.FromRaw(BossI64(ref r));
                p.Patterns = ReadBossKeys(ref r);
                p.Passives = ReadBossKeys(ref r);
                p.Overrides = ReadBossOverrides(ref r);
                d.Phases[i] = p;
            }

            byte patterns = BossU8(ref r);
            if (patterns == 0)
            {
                throw BossesBroken("boss without patterns");
            }
            d.Patterns = new BossPatternDef[patterns];
            int previous = (int)BossPatternDef.PatternKey.None;
            for (int i = 0; i < patterns; i++)
            {
                BossPatternDef p = ReadBossPattern(ref r);
                if ((int)p.Key <= previous)
                {
                    throw BossesBroken("patterns not in ascending key order (or key none)");
                }
                previous = (int)p.Key;
                d.Patterns[i] = p;
            }
            d.SecondCycle = ReadBossOverrides(ref r);

            bool hasCopy = BossBool(ref r);
            var copy = new BossDef.PhantomCopyDef
            {
                IntervalPermille = BossI32(ref r),
                MaxProjectiles = BossI32(ref r),
                SpeedPerTick = Fix64.FromRaw(BossI64(ref r)),
                Damage = BossI32(ref r),
                BounceCap = BossI32(ref r),
                ExplosionRadiusPermille = BossI32(ref r),
                ExplosionRadiusMax = Fix64.FromRaw(BossI64(ref r)),
                ExplosionTelegraphTicks = BossI32(ref r),
                ExplosionDamage = BossI32(ref r),
                ChainLineTelegraphTicks = BossI32(ref r),
                ChainLineWidth = Fix64.FromRaw(BossI64(ref r)),
                ChainLineLength = Fix64.FromRaw(BossI64(ref r)),
                SplitMinAngleCenti = BossI32(ref r),
                CopyMutationsFromCycle = BossI32(ref r),
            };
            if (hasCopy)
            {
                d.PhantomCopy = copy;
            }
            else if (copy.IntervalPermille != 0 || copy.MaxProjectiles != 0 || copy.SpeedPerTick.Raw != 0 || copy.Damage != 0 || copy.BounceCap != 0
                || copy.ExplosionRadiusPermille != 0 || copy.ExplosionRadiusMax.Raw != 0 || copy.ExplosionTelegraphTicks != 0 || copy.ExplosionDamage != 0
                || copy.ChainLineTelegraphTicks != 0 || copy.ChainLineWidth.Raw != 0 || copy.ChainLineLength.Raw != 0 || copy.SplitMinAngleCenti != 0
                || copy.CopyMutationsFromCycle != 0)
            {
                throw BossesBroken("absent phantom copy with nonzero fields");
            }

            bool hasFirst = BossBool(ref r);
            var first = new BossDef.FirstEncounterDef
            {
                HpFloorPercent = BossI32(ref r),
                MaxFightTicks = BossI32(ref r),
                RageStacks = BossI32(ref r),
                StartPhase = BossI32(ref r),
                FinisherTicks = BossI32(ref r),
                FinisherHpZeroTick = BossI32(ref r),
                CollapseTicks = BossI32(ref r),
            };
            if (hasFirst)
            {
                d.FirstEncounter = first;
            }
            else if (first.HpFloorPercent != 0 || first.MaxFightTicks != 0 || first.RageStacks != 0 || first.StartPhase != 0
                || first.FinisherTicks != 0 || first.FinisherHpZeroTick != 0 || first.CollapseTicks != 0)
            {
                throw BossesBroken("absent first encounter with nonzero fields");
            }

            bool hasBody = BossBool(ref r);
            var body = new BossDef.BodyDef
            {
                HeadRadius = Fix64.FromRaw(BossI64(ref r)),
                SegmentRadius = Fix64.FromRaw(BossI64(ref r)),
                TailRadius = Fix64.FromRaw(BossI64(ref r)),
                SegmentCount = BossI32(ref r),
                SegmentSpacing = Fix64.FromRaw(BossI64(ref r)),
                SegmentDamagePermille = BossI32(ref r),
                TailDamagePermille = BossI32(ref r),
                MaxSegmentHits = BossI32(ref r),
            };
            if (hasBody)
            {
                d.Body = body;
            }
            else if (body.HeadRadius.Raw != 0 || body.SegmentRadius.Raw != 0 || body.TailRadius.Raw != 0 || body.SegmentCount != 0
                || body.SegmentSpacing.Raw != 0 || body.SegmentDamagePermille != 0 || body.TailDamagePermille != 0 || body.MaxSegmentHits != 0)
            {
                throw BossesBroken("absent body with nonzero fields");
            }

            // 단계와 덮어쓰기가 가리키는 패턴·단계가 이 보스에 있다
            foreach (BossDef.PhaseDef p in d.Phases)
            {
                BossCheckKeys(d, p.Patterns);
                BossCheckKeys(d, p.Passives);
                BossCheckOverrides(d, p.Overrides);
            }
            BossCheckOverrides(d, d.SecondCycle);
            return d;
        }

        private static BossPatternDef ReadBossPattern(ref ByteReader r)
        {
            var p = new BossPatternDef();
            byte key = BossU8(ref r);
            if (!Enum.IsDefined(typeof(BossPatternDef.PatternKey), key))
            {
                throw BossesBroken("unknown pattern key");
            }
            p.Key = (BossPatternDef.PatternKey)key;
            p.IsPassive = BossBool(ref r);
            byte graze = BossU8(ref r);
            if ((graze & ~(byte)(BossPatternDef.GrazeMask.Bullet | BossPatternDef.GrazeMask.Charge | BossPatternDef.GrazeMask.Zone)) != 0)
            {
                throw BossesBroken("graze mask with unknown bits");
            }
            p.Graze = (BossPatternDef.GrazeMask)graze;
            p.RecoveryTicks = BossI32(ref r);
            p.RecoveryDamagePermille = BossI32(ref r);
            byte steps = BossU8(ref r);
            if (steps == 0)
            {
                throw BossesBroken("pattern without steps");
            }
            p.Steps = new BossPatternDef.StepDef[steps];
            for (int i = 0; i < steps; i++)
            {
                p.Steps[i] = ReadBossStep(ref r);
            }
            return p;
        }

        private static BossPatternDef.StepDef ReadBossStep(ref ByteReader r)
        {
            var s = new BossPatternDef.StepDef();
            s.Kind = (BossPatternDef.StepKind)BossEnumByte(ref r, typeof(BossPatternDef.StepKind));
            switch (s.Kind)
            {
                case BossPatternDef.StepKind.Attack:
                    s.Attack = ReadAttackEntry(ref r);
                    break;
                case BossPatternDef.StepKind.Copy:
                    var copy = new BossPatternDef.CopyDef();
                    byte arts = BossU8(ref r);
                    copy.Arts = new ArtId[arts];
                    int previous = (int)ArtId.None;
                    for (int i = 0; i < arts; i++)
                    {
                        byte art = BossEnumByte(ref r, typeof(ArtId));
                        if (art <= previous)
                        {
                            throw BossesBroken("copy arts not in ascending order (or art none)");
                        }
                        previous = art;
                        copy.Arts[i] = (ArtId)art;
                    }
                    copy.Fallback = (ArtId)BossEnumByte(ref r, typeof(ArtId));
                    copy.WarnTicks = BossI32(ref r);
                    s.Copy = copy;
                    break;
                default:
                    if (!r.TryU16(out ushort enemy))
                    {
                        throw BossesBroken("truncated entry");
                    }
                    if (enemy == (ushort)EnemyTypeId.None || !Enum.IsDefined(typeof(EnemyTypeId), enemy))
                    {
                        throw BossesBroken("unknown summoned enemy");
                    }
                    s.Summon = new BossPatternDef.SummonDef
                    {
                        Enemy = (EnemyTypeId)enemy,
                        Count = BossI32(ref r),
                        FieldCap = BossI32(ref r),
                        WarnTicks = BossI32(ref r),
                    };
                    break;
            }
            s.Count = BossI32(ref r);
            s.Repeat = BossI32(ref r);
            s.RepeatIntervalTicks = BossI32(ref r);
            s.RepeatAngleCenti = BossI32(ref r);
            s.TelegraphEachRepeat = BossBool(ref r);
            s.StartDelayTicks = BossI32(ref r);
            s.RapidFire = BossBool(ref r);
            s.MinIntervalTicks = BossI32(ref r);
            s.FrostTelegraphTicks = BossI32(ref r);
            s.ClearHazardMargin = Fix64.FromRaw(BossI64(ref r));
            s.HazardSpeedPermille = BossI32(ref r);
            s.HazardDriftPerTick = Fix64.FromRaw(BossI64(ref r));
            if (s.Count < 1 || s.Repeat < 0)
            {
                throw BossesBroken("step count below one or negative repeat");
            }

            bool hasStream = BossBool(ref r);
            int interval = BossI32(ref r);
            int sweep = BossI32(ref r);
            int perTick = BossI32(ref r);
            if (hasStream)
            {
                s.Stream = new BossPatternDef.StreamDef { IntervalTicks = interval, SweepCenti = sweep, SweepCentiPerTick = perTick };
            }
            else if (interval != 0 || sweep != 0 || perTick != 0)
            {
                throw BossesBroken("absent stream with nonzero fields");
            }

            bool hasPlacement = BossBool(ref r);
            int atPlayer = BossI32(ref r);
            long minPlayer = BossI64(ref r);
            Fix64? maxPlayer = BossOptionalFix(ref r);
            long minSpacing = BossI64(ref r);
            long wallMargin = BossI64(ref r);
            int tries = BossI32(ref r);
            if (hasPlacement)
            {
                s.Placement = new BossPatternDef.PlacementDef
                {
                    AtPlayer = atPlayer,
                    MinPlayerDistance = Fix64.FromRaw(minPlayer),
                    MaxPlayerDistance = maxPlayer,
                    MinSpacing = Fix64.FromRaw(minSpacing),
                    WallMargin = Fix64.FromRaw(wallMargin),
                    MaxTries = tries,
                };
            }
            else if (atPlayer != 0 || minPlayer != 0 || maxPlayer.HasValue || minSpacing != 0 || wallMargin != 0 || tries != 0)
            {
                throw BossesBroken("absent placement with nonzero fields");
            }

            bool hasObstacle = BossBool(ref r);
            byte kind = BossU8(ref r);
            long radius = BossI64(ref r);
            int duration = BossI32(ref r);
            int hp = BossI32(ref r);
            int maxCount = BossI32(ref r);
            bool blocksPlayer = BossBool(ref r);
            bool blocksEnemy = BossBool(ref r);
            bool hasShards = BossBool(ref r);
            long shardSpeed = BossI64(ref r);
            int shardCount = BossI32(ref r);
            int shardSpread = BossI32(ref r);
            long shardRange = BossI64(ref r);
            long shardHit = BossI64(ref r);
            if (!hasShards && (shardSpeed != 0 || shardCount != 0 || shardSpread != 0 || shardRange != 0 || shardHit != 0))
            {
                throw BossesBroken("absent shards with nonzero fields");
            }
            if (hasObstacle)
            {
                if (!Enum.IsDefined(typeof(BossPatternDef.ObstacleKind), kind))
                {
                    throw BossesBroken("unknown enum value");
                }
                s.Obstacle = new BossPatternDef.ObstacleDef
                {
                    Kind = (BossPatternDef.ObstacleKind)kind,
                    Radius = Fix64.FromRaw(radius),
                    DurationTicks = duration,
                    Hp = hp,
                    MaxCount = maxCount,
                    BlocksPlayerShots = blocksPlayer,
                    BlocksEnemyShots = blocksEnemy,
                    Shards = hasShards
                        ? new AttackDef.ProjDef
                        {
                            SpeedPerTick = Fix64.FromRaw(shardSpeed),
                            Count = shardCount,
                            SpreadCenti = shardSpread,
                            Range = Fix64.FromRaw(shardRange),
                            HitRadius = Fix64.FromRaw(shardHit),
                        }
                        : null,
                };
            }
            else if (kind != 0 || radius != 0 || duration != 0 || hp != 0 || maxCount != 0 || blocksPlayer || blocksEnemy || hasShards)
            {
                throw BossesBroken("absent obstacle with nonzero fields");
            }
            return s;
        }

        private static BossPatternDef.PatternKey[] ReadBossKeys(ref ByteReader r)
        {
            byte n = BossU8(ref r);
            var keys = new BossPatternDef.PatternKey[n];
            int previous = (int)BossPatternDef.PatternKey.None;
            for (int i = 0; i < n; i++)
            {
                byte k = BossEnumByte(ref r, typeof(BossPatternDef.PatternKey));
                if (k <= previous)
                {
                    throw BossesBroken("phase pattern keys not in ascending order (or key none)");
                }
                previous = k;
                keys[i] = (BossPatternDef.PatternKey)k;
            }
            return keys;
        }

        private static BossDef.PatternOverride[] ReadBossOverrides(ref ByteReader r)
        {
            byte n = BossU8(ref r);
            var list = new BossDef.PatternOverride[n];
            for (int i = 0; i < n; i++)
            {
                var o = new BossDef.PatternOverride
                {
                    Pattern = (BossPatternDef.PatternKey)BossEnumByte(ref r, typeof(BossPatternDef.PatternKey)),
                    Step = BossI32(ref r),
                    Key = (BossDef.OverrideKey)BossEnumByte(ref r, typeof(BossDef.OverrideKey)),
                    Value = BossI64(ref r),
                };
                if (i > 0)
                {
                    BossDef.PatternOverride p = list[i - 1];
                    int order = p.Pattern != o.Pattern ? ((byte)p.Pattern).CompareTo((byte)o.Pattern)
                        : p.Step != o.Step ? p.Step.CompareTo(o.Step) : ((byte)p.Key).CompareTo((byte)o.Key);
                    if (order >= 0)
                    {
                        throw BossesBroken("overrides not in ascending (pattern, step, key) order");
                    }
                }
                list[i] = o;
            }
            return list;
        }

        private static void BossCheckKeys(BossDef d, BossPatternDef.PatternKey[] keys)
        {
            foreach (BossPatternDef.PatternKey k in keys)
            {
                if (d.FindPattern(k) == null)
                {
                    throw BossesBroken("phase refers to a pattern the boss does not have");
                }
            }
        }

        private static void BossCheckOverrides(BossDef d, BossDef.PatternOverride[] list)
        {
            foreach (BossDef.PatternOverride o in list)
            {
                BossPatternDef? p = d.FindPattern(o.Pattern);
                if (p == null || o.Step < 0 || o.Step >= p.Steps.Length)
                {
                    throw BossesBroken("override refers to a pattern step the boss does not have");
                }
            }
        }

        private static Fix64? BossOptionalFix(ref ByteReader r)
        {
            bool has = BossBool(ref r);
            long v = BossI64(ref r);
            if (has)
            {
                return Fix64.FromRaw(v);
            }
            if (v != 0)
            {
                throw BossesBroken("absent value is not zero");
            }
            return null;
        }

        // "없음" 칸의 값은 0 이어야 한다
        private static int? BossAbsent(int v)
        {
            if (v != 0)
            {
                throw BossesBroken("absent value is not zero");
            }
            return null;
        }

        // 부분 클래스라 다른 절 파일과 이름이 겹치지 않게 절 이름을 붙인다
        private static byte BossEnumByte(ref ByteReader r, Type enumType)
        {
            byte v = BossU8(ref r);
            if (!Enum.IsDefined(enumType, v))
            {
                throw BossesBroken("unknown enum value");
            }
            return v;
        }

        private static bool BossBool(ref ByteReader r)
        {
            byte v = BossU8(ref r);
            if (v > 1)
            {
                throw BossesBroken("bool is not 0 or 1");
            }
            return v == 1;
        }

        private static byte BossU8(ref ByteReader r)
        {
            if (!r.TryU8(out byte v))
            {
                throw BossesBroken("truncated entry");
            }
            return v;
        }

        private static int BossI32(ref ByteReader r)
        {
            if (!r.TryI32(out int v))
            {
                throw BossesBroken("truncated entry");
            }
            return v;
        }

        private static long BossI64(ref ByteReader r)
        {
            if (!r.TryI64(out long v))
            {
                throw BossesBroken("truncated entry");
            }
            return v;
        }

        private static ContentException BossesBroken(string what) => new ContentException("content section 'bosses': " + what);
    }
}
