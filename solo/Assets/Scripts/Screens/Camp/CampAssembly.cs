using System.Collections.Generic;
using Wizard.Solo.Data;
using Wizard.Solo.Meta;
using Wizard.Solo.Sim;

namespace Wizard.Solo.Screens.Camp
{
    /// <summary>조립 칸 하나에 그릴 것.</summary>
    public struct AssemblyItem
    {
        public string BlockId;
        public bool Borrowed;
        /// <summary>판 중 야생 블록(보관함에서 온 것). false = 서고 블록.</summary>
        public bool Wild;
        /// <summary>판 한정 블록 성장(★). 0 = 없음.</summary>
        public int Stars;
        public bool IsEmpty => string.IsNullOrEmpty(BlockId);
    }

    /// <summary>
    /// 조립 탭의 데이터 원천. 판 중(Camp·Tutorial)은 세계를 읽고 RunCommand를 보내고, 판 밖(Library·LoadoutEdit)은 편집본을 고쳐
    /// IWandService.SetLoadout으로 저장한다. 화면은 이 형만 보고 회로·보관함·대기 칸·서고 칸을 그린다.
    /// </summary>
    public abstract class CampAssembly
    {
        public abstract int LinkCount { get; }
        /// <summary>첫 줄 연결 칸 수(기본, 제자 3은 5). 나머지는 둘째 줄 확장 칸.</summary>
        public abstract int BaseLinkCount { get; }
        public abstract AssemblyItem At(int slotRef);
        /// <summary>보관함(판 중) 칸 수 — 그린다. 판 밖은 0.</summary>
        public virtual int StorageCount => 0;
        public virtual int StorageCap => 0;
        public virtual int BenchCount => 0;
        /// <summary>판 밖 서고 칸 수.</summary>
        public virtual int LibraryCount => 0;
        /// <summary>마지막 조작이 거절된 이유(이름 표 열쇠). 없으면 null.</summary>
        public string LastErrorKey { get; protected set; }

        protected readonly IContentDb Db;
        protected CampAssembly(IContentDb db) { Db = db; }

        /// <summary>그 블록을 그 자리에 둘 수 있나: 코어에는 투사체만(SL-4).</summary>
        public bool Fits(int slotRef, string blockId)
        {
            if (slotRef != SlotRefs.Core || string.IsNullOrEmpty(blockId)) return true;
            var def = Db != null ? Db.Block(blockId) : null;
            return def != null && def.IsProjectile;
        }

        /// <summary>끌어다 놓기·J 옮기기의 유효성. dst = SlotRefs.None이면 빼기.</summary>
        public virtual bool CanMove(int src, int dst)
        {
            if (src == dst || src == SlotRefs.None) return false;
            var a = At(src);
            if (a.IsEmpty) return false;
            bool srcWand = SlotRefs.IsWand(src, LinkCount);
            if (dst == SlotRefs.None) return srcWand;
            bool dstWand = SlotRefs.IsWand(dst, LinkCount);
            if (dstWand)
            {
                if (!Fits(dst, a.BlockId)) return false;
                var b = At(dst);
                if (srcWand && !b.IsEmpty && !Fits(src, b.BlockId)) return false;
                return true;
            }
            // 칸이 아닌 곳끼리: 같은 묶음 안 자리 바꾸기만.
            if (!srcWand)
                return (SlotRefs.IsStorage(src) && SlotRefs.IsStorage(dst) && SlotRefs.IndexOf(dst) < StorageCount)
                    || (SlotRefs.IsBench(src) && SlotRefs.IsBench(dst) && SlotRefs.IndexOf(dst) < BenchCount);
            // 칸 → 보관함·대기 칸·서고 칸 = 빼기(받는 자리에 블록이 있으면 시뮬레이션이 서로 바꾼다).
            return SlotRefs.IsStorage(dst) || SlotRefs.IsBench(dst) || SlotRefs.IsLibrary(dst);
        }

        /// <summary>옮긴다. 판 중이면 명령을 보내고(상태는 다음 틱에 바뀐다), 판 밖이면 편집본을 고쳐 저장한다. 보냈거나 바꿨으면 true.</summary>
        public abstract bool Move(int src, int dst);
        public abstract bool RemoveAll();

        /// <summary>J "끼우기"의 자동 대상: 투사체면 빈 코어 먼저, 아니면 첫 빈 연결 칸. 없으면 −1.</summary>
        public int AutoInsertTarget(string blockId)
        {
            if (string.IsNullOrEmpty(blockId)) return SlotRefs.None;
            if (At(SlotRefs.Core).IsEmpty && Fits(SlotRefs.Core, blockId)) return SlotRefs.Core;
            for (int i = 0; i < LinkCount; i++) if (At(SlotRefs.Link(i)).IsEmpty) return SlotRefs.Link(i);
            return SlotRefs.None;
        }

        /// <summary>지금 조립(코어, 연결 칸 id).</summary>
        public void Snapshot(out string core, List<string> links)
        {
            core = At(SlotRefs.Core).BlockId;
            links.Clear();
            for (int i = 0; i < LinkCount; i++) links.Add(At(SlotRefs.Link(i)).BlockId);
        }
    }

    /// <summary>판 중 조립(Camp·Tutorial). 상태를 직접 고치지 않고 world.Enqueue(RunCommand)만 한다.</summary>
    public sealed class WorldAssembly : CampAssembly
    {
        readonly SimWorld _w;
        readonly PlayerState _p;

        public WorldAssembly(SimWorld w, PlayerState p) : base(w != null ? w.Content : null) { _w = w; _p = p; }

        public SimWorld World => _w;
        public PlayerState Player => _p;
        public override int LinkCount => _p != null && _p.Loadout != null ? _p.Loadout.Links.Length : 0;
        public override int BaseLinkCount
        {
            get
            {
                int b = _p != null && _p.Def != null ? _p.Def.BaseLinkSlots : LinkCount;
                return b < LinkCount ? b : LinkCount;
            }
        }
        public override int StorageCount => _w != null ? _w.Run.Storage.Count : 0;
        public override int StorageCap => Db != null && Db.Drops != null ? Db.Drops.StorageCap : 0;
        public override int BenchCount => _w != null ? _w.Run.LibraryBench.Count : 0;

        public override AssemblyItem At(int r)
        {
            if (_p == null || _w == null) return default;
            if (SlotRefs.IsWand(r, LinkCount))
            {
                var s = r == SlotRefs.Core ? _p.Loadout.Core : _p.Loadout.Links[r - 1];
                return new AssemblyItem { BlockId = s.BlockId, Borrowed = s.Borrowed, Wild = s.WildUid != 0, Stars = s.Stars };
            }
            if (SlotRefs.IsStorage(r))
            {
                int i = SlotRefs.IndexOf(r);
                if (i < 0 || i >= _w.Run.Storage.Count) return default;
                var wb = _w.Run.Storage[i];
                return new AssemblyItem { BlockId = wb.BlockId, Borrowed = wb.Borrowed, Wild = true, Stars = wb.Stars };
            }
            if (SlotRefs.IsBench(r))
            {
                int i = SlotRefs.IndexOf(r);
                if (i < 0 || i >= _w.Run.LibraryBench.Count) return default;
                return new AssemblyItem { BlockId = _w.Run.LibraryBench[i].BlockId, Stars = _w.Run.LibraryBench[i].Stars };
            }
            return default;
        }

        public override bool CanMove(int src, int dst)
        {
            if (!base.CanMove(src, dst)) return false;
            // 칸의 야생 블록을 빼는데 보관함이 가득이면 못 뺀다(Run과 같은 규칙).
            if (SlotRefs.IsWand(src, LinkCount) && (dst == SlotRefs.None || (SlotRefs.IsStorage(dst) && SlotRefs.IndexOf(dst) >= StorageCount)))
            {
                var a = At(src);
                if (a.Wild && StorageCount >= StorageCap) return false;
            }
            return true;
        }

        public override bool Move(int src, int dst)
        {
            LastErrorKey = null;
            if (_w == null || _p == null || !CanMove(src, dst)) return false;
            _w.Enqueue(new RunCommand(RunCommandKind.LoadoutMove, src, dst, null, _p.Slot));
            return true;
        }

        public override bool RemoveAll()
        {
            LastErrorKey = null;
            if (_w == null || _p == null) return false;
            _w.Enqueue(new RunCommand(RunCommandKind.LoadoutRemoveAll, 0, 0, null, _p.Slot));
            return true;
        }
    }

    /// <summary>서고 칸 하나(08 8.2절 "서고(Library 모드)"): 가진 적 있는 블록만 칸이 있다(TV-083).</summary>
    public struct LibraryCell
    {
        public string BlockId;
        public string Rarity;
        public BlockKind Kind;
        /// <summary>서고 수량(끼운 것 포함, ItemStack.Qty).</summary>
        public int Qty;
        /// <summary>지팡이에 끼운 수(ItemStack.QtySocketed) — "끼움"으로 따로 센다.</summary>
        public int Socketed;
        /// <summary>지금 이 편집본에 더 끼울 수 있는 수.</summary>
        public int Available;
        /// <summary>수량 0(가진 적 있음) 상태.</summary>
        public bool ZeroState => Qty - Socketed <= 0 && Socketed <= 0;
        public int BoundQty;
    }

    /// <summary>
    /// 판 밖 조립(Library·LoadoutEdit). 편집본(LoadoutData)을 고치고 바로 IWandService.SetLoadout으로 저장한다.
    /// 거절되면 편집본을 되돌리고 그 오류 문구 열쇠를 LastErrorKey에 둔다(마지막 투사체·코어 규칙 등).
    /// </summary>
    public sealed class LibraryAssembly : CampAssembly
    {
        readonly IWandService _wands;
        readonly ProfileData _profile;
        public readonly long WandId;
        public readonly WandData Wand;
        readonly int _baseLinks;
        LoadoutData _edit;
        LoadoutData _saved;
        readonly List<LibraryCell> _cells = new List<LibraryCell>();

        /// <summary>등급 거르기(null = 전체), 종류 거르기(null = 전체).</summary>
        public string RarityFilter;
        public BlockKind? KindFilter;

        public LibraryAssembly(IContentDb db, IWandService wands, ProfileData profile, long wandId) : base(db)
        {
            _wands = wands;
            _profile = profile;
            WandId = wandId;
            Wand = profile != null ? profile.Wand(wandId) : null;
            var ch = Wand != null && db != null ? db.Character(Ids.Char.Of(Wand.Character)) : null;
            _baseLinks = ch != null ? ch.BaseLinkSlots : (Wand != null ? Wand.Loadout.Links.Count : 0);
            _saved = Normalize(Wand != null ? Wand.Loadout : new LoadoutData());
            _edit = _saved.Clone();
            RebuildCells();
        }

        public LoadoutData Edited => _edit;
        public override int BaseLinkCount => _baseLinks;
        public override int LinkCount => _baseLinks + (Wand != null ? Wand.SlotsExtra : 0);
        public override int LibraryCount => VisibleCells().Count;

        LoadoutData Normalize(LoadoutData src)
        {
            var d = new LoadoutData { Core = string.IsNullOrEmpty(src.Core) ? null : src.Core };
            int extra = Wand != null ? Wand.SlotsExtra : 0;
            for (int i = 0; i < _baseLinks; i++) d.Links.Add(i < src.Links.Count ? Clean(src.Links[i]) : null);
            for (int i = 0; i < extra; i++) d.Extra.Add(i < src.Extra.Count ? Clean(src.Extra[i]) : null);
            return d;
        }

        static string Clean(string s) => string.IsNullOrEmpty(s) ? null : s;

        string GetSlot(LoadoutData d, int r)
        {
            if (r == SlotRefs.Core) return d.Core;
            int i = r - 1;
            return i < d.Links.Count ? d.Links[i] : d.Extra[i - d.Links.Count];
        }

        void SetSlot(LoadoutData d, int r, string id)
        {
            if (r == SlotRefs.Core) { d.Core = id; return; }
            int i = r - 1;
            if (i < d.Links.Count) d.Links[i] = id; else d.Extra[i - d.Links.Count] = id;
        }

        static int CountIn(LoadoutData d, string id)
        {
            if (d == null || id == null) return 0;
            int n = d.Core == id ? 1 : 0;
            for (int i = 0; i < d.Links.Count; i++) if (d.Links[i] == id) n++;
            for (int i = 0; i < d.Extra.Count; i++) if (d.Extra[i] == id) n++;
            return n;
        }

        /// <summary>더 끼울 수 있는 수 = 서고의 안 끼운 수 + 이 지팡이에 저장된 것 − 편집본에 있는 것.</summary>
        public int Available(string blockId)
        {
            int free = _wands != null ? _wands.LibraryFree(blockId) : 0;
            return free + CountIn(_saved, blockId) - CountIn(_edit, blockId);
        }

        void RebuildCells()
        {
            _cells.Clear();
            if (Db == null || _profile == null) return;
            for (int i = 0; i < Db.Blocks.Count; i++)
            {
                var def = Db.Blocks[i];
                ItemStack st;
                // 가진 적 없는 블록은 칸 자체가 없다(TV-083). 프로필에 기록(수량 0 포함)이 있으면 가진 적 있음.
                if (!_profile.Items.TryGetValue(def.Id, out st) || st == null) continue;
                _cells.Add(new LibraryCell
                {
                    BlockId = def.Id, Rarity = def.Rarity, Kind = def.KindEnum, Qty = st.Qty, Socketed = st.QtySocketed,
                    BoundQty = st.QtyBound, Available = Available(def.Id)
                });
            }
        }

        /// <summary>서고 격자에 보이는 칸(거르기 적용). 칸 번호 k = 이 목록의 첨자.</summary>
        public List<LibraryCell> VisibleCells()
        {
            var list = new List<LibraryCell>();
            for (int i = 0; i < _cells.Count; i++)
            {
                var c = _cells[i];
                if (RarityFilter != null && c.Rarity != RarityFilter) continue;
                if (KindFilter.HasValue && c.Kind != KindFilter.Value) continue;
                list.Add(c);
            }
            return list;
        }

        public IReadOnlyList<LibraryCell> AllCells => _cells;

        public override AssemblyItem At(int r)
        {
            if (Wand == null) return default;
            if (SlotRefs.IsWand(r, LinkCount)) return new AssemblyItem { BlockId = GetSlot(_edit, r) };
            if (SlotRefs.IsLibrary(r))
            {
                var vis = VisibleCells();
                int k = SlotRefs.IndexOf(r);
                if (k < 0 || k >= vis.Count || vis[k].Available <= 0) return default;
                return new AssemblyItem { BlockId = vis[k].BlockId };
            }
            return default;
        }

        public override bool CanMove(int src, int dst)
        {
            if (SlotRefs.IsLibrary(src))
            {
                var a = At(src);
                return !a.IsEmpty && SlotRefs.IsWand(dst, LinkCount) && Fits(dst, a.BlockId);
            }
            return base.CanMove(src, dst);
        }

        public override bool Move(int src, int dst)
        {
            LastErrorKey = null;
            if (Wand == null || !CanMove(src, dst)) return false;
            var before = _edit.Clone();
            if (SlotRefs.IsLibrary(src)) SetSlot(_edit, dst, At(src).BlockId);   // 칸에 있던 것은 서고로 돌아간다(수량 셈은 Available이 한다)
            else if (SlotRefs.IsWand(dst, LinkCount))
            {
                string a = GetSlot(_edit, src), b = GetSlot(_edit, dst);
                SetSlot(_edit, dst, a);
                SetSlot(_edit, src, b);
            }
            else SetSlot(_edit, src, null);
            return Commit(before);
        }

        public override bool RemoveAll()
        {
            LastErrorKey = null;
            if (Wand == null) return false;
            var before = _edit.Clone();
            for (int r = 0; r <= LinkCount; r++) SetSlot(_edit, r, null);
            return Commit(before);
        }

        bool Commit(LoadoutData before)
        {
            if (_wands == null) { _edit = before; LastErrorKey = CampFormat.ErrorKey(ErrorCode.NotFound); RebuildCells(); return false; }
            var res = _wands.SetLoadout(WandId, _edit.Clone());
            if (!res.Ok)
            {
                _edit = before;
                LastErrorKey = CampFormat.ErrorKey(res.Error);
                RebuildCells();
                return false;
            }
            _saved = _edit.Clone();
            RebuildCells();
            return true;
        }

        /// <summary>서고 칸 표시 "서고 {used} / {cap}"(한도 = 끼운 것 제외 블록 수 합). 가득이면 호박색 + error.library_full.</summary>
        public void LibraryCounter(out int used, out int cap, out bool full)
        {
            used = _wands != null ? _wands.LibraryTotal() : 0;
            cap = _wands != null ? _wands.LibraryCap() : 0;
            full = cap > 0 && used >= cap;
        }
    }
}
