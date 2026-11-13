using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using Wizard.Content;
using Wizard.Core;
using Wizard.DataBuild.Core;
using Wizard.DataBuild.Validate.Evasion;

// 네임스페이스는 폴더의 앞머리(Wizard.DataBuild)다. Wizard.DataBuild.Convert 로 두면 이 어셈블리 안의 System.Convert 호출이 가려진다
namespace Wizard.DataBuild
{
    // data/sim/bosses.json → 팩 절 6 bosses. 읽는 쪽과 절 바이트의 꼴: src/Wizard.Content/Pack/Sections/ContentDb.Bosses.cs.
    // 스키마: data/schema/bosses.schema.json(꼴과 범위는 스키마가 이미 보았다. 공격 한 줄의 꼴은 enemies.schema.json 의 attack 과 같다).
    //   id → BossId(spec 14 3.8)    element·weak·resist → Element    tags → EnemyTag(tag.*)    패턴 열쇠 → BossPatternDef.PatternKey(아래 표)
    //   copy.arts·fallback → ArtId    summon.enemy → EnemyTypeId    공격 한 줄은 적 절과 같이 읽고 검사하고 쓴다(EnemiesConverter.ReadAttack·WriteAttack)
    // 단위(spec 06 0.2, CONTRACTS 0.3. 반올림은 가장 가까운 정수, 같으면 짝수): 칸 → Raw, 이동 속도 → 센티 칸/초, 탄속·흐르는 속도 → 틱당 Raw,
    //   초 → 틱, 도 → 센티도, 배율 → 천분율. 덮어쓰기 값의 단위는 열쇠가 정한다(BossDef.OverrideKey).
    // 이 절 안에서 보는 규칙(spec 06 1.3~1.5, 2.6, 2.8, 2.12, 3~9절):
    //   겹치는 보스·순번, 약점 ≠ 저항(None 아님), 서리밭 위 걷는 속도 < 평소 속도, 밤삼킴의 몸 반지름 = 머리 반지름,
    //   단계: 1부터 차례로, 처음은 100%, 들어가는 % 는 줄어든다, 꼬리표에 tag.boss, 고르는 패턴은 상시 행동이 아니고 상시 행동은 상시 행동이다,
    //     모든 패턴이 어느 단계에 나온다, 단계마다 하나 이상,
    //   패턴: 열쇠가 그 보스의 것, 겹치지 않는다, 단계마다 공격·복사·부르기 가운데 꼭 하나, 상시 행동만 끝없이 되풀이하고 빈틈이 없다,
    //     보스의 공격에는 접촉·빈틈·재사용 대기가 없다(몸 접촉은 contact_damage, 빈틈은 패턴, 대기는 공통 규칙), 연사형 ⇔ R3 하한,
    //     줄기는 탄이 있는 공격에만(초당 발수가 60 을 나누고 틱당 각이 정수), 자리·장애물은 예고 구역이 있는 공격에만, 서리밭 예고 ≥ 예고,
    //     소화 거리·서리밭 속도는 남는 구역이 있는 공격에만, 복사는 허상 규칙(phantom_copy)이 있는 보스에만,
    //   덮어쓰기: 패턴·단계가 있다, 열쇠가 그 단계에 맞는다, 같은 칸이 두 번 없다, 단계 덮어쓰기의 패턴은 그 단계에 나온다,
    //   첫 만남: 시작 단계 ≤ 단계 수, 격노 스택 ≤ 최대, 체력 0 틱 < 되비침 틱. 격노 스택당 감소 × 최대 스택 < 1.
    // 여러 파일에 걸친 검사(부르는 졸개가 enemies.json 에 있다)는 CrossCheck, 회피 검산(R1~R4)의 원본은 EvasionSource 가 맡는다.
    // 상성 분배(보스의 저항 = 자기 속성 포함)는 tools/Wizard.DataBuild/Validate/Affinity/BossRules/ 가 본다.
    public sealed class BossesConverter : ISectionConverter
    {
        public const string BossesFile = "sim/bosses.json";
        public const string EnemiesFile = "sim/enemies.json";

        public ContentSection Section => ContentSection.Bosses;

        // 패턴 열쇠(spec 06 1.5 의 <보스>.<패턴> 에서 뒤쪽) → PatternKey 와 그 패턴을 가진 보스(spec 06 3.2·4.3·5.2·6.2·7.2·8.2)
        private static readonly PatternRow[] Patterns =
        {
            new PatternRow("triple_charge", BossPatternDef.PatternKey.TripleCharge, BossId.KilnBoar),
            new PatternRow("stomp", BossPatternDef.PatternKey.Stomp, BossId.KilnBoar),
            new PatternRow("rockfall", BossPatternDef.PatternKey.Rockfall, BossId.KilnBoar),
            new PatternRow("copy_fire", BossPatternDef.PatternKey.CopyFire, BossId.MirrorPhantom),
            new PatternRow("summon_slimes", BossPatternDef.PatternKey.SummonSlimes, BossId.MirrorPhantom),
            new PatternRow("mimic_art", BossPatternDef.PatternKey.MimicArt, BossId.MirrorPhantom),
            new PatternRow("bloom_mimic", BossPatternDef.PatternKey.BloomMimic, BossId.MirrorPhantom),
            new PatternRow("spore_ring", BossPatternDef.PatternKey.SporeRing, BossId.SporeQueen),
            new PatternRow("spore_cloud", BossPatternDef.PatternKey.SporeCloud, BossId.SporeQueen),
            new PatternRow("summon_sprouts", BossPatternDef.PatternKey.SummonSprouts, BossId.SporeQueen),
            new PatternRow("leap", BossPatternDef.PatternKey.Leap, BossId.SporeQueen),
            new PatternRow("glide", BossPatternDef.PatternKey.Glide, BossId.StormOwl),
            new PatternRow("feather_fan", BossPatternDef.PatternKey.FeatherFan, BossId.StormOwl),
            new PatternRow("lightning_strike", BossPatternDef.PatternKey.LightningStrike, BossId.StormOwl),
            new PatternRow("lightning_plow", BossPatternDef.PatternKey.LightningPlow, BossId.StormOwl),
            new PatternRow("ice_pillar", BossPatternDef.PatternKey.IcePillar, BossId.FrostGoat),
            new PatternRow("horn_swing", BossPatternDef.PatternKey.HornSwing, BossId.FrostGoat),
            new PatternRow("frost_breath", BossPatternDef.PatternKey.FrostBreath, BossId.FrostGoat),
            new PatternRow("horn_charge", BossPatternDef.PatternKey.HornCharge, BossId.FrostGoat),
            new PatternRow("frost_field", BossPatternDef.PatternKey.FrostField, BossId.FrostGoat),
            new PatternRow("coil", BossPatternDef.PatternKey.Coil, BossId.NightSerpent),
            new PatternRow("dark_spit", BossPatternDef.PatternKey.DarkSpit, BossId.NightSerpent),
            new PatternRow("burrow", BossPatternDef.PatternKey.Burrow, BossId.NightSerpent),
            new PatternRow("scale_volley", BossPatternDef.PatternKey.ScaleVolley, BossId.NightSerpent),
        };

        public void Convert(DataFile file, ByteWriter output, Diagnostics diagnostics)
        {
            int before = diagnostics.Count;
            Common common = ReadCommon(file.Root.GetProperty("common"), file, diagnostics);
            var entries = new List<Boss>();
            int i = 0;
            foreach (JsonElement row in file.Root.GetProperty("bosses").EnumerateArray())
            {
                string at = "/bosses/" + Num(i);
                Boss? b = ReadBoss(row, at, common, file, diagnostics);
                if (b != null)
                {
                    b.Order = entries.Count;
                    entries.Add(b);
                }
                i++;
            }
            // 정규 순서: BossId 오름차순(원본의 나열 순서에 기대지 않는다). 같은 id 끼리는 원본 순서(진단이 뒤에 나온 줄을 가리키게)
            entries.Sort((a, b) => a.Id != b.Id ? ((ushort)a.Id).CompareTo((ushort)b.Id) : a.Order.CompareTo(b.Order));
            for (int k = 1; k < entries.Count; k++)
            {
                if (entries[k].Id == entries[k - 1].Id)
                {
                    diagnostics.Error(file.RepoPath, entries[k].Pointer + "/id", "보스 '" + ContentIds.ToId(entries[k].Id) + "' 가 두 번 있다");
                }
            }
            var byRotation = new List<Boss>(entries);
            byRotation.Sort((a, b) => a.RotationIndex != b.RotationIndex ? a.RotationIndex.CompareTo(b.RotationIndex) : a.Order.CompareTo(b.Order));
            for (int k = 1; k < byRotation.Count; k++)
            {
                if (byRotation[k].RotationIndex == byRotation[k - 1].RotationIndex)
                {
                    diagnostics.Error(file.RepoPath, byRotation[k].Pointer + "/rotation_index",
                        "보스 순번 " + Num(byRotation[k].RotationIndex) + " 이 두 보스에 있다(spec 06 1.3 rotationIndex, 2.1 ROTATION)");
                }
            }
            if (diagnostics.Count != before)
            {
                return;
            }
            WriteCommon(common, output);
            output.U16((ushort)entries.Count);       // id 가 겹치지 않으므로 BossId 의 값 수 이하다
            foreach (Boss b in entries)
            {
                WriteBoss(b, output);
            }
        }

        // ---- 중간 표현(모든 수는 이미 정수) ----

        private sealed class PatternRow
        {
            public PatternRow(string name, BossPatternDef.PatternKey key, BossId owner)
            {
                Name = name;
                Key = key;
                Owner = owner;
            }

            public string Name { get; }

            public BossPatternDef.PatternKey Key { get; }

            public BossId Owner { get; }
        }

        private sealed class Common
        {
            public int IntroTicks, PhaseTransitionTicks, WaitTicks, NoCandidateWaitTicks, R5RetryTicks, R5RepickTicks;
            public int RageStackTicks, RageMaxStacks, RageStepPermille, FinalRageTicks, FinalRageDamagePlus, ActionHaltTicks, ContactCooldownTicks;
        }

        private sealed class Boss
        {
            public BossId Id;
            public int RotationIndex, BaseHp, HpScaleAnchorWave, MoveSpeedCenti, ContactDamage;
            public int? MoveSpeedOnFrostCenti;
            public long BodyRadiusRaw, KeepDistanceRaw, WallMarginRaw;
            public Element Element, Weak, Resist;
            public List<Phase> Phases = new List<Phase>();
            public List<Pattern> Patterns = new List<Pattern>();
            public List<Override> SecondCycle = new List<Override>();
            public PhantomCopy? PhantomCopy;
            public FirstEncounter? FirstEncounter;
            public Body? Body;
            public string Pointer = "";
            public int Order;                         // 원본에서의 순서
        }

        private sealed class Phase
        {
            public int Index, EnterAtHpPercent;
            public EnemyTag Tags;
            public long AnchorRadiusRaw;
            public List<BossPatternDef.PatternKey> Patterns = new List<BossPatternDef.PatternKey>();
            public List<BossPatternDef.PatternKey> Passives = new List<BossPatternDef.PatternKey>();
            public List<Override> Overrides = new List<Override>();
        }

        private sealed class Override
        {
            public BossPatternDef.PatternKey Pattern;
            public int Step;
            public BossDef.OverrideKey Key;
            public long Value;
            public string Pointer = "";
        }

        private sealed class Pattern
        {
            public BossPatternDef.PatternKey Key;
            public bool IsPassive;
            public BossPatternDef.GrazeMask Graze;
            public int RecoveryTicks, RecoveryDamagePermille;
            public List<Step> Steps = new List<Step>();
            public string Pointer = "";
        }

        private sealed class Step
        {
            public BossPatternDef.StepKind Kind;
            public EnemiesConverter.Attack? Attack;
            public Copy? Copy;
            public Summon? Summon;
            public int Count = 1, Repeat = 1, RepeatIntervalTicks, RepeatAngleCenti, StartDelayTicks, MinIntervalTicks, FrostTelegraphTicks, HazardSpeedPermille;
            public bool TelegraphEachRepeat, RapidFire;
            public long ClearHazardMarginRaw, HazardDriftPerTickRaw;
            public Stream? Stream;
            public Placement? Placement;
            public Obstacle? Obstacle;
        }

        private sealed class Copy
        {
            public List<ArtId> Arts = new List<ArtId>();
            public ArtId Fallback;
            public int WarnTicks;
        }

        private sealed class Summon
        {
            public EnemyTypeId Enemy;
            public int Count, FieldCap, WarnTicks;
        }

        private sealed class Stream
        {
            public int IntervalTicks, SweepCenti, SweepCentiPerTick;
        }

        private sealed class Placement
        {
            public int AtPlayer, MaxTries;
            public long MinPlayerDistanceRaw, MinSpacingRaw, WallMarginRaw;
            public long? MaxPlayerDistanceRaw;
        }

        private sealed class Obstacle
        {
            public BossPatternDef.ObstacleKind Kind;
            public long RadiusRaw;
            public int DurationTicks, Hp, MaxCount;
            public bool BlocksPlayerShots, BlocksEnemyShots;
            public EnemiesConverter.Proj? Shards;
        }

        private sealed class PhantomCopy
        {
            public int IntervalPermille, MaxProjectiles, Damage, BounceCap, ExplosionRadiusPermille, ExplosionTelegraphTicks, ExplosionDamage;
            public int ChainLineTelegraphTicks, SplitMinAngleCenti, CopyMutationsFromCycle;
            public long SpeedPerTickRaw, ExplosionRadiusMaxRaw, ChainLineWidthRaw, ChainLineLengthRaw;
        }

        private sealed class FirstEncounter
        {
            public int HpFloorPercent, MaxFightTicks, RageStacks, StartPhase, FinisherTicks, FinisherHpZeroTick, CollapseTicks;
        }

        private sealed class Body
        {
            public long HeadRadiusRaw, SegmentRadiusRaw, TailRadiusRaw, SegmentSpacingRaw;
            public int SegmentCount, SegmentDamagePermille, TailDamagePermille, MaxSegmentHits;
        }

        // ---- 읽기 ----

        private static Common ReadCommon(JsonElement c, DataFile file, Diagnostics diagnostics)
        {
            var x = new Common
            {
                IntroTicks = Ticks(c, "intro_seconds"),
                PhaseTransitionTicks = Ticks(c, "phase_transition_seconds"),
                WaitTicks = Ticks(c, "wait_seconds"),
                NoCandidateWaitTicks = Ticks(c, "no_candidate_wait_seconds"),
                R5RetryTicks = Ticks(c, "r5_retry_seconds"),
                R5RepickTicks = Ticks(c, "r5_repick_seconds"),
                RageStackTicks = Ticks(c, "rage_stack_seconds"),
                RageMaxStacks = Int(c, "rage_max_stacks"),
                RageStepPermille = Units.RatioToPermille(Dec(c, "rage_step_ratio")),
                FinalRageTicks = Ticks(c, "final_rage_seconds"),
                FinalRageDamagePlus = Int(c, "final_rage_damage_plus"),
                ActionHaltTicks = Ticks(c, "action_halt_seconds"),
                ContactCooldownTicks = Ticks(c, "contact_cooldown_seconds"),
            };
            // 대기·간격 × (1000 − 감소 × n) ÷ 1000 이 최대 스택에서도 0 보다 크다(spec 06 2.11 WaitTicks, U2)
            if ((long)x.RageStepPermille * x.RageMaxStacks >= Units.PermillePerUnit)
            {
                diagnostics.Error(file.RepoPath, "/common/rage_step_ratio", "격노 스택당 감소 × 최대 스택이 1 이상이면 대기가 0 이하가 된다(spec 06 2.11·2.12)");
            }
            return x;
        }

        private static Boss? ReadBoss(JsonElement row, string at, Common common, DataFile file, Diagnostics diagnostics)
        {
            var b = new Boss { Pointer = at };
            bool ok = true;
            string id = Str(row, "id");
            if (!ContentIds.TryParse(id, out b.Id) || b.Id == BossId.None)
            {
                diagnostics.Error(file.RepoPath, at + "/id", "'" + id + "' 는 보스 id(spec 14 3.8)가 아니다");
                return null;
            }
            b.RotationIndex = Int(row, "rotation_index");
            b.BaseHp = Int(row, "base_hp");
            b.HpScaleAnchorWave = Int(row, "hp_scale_anchor_wave");
            decimal speed = Dec(row, "move_speed");
            b.MoveSpeedCenti = Units.ToCenti(speed);
            JsonElement frost = row.GetProperty("move_speed_on_frost");
            if (frost.ValueKind != JsonValueKind.Null)
            {
                b.MoveSpeedOnFrostCenti = Units.ToCenti(frost.GetDecimal());
                if (frost.GetDecimal() >= speed)
                {
                    ok &= Fail(file, at + "/move_speed_on_frost", "서리밭 위 걷는 속도는 평소 속도보다 느리다(spec 06 7.6, 흰뿔 3.0 → 2.4)", diagnostics);
                }
            }
            b.ContactDamage = Int(row, "contact_damage");
            b.BodyRadiusRaw = Units.CellsToRaw(Dec(row, "body_radius"));
            ok &= ReadElement(row, "element", at, true, file, diagnostics, out b.Element);
            ok &= ReadElement(row, "weak", at, false, file, diagnostics, out b.Weak);
            ok &= ReadElement(row, "resist", at, false, file, diagnostics, out b.Resist);
            if (ok && b.Weak == b.Resist)
            {
                ok &= Fail(file, at + "/resist", "약점과 저항이 같다(spec 06 1.3, spec 02 AF-3)", diagnostics);
            }
            if (row.TryGetProperty("movement", out JsonElement mv))
            {
                b.KeepDistanceRaw = Units.CellsToRaw(DecOr(mv, "keep_distance"));
                b.WallMarginRaw = Units.CellsToRaw(DecOr(mv, "wall_margin"));
            }
            if (row.TryGetProperty("phantom_copy", out JsonElement pc))
            {
                b.PhantomCopy = ReadPhantomCopy(pc);
            }
            if (row.TryGetProperty("body", out JsonElement body))
            {
                b.Body = new Body
                {
                    HeadRadiusRaw = Units.CellsToRaw(Dec(body, "head_radius")),
                    SegmentRadiusRaw = Units.CellsToRaw(Dec(body, "segment_radius")),
                    TailRadiusRaw = Units.CellsToRaw(Dec(body, "tail_radius")),
                    SegmentCount = Int(body, "segment_count"),
                    SegmentSpacingRaw = Units.CellsToRaw(Dec(body, "segment_spacing")),
                    SegmentDamagePermille = Units.RatioToPermille(Dec(body, "segment_damage_ratio")),
                    TailDamagePermille = Units.RatioToPermille(Dec(body, "tail_damage_ratio")),
                    MaxSegmentHits = Int(body, "max_segment_hits"),
                };
                if (b.Body.HeadRadiusRaw != b.BodyRadiusRaw)
                {
                    ok &= Fail(file, at + "/body/head_radius", "여러 부위로 된 몸의 body_radius 는 머리 반지름이다(spec 06 8.1)", diagnostics);
                }
            }

            int p = 0;
            foreach (JsonElement pr in row.GetProperty("patterns").EnumerateArray())
            {
                Pattern? x = ReadPattern(pr, at + "/patterns/" + Num(p), b, file, diagnostics);
                if (x == null)
                {
                    ok = false;
                }
                else if (FindPattern(b, x.Key) != null)
                {
                    ok &= Fail(file, x.Pointer + "/key", "패턴 열쇠 '" + PatternName(x.Key) + "' 가 이 보스에 두 번 있다", diagnostics);
                }
                else
                {
                    b.Patterns.Add(x);
                }
                p++;
            }
            if (!ok)
            {
                return null;                          // 패턴이 틀렸으면 단계와 덮어쓰기의 참조는 보지 않는다(진단이 엉뚱한 곳을 가리키지 않게)
            }

            int ph = 0;
            int previousPercent = 0;
            foreach (JsonElement phr in row.GetProperty("phases").EnumerateArray())
            {
                string pat = at + "/phases/" + Num(ph);
                Phase x = ReadPhase(phr, pat, b, file, diagnostics, ref ok);
                if (x.Index != ph + 1)
                {
                    ok &= Fail(file, pat + "/index", "단계 번호는 1부터 차례로다(" + Num(ph + 1) + " 이어야 한다, spec 06 1.4)", diagnostics);
                }
                if (ph == 0 && x.EnterAtHpPercent != 100)
                {
                    ok &= Fail(file, pat + "/enter_at_hp_percent", "1단계는 체력 100% 에서 시작한다(spec 06 1.4)", diagnostics);
                }
                if (ph > 0 && x.EnterAtHpPercent >= previousPercent)
                {
                    ok &= Fail(file, pat + "/enter_at_hp_percent", "단계가 들어가는 체력 % 는 단계마다 줄어든다(spec 06 1.3 enterAtHpPct 내림차순)", diagnostics);
                }
                previousPercent = x.EnterAtHpPercent;
                b.Phases.Add(x);
                ph++;
            }
            foreach (Pattern x in b.Patterns)
            {
                bool used = false;
                foreach (Phase f in b.Phases)
                {
                    used |= (x.IsPassive ? f.Passives : f.Patterns).Contains(x.Key);
                }
                if (!used)
                {
                    ok &= Fail(file, x.Pointer + "/key", "패턴 '" + PatternName(x.Key) + "' 가 어느 단계에도 없다", diagnostics);
                }
            }

            int s = 0;
            foreach (JsonElement o in row.GetProperty("second_cycle").EnumerateArray())
            {
                Override? x = ReadOverride(o, at + "/second_cycle/" + Num(s), b, null, file, diagnostics);
                if (x == null)
                {
                    ok = false;
                }
                else
                {
                    b.SecondCycle.Add(x);
                }
                s++;
            }
            ok &= CheckOverrideSet(b, b.SecondCycle, file, diagnostics);

            if (row.TryGetProperty("first_encounter", out JsonElement fe))
            {
                b.FirstEncounter = new FirstEncounter
                {
                    HpFloorPercent = Int(fe, "hp_floor_percent"),
                    MaxFightTicks = Ticks(fe, "max_fight_seconds"),
                    RageStacks = Int(fe, "rage_stacks"),
                    StartPhase = Int(fe, "start_phase"),
                    FinisherTicks = Ticks(fe, "finisher_seconds"),
                    FinisherHpZeroTick = Int(fe, "finisher_hp_zero_ticks"),
                    CollapseTicks = Ticks(fe, "collapse_seconds"),
                };
                if (b.FirstEncounter.StartPhase > b.Phases.Count)
                {
                    ok &= Fail(file, at + "/first_encounter/start_phase", "첫 만남의 시작 단계가 단계 수(" + Num(b.Phases.Count) + ")보다 크다(spec 06 9.2)", diagnostics);
                }
                if (b.FirstEncounter.RageStacks > common.RageMaxStacks)
                {
                    ok &= Fail(file, at + "/first_encounter/rage_stacks", "첫 만남의 격노 스택이 최대 스택(" + Num(common.RageMaxStacks) + ")보다 크다(spec 06 9.2)", diagnostics);
                }
                if (b.FirstEncounter.FinisherHpZeroTick >= b.FirstEncounter.FinisherTicks)
                {
                    ok &= Fail(file, at + "/first_encounter/finisher_hp_zero_ticks", "되비침 안에서 체력이 0 이 되는 틱은 되비침이 끝나기 전이다(spec 06 9.5, C12)", diagnostics);
                }
            }
            return ok ? b : null;
        }

        private static PhantomCopy ReadPhantomCopy(JsonElement pc) => new PhantomCopy
        {
            IntervalPermille = Units.RatioToPermille(Dec(pc, "interval_ratio")),
            MaxProjectiles = Int(pc, "max_projectiles"),
            SpeedPerTickRaw = Units.CellsPerSecondToRawPerTick(Dec(pc, "speed")),
            Damage = Int(pc, "damage"),
            BounceCap = Int(pc, "bounce_cap"),
            ExplosionRadiusPermille = Units.RatioToPermille(Dec(pc, "explosion_radius_ratio")),
            ExplosionRadiusMaxRaw = Units.CellsToRaw(Dec(pc, "explosion_radius_max")),
            ExplosionTelegraphTicks = Ticks(pc, "explosion_telegraph_seconds"),
            ExplosionDamage = Int(pc, "explosion_damage"),
            ChainLineTelegraphTicks = Ticks(pc, "chain_line_telegraph_seconds"),
            ChainLineWidthRaw = Units.CellsToRaw(Dec(pc, "chain_line_width")),
            ChainLineLengthRaw = Units.CellsToRaw(Dec(pc, "chain_line_length")),
            SplitMinAngleCenti = Units.DegreesToCenti(Dec(pc, "split_min_angle_degrees")),
            CopyMutationsFromCycle = Int(pc, "copy_mutations_from_cycle"),
        };

        private static Phase ReadPhase(JsonElement row, string at, Boss b, DataFile file, Diagnostics diagnostics, ref bool ok)
        {
            var x = new Phase
            {
                Index = Int(row, "index"),
                EnterAtHpPercent = Int(row, "enter_at_hp_percent"),
                AnchorRadiusRaw = Units.CellsToRaw(DecOr(row, "anchor_radius")),
            };
            int t = 0;
            foreach (JsonElement tag in row.GetProperty("tags").EnumerateArray())
            {
                string s = tag.GetString() ?? "";
                if (!ContentIds.TryParse(s, out EnemyTag v))
                {
                    ok &= Fail(file, at + "/tags/" + Num(t), "'" + s + "' 는 꼬리표 id(spec 14 3.9)가 아니다", diagnostics);
                }
                else if (v == EnemyTag.Elite)
                {
                    ok &= Fail(file, at + "/tags/" + Num(t), "보스에는 엘리트 꼬리표가 없다(spec 06 2.3)", diagnostics);
                }
                else
                {
                    x.Tags |= v;
                }
                t++;
            }
            if ((x.Tags & EnemyTag.Boss) == EnemyTag.None)
            {
                ok &= Fail(file, at + "/tags", "보스는 늘 꼬리표 tag.boss 를 갖는다(spec 06 1.2·2.3)", diagnostics);
            }
            ok &= ReadKeys(row, "patterns", at, b, false, x.Patterns, file, diagnostics);
            ok &= ReadKeys(row, "passives", at, b, true, x.Passives, file, diagnostics);
            if (x.Patterns.Count + x.Passives.Count == 0)
            {
                ok &= Fail(file, at + "/patterns", "단계에 고를 패턴도 상시 행동도 없다", diagnostics);
            }
            if (row.TryGetProperty("overrides", out JsonElement os))
            {
                int k = 0;
                foreach (JsonElement o in os.EnumerateArray())
                {
                    Override? v = ReadOverride(o, at + "/overrides/" + Num(k), b, x, file, diagnostics);
                    if (v == null)
                    {
                        ok = false;
                    }
                    else
                    {
                        x.Overrides.Add(v);
                    }
                    k++;
                }
                ok &= CheckOverrideSet(b, x.Overrides, file, diagnostics);
            }
            return x;
        }

        // 단계의 patterns(고르는 패턴) 또는 passives(상시 행동). 정규 순서는 PatternKey 오름차순이다(고르기의 난수가 이 순서로 고른다)
        private static bool ReadKeys(JsonElement row, string name, string at, Boss b, bool passive, List<BossPatternDef.PatternKey> into,
            DataFile file, Diagnostics diagnostics)
        {
            bool ok = true;
            int i = 0;
            foreach (JsonElement e in row.GetProperty(name).EnumerateArray())
            {
                string p = at + "/" + name + "/" + Num(i);
                string key = e.GetString() ?? "";
                Pattern? x = FindPattern(b, key);
                if (x == null)
                {
                    ok &= Fail(file, p, "'" + key + "' 는 이 보스의 패턴이 아니다", diagnostics);
                }
                else if (x.IsPassive != passive)
                {
                    ok &= Fail(file, p, passive
                        ? "'" + key + "' 는 상시 행동이 아니다: patterns 에 둔다(spec 06 1.4)"
                        : "'" + key + "' 는 상시 행동이다: passives 에 둔다(spec 06 1.4, C6)", diagnostics);
                }
                else
                {
                    into.Add(x.Key);
                }
                i++;
            }
            into.Sort((l, r) => ((byte)l).CompareTo((byte)r));
            return ok;
        }

        private static Pattern? ReadPattern(JsonElement row, string at, Boss b, DataFile file, Diagnostics diagnostics)
        {
            var x = new Pattern { Pointer = at };
            string key = Str(row, "key");
            PatternRow? def = PatternOf(key);
            if (def == null)
            {
                diagnostics.Error(file.RepoPath, at + "/key", "'" + key + "' 는 보스 패턴 열쇠(spec 06 3~8절의 패턴 표)가 아니다");
                return null;
            }
            if (def.Owner != b.Id)
            {
                diagnostics.Error(file.RepoPath, at + "/key", "'" + key + "' 는 " + ContentIds.ToId(def.Owner) + " 의 패턴이다(spec 06 1.5)");
                return null;
            }
            bool ok = true;
            x.Key = def.Key;
            x.IsPassive = row.GetProperty("passive").GetBoolean();
            foreach (JsonElement g in row.GetProperty("graze").EnumerateArray())
            {
                x.Graze |= GrazeOf(g.GetString());
            }
            x.RecoveryTicks = Ticks(row, "recovery_seconds");
            x.RecoveryDamagePermille = Units.RatioToPermille(Dec(row, "recovery_damage_ratio"));
            if (x.IsPassive && x.RecoveryTicks != 0)
            {
                ok &= Fail(file, at + "/recovery_seconds", "상시 행동에는 빈틈이 없다(빈틈은 고른 패턴 뒤의 것, spec 06 2.8)", diagnostics);
            }
            var attackKeys = new List<string>();
            int s = 0;
            foreach (JsonElement sr in row.GetProperty("steps").EnumerateArray())
            {
                string p = at + "/steps/" + Num(s);
                Step? step = ReadStep(sr, p, b, x, attackKeys, file, diagnostics);
                if (step == null)
                {
                    ok = false;
                }
                else
                {
                    x.Steps.Add(step);
                }
                s++;
            }
            return ok ? x : null;
        }

        private static Step? ReadStep(JsonElement row, string at, Boss b, Pattern owner, List<string> attackKeys, DataFile file, Diagnostics diagnostics)
        {
            var x = new Step();
            bool ok = true;
            bool hasAttack = row.TryGetProperty("attack", out JsonElement ar);
            bool hasCopy = row.TryGetProperty("copy", out JsonElement cr);
            bool hasSummon = row.TryGetProperty("summon", out JsonElement sr);
            if ((hasAttack ? 1 : 0) + (hasCopy ? 1 : 0) + (hasSummon ? 1 : 0) != 1)
            {
                diagnostics.Error(file.RepoPath, at, "패턴의 단계에는 attack·copy·summon 가운데 꼭 하나가 있다");
                return null;
            }
            if (hasAttack)
            {
                x.Kind = BossPatternDef.StepKind.Attack;
                string key = Str(ar, "key");
                if (attackKeys.Contains(key))
                {
                    ok &= Fail(file, at + "/attack/key", "공격 열쇠 '" + key + "' 가 이 패턴에 두 번 있다", diagnostics);
                }
                attackKeys.Add(key);
                x.Attack = EnemiesConverter.ReadAttack(ar, at + "/attack", b.ContactDamage, file, diagnostics);
                if (x.Attack == null)
                {
                    return null;
                }
                if (x.Attack.Kind == AttackDef.AttackKind.Contact)
                {
                    ok &= Fail(file, at + "/attack/kind", "보스의 몸 접촉은 패턴이 아니라 contact_damage 가 정한다(spec 06 1.3·2.4)", diagnostics);
                }
                if (x.Attack.RecoveryTicks != 0)
                {
                    ok &= Fail(file, at + "/attack/recovery_seconds", "보스 공격의 빈틈은 패턴의 recovery_seconds 다(spec 06 1.5 recoveryTicks)", diagnostics);
                }
                if (x.Attack.CooldownTicks != 0)
                {
                    ok &= Fail(file, at + "/attack/cooldown_seconds", "보스 공격에는 재사용 대기가 없다: 패턴 사이 대기는 공통 규칙이다(spec 06 2.11)", diagnostics);
                }
            }
            else if (hasCopy)
            {
                x.Kind = BossPatternDef.StepKind.Copy;
                x.Copy = new Copy { WarnTicks = Ticks(cr, "warn_seconds") };
                int i = 0;
                foreach (JsonElement art in cr.GetProperty("arts").EnumerateArray())
                {
                    ok &= ReadArt(art, at + "/copy/arts/" + Num(i), file, diagnostics, out ArtId v);
                    x.Copy.Arts.Add(v);
                    i++;
                }
                x.Copy.Arts.Sort((l, r) => ((byte)l).CompareTo((byte)r));
                JsonElement fb = cr.GetProperty("fallback");
                if (fb.ValueKind != JsonValueKind.Null)
                {
                    ok &= ReadArt(fb, at + "/copy/fallback", file, diagnostics, out x.Copy.Fallback);
                }
                if (b.PhantomCopy == null)
                {
                    ok &= Fail(file, at + "/copy", "복사 발사는 허상의 복사 규칙(phantom_copy)이 있는 보스만 쓴다(spec 06 4.4)", diagnostics);
                }
            }
            else
            {
                x.Kind = BossPatternDef.StepKind.Summon;
                string enemy = Str(sr, "enemy");
                x.Summon = new Summon
                {
                    Count = Int(sr, "count"),
                    FieldCap = Int(sr, "field_cap"),
                    WarnTicks = Ticks(sr, "warn_seconds"),
                };
                if (!ContentIds.TryParse(enemy, out x.Summon.Enemy) || x.Summon.Enemy == EnemyTypeId.None)
                {
                    ok &= Fail(file, at + "/summon/enemy", "'" + enemy + "' 는 적 id(spec 14 3.6)가 아니다", diagnostics);
                }
            }

            x.Count = row.TryGetProperty("count", out JsonElement count) ? IntOf(count, "count") : 1;
            x.Repeat = row.TryGetProperty("repeat", out JsonElement repeat) ? IntOf(repeat, "repeat") : 1;
            x.RepeatIntervalTicks = Units.SecondsToTicks(DecOr(row, "repeat_interval_seconds"));
            x.RepeatAngleCenti = Units.DegreesToCenti(DecOr(row, "repeat_angle_degrees"));
            x.TelegraphEachRepeat = BoolOr(row, "telegraph_each_repeat");
            x.StartDelayTicks = Units.SecondsToTicks(DecOr(row, "start_delay_seconds"));
            x.RapidFire = BoolOr(row, "rapid_fire");
            x.MinIntervalTicks = row.TryGetProperty("min_interval_ticks", out JsonElement mi) ? IntOf(mi, "min_interval_ticks") : 0;
            x.FrostTelegraphTicks = Units.SecondsToTicks(DecOr(row, "frost_telegraph_seconds"));
            x.ClearHazardMarginRaw = Units.CellsToRaw(DecOr(row, "clear_hazard_margin"));
            x.HazardSpeedPermille = Units.RatioToPermille(DecOr(row, "hazard_speed_ratio"));
            x.HazardDriftPerTickRaw = Units.CellsPerSecondToRawPerTick(DecOr(row, "hazard_drift_speed"));

            EnemiesConverter.Attack? a = x.Attack;
            bool zone = a != null && a.Shape.HasValue;
            if (x.Repeat == 0 && !owner.IsPassive)
            {
                ok &= Fail(file, at + "/repeat", "끝없이 되풀이하는 단계(repeat 0)는 상시 행동에만 있다(spec 06 1.4 passiveIds)", diagnostics);
            }
            if (x.RapidFire != (x.MinIntervalTicks > 0))
            {
                ok &= Fail(file, at + "/min_interval_ticks", "연사형(rapid_fire)에는 R3 하한(min_interval_ticks)이 있고, 연사형이 아니면 없다(spec 06 2.12 표)", diagnostics);
            }
            if (x.RapidFire && a?.Proj == null && x.Kind != BossPatternDef.StepKind.Copy)
            {
                ok &= Fail(file, at + "/rapid_fire", "연사형은 탄을 쏘는 단계(탄이 있는 공격, 복사 발사)에만 있다(spec 06 2.12 \"예고 구역을 잇달아 까는 패턴은 줄이지 않는다\")", diagnostics);
            }
            if (x.TelegraphEachRepeat && !zone)
            {
                ok &= Fail(file, at + "/telegraph_each_repeat", "반복마다 다시 보일 예고 구역이 없다", diagnostics);
            }
            if (x.FrostTelegraphTicks != 0 && (!zone || x.FrostTelegraphTicks < a!.TelegraphTicks))
            {
                ok &= Fail(file, at + "/frost_telegraph_seconds", "서리밭 안의 플레이어를 겨냥한 예고는 예고 구역이 있는 공격에만, 평소 예고보다 짧지 않다(spec 06 7.6)", diagnostics);
            }
            if ((x.ClearHazardMarginRaw != 0 || x.HazardSpeedPermille != 0 || x.HazardDriftPerTickRaw != 0) && a?.Hazard == null)
            {
                ok &= Fail(file, at + (x.ClearHazardMarginRaw != 0 ? "/clear_hazard_margin" : x.HazardSpeedPermille != 0 ? "/hazard_speed_ratio" : "/hazard_drift_speed"),
                    "남는 구역을 지우는 거리·안의 이동 속도·흐르는 속도는 남는 구역(leaves_hazard)이 있는 공격에만 있다", diagnostics);
            }
            if (row.TryGetProperty("stream", out JsonElement st))
            {
                int perSecond = Int(st, "shots_per_second");
                int perSecondCenti = Units.DegreesToCenti(Dec(st, "sweep_degrees_per_second"));
                x.Stream = new Stream
                {
                    IntervalTicks = perSecond > 0 ? Units.TicksPerSecond / perSecond : 0,
                    SweepCenti = Units.DegreesToCenti(Dec(st, "sweep_degrees")),
                    SweepCentiPerTick = perSecondCenti / Units.TicksPerSecond,
                };
                if (a?.Proj == null)
                {
                    ok &= Fail(file, at + "/stream", "탄 줄기는 탄(proj)이 있는 공격에만 있다", diagnostics);
                }
                if (Units.TicksPerSecond % perSecond != 0)
                {
                    ok &= Fail(file, at + "/stream/shots_per_second", "초당 발수가 60틱을 나누지 않는다(줄기 간격이 정수 틱이 아니다)", diagnostics);
                }
                if (perSecondCenti % Units.TicksPerSecond != 0)
                {
                    ok &= Fail(file, at + "/stream/sweep_degrees_per_second", "초당 도는 각이 정수 센티도/틱이 아니다(spec 06 7.2 초당 45도 = 75 센티도/틱)", diagnostics);
                }
                else if (x.Stream.SweepCenti % x.Stream.SweepCentiPerTick != 0)
                {
                    ok &= Fail(file, at + "/stream/sweep_degrees", "쓸어 가는 각이 틱당 각으로 나누어떨어지지 않는다(쓰는 틱 수가 정수가 아니다)", diagnostics);
                }
            }
            if (row.TryGetProperty("placement", out JsonElement pl))
            {
                x.Placement = new Placement
                {
                    AtPlayer = Int(pl, "at_player"),
                    MinPlayerDistanceRaw = Units.CellsToRaw(Dec(pl, "min_player_distance")),
                    MaxPlayerDistanceRaw = EnemiesConverter.OptionalCells(pl, "max_player_distance"),
                    MinSpacingRaw = Units.CellsToRaw(Dec(pl, "min_spacing")),
                    WallMarginRaw = Units.CellsToRaw(Dec(pl, "wall_margin")),
                    MaxTries = Int(pl, "max_tries"),
                };
                if (!zone)
                {
                    ok &= Fail(file, at + "/placement", "여러 구역의 자리는 예고 구역이 있는 공격에만 있다", diagnostics);
                }
                if (x.Placement.AtPlayer > x.Count)
                {
                    ok &= Fail(file, at + "/placement/at_player", "플레이어 위치에 놓는 수가 구역 수(count)보다 많다", diagnostics);
                }
                if (x.Placement.MaxPlayerDistanceRaw.HasValue && x.Placement.MaxPlayerDistanceRaw.Value < x.Placement.MinPlayerDistanceRaw)
                {
                    ok &= Fail(file, at + "/placement/max_player_distance", "플레이어와의 거리 위 끝이 아래 끝보다 작다", diagnostics);
                }
            }
            if (row.TryGetProperty("obstacle", out JsonElement ob))
            {
                x.Obstacle = new Obstacle
                {
                    Kind = ObstacleOf(ob.GetProperty("kind").GetString()),
                    RadiusRaw = Units.CellsToRaw(Dec(ob, "radius")),
                    DurationTicks = Ticks(ob, "duration_seconds"),
                    Hp = Int(ob, "hp"),
                    MaxCount = Int(ob, "max_count"),
                    BlocksPlayerShots = ob.GetProperty("blocks_player_shots").GetBoolean(),
                    BlocksEnemyShots = ob.GetProperty("blocks_enemy_shots").GetBoolean(),
                };
                JsonElement sh = ob.GetProperty("shards");
                if (sh.ValueKind != JsonValueKind.Null)
                {
                    x.Obstacle.Shards = ReadProj(sh);
                }
                if (!zone)
                {
                    ok &= Fail(file, at + "/obstacle", "장애물은 예고 구역이 터진 자리에 생긴다: 예고 구역이 있는 공격에만 있다", diagnostics);
                }
                if (x.Obstacle.DurationTicks == 0 && x.Obstacle.Hp == 0)
                {
                    ok &= Fail(file, at + "/obstacle", "시간도 체력도 없는 장애물은 사라지지 않는다(duration_seconds 또는 hp)", diagnostics);
                }
            }
            return ok ? x : null;
        }

        private static EnemiesConverter.Proj ReadProj(JsonElement pj) => new EnemiesConverter.Proj
        {
            SpeedPerTickRaw = Units.CellsPerSecondToRawPerTick(Dec(pj, "speed")),
            Count = Int(pj, "count"),
            SpreadCenti = Units.DegreesToCenti(Dec(pj, "spread_degrees")),
            RangeRaw = EnemiesConverter.OptionalCells(pj, "range") ?? 0,      // null = 벽에 닿을 때까지(0)
            HitRadiusRaw = Units.CellsToRaw(Dec(pj, "hit_radius")),
        };

        // phase 가 null 이면 2회차(second_cycle) 덮어쓰기다
        private static Override? ReadOverride(JsonElement row, string at, Boss b, Phase? phase, DataFile file, Diagnostics diagnostics)
        {
            string key = Str(row, "pattern");
            Pattern? target = FindPattern(b, key);
            if (target == null)
            {
                diagnostics.Error(file.RepoPath, at + "/pattern", "'" + key + "' 는 이 보스의 패턴이 아니다");
                return null;
            }
            if (phase != null && !phase.Patterns.Contains(target.Key) && !phase.Passives.Contains(target.Key))
            {
                diagnostics.Error(file.RepoPath, at + "/pattern", "'" + key + "' 는 이 단계에 나오지 않는다(덮어쓸 것이 없다)");
                return null;
            }
            int step = Int(row, "step");
            if (step >= target.Steps.Count)
            {
                diagnostics.Error(file.RepoPath, at + "/step", "'" + key + "' 의 단계는 " + Num(target.Steps.Count) + "개다(첨자 0~" + Num(target.Steps.Count - 1) + ")");
                return null;
            }
            Step s = target.Steps[step];
            decimal v = Dec(row, "value");
            var x = new Override { Pattern = target.Key, Step = step, Pointer = at };
            string? problem = null;
            switch (Str(row, "key"))
            {
                case "repeat":
                    x.Key = BossDef.OverrideKey.Repeat;
                    x.Value = IntOf(row.GetProperty("value"), "value");
                    problem = s.Repeat == 0 || x.Value < 1 ? "끝없이 되풀이하는 단계의 반복 수는 바꾸지 않고, 반복 수는 1 이상이다" : null;
                    break;
                case "count":
                    x.Key = BossDef.OverrideKey.Count;
                    x.Value = IntOf(row.GetProperty("value"), "value");
                    problem = x.Value < 1 ? "수는 1 이상이다" : null;
                    break;
                case "repeat_interval_seconds":
                    x.Key = BossDef.OverrideKey.RepeatIntervalTicks;
                    x.Value = Units.SecondsToTicks(v);
                    break;
                case "repeat_angle_degrees":
                    x.Key = BossDef.OverrideKey.RepeatAngleCenti;
                    x.Value = Units.DegreesToCenti(v);
                    break;
                case "radius":
                    x.Key = BossDef.OverrideKey.RadiusRaw;
                    x.Value = Units.CellsToRaw(v);
                    problem = s.Attack == null || !IsRound(s.Attack.Shape) || x.Value <= 0 ? "반지름은 원·부채꼴·고리 예고의 공격에만 있다(0 보다 크다)" : null;
                    break;
                case "inner_radius":
                    x.Key = BossDef.OverrideKey.InnerRadiusRaw;
                    x.Value = Units.CellsToRaw(v);
                    problem = s.Attack?.Shape != AttackDef.TelegraphShape.Ring ? "안 반지름은 고리 예고의 공격에만 있다" : null;
                    break;
                case "sweep_degrees":
                    x.Key = BossDef.OverrideKey.SweepCenti;
                    x.Value = Units.DegreesToCenti(v);
                    problem = s.Stream == null || x.Value <= 0 || s.Stream.SweepCentiPerTick == 0 || x.Value % s.Stream.SweepCentiPerTick != 0
                        ? "쓸어 가는 각은 탄 줄기(stream)가 있는 단계에만 있고 틱당 각으로 나누어떨어진다" : null;
                    break;
                case "shard_count":
                    x.Key = BossDef.OverrideKey.ShardCount;
                    x.Value = IntOf(row.GetProperty("value"), "value");
                    problem = s.Obstacle?.Shards == null || x.Value < 1 ? "파편 수는 파편이 있는 장애물의 단계에만 있다(1 이상)" : null;
                    break;
                case "shard_spread_degrees":
                    x.Key = BossDef.OverrideKey.ShardSpreadCenti;
                    x.Value = Units.DegreesToCenti(v);
                    problem = s.Obstacle?.Shards == null ? "파편 각은 파편이 있는 장애물의 단계에만 있다" : null;
                    break;
                case "hazard_drift_speed":
                    x.Key = BossDef.OverrideKey.HazardDriftPerTickRaw;
                    x.Value = Units.CellsPerSecondToRawPerTick(v);
                    problem = s.Attack?.Hazard == null ? "흐르는 속도는 남는 구역(leaves_hazard)이 있는 공격에만 있다" : null;
                    break;
                default:
                    throw new DataBuildException("모르는 덮어쓰기 열쇠 '" + Str(row, "key") + "'");
            }
            if (problem != null)
            {
                diagnostics.Error(file.RepoPath, at + "/key", "'" + key + "' 의 단계 " + Num(step) + ": " + problem);
                return null;
            }
            return x;
        }

        // 한 목록 안에서 같은 칸을 두 번 바꾸지 않는다. 고리의 안 반지름 < 바깥 반지름(덮어쓴 뒤)
        private static bool CheckOverrideSet(Boss b, List<Override> list, DataFile file, Diagnostics diagnostics)
        {
            bool ok = true;
            for (int i = 0; i < list.Count; i++)
            {
                for (int j = 0; j < i; j++)
                {
                    if (list[i].Pattern == list[j].Pattern && list[i].Step == list[j].Step && list[i].Key == list[j].Key)
                    {
                        ok &= Fail(file, list[i].Pointer, "같은 칸(" + PatternName(list[i].Pattern) + " 단계 " + Num(list[i].Step) + ")을 두 번 바꾼다", diagnostics);
                    }
                }
            }
            foreach (Pattern p in b.Patterns)
            {
                for (int s = 0; s < p.Steps.Count; s++)
                {
                    EnemiesConverter.Attack? a = p.Steps[s].Attack;
                    if (a?.Shape != AttackDef.TelegraphShape.Ring)
                    {
                        continue;
                    }
                    long outer = a.RadiusRaw;
                    long inner = a.InnerRadiusRaw;
                    string pointer = "";
                    foreach (Override o in list)
                    {
                        if (o.Pattern == p.Key && o.Step == s)
                        {
                            outer = o.Key == BossDef.OverrideKey.RadiusRaw ? o.Value : outer;
                            inner = o.Key == BossDef.OverrideKey.InnerRadiusRaw ? o.Value : inner;
                            pointer = o.Pointer;
                        }
                    }
                    if (pointer.Length > 0 && inner >= outer)
                    {
                        ok &= Fail(file, pointer, "덮어쓴 고리의 안 반지름이 바깥 반지름보다 작지 않다", diagnostics);
                    }
                }
            }
            list.Sort((l, r) => l.Pattern != r.Pattern ? ((byte)l.Pattern).CompareTo((byte)r.Pattern)
                : l.Step != r.Step ? l.Step.CompareTo(r.Step) : ((byte)l.Key).CompareTo((byte)r.Key));
            return ok;
        }

        private static bool IsRound(AttackDef.TelegraphShape? s) =>
            s == AttackDef.TelegraphShape.Circle || s == AttackDef.TelegraphShape.Fan || s == AttackDef.TelegraphShape.Ring;

        private static Pattern? FindPattern(Boss b, BossPatternDef.PatternKey key)
        {
            foreach (Pattern p in b.Patterns)
            {
                if (p.Key == key)
                {
                    return p;
                }
            }
            return null;
        }

        private static Pattern? FindPattern(Boss b, string name)
        {
            PatternRow? row = PatternOf(name);
            return row == null ? null : FindPattern(b, row.Key);
        }

        private static PatternRow? PatternOf(string name)
        {
            foreach (PatternRow r in Patterns)
            {
                if (r.Name == name)
                {
                    return r;
                }
            }
            return null;
        }

        public static string PatternName(BossPatternDef.PatternKey key)
        {
            foreach (PatternRow r in Patterns)
            {
                if (r.Key == key)
                {
                    return r.Name;
                }
            }
            return "none";
        }

        // 그 패턴 열쇠를 가진 보스. 표에 없으면 None
        public static BossId OwnerOf(BossPatternDef.PatternKey key)
        {
            foreach (PatternRow r in Patterns)
            {
                if (r.Key == key)
                {
                    return r.Owner;
                }
            }
            return BossId.None;
        }

        private static bool ReadElement(JsonElement row, string name, string at, bool allowNone, DataFile file, Diagnostics diagnostics, out Element v)
        {
            string s = Str(row, name);
            if (!ContentIds.TryParse(s, out v))
            {
                diagnostics.Error(file.RepoPath, at + "/" + name, "'" + s + "' 는 속성 id(spec 14 3.1)가 아니다");
                return false;
            }
            if (!allowNone && v == Element.None)
            {
                diagnostics.Error(file.RepoPath, at + "/" + name, "보스의 약점·저항은 무속성이 아니다(spec 06 1.3, spec 02 AF-2)");
                return false;
            }
            return true;
        }

        private static bool ReadArt(JsonElement e, string at, DataFile file, Diagnostics diagnostics, out ArtId v)
        {
            string s = e.GetString() ?? "";
            if (!ContentIds.TryParse(s, out v) || v == ArtId.None)
            {
                diagnostics.Error(file.RepoPath, at, "'" + s + "' 는 비기 id(spec 14 3.4)가 아니다");
                return false;
            }
            return true;
        }

        private static bool Fail(DataFile file, string pointer, string message, Diagnostics diagnostics)
        {
            diagnostics.Error(file.RepoPath, pointer, message);
            return false;
        }

        // 스키마의 enum 이 이미 거른 값이다
        private static BossPatternDef.GrazeMask GrazeOf(string? s)
        {
            switch (s)
            {
                case "bullet": return BossPatternDef.GrazeMask.Bullet;
                case "charge": return BossPatternDef.GrazeMask.Charge;
                case "zone": return BossPatternDef.GrazeMask.Zone;
                default: throw new DataBuildException("모르는 스침 '" + s + "'");
            }
        }

        private static BossPatternDef.ObstacleKind ObstacleOf(string? s)
        {
            switch (s)
            {
                case "boulder": return BossPatternDef.ObstacleKind.Boulder;
                case "ice_pillar": return BossPatternDef.ObstacleKind.IcePillar;
                default: throw new DataBuildException("모르는 장애물 '" + s + "'");
            }
        }

        private static string Str(JsonElement o, string name) =>
            o.TryGetProperty(name, out JsonElement v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";

        private static decimal Dec(JsonElement o, string name) => o.GetProperty(name).GetDecimal();

        private static decimal DecOr(JsonElement o, string name) => EnemiesConverter.DecOr(o, name);

        private static bool BoolOr(JsonElement o, string name) => EnemiesConverter.BoolOr(o, name);

        private static int Int(JsonElement o, string name) => IntOf(o.GetProperty(name), name);

        private static int IntOf(JsonElement e, string name) => EnemiesConverter.IntOf(e, name);

        private static int Ticks(JsonElement o, string name) => Units.SecondsToTicks(Dec(o, name));

        private static string Num(int v) => v.ToString(CultureInfo.InvariantCulture);

        // ---- 쓰기 ----

        private static void WriteCommon(Common c, ByteWriter w)
        {
            w.I32(c.IntroTicks);
            w.I32(c.PhaseTransitionTicks);
            w.I32(c.WaitTicks);
            w.I32(c.NoCandidateWaitTicks);
            w.I32(c.R5RetryTicks);
            w.I32(c.R5RepickTicks);
            w.I32(c.RageStackTicks);
            w.I32(c.RageMaxStacks);
            w.I32(c.RageStepPermille);
            w.I32(c.FinalRageTicks);
            w.I32(c.FinalRageDamagePlus);
            w.I32(c.ActionHaltTicks);
            w.I32(c.ContactCooldownTicks);
        }

        private static void WriteBoss(Boss b, ByteWriter w)
        {
            w.U16((ushort)b.Id);
            w.I32(b.RotationIndex);
            w.I32(b.BaseHp);
            w.I32(b.HpScaleAnchorWave);
            w.I32(b.MoveSpeedCenti);
            Bool(w, b.MoveSpeedOnFrostCenti.HasValue);
            w.I32(b.MoveSpeedOnFrostCenti ?? 0);
            w.I32(b.ContactDamage);
            w.I64(b.BodyRadiusRaw);
            w.U8((byte)b.Element);
            w.U8((byte)b.Weak);
            w.U8((byte)b.Resist);
            w.I64(b.KeepDistanceRaw);
            w.I64(b.WallMarginRaw);
            w.U8((byte)b.Phases.Count);
            foreach (Phase p in b.Phases)
            {
                w.I32(p.Index);
                w.I32(p.EnterAtHpPercent);
                w.U16((ushort)p.Tags);
                w.I64(p.AnchorRadiusRaw);
                WriteKeys(p.Patterns, w);
                WriteKeys(p.Passives, w);
                WriteOverrides(p.Overrides, w);
            }
            // 정규 순서: PatternKey 오름차순
            b.Patterns.Sort((l, r) => ((byte)l.Key).CompareTo((byte)r.Key));
            w.U8((byte)b.Patterns.Count);
            foreach (Pattern p in b.Patterns)
            {
                w.U8((byte)p.Key);
                Bool(w, p.IsPassive);
                w.U8((byte)p.Graze);
                w.I32(p.RecoveryTicks);
                w.I32(p.RecoveryDamagePermille);
                w.U8((byte)p.Steps.Count);
                foreach (Step s in p.Steps)
                {
                    WriteStep(s, w);
                }
            }
            WriteOverrides(b.SecondCycle, w);

            PhantomCopy? pc = b.PhantomCopy;
            Bool(w, pc != null);
            w.I32(pc?.IntervalPermille ?? 0);
            w.I32(pc?.MaxProjectiles ?? 0);
            w.I64(pc?.SpeedPerTickRaw ?? 0);
            w.I32(pc?.Damage ?? 0);
            w.I32(pc?.BounceCap ?? 0);
            w.I32(pc?.ExplosionRadiusPermille ?? 0);
            w.I64(pc?.ExplosionRadiusMaxRaw ?? 0);
            w.I32(pc?.ExplosionTelegraphTicks ?? 0);
            w.I32(pc?.ExplosionDamage ?? 0);
            w.I32(pc?.ChainLineTelegraphTicks ?? 0);
            w.I64(pc?.ChainLineWidthRaw ?? 0);
            w.I64(pc?.ChainLineLengthRaw ?? 0);
            w.I32(pc?.SplitMinAngleCenti ?? 0);
            w.I32(pc?.CopyMutationsFromCycle ?? 0);

            FirstEncounter? fe = b.FirstEncounter;
            Bool(w, fe != null);
            w.I32(fe?.HpFloorPercent ?? 0);
            w.I32(fe?.MaxFightTicks ?? 0);
            w.I32(fe?.RageStacks ?? 0);
            w.I32(fe?.StartPhase ?? 0);
            w.I32(fe?.FinisherTicks ?? 0);
            w.I32(fe?.FinisherHpZeroTick ?? 0);
            w.I32(fe?.CollapseTicks ?? 0);

            Body? body = b.Body;
            Bool(w, body != null);
            w.I64(body?.HeadRadiusRaw ?? 0);
            w.I64(body?.SegmentRadiusRaw ?? 0);
            w.I64(body?.TailRadiusRaw ?? 0);
            w.I32(body?.SegmentCount ?? 0);
            w.I64(body?.SegmentSpacingRaw ?? 0);
            w.I32(body?.SegmentDamagePermille ?? 0);
            w.I32(body?.TailDamagePermille ?? 0);
            w.I32(body?.MaxSegmentHits ?? 0);
        }

        private static void WriteKeys(List<BossPatternDef.PatternKey> keys, ByteWriter w)
        {
            w.U8((byte)keys.Count);
            foreach (BossPatternDef.PatternKey k in keys)
            {
                w.U8((byte)k);
            }
        }

        private static void WriteOverrides(List<Override> list, ByteWriter w)
        {
            w.U8((byte)list.Count);
            foreach (Override o in list)
            {
                w.U8((byte)o.Pattern);
                w.I32(o.Step);
                w.U8((byte)o.Key);
                w.I64(o.Value);
            }
        }

        private static void WriteStep(Step s, ByteWriter w)
        {
            w.U8((byte)s.Kind);
            switch (s.Kind)
            {
                case BossPatternDef.StepKind.Attack:
                    EnemiesConverter.WriteAttack(s.Attack!, w);
                    break;
                case BossPatternDef.StepKind.Copy:
                    w.U8((byte)s.Copy!.Arts.Count);
                    foreach (ArtId a in s.Copy.Arts)
                    {
                        w.U8((byte)a);
                    }
                    w.U8((byte)s.Copy.Fallback);
                    w.I32(s.Copy.WarnTicks);
                    break;
                default:
                    w.U16((ushort)s.Summon!.Enemy);
                    w.I32(s.Summon.Count);
                    w.I32(s.Summon.FieldCap);
                    w.I32(s.Summon.WarnTicks);
                    break;
            }
            w.I32(s.Count);
            w.I32(s.Repeat);
            w.I32(s.RepeatIntervalTicks);
            w.I32(s.RepeatAngleCenti);
            Bool(w, s.TelegraphEachRepeat);
            w.I32(s.StartDelayTicks);
            Bool(w, s.RapidFire);
            w.I32(s.MinIntervalTicks);
            w.I32(s.FrostTelegraphTicks);
            w.I64(s.ClearHazardMarginRaw);
            w.I32(s.HazardSpeedPermille);
            w.I64(s.HazardDriftPerTickRaw);

            Bool(w, s.Stream != null);
            w.I32(s.Stream?.IntervalTicks ?? 0);
            w.I32(s.Stream?.SweepCenti ?? 0);
            w.I32(s.Stream?.SweepCentiPerTick ?? 0);

            Placement? pl = s.Placement;
            Bool(w, pl != null);
            w.I32(pl?.AtPlayer ?? 0);
            w.I64(pl?.MinPlayerDistanceRaw ?? 0);
            Bool(w, pl?.MaxPlayerDistanceRaw.HasValue ?? false);
            w.I64(pl?.MaxPlayerDistanceRaw ?? 0);
            w.I64(pl?.MinSpacingRaw ?? 0);
            w.I64(pl?.WallMarginRaw ?? 0);
            w.I32(pl?.MaxTries ?? 0);

            Obstacle? ob = s.Obstacle;
            Bool(w, ob != null);
            w.U8(ob != null ? (byte)ob.Kind : (byte)0);
            w.I64(ob?.RadiusRaw ?? 0);
            w.I32(ob?.DurationTicks ?? 0);
            w.I32(ob?.Hp ?? 0);
            w.I32(ob?.MaxCount ?? 0);
            Bool(w, ob?.BlocksPlayerShots ?? false);
            Bool(w, ob?.BlocksEnemyShots ?? false);
            EnemiesConverter.Proj? sh = ob?.Shards;
            Bool(w, sh != null);
            w.I64(sh?.SpeedPerTickRaw ?? 0);
            w.I32(sh?.Count ?? 0);
            w.I32(sh?.SpreadCenti ?? 0);
            w.I64(sh?.RangeRaw ?? 0);
            w.I64(sh?.HitRadiusRaw ?? 0);
        }

        private static void Bool(ByteWriter w, bool v) => w.U8(v ? (byte)1 : (byte)0);

        private static DataFile? FileOf(DataSet data, string path)
        {
            foreach (DataFile f in data.Files)
            {
                if (f.DataPath == path)
                {
                    return f;
                }
            }
            return null;
        }

        // 교차 검사(data/sim/bosses.json ↔ enemies.json): 부르는 졸개의 종이 적 파일에 있다. 두 파일 가운데 하나라도 없으면 보지 않는다
        public sealed class CrossCheck : IDataValidator
        {
            public string Name => "bosses-refs";

            public void Validate(DataSet data, Diagnostics diagnostics)
            {
                DataFile? bosses = FileOf(data, BossesFile);
                DataFile? enemies = FileOf(data, EnemiesFile);
                if (bosses == null || enemies == null || !enemies.Root.TryGetProperty("enemies", out JsonElement rows) || rows.ValueKind != JsonValueKind.Array)
                {
                    return;
                }
                var known = new List<string>();
                foreach (JsonElement r in rows.EnumerateArray())
                {
                    known.Add(Str(r, "id"));
                }
                int i = 0;
                foreach (JsonElement b in bosses.Root.GetProperty("bosses").EnumerateArray())
                {
                    int p = 0;
                    foreach (JsonElement pr in b.GetProperty("patterns").EnumerateArray())
                    {
                        int s = 0;
                        foreach (JsonElement sr in pr.GetProperty("steps").EnumerateArray())
                        {
                            if (sr.TryGetProperty("summon", out JsonElement su))
                            {
                                string enemy = Str(su, "enemy");
                                if (!known.Contains(enemy))
                                {
                                    diagnostics.Error(bosses.RepoPath, "/bosses/" + Num(i) + "/patterns/" + Num(p) + "/steps/" + Num(s) + "/summon/enemy",
                                        "부르는 졸개 '" + enemy + "' 가 " + DataLoader.DataDir + "/" + EnemiesFile + " 에 없다(spec 06 2.5)");
                                }
                            }
                            s++;
                        }
                        p++;
                    }
                    i++;
                }
            }
        }

        // 회피 검산(spec 06 2.13, spec 05 10절 "같은 검증기, 같은 표")의 원본. 보스 공격을 그대로 넘기고 다음을 더 넘긴다:
        //   - 단계(phases.overrides)·2회차(second_cycle)·둘 다의 덮어쓰기를 건 모양(또아리 2단계 고리, 흰뿔 2회차 파편 12방향 …)
        //   - 돌진 띠가 둘 이상(십자 활강, spec 06 6.4)이면 교차점의 벗어나는 거리 √2 × (w/2 + 피격 반지름)
        //   - 서리밭을 까는 보스(흰뿔)의 예고 구역은 V_frost 로도(서리밭 예고가 있으면 그 예고로, spec 06 7.6·7.8)
        //   - 줄기(숨결)는 초당 발수·각속도로 R3 줄기 검사
        //   - 장애물의 파편, 허상의 복사 규칙(복사 탄의 속도, 폭발 예고 원, 직선 번개의 조준선)
        //   - 연사형 R3 하한: min_interval_ticks 가 "3칸 지점의 잇따른 두 탄 사이 ≥ 1.0칸"인 가장 작은 정수 틱보다 작으면 R3 위반(spec 06 2.12, U4)
        //   - 허상의 분열 최소 각: 3칸 지점 호 길이 3 × Δθ ≥ 1.0칸(spec 06 4.4, C9)
        //   - 몸(R2): 걷는 속도와 서리밭 위 걷는 속도(보스는 북두꺼비 버프를 받지 않는다)
        // 피해가 없는 예고 구역(서리밭)은 피할 것이 없어 R1 대상이 아니다. 꼴이 틀린 줄은 변환기가 알리므로 읽을 수 있는 것만 옮긴다.
        public sealed class EvasionSource : IEvasionSource
        {
            private const int MeasureCells = 3;       // R3 은 발사 지점에서 3칸 떨어진 곳에서 잰다(spec 06 2.13 R3)

            public void Collect(DataSet data, EvasionInput input, Diagnostics diagnostics)
            {
                DataFile? file = FileOf(data, BossesFile);
                if (file == null || !file.Root.TryGetProperty("bosses", out JsonElement bosses) || bosses.ValueKind != JsonValueKind.Array)
                {
                    return;
                }
                EvasionBasis? basis = EvasionBasis.Read(data);
                int i = 0;
                foreach (JsonElement b in bosses.EnumerateArray())
                {
                    CollectBoss(b, "/bosses/" + Num(i), file, basis, input, diagnostics);
                    i++;
                }
            }

            private static void CollectBoss(JsonElement b, string at, DataFile file, EvasionBasis? basis, EvasionInput input, Diagnostics diagnostics)
            {
                string id = Str(b, "id");
                JsonElement frostSpeed = b.TryGetProperty("move_speed_on_frost", out JsonElement f) ? f : default;
                input.Movers.Add(new EvasionMover
                {
                    File = file.RepoPath,
                    Pointer = at,
                    Owner = id,
                    ContactDamage = b.TryGetProperty("contact_damage", out JsonElement cd) && cd.TryGetInt32(out int c) ? c : 0,
                    MoveSpeedMilli = EvasionMath.CeilMilli(DecOr(b, "move_speed")),
                    IsBoss = true,
                    FrostMoveSpeedMilli = frostSpeed.ValueKind == JsonValueKind.Number ? EvasionMath.CeilMilli(frostSpeed.GetDecimal()) : (int?)null,
                });
                bool laysFrost = LaysFrostField(b);
                var sets = OverrideSets(b);
                int p = 0;
                foreach (JsonElement pr in b.GetProperty("patterns").EnumerateArray())
                {
                    string pattern = Str(pr, "key");
                    int s = 0;
                    foreach (JsonElement sr in pr.GetProperty("steps").EnumerateArray())
                    {
                        string sat = at + "/patterns/" + Num(p) + "/steps/" + Num(s);
                        var seen = new List<string>();
                        foreach (var (label, overrides) in sets)
                        {
                            CollectStep(sr, sat, id, pattern, s, label, overrides, laysFrost, file, basis, seen, input);
                        }
                        CheckRapidFire(b, sr, sat, id, pattern, file, diagnostics);
                        s++;
                    }
                    p++;
                }
                if (b.TryGetProperty("phantom_copy", out JsonElement pc))
                {
                    CollectPhantomCopy(pc, at + "/phantom_copy", id, file, input, diagnostics);
                }
            }

            // 덮어쓰기 묶음: (이름, 덮어쓰기). 맨 앞은 덮어쓰지 않은 바탕이다. 단계 덮어쓰기와 2회차는 같이 걸릴 수 있다(spec 06 2.6)
            private static List<(string, List<JsonElement>)> OverrideSets(JsonElement b)
            {
                var second = new List<JsonElement>();
                if (b.TryGetProperty("second_cycle", out JsonElement sc) && sc.ValueKind == JsonValueKind.Array)
                {
                    second.AddRange(sc.EnumerateArray());
                }
                var sets = new List<(string, List<JsonElement>)> { ("", new List<JsonElement>()) };
                if (second.Count > 0)
                {
                    sets.Add(("2회차", second));
                }
                foreach (JsonElement ph in b.GetProperty("phases").EnumerateArray())
                {
                    if (!ph.TryGetProperty("overrides", out JsonElement os) || os.GetArrayLength() == 0)
                    {
                        continue;
                    }
                    string name = Num(ph.TryGetProperty("index", out JsonElement ix) && ix.TryGetInt32(out int n) ? n : 0) + "단계";
                    var list = new List<JsonElement>(os.EnumerateArray());
                    sets.Add((name, list));
                    if (second.Count > 0)
                    {
                        var both = new List<JsonElement>(list);
                        both.AddRange(second);       // 같은 칸이면 2회차가 이긴다(BossDef.PatternOverride)
                        sets.Add((name + " + 2회차", both));
                    }
                }
                return sets;
            }

            private static void CollectStep(JsonElement sr, string at, string owner, string pattern, int step, string label, List<JsonElement> overrides,
                bool laysFrost, DataFile file, EvasionBasis? basis, List<string> seen, EvasionInput input)
            {
                if (sr.TryGetProperty("attack", out JsonElement ar))
                {
                    EvasionAttack? a = EnemiesEvasionSource.ReadAttack(ar);
                    if (a != null && DecOr(ar, "damage") > 0m)
                    {
                        int count = sr.TryGetProperty("count", out JsonElement ce) && ce.TryGetInt32(out int cv) ? cv : 1;
                        // 쓸어 가는 전체 각(sweep_degrees)의 덮어쓰기는 탄 사이를 바꾸지 않는다(줄기 간격과 각속도가 정한다)
                        foreach (JsonElement o in overrides)
                        {
                            if (Str(o, "pattern") != pattern || !o.TryGetProperty("step", out JsonElement se) || !se.TryGetInt32(out int si) || si != step)
                            {
                                continue;
                            }
                            decimal v = DecOr(o, "value");
                            switch (Str(o, "key"))
                            {
                                case "radius": a.Radius = v; break;
                                case "inner_radius": a.InnerRadius = v; break;
                                case "count": count = (int)v; break;
                            }
                        }
                        string key = Str(ar, "key") + (label.Length > 0 ? " [" + label + "]" : "");
                        string pointer = at + "/attack";
                        if (sr.TryGetProperty("stream", out JsonElement st) && st.ValueKind == JsonValueKind.Object && a.Proj != null)
                        {
                            a.Proj.ShotsPerSecond = st.TryGetProperty("shots_per_second", out JsonElement sp) && sp.TryGetInt32(out int spv) ? spv : 0;
                            a.Proj.SweepDegreesPerSecond = DecOr(st, "sweep_degrees_per_second");
                        }
                        Add(a, file, pointer, owner, key, seen, input);
                        if (a.Kind == AttackDef.AttackKind.Dash && count >= 2 && basis != null && a.Shape == AttackDef.TelegraphShape.Band)
                        {
                            EvasionAttack cross = EnemiesEvasionSource.ReadAttack(ar)!;
                            cross.EscapeOverrideMilli = EvasionMath.EscapeCrossMilli(EvasionMath.CeilMilli(a.Width), basis.HitMilli);
                            Add(cross, file, pointer, owner, key + " 교차점(띠 " + Num(count) + "줄)", seen, input);
                        }
                        if (laysFrost && a.Shape.HasValue)
                        {
                            EvasionAttack frost = EnemiesEvasionSource.ReadAttack(ar)!;
                            frost.Radius = a.Radius;
                            frost.InnerRadius = a.InnerRadius;
                            frost.FrostVariant = true;
                            decimal frostSeconds = DecOr(sr, "frost_telegraph_seconds");
                            if (frostSeconds > 0m)
                            {
                                frost.TelegraphTicks = Units.SecondsToTicks(frostSeconds);
                            }
                            Add(frost, file, pointer, owner, key + " 서리밭 위", seen, input);
                        }
                    }
                }
                if (sr.TryGetProperty("obstacle", out JsonElement ob) && ob.TryGetProperty("shards", out JsonElement sh) && sh.ValueKind == JsonValueKind.Object)
                {
                    var proj = new EvasionProj
                    {
                        SpeedMilli = EvasionMath.CeilMilli(DecOr(sh, "speed")),
                        Count = sh.TryGetProperty("count", out JsonElement n) && n.TryGetInt32(out int nv) ? nv : 1,
                        SpreadDegrees = DecOr(sh, "spread_degrees"),
                    };
                    foreach (JsonElement o in overrides)
                    {
                        if (Str(o, "pattern") == pattern && o.TryGetProperty("step", out JsonElement se) && se.TryGetInt32(out int si) && si == step)
                        {
                            if (Str(o, "key") == "shard_count")
                            {
                                proj.Count = (int)DecOr(o, "value");
                            }
                            else if (Str(o, "key") == "shard_spread_degrees")
                            {
                                proj.SpreadDegrees = DecOr(o, "value");
                            }
                        }
                    }
                    var a = new EvasionAttack { Kind = AttackDef.AttackKind.Spread, Proj = proj };
                    Add(a, file, at + "/obstacle/shards", owner, Str(ob, "kind") + " 파편" + (label.Length > 0 ? " [" + label + "]" : ""), seen, input);
                }
            }

            // 바탕과 같은 모양의 덮어쓰기 묶음은 한 번만 넘긴다(같은 위반이 여러 번 찍히지 않게)
            private static void Add(EvasionAttack a, DataFile file, string pointer, string owner, string key, List<string> seen, EvasionInput input)
            {
                string sig = Signature(a);
                if (seen.Contains(sig))
                {
                    return;
                }
                seen.Add(sig);
                a.File = file.RepoPath;
                a.Pointer = pointer;
                a.Owner = owner;
                a.Key = key;
                input.Attacks.Add(a);
            }

            private static string Signature(EvasionAttack a)
            {
                EvasionProj? p = a.Proj;
                return string.Join("|", new[]
                {
                    a.Shape.HasValue ? ((int)a.Shape.Value).ToString(CultureInfo.InvariantCulture) : "-",
                    a.Radius.ToString(CultureInfo.InvariantCulture), a.InnerRadius.ToString(CultureInfo.InvariantCulture),
                    a.Width.ToString(CultureInfo.InvariantCulture), a.FanDegrees.ToString(CultureInfo.InvariantCulture),
                    Num(a.TelegraphTicks), Num(a.LockTicks), a.EscapeOverrideMilli.HasValue ? Num(a.EscapeOverrideMilli.Value) : "-",
                    a.FrostVariant ? "f" : "n",
                    p == null ? "-" : Num(p.SpeedMilli) + "/" + Num(p.Count) + "/" + p.SpreadDegrees.ToString(CultureInfo.InvariantCulture) + "/" + Num(p.ShotsPerSecond),
                });
            }

            private static bool LaysFrostField(JsonElement b)
            {
                foreach (JsonElement pr in b.GetProperty("patterns").EnumerateArray())
                {
                    foreach (JsonElement sr in pr.GetProperty("steps").EnumerateArray())
                    {
                        if (sr.TryGetProperty("attack", out JsonElement ar) && ar.TryGetProperty("leaves_hazard", out JsonElement hz)
                            && hz.ValueKind == JsonValueKind.Object && Str(hz, "kind") == "frost_field")
                        {
                            return true;
                        }
                    }
                }
                return false;
            }

            // 연사형의 R3 하한(spec 06 2.12): 격노로 줄어든 간격이 이 아래로 내려가지 않는다. 데이터의 하한이 규칙의 하한보다 작으면 위반이다
            private static void CheckRapidFire(JsonElement b, JsonElement sr, string at, string owner, string pattern, DataFile file, Diagnostics diagnostics)
            {
                if (!BoolOr(sr, "rapid_fire") || !sr.TryGetProperty("min_interval_ticks", out JsonElement mi) || !mi.TryGetInt32(out int min))
                {
                    return;
                }
                decimal speed;
                if (sr.TryGetProperty("attack", out JsonElement ar) && ar.TryGetProperty("proj", out JsonElement pj) && pj.ValueKind == JsonValueKind.Object)
                {
                    speed = DecOr(pj, "speed");
                }
                else if (sr.TryGetProperty("copy", out _) && b.TryGetProperty("phantom_copy", out JsonElement pc))
                {
                    speed = DecOr(pc, "speed");
                }
                else
                {
                    return;                           // 변환기가 알린다
                }
                if (speed <= 0m)
                {
                    return;
                }
                int speedMilli = EvasionMath.CeilMilli(speed);
                decimal degreesPerSecond = sr.TryGetProperty("stream", out JsonElement st) ? DecOr(st, "sweep_degrees_per_second") : 0m;
                int need = MinR3Ticks(speedMilli, degreesPerSecond);
                if (min < need)
                {
                    diagnostics.Error(file.RepoPath, at + "/min_interval_ticks", owner + " 의 패턴 " + pattern + ": R3 위반: 연사형 간격의 하한 " + Num(min)
                        + "틱에서 3칸 지점의 잇따른 두 탄 사이가 1.0칸보다 좁다. 1.0칸 이상인 가장 작은 정수 틱은 " + Num(need) + "(spec 06 2.12 R3 하한, U4)");
                }
            }

            // 같은 방향 연속 탄: ceil(60 × 1.0 ÷ 속도). 회전하는 줄기(각속도 ω): hypot(속도 × dt, 3 × ω × dt) ≥ 1.0 인 가장 작은 dt(틱)
            public static int MinR3Ticks(int speedMilli, decimal sweepDegreesPerSecond)
            {
                for (int t = 1; ; t++)
                {
                    long radial = (long)speedMilli * EvasionMath.MicroPerMilli * t / EvasionMath.TicksPerSecond;
                    long lateral = EvasionMath.ArcMicro(EvasionMath.DegreesToMicroRad(sweepDegreesPerSecond * t / EvasionMath.TicksPerSecond),
                        MeasureCells * EvasionMath.MilliPerUnit);
                    if (EvasionMath.R3StreamSpacing(EvasionMath.FloorSqrt(radial * radial + lateral * lateral)))
                    {
                        return t;
                    }
                }
            }

            // 허상의 복사 규칙(spec 06 4.4): 복사 탄 속도(≤ 9 이면 조준선이 없어도 된다, R4), 폭발 예고 원(R1), 직선 번개의 조준선(R1), 분열 최소 각(R3)
            private static void CollectPhantomCopy(JsonElement pc, string at, string owner, DataFile file, EvasionInput input, Diagnostics diagnostics)
            {
                input.Attacks.Add(new EvasionAttack
                {
                    File = file.RepoPath, Pointer = at + "/speed", Owner = owner, Key = "복사 탄",
                    Kind = AttackDef.AttackKind.AimedShot,
                    Proj = new EvasionProj { SpeedMilli = EvasionMath.CeilMilli(DecOr(pc, "speed")), Count = 1 },
                });
                input.Attacks.Add(new EvasionAttack
                {
                    File = file.RepoPath, Pointer = at + "/explosion_telegraph_seconds", Owner = owner, Key = "복사 폭발",
                    Kind = AttackDef.AttackKind.Zone, Shape = AttackDef.TelegraphShape.Circle, Radius = DecOr(pc, "explosion_radius_max"),
                    TelegraphTicks = Units.SecondsToTicks(DecOr(pc, "explosion_telegraph_seconds")),
                });
                input.Attacks.Add(new EvasionAttack
                {
                    File = file.RepoPath, Pointer = at + "/chain_line_telegraph_seconds", Owner = owner, Key = "복사 직선 번개",
                    Kind = AttackDef.AttackKind.Zone, Shape = AttackDef.TelegraphShape.AimLine, Width = DecOr(pc, "chain_line_width"),
                    TelegraphTicks = Units.SecondsToTicks(DecOr(pc, "chain_line_telegraph_seconds")),
                });
                // 분열 갈래 사이 최소 각은 호 길이 기준(C9). 정수 mrad 내림 식(spec 05 10.6)으로 재면 1/3 rad 의 올림 19.10도가 333 mrad 로 잘리므로 µrad 로 잰다
                decimal angle = DecOr(pc, "split_min_angle_degrees");
                long arc = EvasionMath.ArcMicro(EvasionMath.DegreesToMicroRad(angle), MeasureCells * EvasionMath.MilliPerUnit);
                if (!EvasionMath.R3StreamSpacing(arc))
                {
                    diagnostics.Error(file.RepoPath, at + "/split_min_angle_degrees", owner + " 의 복사 분열: R3 위반: 갈래 사이 최소 각 " + angle.ToString(CultureInfo.InvariantCulture)
                        + "도 → 3칸 지점 호 길이 < 1.0칸(spec 06 4.4, C9)");
                }
            }
        }
    }
}
