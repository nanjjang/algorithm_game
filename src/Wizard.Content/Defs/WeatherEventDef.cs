using Wizard.Core;

namespace Wizard.Content
{
    // CONTRACTS 2.2. 날씨와 돌발 사건(world-characters 2.3 의 표 5행, spec 14 3.9 event.*). 팩에서 읽은 뒤 바뀌지 않는다.
    // 원본 data/sim/weather_events.json. ContentDb.WeatherEvents 는 WeatherEventId 오름차순이다. 칸의 뜻은 DECISIONS-TECH T-55.
    //
    // 표의 세 칸이 그대로 칸이 된다: "언제"(OnDayChange, TodMask, ChanceBp, MaxPerDay), "내용"(IsInstant, DurationTicks, EdgeWidth,
    // VariantCount), "규칙 영향"(AffectsRules).
    // 규칙에 영향이 없는 사건(AffectsRules == false)은 세계의 규칙 상태를 바꾸지 않는다. 다시 보기에서 같게 나오도록 발생과 변형만
    // RngStream.Weather 로 뽑는다(spec 01 3절의 흐름 표).
    // 규칙에 영향을 주는 사건은 별똥 결정 하나이고 그 수치(확률, 때, 자리, 시간, 결정 수)는 절 15 StarfallDef 한 곳에만 있다.
    // 그 행은 목록의 자리(id, 시간대, 한 번 일어나고 끝남)만 든다. 코어는 별똥을 StarfallDef 로 돌리고 이 행의 수치를 읽지 않는다.
    // 데이터에 없는 규칙: 결투에는 날씨와 돌발 사건이 없다(world-characters 2.3, spec 10 3.1). 웨이브마다 한 번만 뽑는다(T-55 의 4번).
    public sealed class WeatherEventDef
    {
        public const int ChanceBpMax = 10000;               // 만분율의 100%

        // TodMask 에 올 수 있는 비트 전부(TimeOfDay 의 None 을 뺀 네 값)
        public const int AllTodsMask = (1 << (int)TimeOfDay.Day) | (1 << (int)TimeOfDay.Dusk) | (1 << (int)TimeOfDay.Night) | (1 << (int)TimeOfDay.BossWave);

        public WeatherEventId Id;

        // "규칙 영향" 칸. false = 없음(연출). true 는 별똥 결정뿐이다(결정 +1. 수치는 StarfallDef)
        public bool AffectsRules;

        // ── 언제 ──
        // true = 날이 바뀔 때(보스 웨이브 뒤 다음 날의 첫 웨이브가 시작될 때) 일어난다: 버린 물건. 이때 TodMask 는 0 이다.
        // false = 웨이브가 시작될 때, 그 웨이브의 시간대가 TodMask 에 들면 한 번 뽑는다
        public bool OnDayChange;

        // 뽑는 웨이브의 시간대. 비트 (1 << (int)TimeOfDay). 소나기 = 낮 | 해질녘, 안개 = 해질녘, 별똥 결정 = 밤, 느루의 훈수 = 네 시간대 전부
        public int TodMask;

        // 기회(웨이브 시작 또는 날 바뀜)마다 일어날 확률(만분율). 버린 물건은 10,000(날이 바뀌면 늘 떨어진다).
        // 원문은 소나기·안개·훈수의 확률을 정하지 않았다 [임시]: 소나기 1,000, 안개 2,000, 훈수 500. 별똥 결정은 0(StarfallDef.ChanceBp 가 주인)
        public int ChanceBp;

        // 하루(10웨이브)에 일어나는 횟수의 상한. 0 = 상한 없음. 소나기 1("하루 1회 이하")
        public int MaxPerDay;

        // ── 내용 ──
        // true = 한 번 일어나고 끝난다(시작만 알린다): 별똥 결정(별똥의 수명은 StarfallDef), 느루의 훈수, 버린 물건.
        // false = 이어진다: 소나기, 안개
        public bool IsInstant;

        // 이어지는 사건의 길이(틱). 0 = 그 웨이브가 끝날 때까지(안개 [임시]). 소나기 3,600(60초).
        // 길이가 남아도 그 웨이브가 끝나면 같이 끝난다 [임시]. IsInstant 면 0
        public int DurationTicks;

        // 전장 가장자리에서 안쪽으로 덮는 폭(칸). 안개 3칸(Raw 12,884,901,888). 표시 계층이 쓰는 값이고 판정에는 쓰지 않는다. 그 밖은 0
        public Fix64 EdgeWidth;

        // 일어날 때 고르는 변형의 수(1 이상). 1 = 변형 없음(뽑지 않는다). 느루의 훈수 2(이름 표의 event.master_advice.line.1~2),
        // 버린 물건 6(소품 그림 6종. spec 14 4.7 "느루가 버린 물건 6")
        public int VariantCount;

        // 웨이브 시작 때 뽑는 사건이 그 시간대에 나올 수 있는가. OnDayChange 인 사건은 언제나 false 다
        public bool Allows(TimeOfDay tod) => (TodMask & (1 << (int)tod) & AllTodsMask) != 0;
    }
}
