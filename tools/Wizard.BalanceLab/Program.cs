using System;
using System.Collections.Generic;
using System.Reflection;

namespace Wizard.BalanceLab
{
    // balancelab 의 진입점(ARCHITECTURE 3.4, T-33). 이 파일은 M0-BT-01 만 고친다.
    // 명령은 모듈 등록으로 붙는다: 이 어셈블리에서 ICommand 를 구현한 클래스를 찾아 첫 인자(Name)로 고른다.
    //   balancelab <명령> [인자…]   예: `balancelab feel <기록>`(M1-TE-02)
    // 명령 폴더: Core/, Feel/(M1-TE-02), Characters/(M5-TE-01), Economy/(M7-TE-01), Bots/(M8-TE-01),
    //            Duel/(M14-TE-02), DuelAccuracy/(M14-TE-03), Gdd17/(M18-QA-01), Online12/(M18-QA-02).
    public static class Program
    {
        public const int UsageExitCode = 64;
        public const int NotImplementedExitCode = 2;

        // 명령 하나. 공개 매개변수 없는 생성자가 있어야 한다. Name 은 소문자 영어 한 단어이고 겹치지 않는다.
        public interface ICommand
        {
            string Name { get; }

            int Run(string[] args);
        }

        public static int Main(string[] args)
        {
            return Dispatch("balancelab", FindCommands(typeof(Program).Assembly), args, Console.Error);
        }

        public static IReadOnlyList<ICommand> FindCommands(Assembly assembly)
        {
            var types = new List<Type>();
            foreach (Type type in assembly.GetTypes())
            {
                if (type.IsClass && !type.IsAbstract && typeof(ICommand).IsAssignableFrom(type))
                {
                    types.Add(type);
                }
            }
            var commands = new List<ICommand>(types.Count);
            foreach (Type type in types)
            {
                commands.Add((ICommand)(Activator.CreateInstance(type)
                    ?? throw new InvalidOperationException("cannot create command " + type.FullName)));
            }
            commands.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
            for (int i = 1; i < commands.Count; i++)
            {
                if (string.Equals(commands[i - 1].Name, commands[i].Name, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("duplicate command name '" + commands[i].Name + "'");
                }
            }
            return commands;
        }

        public static int Dispatch(string tool, IReadOnlyList<ICommand> commands, string[] args, System.IO.TextWriter error)
        {
            if (commands.Count == 0)
            {
                error.WriteLine(tool + ": no commands registered yet (M1-TE-02)");
                return NotImplementedExitCode;
            }
            if (args.Length > 0)
            {
                foreach (ICommand command in commands)
                {
                    if (string.Equals(command.Name, args[0], StringComparison.Ordinal))
                    {
                        string[] rest = new string[args.Length - 1];
                        Array.Copy(args, 1, rest, 0, rest.Length);
                        return command.Run(rest);
                    }
                }
                error.WriteLine(tool + ": unknown command '" + args[0] + "'");
            }
            var names = new List<string>(commands.Count);
            foreach (ICommand command in commands)
            {
                names.Add(command.Name);
            }
            error.WriteLine("usage: " + tool + " <" + string.Join("|", names) + "> [args…]");
            return UsageExitCode;
        }
    }
}
