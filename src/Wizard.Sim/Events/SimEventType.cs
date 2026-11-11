namespace Wizard.Sim.Events
{
    // CONTRACTS 3.3. 번호는 추가만 한다. 400번대 이후는 각 명세의 연결점 절의 이름을 그 구간에 옮긴다(M1-SIM-01 이 전 구간을 선언한다)
    public enum SimEventType : ushort
    {
        None = 0,
        // 100~199 입력·이동·자세 (spec 01 17.2)
        StanceEntered = 100, StanceExited = 101, ReverseFlipped = 102, FacingChanged = 103, CommandReset = 104,
        // 200~299 과열
        ShotFired = 200, CritShotFired = 201, CritHit = 202, HeatEnteredImminent = 203, HeatEnteredCritical = 204,
        HeatEnteredBrink = 205, HeatLockEntered = 206, FireDryWhileLocked = 207, HeatUnlocked = 208,
        HeatExemptArmed = 209, HeatExemptConsumed = 210, KilnAbsorbed = 211, BoiloverTriggered = 212,
        UnstableCoreExploded = 213,
        // 300~399 스침·콤보·결정·체력
        GrazeAwarded = 300, GrazeCosmeticOnly = 301, GrazeBuffStarted = 302, GrazeBuffEnded = 303,
        LightningRodStrike = 304, ComboChanged = 310, ComboReset = 311, CrystalGained = 320, CrystalsFull = 321,
        CrystalGainBlocked = 322, CrystalsSpent = 323, CrystalsShattered = 324, CrystalProtected = 325,
        CrackChanged = 326, DebtCreated = 327, DebtRepaid = 328, CrystalsConverted = 329,
        StarfallTelegraph = 340, StarfallLanded = 341, StarfallBlink = 342, StarfallPicked = 343, StarfallExpired = 344,
        PlayerHit = 360, InvulnEnded = 361, PlayerDowned = 362, PlayerRose = 363, HitstopRequest = 370,
        // 400~499 주문·투사체 (spec 02 19절)
        SpellNodeExecuted = 480,   // A = 실행 트리의 마디 번호(SpellPreview 의 마디 번호와 같다), B = ShotId. HUD 의 지팡이 회로 점등(GDD 13)
        // 500~599 비기·판정기 (spec 03 21절, spec 04 18절)
        // 600~699 적·예고·꼬리표 (spec 05 14절)
        // 700~799 보스·첫 만남·복수 (spec 06 17절)
        // 800~899 웨이브·야영·전리품·점수 (spec 07 20절)
        // 900~999 결투 (spec 10 23절)
        // 1000~ 디버그
    }
}
