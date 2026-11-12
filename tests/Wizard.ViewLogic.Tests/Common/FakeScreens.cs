using System;
using System.Collections.Generic;
using Wizard.ViewLogic.Stack;

namespace Wizard.ViewLogic.Tests.Common
{
    // 화면 스택 시험용 화면. 수명 호출을 공용 기록에 남긴다
    internal sealed class FakeScreen : IScreen
    {
        private readonly List<string> _log;

        public FakeScreen(ScreenId id, ScreenLayer layer, bool blocksInput, List<string> log)
        {
            Id = id;
            Layer = layer;
            BlocksInput = blocksInput;
            _log = log;
        }

        public ScreenId Id { get; }
        public ScreenLayer Layer { get; }
        public bool BlocksInput { get; }
        public bool ConsumeBack { get; set; }
        public ScreenArgs? EnteredWith { get; private set; }
        public int Enters { get; private set; }
        public int Exits { get; private set; }
        public Action<FakeScreen>? WhenEntered { get; set; }

        public void OnEnter(ScreenArgs args)
        {
            Enters++;
            EnteredWith = args;
            _log.Add("enter " + Id);
            WhenEntered?.Invoke(this);
        }

        public void OnExit()
        {
            Exits++;
            _log.Add("exit " + Id);
        }

        public bool OnBack()
        {
            _log.Add("back " + Id);
            return ConsumeBack;
        }
    }

    internal sealed class FakeScreenFactory : IScreenFactory
    {
        private readonly Dictionary<ScreenId, (ScreenLayer layer, bool blocks)> _kinds = new Dictionary<ScreenId, (ScreenLayer, bool)>();

        public List<string> Log { get; } = new List<string>();
        public List<FakeScreen> Created { get; } = new List<FakeScreen>();
        public Func<ScreenId, IScreen?>? Override { get; set; }
        public Action<FakeScreen>? OnCreate { get; set; }

        public FakeScreenFactory Kind(ScreenId id, ScreenLayer layer, bool blocks = false)
        {
            _kinds[id] = (layer, blocks);
            return this;
        }

        public IScreen Create(ScreenId id)
        {
            if (Override != null)
            {
                return Override(id)!;
            }
            (ScreenLayer layer, bool blocks) = _kinds.TryGetValue(id, out var k) ? k : (ScreenLayer.Fullscreen, false);
            var s = new FakeScreen(id, layer, blocks, Log);
            Created.Add(s);
            OnCreate?.Invoke(s);
            return s;
        }

        public FakeScreen Last(ScreenId id) => Created.FindLast(s => s.Id == id)!;
    }
}
