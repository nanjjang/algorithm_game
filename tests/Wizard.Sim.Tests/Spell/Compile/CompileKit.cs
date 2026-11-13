using System;
using System.Collections.Generic;
using Wizard.Content;
using Wizard.Core;
using Wizard.Sim.Events;
using Wizard.Sim.Heat;
using Wizard.Sim.Spell;
using Wizard.Sim.Spell.Compile;
using Wizard.Sim.Tests.Heat;
using Wizard.Sim.Tests.World;
using Xunit;

namespace Wizard.Sim.Tests.Spell.Compile
{
    // 컴파일러 시험의 도우미. 블록 수치는 저장소 data/ 의 팩(blocks 16종)에서 읽는다.
    // spec 02 16절 머리말의 기본 문맥: 캐릭터 1(연결 4칸, 감쇠 0.85, 밑줄 없음), 확장 0, 변이 없음.
    // 캐릭터 다섯의 기본 연결 칸 수·감쇠·밑줄은 아직 팩에 없어(M5-DAT-01) 명세 값(SL-1, 5.2)을 시험 입력으로 준다.
    // 컴파일 규칙(64, 밑줄 상한 5 = 600 HU, 거울상 1.5)은 팩의 blocks 절 꼬리에서 읽는다(T-57). 아래 상수는 대조용 명세 값이다.
    internal static class CompileKit
    {
        public const int TreeCap = 64;                     // spec 02 SF-2
        public const int UnderlineMaxCutHu = 5 * 120;      // spec 02 HT-5: 최대 5(열) = 600 HU
        public const int PermilleMirror = 1500;            // spec 02 HT-7 거울상 열 +50%

        // 컴파일 규칙은 팩에서 읽는다(blocks 절의 꼬리, T-57). 위 세 상수는 명세 값이고 SpecRules_AreInThePack 이 팩과 대조한다
        public static CompileRules TestRules => CompileRules.FromContent(Db);

        // 글자 출력의 "/64"는 컴파일에 쓴 상한이다
        public static string Print(CompiledSpell m) => SpellDebugText.Print(m, TestRules.TreeProjectileCap);

        public static ContentDb Db => WorldData.RepoDb;

        // 열(소수 첫째 자리까지) → HU. 열 1 = 120 HU(spec 01 2.4) → 0.1 = 12 HU
        public static int HuTenths(int heatTenths) => heatTenths * 12;

        public static int Hu(int heat) => heat * 120;

        public static BlockId[] L(params BlockId[] links) => links;

        public const BlockId __ = BlockId.None;
        public const BlockId Bolt = BlockId.MagicBolt;
        public const BlockId Fireball = BlockId.Fireball;
        public const BlockId Chain = BlockId.ChainLightning;
        public const BlockId Ice = BlockId.IceSpike;
        public const BlockId Orb = BlockId.ShadowOrb;
        public const BlockId Split2 = BlockId.SplitTwo;
        public const BlockId Split3 = BlockId.SplitThree;
        public const BlockId Trig = BlockId.OnHitTrigger;
        public const BlockId Bounce = BlockId.WallBounce;
        public const BlockId Size = BlockId.DoubleSize;
        public const BlockId Pierce = BlockId.Pierce;
        public const BlockId Frost = BlockId.Frost;
        public const BlockId Homing = BlockId.Homing;
        public const BlockId Triple = BlockId.TripleHeat;
        public const BlockId Corrosion = BlockId.Corrosion;

        // 캐릭터 1(달구): 연결 4칸, 감쇠 0.85, 밑줄 없음(SL-1, 5.2)
        public static CompileSource Dalgu(BlockId core, BlockId[] links, int ext = 0, byte mutations = 0) =>
            new CompileSource(core, links, CharacterId.Char1, 4, ext, PowBase.Decay085, false, mutations);

        // 캐릭터 3(갈피): 연결 5칸, 감쇠 0.92, 밑줄(SL-1, 5.2, W 4.3)
        public static CompileSource Galpi(BlockId core, BlockId[] links, int ext = 0, byte mutations = 0) =>
            new CompileSource(core, links, CharacterId.Char3, 5, ext, PowBase.Decay092, true, mutations);

        public static CompiledSpell Build(CompileSource src) => SpellCompilerCore.Compile(Db, src, TestRules);

        public static CompiledSpell Dalgu1(BlockId core, params BlockId[] links) => Build(Dalgu(core, links));

        public static byte Mut(params MutationId[] ids)
        {
            int bits = 0;
            foreach (MutationId id in ids)
            {
                bits |= 1 << (int)id;
            }
            return (byte)bits;
        }

        public static void AssertDiags(CompiledSpell m, params (DiagCode Code, int Slot)[] expected)
        {
            var actual = new List<(DiagCode, int)>();
            foreach (CompileDiagnostic d in m.Diagnostics)
            {
                actual.Add((d.Code, d.Slot));
            }
            Assert.Equal(expected, actual.ToArray());
        }

        public static BlockId[] Projectiles(SpellScope s) => Array.ConvertAll(s.Projectiles, p => p.Block);

        public static int[] ProjectileSlots(SpellScope s) => Array.ConvertAll(s.Projectiles, p => p.Slot);

        // ── 과열 세계와 맞물린 시험(TV-D09, D17~D19). 컴파일한 발사 열과 간격을 세계의 Heat 모듈에 넣는다(SetHeatShot)
        public static HeatRig ArmedRig(ContentDb heatDb, CompiledSpell spell)
        {
            Assert.True(spell.IsValid);
            HeatRig rig = HeatRig.Endless(heatDb);
            rig.World.SetHeatShot(new PlayerSlot(0), spell.Scopes[0].Projectiles[0].Block, spell.HeatPerShotHu, spell.FireIntervalTicks);
            return rig;
        }

        // 꾹 누르기 1주기: 틱 0부터 J 를 누르고 잠길 때까지. 주기 = 풀리는 틱
        public static (List<SimEvent> Shots, uint Period) HoldOneCycle(HeatRig rig)
        {
            while (!rig.H.Locked)
            {
                rig.Step(true);
                Assert.True(rig.Tick < 5000);
            }
            return (rig.Shots(), rig.H.UnlockTick);
        }

        // 임계 유지(spec 02 TV-D18, GDD 18): 다음 발사가 100을 넘기지 않는 동안 쏘고, 열이 100 − 발사 열 × n 이하로 식으면 다시 쏜다.
        // 열 0에서 시작해 첫 임계 발사부터 6,000틱(100초)의 피해를 잰다. 피해는 코어 기본 피해 × (임계 2 / 아니면 1)
        public static (double Dps, int N, int Locks) CritMaintain(HeatRig rig, int shotHu, int baseDamage)
        {
            int n = HeatRules.CritShotCount(shotHu, rig.World.HeatCritStartHu(0));
            int resumeAt = HeatRules.MaxHeatHu - shotHu * n;
            bool firing = true;
            uint? start = null;
            while (start == null || rig.Tick < start.Value + 6000U)
            {
                int heat = rig.H.HeatHu;
                if (firing && heat + shotHu > HeatRules.MaxHeatHu)
                {
                    firing = false;
                }
                else if (!firing && heat <= resumeAt)
                {
                    firing = true;
                }
                int before = rig.Log.Events.Count;
                rig.Step(firing);
                if (start == null)
                {
                    for (int k = before; k < rig.Log.Events.Count; k++)
                    {
                        if (rig.Log.Events[k].Type == SimEventType.CritShotFired)
                        {
                            start = rig.Log.Events[k].Tick;
                        }
                    }
                }
                Assert.True(rig.Tick < 20000);
            }
            int damage = 0;
            foreach (SimEvent e in rig.Shots())
            {
                if (e.Tick >= start!.Value && e.Tick < start.Value + 6000U)
                {
                    damage += HeatRig.Damage(e, baseDamage);
                }
            }
            return (Dps(damage, 6000), n, rig.Log.Count(SimEventType.HeatLockEntered));
        }

        public static double Dps(int damage, long ticks) => damage * (double)SimVersion.TickRate / ticks;
    }
}
