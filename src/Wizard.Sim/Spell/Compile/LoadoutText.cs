using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Wizard.Content;
using Wizard.Core;

namespace Wizard.Sim.Spell.Compile
{
    // spec 02 14.5 입력 문법(개발용 임시 조립기). 형식은 [임시]:
    //   loadout := "core=" id "; links=" slot ("," slot)* [ "; char=" 1..5 ] [ "; ext=" 0..3 ] [ "; mut=" mut ("," mut)* ]
    //   slot    := id | "_"            (빈 칸)
    //   id      := spec 14 블록 id 에서 "block." 을 뗀 이름(magic_bolt, fireball, split_three …). "block." 을 붙여도 받고,
    //              ContentIds 의 별칭(명세의 great_fireball, split_3 …)도 받는다. 쓸 때(Format)는 spec 14 이름만 쓴다
    //   mut     := spec 14 변이 id 에서 "mutation." 을 뗀 이름(mirror_image, twisted_barrel, glass_wand, unstable_core,
    //              hypersensitive_circuit, gluttony). "mutation." 을 붙여도 받고, 명세 문법의 mirror·hair_trigger 도 받는다
    // char 를 빼면 1, ext 를 빼면 0 이다. 캐릭터의 기본 연결 칸 수·깊이 감쇠·밑줄은 문법에 없다: 캐릭터 데이터에서 얻어 ToSource 에 넘긴다.
    internal sealed class LoadoutText
    {
        private const string BlockPrefix = "block.";
        private const string MutationPrefix = "mutation.";
        private const string Empty = "_";

        public BlockId Core;
        public BlockId[] Links = Array.Empty<BlockId>();
        public CharacterId Character = CharacterId.Char1;
        public int ExtraSlots;
        public byte MutationBits;

        public CompileSource ToSource(int baseLinks, PowBase depthDecay, bool underlinePassive) =>
            new CompileSource(Core, Links, Character, baseLinks, ExtraSlots, depthDecay, underlinePassive, MutationBits);

        // 원천을 입력 문법으로 쓴다. 기본값(char=1, ext=0, 변이 없음)인 칸은 쓰지 않는다
        public static string Format(CompileSource src)
        {
            var sb = new StringBuilder();
            sb.Append("core=").Append(SlotName(src.Core)).Append("; links=");
            for (int i = 0; i < src.LinkCount; i++)
            {
                if (i > 0)
                {
                    sb.Append(',');
                }
                sb.Append(SlotName(src.Links[i]));
            }
            if (src.Character != CharacterId.Char1)
            {
                sb.Append("; char=").Append(((int)src.Character).ToString(CultureInfo.InvariantCulture));
            }
            if (src.ExtraSlots != 0)
            {
                sb.Append("; ext=").Append(src.ExtraSlots.ToString(CultureInfo.InvariantCulture));
            }
            if (src.MutationBits != 0)
            {
                sb.Append("; mut=");
                bool first = true;
                for (int bit = 0; bit < sizeof(byte) * 8; bit++)
                {
                    if ((src.MutationBits & (1 << bit)) == 0)
                    {
                        continue;
                    }
                    if (!first)
                    {
                        sb.Append(',');
                    }
                    first = false;
                    string id = ContentIds.ToId((MutationId)bit);
                    sb.Append(id.StartsWith(MutationPrefix, StringComparison.Ordinal) ? id.Substring(MutationPrefix.Length) : id);
                }
            }
            return sb.ToString();
        }

        // 받지 못하는 글자(모르는 열쇠·이름, 겹친 열쇠, core 나 links 가 없음, 범위 밖의 수)면 false. 예외를 던지지 않는다
        public static bool TryParse(string text, out LoadoutText result)
        {
            result = new LoadoutText();
            if (text == null)
            {
                return false;
            }
            bool sawCore = false, sawLinks = false, sawChar = false, sawExt = false, sawMut = false;
            foreach (string rawPart in text.Split(';'))
            {
                string part = rawPart.Trim();
                int eq = part.IndexOf('=');
                if (eq <= 0)
                {
                    return false;
                }
                string key = part.Substring(0, eq).Trim();
                string value = part.Substring(eq + 1).Trim();
                switch (key)
                {
                    case "core":
                        if (sawCore || !TryBlock(value, out result.Core))
                        {
                            return false;
                        }
                        sawCore = true;
                        break;
                    case "links":
                        if (sawLinks)
                        {
                            return false;
                        }
                        sawLinks = true;
                        string[] items = value.Split(',');
                        var links = new List<BlockId>(items.Length);
                        foreach (string item in items)
                        {
                            if (!TryBlock(item.Trim(), out BlockId b))
                            {
                                return false;
                            }
                            links.Add(b);
                        }
                        result.Links = links.ToArray();
                        break;
                    case "char":
                        if (sawChar || !TryInt(value, out int c) || c < (int)CharacterId.Char1 || c > (int)CharacterId.Char5)
                        {
                            return false;
                        }
                        sawChar = true;
                        result.Character = (CharacterId)c;
                        break;
                    case "ext":
                        if (sawExt || !TryInt(value, out int ext) || ext > CompileSource.MaxLinks)
                        {
                            return false;
                        }
                        sawExt = true;
                        result.ExtraSlots = ext;
                        break;
                    case "mut":
                        if (sawMut)
                        {
                            return false;
                        }
                        sawMut = true;
                        foreach (string item in value.Split(','))
                        {
                            string name = MutationAlias(item.Trim());
                            if (!name.StartsWith(MutationPrefix, StringComparison.Ordinal))
                            {
                                name = MutationPrefix + name;
                            }
                            if (!ContentIds.TryParse(name, out MutationId mut) || mut == MutationId.None)
                            {
                                return false;
                            }
                            result.MutationBits |= (byte)(1 << (int)mut);
                        }
                        break;
                    default:
                        return false;
                }
            }
            return sawCore && sawLinks;
        }

        // 명세 14.5 문법의 변이 이름 가운데 spec 14 id 와 다른 둘(CONTRACTS 2.1 대응표). 이 임시 문법 안에서만 받는다
        private static string MutationAlias(string name)
        {
            switch (name)
            {
                case "mirror": return "mirror_image";
                case "hair_trigger": return "hypersensitive_circuit";
                default: return name;
            }
        }

        private static string SlotName(BlockId id) => id == BlockId.None ? Empty : SpellDebugText.BlockName(id);

        private static bool TryBlock(string name, out BlockId id)
        {
            if (string.Equals(name, Empty, StringComparison.Ordinal))
            {
                id = BlockId.None;
                return true;
            }
            // spec 14 id 와 그 별칭(명세의 great_fireball, split_3 …. ContentIds.ResolveAlias), 그다음 "block." 을 뗀 이름
            if (ContentIds.TryParse(name, out id) && id != BlockId.None)
            {
                return true;
            }
            return ContentIds.TryParse(BlockPrefix + name, out id) && id != BlockId.None;
        }

        private static bool TryInt(string s, out int v) =>
            int.TryParse(s, NumberStyles.None, CultureInfo.InvariantCulture, out v);
    }
}
