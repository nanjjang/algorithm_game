using System.Collections.Generic;
using UnityEngine.InputSystem;
using Wizard.Solo.Meta;

namespace Wizard.Solo.Screens.Battle
{
    /// <summary>조작 보기 한 줄: 논리 키 하나의 기능 이름과 지금 배치의 기본·보조 키.</summary>
    public sealed class ControlsRow
    {
        public LogicalKey Action;
        /// <summary>기능 이름 열쇠(str.key_rebind.action.*).</summary>
        public string ActionNameKey;
        /// <summary>기본 키 칸에 보일 키(빈 배열 = 없음). 일시정지 줄은 Esc(고정).</summary>
        public Key[] Primary;
        /// <summary>보조 키 칸에 보일 키(빈 배열 = 없음). 일시정지 줄은 재설정한 키(기본·보조 중 있는 것).</summary>
        public Key[] Secondary;
        /// <summary>Esc가 고정 키로 붙은 줄(일시정지).</summary>
        public bool FixedEsc;
    }

    /// <summary>
    /// 조작 보기(ControlsView)의 키 표(순수). 08 요약서 7절 "지금 키 배치 그대로", 02 요약서 3.1절 키 표.
    /// 키 배치는 설정(SettingsData.KeyPrimary/KeySecondary)을 그대로 읽는다. 설정이 없으면 Core의 KeyLayouts 기본 배치.
    /// 일시정지 줄 = Esc(메뉴 길의 고정 키, D-11) + 재설정한 키.
    /// </summary>
    public static class ControlsViewModel
    {
        public const string TitleKey = "str.controls_view.title";
        public const string PrimaryHeaderKey = "str.key_rebind.primary";
        public const string SecondaryHeaderKey = "str.key_rebind.secondary";
        /// <summary>표 아래 한 줄: 비기를 내는 법(자세 키 + 방향 → 비기 키)과 도감 안내. 값 stance·art = 지금 배치의 기본 키.</summary>
        public const string ArtNoteKey = "str.controls_view.art_note";

        /// <summary>그 논리 키 줄의 첫 키(기본 키 칸, 없으면 보조 키 칸). 없으면 Key.None.</summary>
        public static Key FirstKey(List<ControlsRow> rows, LogicalKey k)
        {
            if (rows == null) return Key.None;
            for (int i = 0; i < rows.Count; i++)
            {
                var r = rows[i];
                if (r == null || r.Action != k) continue;
                if (r.Primary != null && r.Primary.Length > 0) return r.Primary[0];
                if (r.Secondary != null && r.Secondary.Length > 0) return r.Secondary[0];
            }
            return Key.None;
        }

        /// <summary>표에 그리는 논리 키 9개(위·왼쪽·아래·오른쪽·발사·비기·자세·반전 자세·일시정지 — LogicalKey 번호 순).</summary>
        public static readonly LogicalKey[] Order =
        {
            LogicalKey.Up, LogicalKey.Left, LogicalKey.Down, LogicalKey.Right,
            LogicalKey.Fire, LogicalKey.Skill, LogicalKey.Stance, LogicalKey.Reverse, LogicalKey.Pause
        };

        public static string ActionNameKey(LogicalKey k)
        {
            switch (k)
            {
                case LogicalKey.Up: return "str.key_rebind.action.move_up";
                case LogicalKey.Left: return "str.key_rebind.action.move_left";
                case LogicalKey.Down: return "str.key_rebind.action.move_down";
                case LogicalKey.Right: return "str.key_rebind.action.move_right";
                case LogicalKey.Fire: return "str.key_rebind.action.fire";
                case LogicalKey.Skill: return "str.key_rebind.action.art";
                case LogicalKey.Stance: return "str.key_rebind.action.stance";
                case LogicalKey.Reverse: return "str.key_rebind.action.reverse_stance";
                case LogicalKey.Pause: return "str.key_rebind.action.pause";
            }
            return string.Empty;
        }

        public static List<ControlsRow> Build(SettingsData s)
        {
            IReadOnlyList<int> prim, sec;
            if (s != null && s.KeyPrimary != null && s.KeySecondary != null)
            {
                prim = s.KeyPrimary;
                sec = s.KeySecondary;
            }
            else
            {
                prim = KeyLayouts.DefaultPrimary;
                sec = KeyLayouts.DefaultSecondary;
            }
            var rows = new List<ControlsRow>(Order.Length);
            for (int i = 0; i < Order.Length; i++)
            {
                var k = Order[i];
                int p = At(prim, (int)k), q = At(sec, (int)k);
                var row = new ControlsRow { Action = k, ActionNameKey = ActionNameKey(k) };
                if (k == LogicalKey.Pause)
                {
                    row.FixedEsc = true;
                    row.Primary = new[] { Key.Escape };
                    var extra = new List<Key>(2);
                    if (p != 0 && p != (int)Key.Escape) extra.Add((Key)p);
                    if (q != 0 && q != (int)Key.Escape && q != p) extra.Add((Key)q);
                    row.Secondary = extra.ToArray();
                }
                else
                {
                    row.Primary = p != 0 ? new[] { (Key)p } : new Key[0];
                    row.Secondary = q != 0 ? new[] { (Key)q } : new Key[0];
                }
                rows.Add(row);
            }
            return rows;
        }

        static int At(IReadOnlyList<int> keys, int i) => keys != null && i >= 0 && i < keys.Count ? keys[i] : 0;
    }
}
