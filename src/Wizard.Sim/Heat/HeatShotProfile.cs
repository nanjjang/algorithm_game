using Wizard.Content;
using Wizard.Core;

namespace Wizard.Sim.Heat
{
    // J 발사 한 번의 열과 간격(spec 01 7.2·7.3). 주문을 가진 쪽(M2부터 Spell 모듈의 CompiledSpell, M1에서는 시제품 내용)이
    // SimWorld.SetHeatShot 으로 넣는다. 열은 거울상 배수까지 적용한 값(ShotHeat.ShotHeatHu)이다.
    // Armed 가 거짓이면 J 가 아무것도 쏘지 않는다(주문이 없다).
    internal struct HeatShotProfile
    {
        public bool Armed;
        public BlockId Core;               // ShotFired 의 coreBlockId
        public int HeatHu;                 // 발사 1회의 열(HU)
        public int IntervalTicks;          // 코어 블록의 발사 간격(틱). 1 이상

        // 바이트: armed bool, core u8, heat i32, interval i32
        public void Write(ref StateWriter w)
        {
            w.Bool(Armed);
            w.U8((byte)Core);
            w.I32(HeatHu);
            w.I32(IntervalTicks);
        }

        public void Read(ref StateReader r)
        {
            Armed = r.Bool();
            Core = (BlockId)r.U8();
            HeatHu = r.I32();
            IntervalTicks = r.I32();
            if (Armed ? (HeatHu < 0 || IntervalTicks < 1) : (Core != BlockId.None || HeatHu != 0 || IntervalTicks != 0))
            {
                throw new SimInvariantException("heat: shot profile out of range");
            }
        }
    }
}
