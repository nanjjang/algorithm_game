using Wizard.Content;

namespace Wizard.DataBuild.Validate.Affinity
{
    // 약점·저항을 가진 종 하나(일반 적 한 종 또는 보스 하나). 졸개와 엘리트는 줄이 아니다(spec 05 1.3 A4: 엘리트는 바탕 종 그대로, 졸개는 상성 없음)
    public sealed class AffinityRow
    {
        public string File { get; set; } = "";                  // 저장소 기준 경로(진단)
        public string Pointer { get; set; } = "";               // JSON 위치(진단)
        public string Id { get; set; } = "";                    // enemy.* / boss.*

        public AffinityGroup Group { get; set; }

        public Element Weak { get; set; }

        public Element Resist { get; set; }

        public int FirstWave { get; set; }                      // 처음 나오는 웨이브. 모르면 0

        public Element Own { get; set; }                        // 보스의 자기 속성. 일반 적과 속성 없는 보스는 None
    }
}
