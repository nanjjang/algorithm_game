namespace Wizard.ViewLogic.Presentation
{
    // CONTRACTS 13
    public interface IAssetCatalog
    {
        bool TryGetSprite(AssetKey key, int frame, out SpriteRef sprite);
        bool TryGetClip(AssetKey key, out ClipRef clip);
        AssetKey Placeholder(AssetKey missing);           // 회청 바둑판 + id 글자. 위험색·금색·연두·청록을 쓰지 않는다
    }
}
