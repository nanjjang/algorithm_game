using System.Collections.Generic;
using System.Text;
using Wizard.Solo.Meta;
using Wizard.Solo.Sim;

namespace Wizard.Solo.View.Effects
{
    /// <summary>피해 숫자 한 개의 모양(그리기 전 단계, 순수).</summary>
    public struct DamageNumberSpec
    {
        public DamageNumberKind Kind;
        /// <summary>철갑에 막힘(골렘 정면): 회색 0.8배(10 요약서 3.7절). 종류는 Normal로 센다.</summary>
        public bool Armored;
        /// <summary>잠식 중 피해: 작은 보라 ▲(03 요약서 10.3절).</summary>
        public bool Corroded;
        public int Amount;
        /// <summary>글리프로 그릴 글자(0~9 ! ▲ ▼). 골드의 '+'는 글리프에 없어 따로(uGUI Text) 그린다.</summary>
        public string Glyphs;
        /// <summary>글리프 앞에 붙는 글리프 밖 글자(골드 "+"). 없으면 null.</summary>
        public string Prefix;
        /// <summary>약점 숫자에 붙는 속성 문양(16×16)의 속성 id.</summary>
        public string ElementId;
        public float Scale;
        public int OutlinePx;
    }

    /// <summary>
    /// 피해 숫자 규칙(순수, 08 요약서 6.2절, 03 요약서 14절, TV-054):
    /// Normal 흰색 / Crit 1.5배 흰색 + 검정 굵은 테 + "!"(금색 안 씀) / Weak 1.25배 + ▲ + 속성 문양 / Resist 회청 0.8배 + ▼ / Gold 금색 "+숫자".
    /// 옵션 damage_number_mode: all = 전부, crit_weak_only = 임계·약점만, off = 없음.
    /// 한 피해에 임계와 약점이 같이 서면 임계 모양이 앞선다(솔로 임시: 강조가 큰 쪽 — 08 표는 둘을 따로만 적었다).
    /// </summary>
    public static class DamageNumberRules
    {
        public const char Exclaim = '!', Up = '▲', Down = '▼';
        public const string GoldPrefix = "+";

        public static DamageNumberKind KindOf(int damageFlags)
        {
            if ((damageFlags & DamageFlags.Crit) != 0) return DamageNumberKind.Crit;
            if ((damageFlags & DamageFlags.Weak) != 0) return DamageNumberKind.Weak;
            if ((damageFlags & DamageFlags.Resist) != 0) return DamageNumberKind.Resist;
            return DamageNumberKind.Normal;
        }

        /// <summary>합칠 때 남기는 종류: 강조가 큰 쪽(Crit &gt; Weak &gt; Resist &gt; Normal).</summary>
        public static DamageNumberKind Stronger(DamageNumberKind a, DamageNumberKind b) => Rank(a) >= Rank(b) ? a : b;

        static int Rank(DamageNumberKind k)
        {
            switch (k)
            {
                case DamageNumberKind.Crit: return 4;
                case DamageNumberKind.Weak: return 3;
                case DamageNumberKind.Resist: return 2;
                case DamageNumberKind.Gold: return 1;
                default: return 0;
            }
        }

        /// <summary>옵션이 그 종류를 보이게 하는가.</summary>
        public static bool Visible(string mode, DamageNumberKind kind)
        {
            if (mode == OptionValues.Off) return false;
            if (mode == OptionValues.CritWeakOnly) return kind == DamageNumberKind.Crit || kind == DamageNumberKind.Weak;
            return true;
        }

        /// <summary>글리프 글자열: 숫자 + 종류 표식.</summary>
        public static string Glyphs(DamageNumberKind kind, int amount)
        {
            if (amount < 0) amount = -amount;
            var sb = new StringBuilder(amount.ToString(System.Globalization.CultureInfo.InvariantCulture));
            if (kind == DamageNumberKind.Crit) sb.Append(Exclaim);
            else if (kind == DamageNumberKind.Weak) sb.Append(Up);
            else if (kind == DamageNumberKind.Resist) sb.Append(Down);
            return sb.ToString();
        }

        public static DamageNumberSpec Build(EffectsTuning t, DamageNumberKind kind, int amount, bool armored, bool corroded, string elementId)
        {
            string styleKey = armored && kind == DamageNumberKind.Normal ? "Armored" : kind.ToString();
            return new DamageNumberSpec
            {
                Kind = kind,
                Armored = armored && kind == DamageNumberKind.Normal,
                Corroded = corroded,
                Amount = amount,
                Glyphs = Glyphs(kind, amount),
                Prefix = kind == DamageNumberKind.Gold ? GoldPrefix : null,
                ElementId = kind == DamageNumberKind.Weak ? elementId : null,
                Scale = t != null ? t.F("damage_numbers", "scale", styleKey) : 1f,
                OutlinePx = t != null ? t.I("damage_numbers", "outline_px", styleKey) : 1
            };
        }

        /// <summary>글리프 색인(글리프 순서 "0123456789!▲▼"). 없는 글자면 −1.</summary>
        public static int GlyphIndex(string order, char c) => order == null ? -1 : order.IndexOf(c);

        /// <summary>
        /// 글자 배치(순수): n개 글리프를 가운데 맞춰 놓을 때 i번째 글리프 중심의 x(px, 숫자 중심 = 0). 폭 = 글리프 폭 × 배율.
        /// </summary>
        public static float GlyphOffsetPx(int index, int count, float glyphPx, float scale)
        {
            float w = glyphPx * scale;
            return (index - (count - 1) * 0.5f) * w;
        }
    }

    /// <summary>
    /// 같은 적 0.2초 안 피해를 한 숫자로 합친다(08 6.2절, particle_budget shares.damage_merge_window_seconds). 화면 상한 40개.
    /// 순수: 그리는 쪽은 Add가 돌려준 결과(새 숫자 / 기존 숫자 갱신)로 그린다.
    /// </summary>
    public sealed class DamageNumberMerger
    {
        public struct Live
        {
            public int Handle;
            public int EnemyId;
            public int StartTick;
            public DamageNumberKind Kind;
            public int Amount;
            public bool Armored, Corroded;
            public string ElementId;
        }

        readonly int _windowTicks;
        readonly int _max;
        readonly List<Live> _live = new List<Live>();
        int _nextHandle = 1;

        public DamageNumberMerger(float mergeWindowSeconds, int maxOnScreen)
        {
            _windowTicks = SimConst.Ticks(mergeWindowSeconds);
            _max = maxOnScreen;
        }

        public int Count => _live.Count;
        public IReadOnlyList<Live> Items => _live;

        /// <summary>
        /// 피해 하나를 더한다. merged = 기존 숫자에 합쳤다(handle은 그 숫자). evicted = 상한을 넘어 내보낸 가장 오래된 숫자(없으면 0).
        /// 골드(enemyId &lt;= 0)는 합치지 않는다.
        /// </summary>
        public Live Add(int enemyId, int tick, DamageNumberKind kind, int amount, bool armored, bool corroded, string elementId,
            out bool merged, out int evicted)
        {
            merged = false;
            evicted = 0;
            if (enemyId > 0 && kind != DamageNumberKind.Gold)
            {
                for (int i = _live.Count - 1; i >= 0; i--)
                {
                    var l = _live[i];
                    if (l.EnemyId != enemyId || l.Kind == DamageNumberKind.Gold) continue;
                    if (tick - l.StartTick > _windowTicks) continue;
                    l.Amount += amount;
                    l.Kind = DamageNumberRules.Stronger(l.Kind, kind);
                    l.Armored = l.Armored && armored;
                    l.Corroded = l.Corroded || corroded;
                    if (l.Kind == DamageNumberKind.Weak && l.ElementId == null) l.ElementId = elementId;
                    _live[i] = l;
                    merged = true;
                    return l;
                }
            }
            if (_max > 0 && _live.Count >= _max)
            {
                evicted = _live[0].Handle;
                _live.RemoveAt(0);
            }
            var n = new Live
            {
                Handle = _nextHandle++, EnemyId = enemyId, StartTick = tick, Kind = kind, Amount = amount,
                Armored = armored, Corroded = corroded, ElementId = elementId
            };
            _live.Add(n);
            return n;
        }

        /// <summary>그 적의 숫자가 합칠 창 안에 떠 있는가(새 숫자가 아니라 갱신이 된다).</summary>
        public bool HasMergeTarget(int enemyId, int tick)
        {
            if (enemyId <= 0) return false;
            for (int i = _live.Count - 1; i >= 0; i--)
                if (_live[i].EnemyId == enemyId && _live[i].Kind != DamageNumberKind.Gold && tick - _live[i].StartTick <= _windowTicks) return true;
            return false;
        }

        public void Remove(int handle)
        {
            for (int i = 0; i < _live.Count; i++) if (_live[i].Handle == handle) { _live.RemoveAt(i); return; }
        }

        public void Clear() => _live.Clear();
    }
}
