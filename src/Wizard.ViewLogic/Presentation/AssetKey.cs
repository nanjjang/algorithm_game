namespace Wizard.ViewLogic.Presentation
{
    // CONTRACTS 13. 그림을 가리키는 열쇠. 에셋이 오면 카탈로그만 채운다(ARCHITECTURE P12)
    public readonly struct AssetKey
    {
        public readonly AssetPack Pack;
        public readonly string Id;
        public readonly string? Variant;
        public readonly string? State;
        
        public AssetKey(AssetPack pack, string id, string? variant = null, string? state = null)
        {
        Pack = pack;
        Id = id;
        Variant = variant;
        State = state;
        }
    }
}
