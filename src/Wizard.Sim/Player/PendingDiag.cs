using Wizard.Core;

namespace Wizard.Sim.Player
{
    // spec 01 3.9 `PendingDiag`, 5.4. 대각 시야 유지 창. 자세 밖(StanceMode.Normal)에서만 열린다.
    // 열려 있는 동안 이동은 남은 한 키 방향으로 이미 바뀌었고 시야(= 발사 방향)만 Diag 로 남는다.
    internal struct PendingDiag
    {
        public bool Active;
        public long ExpireTime;            // 입력 시각. 이 시각까지(포함) 두 번째 뗌이 오면 대각을 유지한다
        public Dir8 Diag;

        public void Write(ref StateWriter w)
        {
            w.Bool(Active);
            w.I64(ExpireTime);
            w.U8((byte)Diag);
        }

        public void Read(ref StateReader r)
        {
            Active = r.Bool();
            ExpireTime = r.I64();
            Diag = PlayerState.ReadDir(ref r);
            if (Active && !PlayerRules.IsDiagonal(Diag))
            {
                throw new SimInvariantException("player: pending diagonal window holds a cardinal direction");
            }
        }
    }
}
