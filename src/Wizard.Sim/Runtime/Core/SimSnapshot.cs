namespace Wizard.Sim
{
    // CONTRACTS 3.2, T-14. 세계 상태의 값 복사를 담는 버퍼. 내용은 불투명하다.
    // 안에는 같은 모양(같은 ContentDb, 모드, 플레이어 수, 씨앗)으로 만든 그림자 세계가 하나 있고, 모듈의 Snapshot 훅이
    // 자기 필드를 그 세계의 같은 필드로 복사한다. 그림자 세계는 Step 하지 않는다. 만들 때 한 번 할당하고 그 뒤로는 할당이 없다.
    public sealed class SimSnapshot
    {
        internal SimSnapshot(SimWorld world)
        {
            World = world;
        }

        // 마지막으로 저장한 상태의 틱(아직 저장하지 않았으면 0)
        public uint Tick => World.Tick;

        internal SimWorld World { get; }
    }
}
