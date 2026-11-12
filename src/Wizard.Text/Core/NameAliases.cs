namespace Wizard.Text
{
    // spec 14 1.2절 끝 "WC 0의 기존 표기와의 호환": 이름 표 열쇠로 네임스페이스 없는 master, field, plaza, arena 를 받고
    // 정식 id(npc.master, place.*)로 바꿔 읽는다. 이진 절(names.<locale>.wznm)에는 별칭 칸이 없어서(CONTRACTS 5절)
    // 표를 코드에 고정한다(Wizard.Content.ContentIds.ResolveAlias 와 같은 방식. Wizard.Text 는 Content 를 참조하지 못한다).
    // 별칭은 id 이고 화면 글자가 아니다. 더하기만 한다.
    internal static class NameAliases
    {
        public static string Resolve(string nameKey)
        {
            switch (nameKey)
            {
                case "master":
                    return "npc.master";
                case "field":
                    return "place.field";
                case "plaza":
                    return "place.plaza";
                case "arena":
                    return "place.arena";
                default:
                    return nameKey;
            }
        }
    }
}
