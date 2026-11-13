using Wizard.Core;

namespace Wizard.Content
{
    // CONTRACTS 2.2. spec 04 8절, spec 10 7.2~7.3(world-characters 5.4 가 유일한 기준). 결투에서 바뀌는 캐릭터 값.
    // 원본 data/sim/duel_rules.json 의 characters, 절 꼴은 Pack/Sections/ContentDb.DuelRules.cs. 팩에서 읽은 뒤 바뀌지 않는다.
    // CharacterDef.Duel 과 DuelRuleSet.Characters[id] 가 같은 객체다(duel_rules 절이 팩에 있을 때).
    // 여기 없는 결투 값: 고유 비기(불가마·벼락 질주·베껴 쓰기·얼음 병풍·그림자 손)의 결투 값은 ArtDef.Duel(spec 03 16절),
    // 결투에서도 그대로인 값(이동, 자세, 냉각, 임계, 잠금, 스침 감지, 결정 상한, 결정당 콤보)은 CharacterDef(spec 10 7.1).
    // 그 캐릭터의 능력이 아닌 칸은 0(거짓)이다. "T"는 트리 1회 실행의 피해(spec 10 U-01)이고 배수를 천분율로 둔다(1.5T = 1500).
    public sealed class DuelCharacterOverride
    {
        public CharacterId Character;

        // 피격 벌칙(spec 10 5.3, 7.2 "피격 시 결정 손실"). 벌칙이 PenaltiesPerCrystalLoss 번 발동할 때마다 결정 PenaltyCrystalLoss 개를 잃는다.
        // 기본 1/1, 그믐 2/1, 성에 1/2(얼린 결정의 금: 0 아니면 1, 라운드가 바뀌면 0)
        public int PenaltyCrystalLoss;
        public int PenaltiesPerCrystalLoss;

        // 끓어넘침(char.1, passive.overflow): 잠금 진입 틱에 발밑 반경 안의 상대에게 피해, 넉백(3틱 뒤, spec 10 7.4)
        public int OverflowDamagePermille;                  // 1.5T = 1500 (그대로)
        public Fix64 OverflowRadius;                        // 2.5칸 (W 4.1 그대로)
        public Fix64 OverflowKnockback;                     // 1칸 (무한 2칸)
        public bool OverflowClearsBullets;                  // 거짓. 상대 탄을 지우지 않는다

        // 피뢰침 체질(char.2, passive.lightning_rod): 인정된 스침에서만. 표시가 뜬 틱부터 RodCooldownTicks(spec 10 U-09),
        // 표시 원은 뜬 순간 상대 자리에 고정, RodAimTicks 뒤 표시 원과 상대 피격 원이 겹치면 맞는다
        public int RodDamagePermille;                       // 0.5T = 500
        public int RodCooldownTicks;                        // 2초 = 120
        public int RodAimTicks;                             // 0.25초 = 15
        public Fix64 RodRange;                              // 5칸. 상대가 이 반경 안일 때만
        public Fix64 RodMarkerRadius;                       // 0.5칸 (지름 1칸 원)

        // 살얼음(char.4, passive.thin_ice): 같은 상대 ThinIceHits 번 적중 → 이동 감속. 감속이 걸린 틱부터 ThinIceImmuneTicks 동안
        // 다시 걸리지 않고 적중도 쌓이지 않는다(spec 10 U-29). 블록 감속과 더하지 않고 큰 쪽만
        public int ThinIceHits;                             // 6
        public int ThinIceSlowPermille;                     // −30% = 300
        public int ThinIceSlowTicks;                        // 1.0초 = 60
        public int ThinIceImmuneTicks;                      // 6초 = 360
        public bool ThinIceFreezes;                         // 거짓. 결투에서는 얼지 않는다

        // 그림자 계약(char.5, passive.shadow_contract): 가진 결정 1개당 피해 증가(그대로, 7개에서 +21%)
        public int ContractPerCrystalPermille;              // 3% = 30

        // 외상(char.5, wand_trait.on_credit): 빚이 있을 때 벌칙이 발동한 피격의 추가 피해(칸. × DuelRuleSet.HpPerCell = +100).
        // 빚 상한 2는 판정기 값(TrieDef, spec 03 9.2)
        public int DebtPenaltyExtraCells;                   // 1
    }
}
