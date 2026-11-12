namespace Wizard.DataBuild.Core
{
    // 팩 머리말의 값. core_compat 은 이 도구가 링크한 코어의 SimVersion.CoreVersion 이다(팩이 맞는 코어).
    public sealed class PackOptions
    {
        // [임시] data_version 의 원본 자리가 정해지지 않았다(docs/dev/OPEN-QUESTIONS.md). 정해질 때까지 명령줄 --data-version, 없으면 1.
        public const uint DefaultDataVersion = 1;

        public PackOptions(uint dataVersion, string coreVersion)
        {
            DataVersion = dataVersion;
            CoreVersion = coreVersion;
        }

        public uint DataVersion { get; }

        public string CoreVersion { get; }

        public static PackOptions Default(uint dataVersion) => new PackOptions(dataVersion, Wizard.Sim.SimVersion.CoreVersion);
    }
}
