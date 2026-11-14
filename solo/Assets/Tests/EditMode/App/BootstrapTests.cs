using NUnit.Framework;
using Wizard.Solo.App;
using Wizard.Solo.Meta;
using Wizard.Solo.UI;

namespace Wizard.Solo.Tests.App
{
    /// <summary>첫 화면 고르기(08 요약서 2절): 설정 파일이 없었으면 FirstRunPrompt, 있으면 Title. 인자는 ScreenArgKeys로만.</summary>
    public class BootstrapTests
    {
        [Test]
        public void FirstRun_GoesToFirstRunPrompt_ElseTitle()
        {
            Assert.AreEqual(ScreenId.FirstRunPrompt, Bootstrap.FirstScreen(new BootResult { FirstRun = true }));
            Assert.AreEqual(ScreenId.Title, Bootstrap.FirstScreen(new BootResult { FirstRun = false }));
            Assert.AreEqual(ScreenId.Title, Bootstrap.FirstScreen(null));
        }

        [Test]
        public void Args_ProfileRestoredAlways_PendingSettledOnlyWhenPresent()
        {
            var a = Bootstrap.FirstScreenArgs(new BootResult { ProfileRestored = true });
            Assert.IsTrue(a.Values.ContainsKey(ScreenArgKeys.ProfileRestored));
            Assert.IsTrue(a.Get<bool>(ScreenArgKeys.ProfileRestored));
            Assert.IsFalse(a.Values.ContainsKey(ScreenArgKeys.PendingSettled));

            var settled = new SettlementResult();
            var b = Bootstrap.FirstScreenArgs(new BootResult { PendingSettled = settled });
            Assert.IsFalse(b.Get<bool>(ScreenArgKeys.ProfileRestored, true));
            Assert.AreSame(settled, b.Get<SettlementResult>(ScreenArgKeys.PendingSettled));
        }
    }
}
