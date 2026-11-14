using System;
using System.Collections.Generic;
using Wizard.Solo.Data;
using Wizard.Solo.Meta;

namespace Wizard.Solo.Save
{
    /// <summary>
    /// 새 오프라인 프로필(07 요약서 9.2절 = S8 8.4, 01 요약서 3.2절, 00 문서 11절).
    /// 캐릭터 5명, 캐릭터마다 +0 지팡이 한 자루(코어 마력탄, 연결 칸 base_link_slots개 빈 칸, 든 지팡이),
    /// 서고 마력탄 = 지팡이 수(전부 끼움), 골드·강화석·부적 0, 해금 = 마력 방출·반동 사격·고유 비기, 허상 못 만남.
    /// </summary>
    public static class NewProfile
    {
        public const string OriginStarter = "Starter";

        public static ProfileData Create(IContentDb content, IClock clock)
        {
            DateTime now = clock != null ? clock.Now : DateTime.Now;
            string nowIso = SystemClock.Iso(now);
            var p = new ProfileData
            {
                DataVersion = content != null ? content.DataVersion : null,
                ProfileKind = ProfileKind.Offline.ToString(),
                CreatedAt = nowIso,
                LibraryExpansions = 0,
                LastCharacter = 1,
            };
            p.ShopWeek.WeekStart = SystemClock.WeekStartIso(now);
            p.ShopWeek.CharmBought = 0;
            p.Flags.PhantomMet = false;
            p.Currencies.Gold = 0;

            long nextWandId = 1;
            for (int n = 1; n <= Ids.Char.Count; n++)
            {
                CharacterDef def = content != null ? (content.Character(Ids.Char.Of(n)) ?? content.BaselineCharacter) : null;
                int linkSlots = def != null ? def.BaseLinkSlots : 0;

                var wand = new WandData
                {
                    Id = nextWandId++,
                    Character = n,
                    Stage = 0,
                    Origin = OriginStarter,
                    OwnerCount = 1,
                    AcquiredAt = nowIso,
                    ShowScars = false,
                    Loadout = new LoadoutData { Core = Ids.Block.MagicBolt }
                };
                for (int i = 0; i < linkSlots; i++) wand.Loadout.Links.Add(null);
                p.Wands.Add(wand);

                var arts = new List<string> { Ids.Art.Burst, Ids.Art.Recoil };
                string unique = def != null ? def.UniqueArt : null;
                if (!string.IsNullOrEmpty(unique) && !arts.Contains(unique)) arts.Add(unique);
                p.Characters.Add(new CharacterRecord { Id = n, EquippedWandId = wand.Id, UnlockedArts = arts });
            }
            p.NextWandId = nextWandId;

            p.Items[Ids.Item.StoneLow] = new ItemStack();
            p.Items[Ids.Item.StoneMid] = new ItemStack();
            p.Items[Ids.Item.StoneHigh] = new ItemStack();
            p.Items[Ids.Item.Charm] = new ItemStack();
            // 서고의 마력탄은 지팡이마다 끼운 한 개씩이 전부다(여분 없음).
            p.Items[Ids.Block.MagicBolt] = new ItemStack { Qty = p.Wands.Count, QtySocketed = p.Wands.Count };
            return p;
        }
    }
}
