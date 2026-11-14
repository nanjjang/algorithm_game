namespace Wizard.Solo.Screens.Enhance
{
    /// <summary>
    /// 강화 화면의 소리 사건 열쇠(08 요약서 10.12절, 10 요약서 5.1절 E-01~E-36, event_bindings.json).
    /// 이름이 둘인 사건은 S13 21.5 이름을 쓴다(08 18.1 M-03). 결과 연출의 시점별 사건은 G23.json result_events에 있다.
    /// E-10(숨 멈춤)과 +13 침묵은 Hold·Plus13Silence 사건 id로 재생기에 넘긴다(재생기가 모든 소리를 끊는다).
    /// 결과 늦음(E-32)·서버 알림(E-34·35)은 솔로에서 내지 않는다.
    /// </summary>
    public static class EnhanceSoundIds
    {
        public const string Open = "ui.enhance.open", Close = "ui.enhance.close";
        public const string SacrificePut = "ui.enhance.sacrifice_put", SacrificeTake = "ui.enhance.sacrifice_take";
        public const string ProbRoll = "ui.enhance.prob_roll", CharmSet = "ui.enhance.charm_set", CostShake = "ui.enhance.cost_shake";
        public const string WandSwitch = "ui.enhance.wand_switch", TraceToggle = "ui.enhance.trace_toggle";
        public const string Press = "ui.enhance.press", Pay = "ui.enhance.pay", Offer = "ui.enhance.offer";
        public const string ChargeBegin = "ui.enhance.charge_begin", ChargeProgress = "ui.enhance.charge_progress", Hold = "ui.enhance.hold";
        public const string ResultCard = "ui.enhance.result_card";
        public const string MutationAccept = "ui.enhance.mutation_accept", MutationReject = "ui.enhance.mutation_reject";
        public const string ShortResult = "ui.enhance.short_result";
        public const string Plus13Silence = "ui.enhance.plus13_silence";
        public const string Release = "ui.enhance.release";
        public const string EngraveTry = "ui.enhance.engrave_try", EngraveOk = "ui.enhance.engrave_ok", EngraveFail = "ui.enhance.engrave_fail";
        public const string MutationRemove = "ui.enhance.mutation_remove";
        /// <summary>지팡이 대기 반복음(사건 id = 반복 열쇠). event_bindings에 없는 솔로 사건 — DataSolo/Audio.json의 캐릭터 events가 클립을 준다.</summary>
        public const string WandIdle = "ui.enhance.wand_idle";
    }

    /// <summary>시간표 사건의 화면 효과 이름(G23.json의 fx 값과 같다).</summary>
    public static class EnhanceFx
    {
        public const string Pay = "pay", Offer = "offer", Charge = "charge", Heartbeat = "heartbeat", Hold = "hold";
        public const string Flash = "flash", Shake = "shake", Hitstop = "hitstop", Slam = "slam", SlamGold = "slam_gold";
        public const string Darken = "darken", Ring = "ring", StageChange = "stage_change", Aura = "aura", Vine = "vine";
        public const string Column = "column", Burst = "burst", Extinguish = "extinguish", Ward = "ward", HoldProp = "hold_prop";
        public const string Slot = "slot", TitleBand = "title_band";

        public const string ColorElement = "element", ColorGold = "gold", ColorWhiteGold = "white_gold";
        public const string ShakeByIntensity = "by_intensity", ShakeSmall = "small", ShakeMedium = "medium";
    }
}
