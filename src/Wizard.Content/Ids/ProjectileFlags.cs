namespace Wizard.Content
{
    // spec 02 4.2, DECISIONS-TECH T-53. 투사체 블록의 동작 표식. 변형·제어 블록은 None 이다.
    // 비트는 팩의 blocks 절에 그대로 들어간다. 더하기만 하고 바꾸지 않는다
    [System.Flags]
    public enum ProjectileFlags : ushort
    {
        None = 0,
        Explodes = 1,              // 멈춘 자리에서 터진다(거대 화염구). 반경은 BlockParams.ExplosionRadius
        Chains = 2,                // 즉발 광선 + 전이(연쇄 번개). 횟수·반경은 BlockParams.ChainHops·ChainRadius
        Returns = 4,               // 편도 끝이나 벽에서 돌아온다(부메랑). 갈 때·올 때 각 1회 타격
        PassThrough = 8,           // 적을 몇이든 지나가고 적마다 1회 타격, 벽에서 사라진다(그림자 구체)
        IgnoresWolfDodge = 16,     // 덤불 늑대의 탄 회피 대시를 무시한다(연쇄 번개)
    }
}
