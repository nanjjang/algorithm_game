namespace Wizard.DataBuild.Core
{
    // 팩에 넣을 절 하나(id 와 이미 정수로 바뀐 바이트)
    public sealed class PackSection
    {
        public PackSection(ushort id, byte[] body)
        {
            Id = id;
            Body = body;
        }

        public ushort Id { get; }

        public byte[] Body { get; }
    }
}
