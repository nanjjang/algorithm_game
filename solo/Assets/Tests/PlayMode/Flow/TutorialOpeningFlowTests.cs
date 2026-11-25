using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Wizard.Solo.Art;
using Wizard.Solo.Screens.Prep;
using Wizard.Solo.Screens.Start;
using Wizard.Solo.Screens.Story;
using Wizard.Solo.Sim.Run;
using Wizard.Solo.UI;

namespace Wizard.Solo.Tests.PlayMode.Flow
{
    public sealed class TutorialOpeningFlowTests
    {
        FlowKit _flow;

        [SetUp]
        public void Start()
        {
            _flow = new FlowKit();
            _flow.Start("tutorial-opening");
        }

        [TearDown]
        public void Stop() => _flow.Stop();

        [UnityTest]
        public IEnumerator OpeningHandsControlToMovement()
        {
            yield return Depart();
            Assert.That(_flow.World, Is.Null);
            yield return _flow.WaitUntil(() => _flow.World != null, "opening finished", 22f);
            AssertMovementStep();
            yield return WaitForSceneUnload();
        }

        [UnityTest]
        public IEnumerator EscapeSkipsTheOpeningOnly()
        {
            yield return Depart();
            _flow.Key(MenuKey.Back);
            AssertMovementStep();
            yield return WaitForSceneUnload();
        }

        IEnumerator Depart()
        {
            yield return _flow.WaitTop(ScreenId.FirstRunPrompt);
            _flow.Key(MenuKey.Confirm);
            yield return _flow.WaitTop(ScreenId.Title);
            _flow.Key(MenuKey.Confirm);
            yield return _flow.WaitTop(ScreenId.OfflineGuide);
            _flow.Click(StartNameKeys.CommonOk);
            yield return _flow.WaitTop(ScreenId.MainMenu);
            _flow.Activate(FlowKit.Field<UiButton>(_flow.TopScreen, "_expedition"), "expedition");
            yield return _flow.WaitTop(ScreenId.ExpeditionPrep);
            _flow.Click(PrepKeys.Depart);
            yield return _flow.WaitTop(ScreenId.Tutorial);
            yield return _flow.WaitUntil(() => GameObject.Find("Opening/Field/char.1") != null, "in-game sprite staged");
            for (int i = 1; i <= Ids.Char.Count; i++)
            {
                string id = Ids.Char.Of(i);
                var body = GameObject.Find("Opening/Field/" + id).GetComponent<UnityEngine.UI.Image>();
                Assert.That(_flow.Ctx.Art.CharacterClip(id, "idle", Facing3.Up).Frames, Does.Contain(body.sprite));
            }
        }

        void AssertMovementStep()
        {
            Assert.That(_flow.Screens.Top, Is.EqualTo(ScreenId.Tutorial));
            var plan = TutorialPlan.From(_flow.Ctx.Content);
            Assert.That(_flow.World?.Run?.TutorialStep, Is.EqualTo(plan.StepOf(TutorialStepIds.Move)));
            Assert.That(_flow.Ctx.Profile.Flags.TutorialDummyDone, Is.False);
        }

        IEnumerator WaitForSceneUnload()
        {
            yield return _flow.WaitUntil(
                () => !SceneManager.GetSceneByPath(TutorialOpening.ScenePath).isLoaded, "opening scene unloaded");
        }
    }
}
