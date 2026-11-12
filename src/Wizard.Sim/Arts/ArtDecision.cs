using Wizard.Content;
using Wizard.Core;

namespace Wizard.Sim.Arts
{
    // CONTRACTS 3.5. K 판정 하나의 결과. 필드는 spec 03 7.4절 그대로이고 ArtId 만 2.1절의 정본을 쓴다.
    // 명세와 다른 이름은 단위를 붙인 둘뿐이다: LateBy → LateByUnits, 그리고 7.5절의 섬 초과량을 담는 JustLateByUnits(T-52).
    // 보관된 K(판정기 상태)와 결투의 준비 중인 비기처럼 틱을 넘겨 들고 있는 판정은 Write/Read 로 스냅숏·직렬화·해시에 들어간다.
    public readonly struct ArtDecision
    {
        public readonly ArtOutcome Outcome;
        public readonly ArtId Art;              // Fire: 나간 기술. Fizzle: 고른 기술(FullBloomLate 는 Bloom)
        public readonly ArtId SelectedArt;      // 부모 대체 전의 기술
        public readonly bool ViaFallback;       // 고유 → 부모 대체로 나갔다
        public readonly bool IsJust;            // 섬 기술을 골랐다(Fire 면 섬으로 나갔다)
        public readonly byte CrystalsPaid;      // 실제 결정 소모
        public readonly byte DebtAdded;         // 외상으로 생긴 빚
        public readonly byte CrystalsAfter;     // 소모 뒤 결정 수(그림자 계약 계산용, spec 03 8절 A-11)
        public readonly byte Repeats;           // 만개 반복 수(그 밖 0)
        public readonly FizzleReason Reason;
        public readonly byte LateIndex;         // FullBloomLate: 2~5("n번째 입력")
        public readonly long LateByUnits;       // FullBloomLate: 초과량(입력 시각 단위)
        public readonly long JudgeTime;         // 판정 시각(입력 시각)
        public readonly int NodeId;             // 고른 노드(디버그, 연습장 표시)
        public readonly long JustLateByUnits;   // 섬 자리에서 섬 창을 넘겨 일반 기술을 골랐을 때 넘긴 양(spec 03 7.5, B38). 그 밖 0

        public ArtDecision(ArtOutcome outcome, ArtId art, ArtId selectedArt, bool viaFallback, bool isJust,
                           byte crystalsPaid, byte debtAdded, byte crystalsAfter, byte repeats,
                           FizzleReason reason, byte lateIndex, long lateByUnits, long judgeTime, int nodeId, long justLateByUnits)
        {
            Outcome = outcome;
            Art = art;
            SelectedArt = selectedArt;
            ViaFallback = viaFallback;
            IsJust = isJust;
            CrystalsPaid = crystalsPaid;
            DebtAdded = debtAdded;
            CrystalsAfter = crystalsAfter;
            Repeats = repeats;
            Reason = reason;
            LateIndex = lateIndex;
            LateByUnits = lateByUnits;
            JudgeTime = judgeTime;
            NodeId = nodeId;
            JustLateByUnits = justLateByUnits;
        }

        public bool IsFire => Outcome == ArtOutcome.Fire;

        // 바이트: outcome u8, art u8, selected u8, via bool, just bool, paid u8, debt u8, after u8, repeats u8,
        //         reason u8, late_index u8, late_by i64, judge_time i64, node i32, just_late_by i64
        public void Write(ref StateWriter w)
        {
            w.U8((byte)Outcome);
            w.U8((byte)Art);
            w.U8((byte)SelectedArt);
            w.Bool(ViaFallback);
            w.Bool(IsJust);
            w.U8(CrystalsPaid);
            w.U8(DebtAdded);
            w.U8(CrystalsAfter);
            w.U8(Repeats);
            w.U8((byte)Reason);
            w.U8(LateIndex);
            w.I64(LateByUnits);
            w.I64(JudgeTime);
            w.I32(NodeId);
            w.I64(JustLateByUnits);
        }

        // Write 의 역. 모르는 열거형 값은 SimInvariantException
        public static ArtDecision Read(ref StateReader r)
        {
            byte outcome = r.U8();
            byte art = r.U8();
            byte selected = r.U8();
            bool via = r.Bool();
            bool just = r.Bool();
            byte paid = r.U8();
            byte debt = r.U8();
            byte after = r.U8();
            byte repeats = r.U8();
            byte reason = r.U8();
            byte lateIndex = r.U8();
            long lateBy = r.I64();
            long judge = r.I64();
            int node = r.I32();
            long justLate = r.I64();
            // 상태 읽기는 Step 밖이라 IsDefined 의 박싱이 허용된다
            if (!System.Enum.IsDefined(typeof(ArtOutcome), outcome) || !System.Enum.IsDefined(typeof(FizzleReason), reason)
                || !System.Enum.IsDefined(typeof(ArtId), art) || !System.Enum.IsDefined(typeof(ArtId), selected))
            {
                throw new SimInvariantException("arts: stored decision has an unknown enum value");
            }
            return new ArtDecision((ArtOutcome)outcome, (ArtId)art, (ArtId)selected, via, just, paid, debt, after, repeats,
                                   (FizzleReason)reason, lateIndex, lateBy, judge, node, justLate);
        }
    }
}
