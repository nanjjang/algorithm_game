using System;
using Wizard.Sim.Input;

namespace Wizard.ReplayCli
{
    // 봇 입력의 꼴(천분율). 시험이 쓰는 값이라 기획 수치가 아니다. 이름 붙은 꼴은 골든의 notes 에 적힌다(바꾸면 그 골든을 다시 만들어야 하므로
    // 이미 쓰인 꼴의 값은 바꾸지 않는다. 새 꼴을 더한다).
    public sealed class BotProfile
    {
        public static readonly LogicalKey[] AllKeys =
        {
            LogicalKey.Up, LogicalKey.Left, LogicalKey.Down, LogicalKey.Right, LogicalKey.Fire, LogicalKey.Skill,
            LogicalKey.Stance, LogicalKey.Reverse, LogicalKey.Pause,
        };

        // 이동·자세·발사·비기만(일시정지 없음)
        public static readonly LogicalKey[] PlayKeys =
        {
            LogicalKey.Up, LogicalKey.Left, LogicalKey.Down, LogicalKey.Right, LogicalKey.Fire, LogicalKey.Skill,
            LogicalKey.Stance, LogicalKey.Reverse,
        };

        public string Name { get; private set; } = "custom";

        // 이 틱에 키 이벤트가 생길 확률
        public int KeyTickPermille { get; set; } = 300;

        // 키가 생기는 틱의 이벤트 수 1..MaxKeysPerTick
        public int MaxKeysPerTick { get; set; } = 3;

        // 몰린 틱(이벤트 9~12개. 기록기가 아홉째부터 버린다, spec 01 T-I05)
        public int BurstTickPermille { get; set; }

        // 이미 눌린 키의 누름, 안 눌린 키의 뗌(KeyState 가 무시한다, T-I07·T-I08)
        public int RedundantPermille { get; set; } = 40;

        // 이 틱에 명령을 시도할 확률(CanApply 가 참인 것만 기록한다. RunSession 과 같다)
        public int CommandTickPermille { get; set; } = 6;

        // 같은 틱·같은 오프셋에 방향과 K 를 같이 넣을 확률(T-I01)
        public int SameOffsetPermille { get; set; } = 30;

        public LogicalKey[] Keys { get; set; } = PlayKeys;

        // 이름 붙은 꼴
        public static BotProfile Named(string name)
        {
            switch (name)
            {
                case "play":
                    return new BotProfile { Name = name };
                case "dense":
                    return new BotProfile
                    {
                        Name = name, KeyTickPermille = 700, MaxKeysPerTick = 6, BurstTickPermille = 15, RedundantPermille = 120,
                        CommandTickPermille = 20, SameOffsetPermille = 80, Keys = AllKeys,
                    };
                case "stance":
                    return new BotProfile
                    {
                        Name = name, KeyTickPermille = 450, MaxKeysPerTick = 4, RedundantPermille = 20, CommandTickPermille = 3,
                        SameOffsetPermille = 150,
                        Keys = new[]
                        {
                            LogicalKey.Stance, LogicalKey.Stance, LogicalKey.Reverse, LogicalKey.Up, LogicalKey.Left, LogicalKey.Down,
                            LogicalKey.Right, LogicalKey.Skill, LogicalKey.Skill, LogicalKey.Fire,
                        },
                    };
                case "loadout":
                    return new BotProfile
                    {
                        Name = name, KeyTickPermille = 250, MaxKeysPerTick = 2, CommandTickPermille = 60,
                        Keys = new[] { LogicalKey.Fire, LogicalKey.Fire, LogicalKey.Up, LogicalKey.Left, LogicalKey.Down, LogicalKey.Right },
                    };
                default:
                    throw new FormatException("bot profile '" + name + "' is not play, dense, stance or loadout");
            }
        }
    }
}
