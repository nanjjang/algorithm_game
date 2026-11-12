using Wizard.Core;
using Tod = Wizard.Content.TimeOfDay;

namespace Wizard.Sim.World
{
    // 하루와 웨이브 번호. DECISIONS 2.3(확정), spec 01 5.7, spec 14 3.9·7.3. 상태가 없는 순수 함수다.
    // 하루 = 10웨이브: 낮(1~4), 해질녘(5~7), 밤(8~9), 보스(10). 그 뒤 다음 날로 끝없이 되풀이한다.
    // 하루의 구획은 DECISIONS 의 확정 사항이라 데이터가 아니라 여기의 상수다. 시간대마다의 표시 값(림 세기, 팔레트)과
    // 보스 순번 표는 데이터(ContentDb.TimeOfDay, ContentDb.BossRotation)에 있다.
    // 웨이브는 1부터다. 1보다 작은 웨이브는 웨이브가 아니므로 TimeOfDay.None 과 0을 돌려준다(예외를 던지지 않는다).
    public static class DayCycle
    {
        public const int WavesPerDay = 10;

        // 하루 안 순번(1~10)의 구획 끝
        private const int LastDaySlot = 4;
        private const int LastDuskSlot = 7;
        private const int LastNightSlot = 9;

        // 하루 안 순번 1~10(spec 01 5.7 Slot): ((w − 1) % 10) + 1
        public static int SlotInDay(int wave)
        {
            if (wave < 1)
            {
                return 0;
            }
            return (wave - 1) % WavesPerDay + 1;
        }

        // N일째 = 올림(w ÷ 10)(spec 14 3.9 DayNumber, spec 01 5.7 Day): (w + 9) / 10
        public static int DayNumber(int wave)
        {
            if (wave < 1)
            {
                return 0;
            }
            return (wave - 1) / WavesPerDay + 1;      // (w + 9) / 10 과 같고 int.MaxValue 근처에서 넘치지 않는다
        }

        // 시간대(spec 14 3.9 TimeOfDay). 코드는 spec 14 3.9의 표(1 낮, 2 해질녘, 3 밤, 4 보스)
        public static Tod TimeOfDay(int wave)
        {
            int slot = SlotInDay(wave);
            if (slot == 0)
            {
                return Tod.None;
            }
            if (slot <= LastDaySlot)
            {
                return Tod.Day;
            }
            if (slot <= LastDuskSlot)
            {
                return Tod.Dusk;
            }
            return slot <= LastNightSlot ? Tod.Night : Tod.BossWave;
        }

        // 보스 웨이브(하루의 10번째)인가
        public static bool IsBossWave(int wave) => SlotInDay(wave) == WavesPerDay;

        // 보스 순번 표의 색인(spec 01 5.7 BossOrderIndex): (w / 10 − 1) % 순번 길이. 0 = 첫 보스(가마등).
        // 순번 길이는 데이터(ContentDb.BossRotation)의 것이다. 보스 웨이브가 아니거나 길이가 1보다 작으면 −1.
        // 허상과의 첫 만남(웨이브 10의 예외)은 여기서 가르지 않는다(IsPhantomFirstEncounter, spec 14 7.3 BossForWave)
        public static int BossOrderIndex(int wave, int rotationLength)
        {
            if (!IsBossWave(wave) || rotationLength < 1)
            {
                return -1;
            }
            return FixMath.ModInt(wave / WavesPerDay - 1, rotationLength);
        }

        // 이 웨이브가 허상과의 첫 만남인가(spec 01 5.7, DECISIONS 1.21). 판의 시작 정보 phantom_first 가 참이고
        // 첫 보스 웨이브(10)일 때만 참이다. 참인 웨이브에서는 쓰러져도 판이 끝나지 않는다(spec 01 6.3).
        // 그 뒤의 보스 웨이브(20, 30 …)는 phantom_first 와 상관없이 평소 보스다(spec 14 TV-046)
        public static bool IsPhantomFirstEncounter(int wave, bool phantomFirst) => phantomFirst && wave == WavesPerDay;
    }
}
