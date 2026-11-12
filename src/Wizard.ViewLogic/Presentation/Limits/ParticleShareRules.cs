namespace Wizard.ViewLogic.Presentation.Limits
{
    // effects.md 9.3 결투·광장의 지팡이 대기 나누기, 피해 숫자(입자가 아니다), 일회성 흐려짐 길이
    public readonly struct ParticleShareRules
    {
        public readonly int DuelOpponentIdleCap;     // 상대 지팡이 대기: 요청량의 절반(올림), 30까지
        public readonly int PlazaSelfIdleCap;        // 광장의 나: 요청량 전부(60까지)
        public readonly int PlazaNearCount;          // 가까운 순으로 8명
        public readonly int PlazaNearIdleCap;        // 그 8명: 요청량의 절반(올림), 30까지. 나머지는 0
        public readonly int DamageNumbersMax;        // 피해 숫자는 화면에 40개까지
        public readonly int DamageMergeWindowMs;     // 같은 적에게 0.2초 안에 들어간 피해는 한 숫자로
        public readonly int OneShotFadeMs;           // 일회성 효과가 자리를 내줄 때 흐려지는 길이(0.1초)

        public ParticleShareRules(int duelOpponentIdleCap, int plazaSelfIdleCap, int plazaNearCount, int plazaNearIdleCap,
                                  int damageNumbersMax, int damageMergeWindowMs, int oneShotFadeMs)
        {
            DuelOpponentIdleCap = duelOpponentIdleCap;
            PlazaSelfIdleCap = plazaSelfIdleCap;
            PlazaNearCount = plazaNearCount;
            PlazaNearIdleCap = plazaNearIdleCap;
            DamageNumbersMax = damageNumbersMax;
            DamageMergeWindowMs = damageMergeWindowMs;
            OneShotFadeMs = oneShotFadeMs;
        }
    }
}
