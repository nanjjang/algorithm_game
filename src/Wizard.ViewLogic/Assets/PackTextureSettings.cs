namespace Wizard.ViewLogic.Assets
{
    // 묶음 하나의 텍스처 설정(매니페스트 최상위 필드). spec 14 4.4절 "전부 점 필터, 밉맵 없음, 압축 없음", 5.7.1 2번.
    // 매니페스트가 압축을 적지 않은 묶음(players, wands, monsters, effects)은 "압축 없음"으로 읽는다 [임시]:
    // 규격(4.4)이 모든 그림에 압축 없음을 정했고, 가져오기 설정 강제(M9-AST-03)가 같은 값을 쓴다. 적었는지는 CompressionDeclared 로 남긴다.
    public readonly struct PackTextureSettings
    {
        public readonly bool PointFilter;
        public readonly bool Mipmaps;
        public readonly bool Compression;
        public readonly bool CompressionDeclared;

        public PackTextureSettings(bool pointFilter, bool mipmaps, bool compression, bool compressionDeclared)
        {
            PointFilter = pointFilter;
            Mipmaps = mipmaps;
            Compression = compression;
            CompressionDeclared = compressionDeclared;
        }

        // 규격(점 필터, 밉맵 없음, 압축 없음)을 지키는가
        public bool IsPixelExact => PointFilter && !Mipmaps && !Compression;
    }
}
