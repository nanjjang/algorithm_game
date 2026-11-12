using Wizard.Sim.Arts;

namespace Wizard.Sim.Proto
{
    // 허수아비 연습의 결정 지갑: 결정 무한(GDD 13 "허수아비 연습: 결정 무한"). 언제나 상한만큼 있고 쓴 만큼 줄지 않는다.
    // 그래서 상태가 없다(해시·스냅숏에 넣을 것이 없다). 상한은 캐릭터 값을 세계가 넣는 캐시다. 결정·빚은 Combo 모듈(M3-CMB-01)의 것이 되고
    // 그때 실제 지갑(PlayerState)으로 바뀐다
    internal sealed class ProtoWallet : ICrystalWallet
    {
        private int _cap;

        public int Crystals
        {
            get => _cap;
            set { }                                       // 무한: 결제해도 줄지 않는다
        }

        public int Cap => _cap;

        public int Debt
        {
            get => 0;
            set { }
        }

        public bool HasCredit => false;                   // 외상은 제자 5만. 연습의 캐릭터는 기준값이다

        internal void SetCap(int cap) => _cap = cap;
    }
}
