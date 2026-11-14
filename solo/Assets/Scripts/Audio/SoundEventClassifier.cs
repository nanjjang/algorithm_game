using System;

namespace Wizard.Solo.Audio
{
    /// <summary>
    /// 사건 id(data/presentation/event_bindings.json의 열쇠, 10 요약서 5.1절) → 섞는 묶음.
    /// 정확히 맞는 id가 먼저, 그다음 앞머리(점까지). 어디에도 안 걸리면 환경.
    /// </summary>
    public static class SoundEventClassifier
    {
        // (id 또는 앞머리, 묶음). 앞머리는 '.'으로 끝난다. 위에서부터 먼저 맞는 것.
        static readonly (string Key, SoundCategory Cat)[] Rules =
        {
            // H-08 내가 맞음, H-09 체력 칸, H-11 심장, H-12 사망
            ("player.hit", SoundCategory.PlayerHit),
            ("player.downed", SoundCategory.PlayerHit),
            ("player.hp_cell_broken", SoundCategory.PlayerHit),
            // N-01~N-03 예고
            ("telegraph.", SoundCategory.Telegraph),
            ("boss.telegraph_begin", SoundCategory.Telegraph),
            ("boss.telegraph_resolve", SoundCategory.Telegraph),
            // C-03~C-11 스침·임계·잠금
            ("graze.", SoundCategory.GrazeHeat),
            ("heat.", SoundCategory.GrazeHeat),
            // T 묶음 비기
            ("art.", SoundCategory.Art),
            ("cmd.", SoundCategory.Art),
            ("copy_circle.", SoundCategory.Art),
            // H-05~H-07 처치
            ("enemy.death", SoundCategory.Kill),
            ("enemy.death.", SoundCategory.Kill), // 솔로: 종별 음색 묶음(G16 sound.kill_groups)
            ("enemy.multi_kill", SoundCategory.Kill),
            ("boss.defeated", SoundCategory.Kill),
            ("boss.killed", SoundCategory.Kill),
            ("elite.death", SoundCategory.Kill),
            ("elite.killed", SoundCategory.Kill),
            // H-01~H-04, H-13~H-15 적중
            ("hit.", SoundCategory.Hit),
            ("explosion.", SoundCategory.Hit),
            ("chain.", SoundCategory.Hit),
            // 화면
            ("ui.", SoundCategory.Interface),
            ("enhance.", SoundCategory.Interface),
        };

        public static SoundCategory Classify(string eventId)
        {
            if (string.IsNullOrEmpty(eventId)) return SoundCategory.Ambient;
            for (int i = 0; i < Rules.Length; i++)
            {
                string k = Rules[i].Key;
                bool prefix = k[k.Length - 1] == '.';
                if (prefix ? eventId.StartsWith(k, StringComparison.Ordinal) : string.Equals(eventId, k, StringComparison.Ordinal))
                    return Rules[i].Cat;
            }
            return SoundCategory.Ambient;
        }
    }
}
