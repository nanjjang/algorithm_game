using System.Collections.Generic;
using Wizard.Content;
using Wizard.DataBuild.Core;
using Wizard.DataBuild.Validate.Evasion;
using Wizard.TestKit;
using Xunit;

namespace Wizard.DataBuild.Tests.Evasion
{
    // spec 05 11.6 R3·R4 탄(TV-R3-01~15), 11.5 R2 와 북두꺼비 버프(TV-R2-01~10)
    public sealed class EvasionShotAndSpeedTests
    {
        private const int V = EvasionR1Tests.V;
        private const int Hit = EvasionR1Tests.Hit;

        private static EvasionAttack Shots(AttackDef.AttackKind kind, decimal speed, int count, decimal spreadDegrees) =>
            new EvasionAttack
            {
                Owner = "test", Key = "shots", Kind = kind,
                Proj = new EvasionProj { SpeedMilli = EvasionMath.CeilMilli(speed), Count = count, SpreadDegrees = spreadDegrees },
            };

        private static EvasionAttack AimedShot(decimal speed, int telegraphTicks, int lockTicks)
        {
            EvasionAttack a = EvasionR1Tests.AimLocked(0.4m, telegraphTicks, lockTicks);
            a.Proj = new EvasionProj { SpeedMilli = EvasionMath.CeilMilli(speed), Count = 1 };
            return a;
        }

        // 3칸 지점 이웃 탄 사이(µ칸)
        private static long Arc(EvasionAttack a)
        {
            EvasionProj p = a.Proj!;
            return EvasionMath.ArcMicro(EvasionMath.DegreesToMicroRad(EvasionMath.MinGapDegrees(p.SpreadDegrees, p.Count)), EvasionMath.SpacingMeasureMilli);
        }

        private static void Spacing(EvasionAttack a, long expectedMicro, long toleranceMicro, bool pass)
        {
            Assert.InRange(Arc(a), expectedMicro - toleranceMicro, expectedMicro + toleranceMicro);
            List<EvasionViolation> found = EvasionCheck.CheckAttack(a, V, Hit);
            if (pass)
            {
                Assert.Empty(found);
            }
            else
            {
                Assert.Contains(found, v => v.Rule == "R3");
            }
        }

        private static string[] Rules(List<EvasionViolation> found)
        {
            var r = new string[found.Count];
            for (int i = 0; i < found.Count; i++)
            {
                r[i] = found[i].Rule;
            }
            return r;
        }

        [Fact]
        public void S05_TV_R3_01__Wisp8Way() => Spacing(Shots(AttackDef.AttackKind.RingShot, 4.5m, 8, 45m), 2356000, 1000, true);

        [Fact]
        public void S05_TV_R3_02__CapstoneShards8Way() => Spacing(Shots(AttackDef.AttackKind.RingShot, 5m, 8, 45m), 2360000, 5000, true);

        [Fact]
        public void S05_TV_R3_03__KilnBoarStomp12Way() => Spacing(Shots(AttackDef.AttackKind.RingShot, 5m, 12, 30m), 1571000, 1000, true);

        [Fact]
        public void S05_TV_R3_04__SporeQueen16Way() => Spacing(Shots(AttackDef.AttackKind.RingShot, 4m, 16, 22.5m), 1178000, 1000, true);

        [Fact]
        public void S05_TV_R3_05__BigCapShaman5Way20Deg()
        {
            EvasionAttack a = Shots(AttackDef.AttackKind.Spread, 6.5m, 5, 20m);
            Spacing(a, 1047000, 1000, true);
            Assert.Equal(104, Arc(a) / 10000);         // 원본 표기 1.04(내림)
        }

        [Fact]
        public void S05_TV_R3_06__StormOwl5Way20Deg()
        {
            EvasionAttack a = Shots(AttackDef.AttackKind.Spread, 7m, 5, 20m);
            Spacing(a, 1047000, 1000, true);
            Assert.Equal(104, Arc(a) / 10000);
        }

        [Fact]
        public void S05_TV_R3_07__FrostBreathStream()
        {
            // hypot(5.5/4, d × 11.25°): 3칸 1.50, 2칸 1.43
            Assert.InRange(EvasionMath.StreamSpacingMicro(5500, 4, 45m, 3000), 1495000, 1505000);
            Assert.InRange(EvasionMath.StreamSpacingMicro(5500, 4, 45m, 2000), 1425000, 1435000);
            var breath = new EvasionAttack
            {
                Owner = "boss.frost_goat", Key = "breath", Kind = AttackDef.AttackKind.Spread,
                Proj = new EvasionProj { SpeedMilli = 5500, Count = 1, ShotsPerSecond = 4, SweepDegreesPerSecond = 45m },
            };
            Assert.Empty(EvasionCheck.CheckAttack(breath, V, Hit));
        }

        [Fact]
        public void S05_TV_R3_08__HogNeedleSpeed16IsR4AndPasses()
        {
            EvasionAttack a = AimedShot(16m, 60, 24);
            Assert.True(a.Proj!.SpeedMilli > EvasionMath.ScatterSpeedMaxMilli);   // R3 아님(속도 > 9, 한 발)
            Assert.Equal(24, EvasionCheck.WindowTicks(a));
            Assert.Empty(EvasionCheck.CheckAttack(a, V, Hit));
        }

        [Fact]
        public void S05_TV_R3_09__NightSerpentSpitSpeed9PassesR4()
        {
            EvasionAttack a = AimedShot(9m, 60, 24);
            Assert.False(a.Proj!.SpeedMilli > EvasionMath.ScatterSpeedMaxMilli);
            Assert.Empty(EvasionCheck.CheckAttack(a, V, Hit));
            a.LockTicks = 18;                          // 조준선으로 겨누는 탄은 속도 9 이하여도 R4 를 본다
            Assert.Equal(new[] { "R1", "R4" }, Rules(EvasionCheck.CheckAttack(a, V, Hit)));
        }

        [Fact]
        public void S05_TV_R3_10__Hypothetical6Way15DegFailsLoad()
        {
            EvasionAttack a = Shots(AttackDef.AttackKind.Spread, 5m, 6, 15m);
            Spacing(a, 785000, 1000, false);
            Assert.Equal(new[] { "R3" }, Rules(EvasionCheck.CheckAttack(a, V, Hit)));
            Assert.False(EvasionMath.R3Spacing(EvasionMath.DegreesToMicroRad(15m)));   // 3000 × 261 = 783000 < 1000000
        }

        [Fact]
        public void S05_TV_R3_11__HypotheticalRingSpeed9_5WithoutAimLineFailsLoad()
        {
            EvasionAttack a = Shots(AttackDef.AttackKind.RingShot, 9.5m, 8, 45m);
            Assert.Equal(new[] { "R3", "R4" }, Rules(EvasionCheck.CheckAttack(a, V, Hit)));
        }

        [Fact]
        public void S05_TV_R3_12__HypotheticalAimedSpeed12Lock18FailsLoad()
        {
            EvasionAttack a = AimedShot(12m, 60, 18);
            List<EvasionViolation> found = EvasionCheck.CheckAttack(a, V, Hit);
            Assert.Contains(found, v => v.Rule == "R4" && v.Message.Contains("18틱"));
        }

        [Fact]
        public void S05_TV_R3_13__ShamanTripleFromData()
        {
            using var data = DataLoader.Load(TestRepo.Root, new Diagnostics());
            var input = new EvasionInput();
            new EnemiesEvasionSource().Collect(data, input, new Diagnostics());
            EvasionAttack triple = input.Attacks.Find(x => x.Owner == "enemy.shaman_cap" && x.Key == "triple")!;
            Assert.NotNull(triple);
            Assert.Equal(3, triple.Proj!.Count);
            Assert.Equal(20m, triple.Proj.SpreadDegrees);
            Spacing(triple, 1047000, 1000, true);
        }

        [Fact]
        public void S05_TV_R3_14__TwinRingsOffset22_5Deg()
        {
            // 8방향 고리 둘이 22.5도 어긋나면 16발이 22.5도 간격
            Spacing(Shots(AttackDef.AttackKind.RingShot, 4.5m, 16, 22.5m), 1178000, 1000, true);
            Assert.True(EvasionMath.R3Spacing(EvasionMath.DegreesToMicroRad(22.5m)));
        }

        [Fact]
        public void S05_TV_R3_15__R4BasisAfterLock()
        {
            // 0.25 + 0.35 ÷ 3.76 = 0.343초 ≤ 0.4 − 0.05
            long need = EvasionMath.NeedMicros(350, V);
            Assert.InRange(need, 342500, 343500);
            Assert.True(need <= 350000);
            Assert.True(EvasionMath.R1(400, 350, V));
        }

        // ---- R2 ----

        [Fact]
        public void S05_TV_R2_01__SlimeBuffed() => BuffRow(3000, 3600, R2Status.Pass);

        [Fact]
        public void S05_TV_R2_02__LanternBugBuffedCapped() => BuffRow(3600, 3700, R2Status.Pass);

        [Fact]
        public void S05_TV_R2_03__BatBuffedCapped() => BuffRow(3600, 3700, R2Status.Pass);

        [Fact]
        public void S05_TV_R2_04__SporeSeedlingBuffed() => BuffRow(2500, 3000, R2Status.Pass);

        [Fact]
        public void S05_TV_R2_05__WolfNoSpeedEffectAndExempt()
        {
            Assert.Equal(4500, EvasionMath.BuffedSpeedMilli(4500));
            var wolf = new EvasionMover { Owner = "enemy.wolf_bramble", ContactDamage = 0, MoveSpeedMilli = 4500 };
            Assert.Equal(R2Status.Exempt, EvasionCheck.R2Of(wolf, V));
            Assert.Empty(EvasionCheck.CheckMover(wolf, V));
        }

        [Fact]
        public void S05_TV_R2_06__OtherSpeciesBuffed()
        {
            int[] base_ = { 1800, 2500, 2200, 2000, 2400, 0 };
            int[] buffed = { 2160, 3000, 2640, 2400, 2880, 0 };
            for (int i = 0; i < base_.Length; i++)
            {
                Assert.Equal(buffed[i], EvasionMath.BuffedSpeedMilli(base_[i]));
            }
        }

        [Fact]
        public void S05_TV_R2_07__WeaselBurrowedBuffedCapped() => BuffRow(3500, 3700, R2Status.Pass);

        [Fact]
        public void S05_TV_R2_08__BossesTakeNoBuff()
        {
            foreach (int s in new[] { 2200, 1000, 3000, 3000, 3500 })
            {
                Assert.Equal(s, EvasionMath.ContactSpeedMilli(s, true));
                Assert.Equal(R2Status.Pass, EvasionCheck.R2Of(new EvasionMover { Owner = "boss", ContactDamage = 1, MoveSpeedMilli = s, IsBoss = true }, V));
            }
        }

        [Fact]
        public void S05_TV_R2_09__AllContactSpeciesInDataPass()
        {
            using var data = DataLoader.Load(TestRepo.Root, new Diagnostics());
            var input = new EvasionInput();
            new EnemiesEvasionSource().Collect(data, input, new Diagnostics());
            Assert.NotEmpty(input.Movers);
            foreach (EvasionMover m in input.Movers)
            {
                R2Status s = EvasionCheck.R2Of(m, V);
                if (m.ContactDamage > 0)
                {
                    Assert.True(s == R2Status.Pass, m.Owner + " " + s);
                }
                else
                {
                    Assert.Equal(R2Status.Exempt, s);
                }
            }
            Assert.Equal(R2Status.Exempt, EvasionCheck.R2Of(input.Movers.Find(m => m.Owner == "enemy.wolf_bramble")!, V));
            var d = new Diagnostics();
            new EvasionValidator().Validate(data, d);
            Assert.False(d.HasErrors, string.Join("\n", d.Sorted()));
        }

        [Fact]
        public void S05_TV_R2_10__ContactEnemyAt3_8FailsLoad()
        {
            using var repo = new RepoDataCopy();
            repo.SetAllStanceSpeeds("3.76");
            repo.ReplaceOnce("data/sim/enemies.json", "\"enemies\": [\n",
                "\"enemies\": [\n    {\n      \"id\": \"enemy.bat_dusk\",\n      \"role\": \"night_swarm\",\n      \"base_hp\": 12,\n      \"move_speed\": 3.8,\n"
                + "      \"attack_damage\": 1,\n      \"contact_damage\": 1,\n      \"cost\": 1,\n      \"first_wave\": 7,\n      \"times\": [\"tod.dusk\", \"tod.night\"],\n"
                + "      \"weak\": \"element.fire\",\n      \"resist\": \"element.dark\",\n      \"tags\": [\"tag.flying\"],\n      \"knockback_immune\": false,\n"
                + "      \"species_cap\": null,\n      \"body_radius\": 0.3,\n      \"attacks\": [\n        { \"key\": \"contact\", \"kind\": \"contact\", \"damage\": 1, \"damage_source\": \"contact\", \"cooldown_seconds\": 1.5 }\n"
                + "      ]\n    },\n");
            if (!repo.Read("data/names/names.ko.json").Contains("\"enemy.bat_dusk\":"))   // 이름 표에 열쇠가 아직 없으면 더한다(이름 열쇠 검사)
            {
                repo.ReplaceOnce("data/names/names.ko.json", "  \"~end\": \"\"", "  \"enemy.bat_dusk\": \"시험\",\n  \"~end\": \"\"");
                Assert.Equal(Program.ExitOk, repo.Run("names", "--fix").Code);
            }
            var (code, _, err) = repo.Run("check");
            Assert.Equal(Program.ExitProblems, code);
            Assert.Contains("data/sim/enemies.json:/enemies/0: enemy.bat_dusk: R2 위반:", err);
            Assert.Contains("3.800칸/초", err);
        }

        private static void BuffRow(int baseMilli, int buffedMilli, R2Status status)
        {
            Assert.Equal(buffedMilli, EvasionMath.BuffedSpeedMilli(baseMilli));
            Assert.True(buffedMilli < V);
            Assert.Equal(status, EvasionCheck.R2Of(new EvasionMover { Owner = "test", ContactDamage = 1, MoveSpeedMilli = baseMilli }, V));
        }
    }
}
