using System;
using System.Collections.Generic;
using Wizard.Solo.Data;
using Wizard.Solo.Sim;

namespace Wizard.Solo.Screens.Camp
{
    /// <summary>트리 마디의 틀 모양(03 요약서 13.4절: 투사체 둥근 / 제어 육각 / 변형 깎은 네모 / 스코프 주인 굵은 테).</summary>
    public enum TreeNodeShape { Projectile = 0, Control = 1, Modifier = 2, ScopeOwner = 3 }

    /// <summary>트리 마디 하나(08 요약서 8.3절 TreeNode).</summary>
    public sealed class TreeNodeRow
    {
        /// <summary>실행 순서 번호(1부터, 마디 왼쪽 위).</summary>
        public int Order;
        public string BlockId;
        public TreeNodeShape Shape;
        public int Depth;
        /// <summary>회로 칸 번호(0 = 코어, 1..L = 연결 칸) — 마디 커서 ↔ 회로 괄호 연동.</summary>
        public int Slot;
        /// <summary>오른쪽 꼬리에 그릴 변형 블록(최대 6개). 넘친 수는 MoreModifiers.</summary>
        public readonly List<string> Modifiers = new List<string>();
        public int MoreModifiers;
        /// <summary>분열 갈래(0 = 분열 아님, 2 / 3).</summary>
        public int ForkWays;
        /// <summary>64 상한으로 실행하지 않는 스코프의 마디(점선 + "생략").</summary>
        public bool Omitted;
        public bool HasWarning;
    }

    /// <summary>스코프 하나의 머리(들여쓰기 깊이, 배율 꼬리표, 갈피 각주 점).</summary>
    public sealed class TreeScopeRow
    {
        public int Depth;
        /// <summary>"0.85" 같은 배율 글자(× 기호는 이름 표 틀이 붙인다). 깊이 0이면 null.</summary>
        public string MultiplierText;
        /// <summary>기본과 다른 감쇠(지팡이 특성 0.92)면 깊이만큼 각주 점.</summary>
        public int FootnoteDots;
        public bool Omitted;
        /// <summary>밝기(깊이 1~3 = 100/70/50%, 깊이 0 = 100%).</summary>
        public float Brightness;
        public readonly List<TreeNodeRow> Nodes = new List<TreeNodeRow>();
    }

    /// <summary>
    /// 컴파일 결과(CompiledSpell) → 트리 미리보기 줄(08 요약서 8.3절, 03 요약서 13.4절). 컴파일러 결과를 그대로 옮길 뿐 다시 계산하지 않는다.
    /// </summary>
    public sealed class TreePreviewModel
    {
        /// <summary>변형 꼬리 한 줄 최대 개수(08 8.3절 "한 줄 최대 6개"). 자료구조 크기.</summary>
        public const int MaxModifierIcons = 6;
        /// <summary>경고 줄 최대 수(08 8.2절 "조립 경고 최대 3줄", TV-077). 자료구조 크기.</summary>
        public const int MaxWarningLines = 3;

        public readonly List<TreeScopeRow> Scopes = new List<TreeScopeRow>();
        /// <summary>경고 줄(이름 표 열쇠)과 그 진단의 칸 번호. 정렬 순서 앞 MaxWarningLines개.</summary>
        public readonly List<string> WarningKeys = new List<string>();
        public readonly List<int> WarningSlots = new List<int>();
        /// <summary>맨 아래 줄의 n(가지치기 뒤 실행할 스코프의 InstancesTotal 합).</summary>
        public int ProjectilesPerShot;
        /// <summary>맨 아래 줄의 열(HeatPerShot, 열 단위).</summary>
        public float HeatPerShot;
        public bool IsValid;
        /// <summary>오류(출발 불가) 진단이 있다.</summary>
        public bool HasError;

        public static TreePreviewModel Build(IContentDb db, CompiledSpell spell, float defaultDecay)
        {
            var m = new TreePreviewModel();
            if (spell == null) return m;
            m.IsValid = spell.IsValid;
            m.HeatPerShot = spell.HeatPerShotHu / (float)SimConst.HuPerHeat;

            for (int i = 0; i < spell.Diagnostics.Count; i++)
            {
                var d = spell.Diagnostics[i];
                if (d.IsError) m.HasError = true;
                string key = d.NameKey;
                if (key == null || m.WarningKeys.Count >= MaxWarningLines) continue;
                m.WarningKeys.Add(key);
                m.WarningSlots.Add(d.Code == SpellDiagCode.W_PROJECTILE_CAP ? -1 : d.Slot);
            }

            // 스코프를 깊이 순으로(S0 → S3 한 줄 사슬).
            var scopes = new List<SpellScope>(spell.Scopes);
            scopes.Sort((a, b) => a.Depth.CompareTo(b.Depth));
            string[] slots = spell.SlotBlockIds ?? new string[0];
            int order = 1;
            bool trait = Math.Abs(spell.DepthDecay - defaultDecay) > 1e-4f && spell.DepthDecay > 0f;

            for (int k = 0; k < scopes.Count; k++)
            {
                var sc = scopes[k];
                var row = new TreeScopeRow
                {
                    Depth = sc.Depth,
                    Omitted = sc.Depth > spell.MaxDepthNormal,
                    Brightness = BrightnessOf(sc.Depth)
                };
                if (sc.Depth > 0)
                {
                    double mul = sc.DepthMultiplier > 0f ? sc.DepthMultiplier : Math.Pow(spell.DepthDecay, sc.Depth);
                    row.MultiplierText = CampFormat.TwoDecimals(mul);
                    row.FootnoteDots = trait ? sc.Depth : 0;
                }
                if (!row.Omitted) m.ProjectilesPerShot += sc.InstancesTotal;

                // 이 스코프의 칸 구간: (이 스코프를 연 트리거, 다음 스코프를 여는 트리거]. 다음 트리거는 이 스코프의 제어 마디로 그린다.
                int from = sc.TriggerSlot + 1;
                int to = k + 1 < scopes.Count ? scopes[k + 1].TriggerSlot : slots.Length - 1;
                TreeNodeRow owner = null;
                var mods = new List<string>();
                for (int s = from; s <= to && s < slots.Length; s++)
                {
                    string id = slots[s];
                    if (string.IsNullOrEmpty(id)) continue;
                    BlockDef def = db != null ? db.Block(id) : null;
                    BlockKind kind = def != null ? def.KindEnum : BlockKind.Modifier;
                    if (kind == BlockKind.Modifier) { mods.Add(id); continue; }
                    var node = new TreeNodeRow
                    {
                        Order = order++, BlockId = id, Depth = sc.Depth, Slot = s, Omitted = row.Omitted,
                        Shape = kind == BlockKind.Projectile ? (s == sc.OwnerSlot ? TreeNodeShape.ScopeOwner : TreeNodeShape.Projectile) : TreeNodeShape.Control,
                        ForkWays = def != null && def.IsSplit ? def.SplitCount : 0
                    };
                    if (kind == BlockKind.Projectile && owner == null) owner = node;
                    row.Nodes.Add(node);
                }
                // 변형 꼬리: 주인 투사체 마디 오른쪽(투사체가 없으면 꼬리 없이 변형 마디로 남긴다).
                if (owner != null) FillModifiers(owner, mods);
                else
                {
                    for (int i = 0; i < mods.Count; i++)
                        row.Nodes.Add(new TreeNodeRow { Order = order++, BlockId = mods[i], Depth = sc.Depth, Slot = SlotOf(slots, mods[i], from, to), Shape = TreeNodeShape.Modifier, Omitted = row.Omitted });
                }
                m.Scopes.Add(row);
            }

            // 경고 딱지: 진단의 칸 번호가 마디의 칸이면.
            for (int i = 0; i < spell.Diagnostics.Count; i++)
            {
                var d = spell.Diagnostics[i];
                if (d.Code == SpellDiagCode.W_PROJECTILE_CAP) continue;
                for (int a = 0; a < m.Scopes.Count; a++)
                    for (int b = 0; b < m.Scopes[a].Nodes.Count; b++)
                        if (m.Scopes[a].Nodes[b].Slot == d.Slot) m.Scopes[a].Nodes[b].HasWarning = true;
            }
            return m;
        }

        static int SlotOf(string[] slots, string id, int from, int to)
        {
            for (int s = from; s <= to && s < slots.Length; s++) if (slots[s] == id) return s;
            return -1;
        }

        /// <summary>변형 꼬리를 채운다: 최대 6개, 넘친 수는 "+N"(TV-075: 8개 → 6 + 2).</summary>
        public static void FillModifiers(TreeNodeRow node, IList<string> mods)
        {
            node.Modifiers.Clear();
            for (int i = 0; i < mods.Count && i < MaxModifierIcons; i++) node.Modifiers.Add(mods[i]);
            node.MoreModifiers = mods.Count > MaxModifierIcons ? mods.Count - MaxModifierIcons : 0;
        }

        /// <summary>깊이 1~3은 분필 흰색 밝기 100/70/50%(03 13.4절). 깊이 0은 100%.</summary>
        public static float BrightnessOf(int depth)
        {
            switch (depth)
            {
                case 2: return 0.7f;
                case 3: return 0.5f;
                default: return 1f;
            }
        }

        /// <summary>깊이 d의 배율 꼬리표 글자(감쇠^d, 소수 둘째 자리 반올림). TV-073: 0.85 → 0.85 / 0.72 / 0.61, TV-074: 0.92 → 0.92 / 0.85 / 0.78.</summary>
        public static string ScopeMultiplierText(float decay, int depth) => CampFormat.TwoDecimals(Math.Pow(decay, depth));
    }
}
