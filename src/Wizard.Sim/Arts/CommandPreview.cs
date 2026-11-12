namespace Wizard.Sim.Arts
{
    // CONTRACTS 3.5, spec 03 11절. HUD "지금 K =" 의 판정. 지갑과 판정기 상태를 바꾸지 않고 낸 ArtDecision 이다(T-52).
    // 표시 값(기술명, 비용, 외상 N, 늦은 입력)은 이 판정에서 뽑는다: 비용 = CrystalsPaid + DebtAdded, 외상 = DebtAdded,
    // 불발 표시 = Outcome·Reason·LateIndex·LateByUnits. PlayerView.Preview(ArtPreview)로 옮기는 것은 비기 공통 작업이 한다.
    public readonly struct CommandPreview
    {
        // 한 줄일 때의 줄이자 두 줄일 때의 아랫줄. HasJust 가 거짓이면 now 에 K 를 누른 판정이고,
        // 참이면 섬 창을 넘긴 시각(max(now, 마지막 방향 시각 + 섬 창 + 1))의 판정이다
        public readonly ArtDecision Normal;

        // 고른 마디에 해금된 섬 기술이 있다 → 항상 두 줄(spec 03 11절, U-29)
        public readonly bool HasJust;

        // 윗줄: 마지막 방향 시각에 K 를 누른 판정. HasJust 가 거짓이면 빈 값
        public readonly ArtDecision Just;

        public CommandPreview(in ArtDecision normal, bool hasJust, in ArtDecision just)
        {
            Normal = normal;
            HasJust = hasJust;
            Just = just;
        }
    }
}
