namespace Wizard.ViewLogic.Convert
{
    // 회색 도형 표시(M1-UE-02)가 그리는 것의 종류. 표시 전용 번호라 상태·해시·파일에 들어가지 않는다. 그래도 더하기만 한다.
    // 적의 종(EnemyTypeId)에서 고른다(EntityShapeSampler.KindOf): 이끼 슬라임 = Slime, 갓버섯 술사(손맛 시제품의 포탑) = Turret,
    // 그 밖의 종 = Enemy. 종마다의 그림은 에셋이 들어오는 작업의 일이다.
    public enum ShapeKind : byte
    {
        None = 0,
        Player = 1,
        Slime = 2,
        Turret = 3,
        PlayerBullet = 4,
        EnemyShot = 5,
        Enemy = 6,
    }
}
