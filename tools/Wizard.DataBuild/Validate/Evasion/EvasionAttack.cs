using Wizard.Content;

namespace Wizard.DataBuild.Validate.Evasion
{
    // 회피 검산이 보는 공격 하나(spec 05 2.3 AttackDef 에서 검산에 쓰는 칸만). 데이터 원본(적, 보스 …)마다 IEvasionSource 가 만든다.
    // 길이는 칸(decimal, 원본의 십진 그대로), 시간은 틱이다.
    public sealed class EvasionAttack
    {
        public string File { get; set; } = "";                  // 저장소 기준 경로(진단)
        public string Pointer { get; set; } = "";               // JSON 위치(진단)
        public string Owner { get; set; } = "";                 // 공격하는 쪽의 id(예: enemy.golem_menhir)
        public string Key { get; set; } = "";                   // 공격 열쇠(예: slam)

        public AttackDef.AttackKind Kind { get; set; }

        public AttackDef.TelegraphShape? Shape { get; set; }    // null 이면 예고 구역이 없다

        public decimal Radius { get; set; }                     // Circle·Fan 반지름, Ring 바깥 반지름

        public decimal InnerRadius { get; set; }                // Ring 안 반지름

        public decimal Width { get; set; }                      // Band·AimLine 폭

        public decimal FanDegrees { get; set; }                 // 부채꼴 전체 각도

        public int TelegraphTicks { get; set; }

        public AttackDef.LockMode Lock { get; set; }

        public int LockTicks { get; set; }                      // LastNTicks 의 n

        public int? EscapeOverrideMilli { get; set; }           // 검산용 벗어나는 거리(spec 05 10.1 예외)

        public EvasionProj? Proj { get; set; }

        // 서리밭 위의 플레이어를 겨냥한 변형: V_frost 로 검사한다(spec 05 10.4)
        public bool FrostVariant { get; set; }

        // EvasionRuleExempt.PhantomFinisher(spec 05 1.2 보충 7). 다섯 줄을 보지 않는다. ExemptAllowed 가 아닌 공격에 있으면 실패
        public bool Exempt { get; set; }

        public bool ExemptAllowed { get; set; }

        public string Label => Owner + " 의 공격 " + Key;
    }
}
