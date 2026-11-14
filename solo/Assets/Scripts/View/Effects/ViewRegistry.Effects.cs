using System.Collections.Generic;
using Wizard.Solo.View.Effects;

namespace Wizard.Solo.View
{
    /// <summary>
    /// G16 등록(00 문서 2.1절): 내 탄 층(AddShots), 이펙트 층(AddEffects), 소리 층(AddAudio).
    /// 내 탄 층과 이펙트 층은 입자 예산·가림 넓이 셈을 하나로 써야 해서(10 3.2·3.3절) 같은 EffectsShared를 받는다 —
    /// CreateAll이 같은 목록을 차례로 넘기므로 AddEffects가 앞서 더한 ShotLayer를 찾아 그 묶음을 쓴다.
    /// </summary>
    public static partial class ViewRegistry
    {
        static partial void AddShots(List<IWorldViewLayer> layers)
        {
            layers.Add(new ShotLayer(new EffectsShared()));
        }

        static partial void AddEffects(List<IWorldViewLayer> layers)
        {
            EffectsShared shared = null;
            for (int i = 0; i < layers.Count; i++)
            {
                var s = layers[i] as ShotLayer;
                if (s != null) { shared = s.Shared; break; }
            }
            var fx = new EffectLayer(shared ?? new EffectsShared());
            layers.Add(fx);
            // 타격감 덧층(표시 전용 연출, Juice.json). 같은 입자 예산·가림 셈·번쩍 관리기를 쓴다.
            layers.Add(new JuiceLayer(fx.Shared, fx));
        }

        static partial void AddAudio(List<IWorldViewLayer> layers)
        {
            layers.Add(new AudioLayer());
        }
    }
}
