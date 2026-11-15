using System.Collections.Generic;
using Wizard.Solo.Data;
using Wizard.Solo.Sim;

namespace Wizard.Solo.Net
{
    /// <summary>
    /// 온라인 판의 한 사람 출발 설정(11-online.md 6.8절). 오프라인 프로필을 읽지 않는다(오프라인·서버 프로필 분리):
    /// 캐릭터 기본 지팡이(코어 마력탄, 연결 칸 = 캐릭터 기본 칸 수, 빈 칸), 시작 비기(폭발·반동·고유), 지팡이 단계 = 서버 프로필 값(손님 0).
    /// 클라이언트는 캐릭터와 기본 공격 유형만 정하고, 서버가 Hello를 받을 때 이 함수로 다시 만든다(손님이 보낸 단계·조립은 믿지 않는다).
    /// </summary>
    public static class OnlineMember
    {
        public static RunConfig Build(IContentDb db, int character, int stage, string attackStyle = null, LoadoutSpec loadout = null)
        {
            if (character < 1 || character > Ids.Char.Count) character = 1;
            string charId = Ids.Char.Of(character);
            CharacterDef def = db != null ? (db.Character(charId) ?? db.BaselineCharacter) : null;
            int links = def != null ? def.BaseLinkSlots : 0;
            var spec = new LoadoutSpec { Core = Ids.Block.MagicBolt, Links = new List<string>() };
            for (int i = 0; i < links; i++) spec.Links.Add(null);
            var arts = new List<string> { Ids.Art.Burst, Ids.Art.Recoil };
            if (def != null && !string.IsNullOrEmpty(def.UniqueArt) && !arts.Contains(def.UniqueArt)) arts.Add(def.UniqueArt);
            int max = db != null && db.Enhance != null && db.Enhance.MaxStage > 0 ? db.Enhance.MaxStage : 13;
            return new RunConfig
            {
                Mode = RunMode.Endless,
                CharId = charId,
                WandId = 0,
                WandStage = stage < 0 ? 0 : (stage > max ? max : stage),
                LinkSlotCount = loadout != null ? loadout.Links.Count : links,
                Loadout = loadout != null ? loadout.Clone() : spec,
                UnlockedArts = arts,
                AttackStyle = attackStyle,
            };
        }
    }
}
