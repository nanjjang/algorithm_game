using System;
using Wizard.Content;

namespace Wizard.Sim.Arts
{
    // 콘텐츠 팩의 arts·tries 절에서 판정기 데이터(ParserSource)를 만드는 자리(CONTRACTS 3.5, T-52).
    // CommandTrie.Build(character, ContentDb)가 부른다. ArtDef·TrieDef 의 필드와 두 절의 읽기는 비기 데이터 작업(M5-DAT-01)이
    // 채우고, 그 작업이 이 파일의 몸통을 "ArtDef 15종 → ParserArt, 판정기 상수 → ParserTimings"로 바꾼다.
    // 그때까지는 절이 팩에 없으므로 접근자가 ContentException 을 던진다. 절이 있어도 옮길 필드가 없으면 여기서 던진다.
    // 판을 시작할 때만 불린다(할당한다). Step 안에서 부르지 않는다.
    public static class ParserSourceLoader
    {
        public static ParserSource FromContent(ContentDb db)
        {
            if (db == null)
            {
                throw new ArgumentNullException(nameof(db));
            }
            db.Art(ArtId.Burst);
            throw new ContentException("content section 'arts': parser fields are not defined yet (M5-DAT-01)");
        }
    }
}
