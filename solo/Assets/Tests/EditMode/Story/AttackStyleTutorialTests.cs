using NUnit.Framework;
using UnityEngine;
using Wizard.Solo.Data;
using Wizard.Solo.Sim;
using Wizard.Solo.Sim.Run;
using Wizard.Solo.Tests.Spells;

namespace Wizard.Solo.Tests.Story
{
    /// <summary>
    /// 튜토리얼 판은 그 캐릭터의 A 유형으로 돈다(손잡이 tutorial_uses_default_style, FunPass가 켠다 — 사용자 지시).
    /// 관문 값(Tutorial.json)은 그대로 두고, 다섯 캐릭터의 A 유형으로 발사·과열·임계·콤보 관문을 진짜 입력(J)만으로 통과하는지 본다.
    /// </summary>
    public sealed class AttackStyleTutorialTests
    {
        const int Limit = 60 * 40;
        static IContentDb Db => AttackStyleKit.Tuned;
        static TutorialPlan Plan => TutorialPlan.From(Db);

        static SimWorld Start(int character, string stepId)
        {
            var cfg = AttackStyleKit.PracticeConfig(Db, character, AttackStyleKit.Styles[character - 1][2], RunMode.Tutorial);
            cfg.TutorialStep = Plan.StepOf(stepId);
            return AttackStyleKit.Start(Db, cfg);
        }

        /// <summary>그 단계를 끝내기까지의 틱(못 끝내면 −1). tap = 임계 끊어 쏘기(다음 발이 잠그면 손을 뗀다), 아니면 꾹.</summary>
        static int Run(SimWorld w, string stepId, bool tap)
        {
            int step = Plan.StepOf(stepId);
            var p = w.P0;
            bool down = false;
            for (int t = 0; t < Limit; t++)
            {
                int h = p.Spell != null ? p.Spell.HeatPerShotHu : 0;
                bool want = !tap || (!p.Locked && p.HeatHu + h <= 100 * SimConst.HuPerHeat);
                if (want != down)
                {
                    down = want;
                    w.EnqueueInput(0, new InputEvent(LogicalKey.Fire, down, SimConst.TickStartU(w.Tick)));
                }
                p.InvulnUntilTick = w.Tick + 2;
                w.Step();
                for (int i = 0; i < w.Events.Count; i++)
                    if (w.Events[i].Type == SimEventType.TutorialStepDone && w.Events[i].A == step) return t;
                w.Events.Clear();
            }
            return -1;
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        public void AS_Tutorial__StyleA_PassesFireOverheatCritComboGates(int character)
        {
            string a = AttackStyleKit.Styles[character - 1][0];
            var w = Start(character, TutorialStepIds.Fire);
            Assert.AreEqual(a, w.P0.Spell.Core.Id, "튜토리얼은 A 유형");
            Assert.GreaterOrEqual(Run(w, TutorialStepIds.Fire, false), 0, a + " 발사 관문");
            Assert.GreaterOrEqual(Run(Start(character, TutorialStepIds.Overheat), TutorialStepIds.Overheat, false), 0, a + " 과열 관문");
            var crit = Start(character, TutorialStepIds.Crit);
            int critTicks = Run(crit, TutorialStepIds.Crit, true);
            Assert.GreaterOrEqual(critTicks, 0, a + " 임계 관문(끊어 쏘기로)");
            Assert.Greater(Plan.CritGoal(crit.P0), 0);
            Assert.GreaterOrEqual(Run(Start(character, TutorialStepIds.Combo), TutorialStepIds.Combo, false), 0, a + " 콤보 관문");
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        public void AS_Tutorial__CritGateStillNotPassableByHolding(int character)
        {
            // 관문 설계(TutorialGateTests): 꾹 눌러서는 임계 관문을 통과할 수 없다 — A 유형에서도.
            Assert.AreEqual(-1, Run(Start(character, TutorialStepIds.Crit), TutorialStepIds.Crit, false));
        }
    }
}
