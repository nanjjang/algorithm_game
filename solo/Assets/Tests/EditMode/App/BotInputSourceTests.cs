using System.Collections.Generic;
using NUnit.Framework;
using Wizard.Solo.App;

namespace Wizard.Solo.Tests.App
{
    /// <summary>각본 입력원: 시각이 된 줄만, 늦은 줄은 지금 시각, 같은 Poll 안의 누름·뗌 둘 다, Resync·ReleaseAll은 키보드와 같은 규칙.</summary>
    public class BotInputSourceTests
    {
        readonly List<InputEvent> _ev = new List<InputEvent>();

        // NUnit은 테스트마다 새 객체를 만들지 않는다(한 객체로 전부 돈다). 앞 테스트의 이벤트가 남지 않게 비운다.
        [SetUp]
        public void SetUp() => _ev.Clear();

        [Test]
        public void EntriesFireWhenDue()
        {
            var bot = new BotInputSource().Press(2, LogicalKey.Right).Release(4, LogicalKey.Right);
            bot.Poll(SimConst.TickStartU(1), _ev);
            Assert.AreEqual(0, _ev.Count);
            bot.Poll(SimConst.TickStartU(2), _ev);
            Assert.AreEqual(1, _ev.Count);
            Assert.AreEqual(LogicalKey.Right, _ev[0].Key);
            Assert.IsTrue(_ev[0].Down);
            Assert.AreEqual(SimConst.TickStartU(2), _ev[0].TimeU);
            Assert.IsTrue(bot.IsDown(LogicalKey.Right));
            _ev.Clear();
            bot.Poll(SimConst.TickStartU(4) + 10, _ev);
            Assert.AreEqual(1, _ev.Count);
            Assert.IsFalse(_ev[0].Down);
            Assert.AreEqual(SimConst.TickStartU(4) + 10, _ev[0].TimeU, "late entry uses current time");
            Assert.IsTrue(bot.Finished);
        }

        [Test]
        public void TapInsideOnePoll_BothEvents()
        {
            var bot = new BotInputSource().Press(1, LogicalKey.Fire, 10).Release(1, LogicalKey.Fire, 20);
            bot.Poll(SimConst.TickStartU(1) + 100, _ev);
            Assert.AreEqual(2, _ev.Count);
            Assert.IsTrue(_ev[0].Down);
            Assert.IsFalse(_ev[1].Down);
        }

        [Test]
        public void OutOfOrderScript_SortedStable()
        {
            var bot = new BotInputSource().Press(5, LogicalKey.Skill).Press(3, LogicalKey.Up).Press(3, LogicalKey.Fire);
            bot.Poll(SimConst.TickStartU(10), _ev);
            Assert.AreEqual(3, _ev.Count);
            Assert.AreEqual(LogicalKey.Up, _ev[0].Key);
            Assert.AreEqual(LogicalKey.Fire, _ev[1].Key);
            Assert.AreEqual(LogicalKey.Skill, _ev[2].Key);
        }

        [Test]
        public void DuplicatePress_NoSecondEvent()
        {
            var bot = new BotInputSource().Press(0, LogicalKey.Up).Press(1, LogicalKey.Up);
            bot.Poll(SimConst.TickStartU(2), _ev);
            Assert.AreEqual(1, _ev.Count);
        }

        [Test]
        public void Resync_SwallowsHeldAction_ReleaseAllClears()
        {
            var bot = new BotInputSource().Press(0, LogicalKey.Down).Press(0, LogicalKey.Skill);
            bot.Resync(0, _ev, swallowHeldActions: true);
            Assert.AreEqual(1, _ev.Count);
            Assert.AreEqual(LogicalKey.Down, _ev[0].Key);
            Assert.IsFalse(bot.IsDown(LogicalKey.Skill));
            Assert.IsTrue(bot.IsPhysicallyDown(LogicalKey.Skill));

            _ev.Clear();
            bot.Release(1, LogicalKey.Skill).Press(2, LogicalKey.Skill);
            bot.Poll(SimConst.TickStartU(1), _ev);
            Assert.AreEqual(0, _ev.Count, "release of swallowed key is silent");
            bot.Poll(SimConst.TickStartU(2), _ev);
            Assert.AreEqual(1, _ev.Count);
            Assert.AreEqual(LogicalKey.Skill, _ev[0].Key);

            _ev.Clear();
            bot.ReleaseAll(SimConst.TickStartU(3), _ev);
            Assert.AreEqual(2, _ev.Count);
            Assert.IsFalse(bot.IsDown(LogicalKey.Down));
            Assert.IsFalse(bot.IsDown(LogicalKey.Skill));
        }

        [Test]
        public void Now_AppliesOnNextPoll()
        {
            var bot = new BotInputSource();
            bot.Press(100, LogicalKey.Left);
            bot.Now(LogicalKey.Up, true);
            bot.Poll(7, _ev);
            Assert.AreEqual(1, _ev.Count);
            Assert.AreEqual(LogicalKey.Up, _ev[0].Key);
            Assert.AreEqual(7L, _ev[0].TimeU);
            Assert.AreEqual(1, bot.Remaining);
        }
    }
}
