using Wizard.Content;
using Wizard.Netcode.Duel;

namespace Wizard.ViewLogic.Presentation
{
    // CONTRACTS 13. UnityEngine 타입이 없다(System.Numerics.Vector2)
    public readonly struct FxRequest
    {
        public readonly string FxId;                      // event_bindings.json 이 정한다 ("fx_lock_steam" 등)
        public readonly System.Numerics.Vector2 WorldPos;
        public readonly float AngleDeg;
        public readonly Element Element;
        public readonly int A, B;
        public readonly EventPhase Phase;                 // Retracted 면 0.1초 흐려지며 사라진다
        
        public FxRequest(string fxId, System.Numerics.Vector2 worldPos, float angleDeg, Element element, int a, int b, EventPhase phase)
        {
        FxId = fxId;
        WorldPos = worldPos;
        AngleDeg = angleDeg;
        Element = element;
        A = a;
        B = b;
        Phase = phase;
        }
    }
}
