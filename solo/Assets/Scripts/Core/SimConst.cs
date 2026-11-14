using System;
using UnityEngine;

namespace Wizard.Solo
{
    /// <summary>
    /// 단위 환산 상수와 자료구조 크기. 기획 수치는 여기에 두지 않는다(JSON의 Def에서 읽는다).
    /// 좌표: 1 unit = 1칸, 시뮬레이션은 원문 좌표(왼쪽 위 (0,0), y 아래가 +)를 그대로 쓰고 표시에서만 뒤집는다.
    /// </summary>
    public static class SimConst
    {
        public const int TicksPerSecond = 60;
        public const float TickSeconds = 1f / 60f;
        /// <summary>열 1 = 120 HU. 열은 int HU로만 다룬다(02 요약서 1절).</summary>
        public const int HuPerHeat = 120;
        /// <summary>입력 시각 단위 u: 1틱 = 256u, 1초 = 15,360u(04 요약서 0.2절).</summary>
        public const int UnitsPerTick = 256;
        public const int UnitsPerSecond = 15360;
        /// <summary>체력 1칸 = 100점(02 요약서 5.1절 [임시]).</summary>
        public const int HpPerCell = 100;
        public const int PixelsPerUnit = 64;
        /// <summary>플레이어 배열 크기. 솔로는 슬롯 0만 쓰지만 코드는 배열을 돈다.</summary>
        public const int MaxPlayers = 4;
        public const int LogicalKeyCount = 9;

        /// <summary>초 → 틱(가장 가까운 정수). 데이터의 *_seconds 값을 틱으로 바꿀 때 쓴다.</summary>
        public static int Ticks(float seconds) => (int)Math.Round(seconds * TicksPerSecond, MidpointRounding.AwayFromZero);
        /// <summary>초 → 입력 단위 u(가장 가까운 정수. 0.07초 = 1,075u, 0.18초 = 2,765u).</summary>
        public static long Units(double seconds) => (long)Math.Round(seconds * UnitsPerSecond, MidpointRounding.AwayFromZero);
        public static long TickStartU(int tick) => (long)tick * UnitsPerTick;
        public static int HeatToHu(float heat) => (int)Math.Round(heat * HuPerHeat, MidpointRounding.AwayFromZero);

        /// <summary>시뮬레이션 좌표 → Unity 월드 좌표(전장 높이에서 y를 뒤집는다: unityY = H − y).</summary>
        public static Vector3 ToView(Vector2 simPos, float arenaHeight) => new Vector3(simPos.x, arenaHeight - simPos.y, 0f);
        /// <summary>시뮬레이션 방향 벡터 → Unity 방향 벡터(y 부호만 뒤집는다).</summary>
        public static Vector2 DirToView(Vector2 simDir) => new Vector2(simDir.x, -simDir.y);
    }

    /// <summary>하루·웨이브 번호 순수 함수. 01 요약서 4.1절, 02 요약서 12절.</summary>
    public static class DayCycle
    {
        public const int DayLength = 10;
        /// <summary>N일째 = (w + 9) / 10.</summary>
        public static int Day(int wave) => (wave + 9) / DayLength;
        /// <summary>하루 속 순번 1~10.</summary>
        public static int Slot(int wave) => (wave - 1) % DayLength + 1;
        public static bool IsBossWave(int wave) => wave > 0 && wave % DayLength == 0;
        /// <summary>보스 순환 색인 = (w / 10 − 1) % count.</summary>
        public static int BossRotationIndex(int wave, int rotationCount) => (wave / DayLength - 1) % rotationCount;
        /// <summary>순환 회차. w10~60 → 1, 70~120 → 2.</summary>
        public static int BossCycle(int wave, int rotationCount) => (wave / DayLength - 1) / rotationCount + 1;
        /// <summary>시간대 id. 1~4 낮, 5~7 해질녘, 8~9 밤, 10 보스(waves.json tod_ranges와 같은 값).</summary>
        public static string Tod(int wave)
        {
            int s = Slot(wave);
            if (s <= 4) return Ids.Tod.Day;
            if (s <= 7) return Ids.Tod.Dusk;
            if (s <= 9) return Ids.Tod.Night;
            return Ids.Tod.Boss;
        }
    }
}
