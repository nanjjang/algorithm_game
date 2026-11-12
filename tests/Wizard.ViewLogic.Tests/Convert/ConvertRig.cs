using System;
using System.Collections.Generic;
using Wizard.Content;
using Wizard.Core;
using Wizard.DataBuild.Core;
using Wizard.Sim;
using Wizard.Sim.Events;
using Wizard.Sim.Input;
using Wizard.TestKit;
using Xunit;

namespace Wizard.ViewLogic.Tests.Convert
{
    // 표시 변환 시험의 도우미. 저장소 data/ 로 만든 팩으로 연습 세계(손맛 시제품이 도는 세계)를 만들고 틱마다 키를 넣는다.
    // 키는 InputOrder 로 정렬한 뒤 순번을 매긴다(CONTRACTS 3.1).
    internal sealed class ConvertRig
    {
        public static readonly PlayerSlot P0 = new PlayerSlot(0);

        private static readonly Lazy<ContentDb> RepoDbLazy = new Lazy<ContentDb>(BuildRepoDb);

        public readonly SimWorld World;
        public readonly List<SimEvent> Events = new List<SimEvent>();
        private readonly Sink _sink;
        private uint _seq;

        private ConvertRig(SimWorld world)
        {
            World = world;
            _sink = new Sink(Events);
        }

        public static ContentDb Db => RepoDbLazy.Value;

        public static ConvertRig Practice() =>
            new ConvertRig(SimWorld.CreatePractice(Db, SimConfig.ForPractice(Db, SimConfig.ForEndless(Db)), new PracticeSetup()));

        public uint Tick => World.Tick;

        // 이번 Step 이 낸 이벤트만 남긴다
        public void Step(params (LogicalKey Key, bool Down, byte Offset)[] keys)
        {
            Events.Clear();
            var inputs = new PlayerInput[keys.Length];
            for (int i = 0; i < keys.Length; i++)
            {
                inputs[i] = new PlayerInput(P0, new InputEvent(World.Tick, keys[i].Offset, keys[i].Key, keys[i].Down, 0));
            }
            InputOrder.Sort(inputs);
            for (int i = 0; i < inputs.Length; i++)
            {
                InputEvent e = inputs[i].Event;
                inputs[i] = new PlayerInput(P0, new InputEvent(e.Tick, e.Offset, e.Key, e.Down, ++_seq));
            }
            World.Step(inputs, ReadOnlySpan<RunCommand>.Empty, _sink);
        }

        public static (LogicalKey, bool, byte) Press(LogicalKey key, byte offset = 0) => (key, true, offset);

        public static (LogicalKey, bool, byte) Release(LogicalKey key, byte offset = 0) => (key, false, offset);

        private static ContentDb BuildRepoDb()
        {
            var d = new Diagnostics();
            Pipeline.Output o = Pipeline.Run(TestRepo.Root, ModuleRegistry.Discover(),
                                             PackOptions.Default(PackOptions.DefaultDataVersion), d);
            Assert.False(d.HasErrors, string.Join("\n", d.Sorted()));
            return ContentDb.Build(ContentPack.Load(o.Pack!));
        }

        private sealed class Sink : ISimEventSink
        {
            private readonly List<SimEvent> _into;

            public Sink(List<SimEvent> into)
            {
                _into = into;
            }

            public void Emit(in SimEvent e) => _into.Add(e);
        }
    }
}
