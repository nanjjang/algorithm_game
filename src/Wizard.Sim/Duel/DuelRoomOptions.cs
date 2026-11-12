using Wizard.Content;

namespace Wizard.Sim.Duel
{
    // CONTRACTS 3.9, spec 10 9.2의 방 옵션(DECISIONS-TECH T-48). 칸의 순서는 명세 표의 순서이고 WZDR 머리말의 순서다(11.1절).
    // 선택지와 기본값은 데이터(duel_rules)의 것이라 여기에 없다. 새 객체는 "아직 고르지 않은 옵션"이다(수는 0, 상한은 없음).
    // 값이 선택지 안인지는 방을 만드는 쪽(서버)이 본다. 정규 바이트는 담기는지만 본다.
    // 방 비밀번호는 여기에 없다: 로비의 비밀이고, 이 타입은 다시 보기 파일(WZDR)에 그대로 실린다.
    public sealed class DuelRoomOptions
    {
        public const int NoCap = -1;        // IlCap·HeroBlockCap 의 "없음"

        public int Rounds;                  // rounds: 1 / 3 / 5
        public int RoundSeconds;            // round_seconds: 60 / 90 / 120
        public int HpMultPermille;          // hp_mult: ×0.7 / ×1 / ×1.5 = 700 / 1000 / 1500
        public int EnhanceCap;              // enhance_cap: 0~13
        public int IlCap = NoCap;           // il_cap: 없음 또는 0~237
        public bool AllowMutations;         // allow_mutations
        public int HeroBlockCap = NoCap;    // hero_block_cap: 없음 또는 0~8
        public bool Equalize;               // equalize: 장비 평준화
        public bool WildIntrusion;          // wild_intrusion
        public bool ThornsOff;              // thorns_off
        public TimeOfDay FloorColor;        // floor_color: Day / Dusk / Night. 색만 바꾼다(규칙과 밝기는 같다)
        public bool Spectate;               // spectate
    }
}
