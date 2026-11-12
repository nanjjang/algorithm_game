namespace Wizard.Sim.Proto
{
    // 손맛 시제품(GDD 16 1단계, spec 01 13절 "만드는 순서" 6·7번)의 임시 수치. M3-CB-02 가 이 폴더째 지운다.
    //
    // 팩에서 읽을 수 있는 값은 팩에서 읽는다: 마력탄·화염구의 열·피해·간격·속도·사거리·판정 반지름·속성(blocks 절),
    // 캐릭터의 결정 상한·피격 반지름(characters 절), 전장 크기(arena 절).
    // 아래 값은 그 값이 들어갈 데이터 절이 아직 팩에 없다(3갈래 분열·임계 배율·속성 배율 = M2-SPL-01, 적 = M3-DAT-01,
    // 비기·판정기 상수 = M5-DAT-01). 이 작업이 고칠 수 있는 곳은 Proto 폴더뿐이라 data/ 에 두지 못하고 여기 모은다
    // [임시: 결과 보고와 OPEN-QUESTIONS]. 값은 명세 그대로이고 출처를 줄마다 적는다. 단위는 이름에 붙인다.
    internal static class ProtoTuning
    {
        // ── 피해(spec 02 11절 피해 공식의 임계 배율·속성 배율, AF-1)
        public const int CritMulPermille = 2000;              // 임계 ×2 [제안]
        public const int NoMulPermille = 1000;                // ×1(단위 환산)
        public const int WeaknessMulPermille = 1250;          // 약점 ×1.25 [조율]
        public const int ResistanceMulPermille = 800;         // 저항 ×0.8 [조율]

        // ── 3갈래 분열(spec 01 2.5, spec 02 4.1·4.2 split_3, 9.3 부채꼴 각도, TV-F02)
        public const int SplitThreeHeatMulPermille = 2200;    // 열 배수 ×2.2
        public const int SplitThreeCount = 3;
        public const int SplitThreeStepCenti = 1500;          // 부채꼴 −15/0/+15도(spec 02 9.3) [제안]. 19.19도 넓히기(13.2 R3)는 적의 복사 전용이다

        // ── 판정기 상수(spec 03 1.2, 입력 시각 단위)와 빚 상한(9.2)
        public const long JustWindowUnits = 1075;
        public const long GapFullBloomUnits = 2765;
        public const long GapShortUnits = 3072;
        public const long GapSameSideUnits = 3840;
        public const long GapDefaultUnits = 4608;
        public const long ArtCooldownUnits = 6144;
        public const long KBufferWindowUnits = 1536;
        public const int MaxDebt = 2;

        // ── 임시 효과의 기술 셋(spec 03 12.1 비용, 12.2 효과. 효과는 시제품용으로 줄였다: 결과 보고)
        public const int BurstCost = 1;                       // ① 마력 방출: 트리 1회, 2T, 열 면제
        public const int BurstMulPermille = 2000;
        public const int RecoilCost = 1;                      // ② 반동 사격: 트리 1회 + 맞은 적 넉백 2칸(시전자 뒤로 2칸은 빠짐)
        public const int RecoilMulPermille = 1000;
        public const int RecoilKnockbackMilli = 2000;
        public const int SpearCost = 2;                       // ⑥ 꿰뚫는 창: 3T 즉발 선분, 폭 0.5, 사거리 12
        public const int SpearMulPermille = 3000;
        public const int SpearHalfWidthMilli = 250;
        public const int SpearRangeMilli = 12000;

        // ── 이끼 슬라임(spec 05 2.5 표, 4.1, 3.6 접촉, U-04 몸 반지름)
        public const int SlimeHpMilli = 20000;
        public const int SlimeSpeedCenti = 300;               // 3.0칸/초
        public const int SlimeContactCells = 1;
        public const int SlimeContactCooldownTicks = 90;
        public const int SlimeBodyMilli = 350;                // [임시: U-04]

        // ── 3갈래 탄 포탑 = 갓버섯 술사의 shaman_cap.triple 을 제자리에서 쏘는 것(spec 05 4.4, U-04·U-05·U-34)
        public const int TurretCastTicks = 24;                // 시전 0.4초(이것이 예고다: 갓 가장자리 포자)
        public const int TurretRecoveryTicks = 36;            // 빈틈 0.6초
        public const int TurretCooldownTicks = 120;           // 시전 간격 2.0초 [임시: U-05]
        public const int TurretShotSpeedCenti = 650;          // 6.5칸/초
        public const int TurretShotCount = 3;
        public const int TurretSpreadCenti = 2000;            // 갈래 사이 20도 [임시: U-34]
        public const int TurretRangeMilli = 9000;             // 시전 조건과 탄 사거리 9칸 [임시: U-34]
        public const int TurretShotRadiusMilli = 120;         // 탄 판정 반지름 0.12
        public const int TurretShotCells = 1;
        public const int TurretBodyMilli = 300;               // [임시: U-04]

        // ── 명세가 정하지 않아 시제품이 정한 값 [임시: OPEN-QUESTIONS]
        public const int TurretXMilli = 12000;                // 포탑 자리: 한뎃벌 위쪽 가운데(시작 위치 (12, 7)에서 5칸)
        public const int TurretYMilli = 2000;
        public const int SlimeSpawnIntervalTicks = 90;        // 1.5초에 한 마리
        public const int SlimeMaxAlive = 8;                   // 살아 있는 슬라임이 이만큼이면 생성을 쉰다

        // ── 자료구조 크기
        public const int BulletCapacity = 256;                // 플레이어 탄 풀. 넘치면 새 탄을 버린다(SpellCapDropped)
        public const int EnemyCapacity = 32;                  // 슬라임 최대 + 포탑보다 넉넉하다
        public const int ShotCapacity = 192;                  // 적 탄 풀(적 탄 상한 150, GDD 4 보다 넉넉하다)
        public const int ArtDecisionsPerTick = 8;             // 한 틱 한 플레이어의 K 판정 수 상한 = 입력 이벤트 상한(CONTRACTS 3.1)

        // ── 단위 환산
        public const int MilliPerCell = 1000;
        public const int CentiPerCell = 100;
    }
}
