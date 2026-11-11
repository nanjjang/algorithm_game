namespace Wizard.Unity.Presentation
{
    // CONTRACTS 13. 엔진 타입으로 옮기기만 한다. Fix64 를 직접 만지지 않는다(실수 변환은 Wizard.ViewLogic.FixConvert).
    public static class UnityConvert
    {
        public static UnityEngine.Vector2 ToUnity(System.Numerics.Vector2 v) => new UnityEngine.Vector2(v.X, v.Y);
    }
}
