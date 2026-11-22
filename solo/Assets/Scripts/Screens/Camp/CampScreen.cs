using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Wizard.Solo.Art;
using Wizard.Solo.Data;
using Wizard.Solo.Meta;
using Wizard.Solo.Sim;
using Wizard.Solo.UI;

namespace Wizard.Solo.Screens.Camp
{
    /// <summary>
    /// 야영 화면(ScreenId.Camp). 인자 ScreenArgKeys.CampMode의 모드 넷(08 요약서 8.2절, 03 요약서 13절):
    /// Camp(판 중 야영: [조립 | 비기], 회복·출발·조립 시험·다음 웨이브), Library(메인 메뉴 "조립과 서고": [조립 | 서고], Q/E 캐릭터),
    /// LoadoutEdit(원정 준비 [조립 바꾸기]: 인자 WandId의 지팡이 하나), Tutorial(튜토리얼 5단계: 조립만).
    /// 어느 모드에도 강화 탭·버튼은 없다(TV-068, 루트 CLAUDE.md 2.5절). 판 중 조작은 world.Enqueue(RunCommand)로만 한다.
    /// </summary>
    public sealed class CampScreen : ScreenBase
    {
        // 화면 배치: UiLayout 12열 격자(Content 좌표 = 화면 좌표 − 위 띠 80).
        //   왼쪽 0~2열 = 지팡이 회로·주문 흐름, 가운데 3~8열 = 탭 + 판(조립/서고/비기), 오른쪽 9~11열 = 수치·다음 웨이브.
        //   주 버튼(출발)은 UiLayout.PrimaryRect, 보조 행동(회복·연습)은 그 왼쪽 같은 줄.
        const float Top = ScreenBase.TopBarHeight;
        static readonly float LeftX = UiLayout.ColX(0), ColW = UiLayout.ColW(3);
        static readonly float CenterX = UiLayout.ColX(3), CenterW = UiLayout.ColW(6);
        static readonly float RightX = UiLayout.ColX(9);
        /// <summary>비기 트라이 판(폭 912 고정)을 가운데 6열 위에 가운데 맞춤으로 둔다.</summary>
        static readonly float TrieX = UiLayout.ColX(3) + (UiLayout.ColW(6) - ArtTrieView.W) * 0.5f;
        const float StorageCell = 64f, StorageGap = 16f, LibCellW = 96f, LibCellH = 112f, LibGap = 16f;
        /// <summary>세로: 첫 줄 24, 탭 48, 판은 그 밑 80부터. 왼쪽 회로 72, 주문 흐름 구역은 회로 밑.</summary>
        const float RowY = 24f, BoardY = 80f, CircuitY = 72f, TreeMaxH = 432f, WarnH = 28f;
        const float StatsY = 24f, NextWaveY = 208f, HealW = 340f, PracticeW = 300f;
        const int StorageColumns = 6, BenchColumns = 5, LibraryColumns = 8;
        /// <summary>바뀐 값이 밝아지는 길이(08 8.2절·TV-070: 0.5초 = 30틱).</summary>
        const int HighlightTicks = SimConst.TicksPerSecond / 2;

        public override ScreenId Id => ScreenId.Camp;
        protected override string TitleKey => "str.camp.title";

        CampModeLayout _layout;
        CampAssembly _assembly;
        SimWorld _world;
        PlayerState _player;
        int _character;
        long _wandId;
        int _held = SlotRefs.None;
        int _selected = SlotRefs.None;
        int _treeBracketSlot = -1;
        CampTab _tab;
        string _sig;
        string _noticeKey;
        readonly StatHighlight _highlight = new StatHighlight(HighlightTicks);

        UiLabel _header, _headerPurse, _headerHp, _wandName, _notice, _statHeat, _statCrit, _statDps, _critWarn, _bag, _departHint, _dawn, _libCounter;
        readonly List<UiLabel> _warnLines = new List<UiLabel>();
        readonly List<UiButton> _warnButtons = new List<UiButton>();
        readonly List<int> _warnSlots = new List<int>();
        RectTransform _headerChips;
        CircuitView _circuit;
        TreePreviewView _tree;
        RectTransform _assemblyPanel, _storageRoot, _benchRoot, _libraryRoot, _wandListRoot, _descRoot, _opsRoot, _nextWaveRoot, _filterRoot;
        ArtTrieView _trie;
        UiButton _heal, _practice, _depart, _insert, _remove, _swap, _removeAll;
        readonly Dictionary<UiButton, int> _cellRefs = new Dictionary<UiButton, int>();
        readonly List<UiButton> _dynamicCells = new List<UiButton>();
        string[] _tabKeys;

        // ---------------------------------------------------------------- 만들기

        protected override void BuildContent(RectTransform content)
        {
            // 위 띠 오른쪽: 긴 한 줄 대신 값 꼬리표 셋(날·웨이브 | 지갑 | 체력). 오른쪽에서 왼쪽으로 쌓는다.
            // 위 띠 오른쪽: UiLayout.Chip 셋(날·웨이브 | 지갑 | 체력). 글 길이에 맞춰 RebuildHeader가 다시 만든다.
            _headerChips = UiKit.Node(TopBar, "HeaderChips", 0f, (Top - UiLayout.ChipH) * 0.5f, UiScale.RefWidth - UiLayout.Outer, UiLayout.ChipH);

            // 왼쪽 단(0~2열): 지팡이 이름·단계, 회로(칸은 폭에 맞춰 크게), 주문 흐름(내용 높이에 맞춘 판), 경고 줄.
            _wandName = UiKit.Label(Ctx, content, string.Empty, UiFontId.F24, LeftX, RowY, ColW, 40f);
            _circuit = new CircuitView(Ctx, content, LeftX, CircuitY, ColW, 200f);
            _treeSection = UiKit.Node(content, "TreeSection", LeftX, 0f, ColW, UiLayout.SectionH);
            UiLayout.Section(Ctx, _treeSection, 0f, 0f, ColW, T("str.camp.section.spell"));
            _tree = new TreePreviewView(Ctx, content, LeftX, 0f, ColW, TreeMaxH);
            for (int i = 0; i < TreePreviewModel.MaxWarningLines; i++)
            {
                // 경고 줄을 고르면 트리의 그 마디에 괄호(08 8.2절).
                int line = i;
                var b = UiKit.Button(Ctx, content, null, LeftX, 0f, ColW, WarnH, () => OnWarningLine(line), UiButtonStyle.Plain, Focus, "");
                UiKit.WarningTriangle(b.ContentRoot, 0f, 6f, 16f);
                var l = UiKit.Label(Ctx, b.ContentRoot, string.Empty, UiFontId.F16, 24f, 2f, ColW - 24f, 24f, UiTheme.Amber);
                _warnButtons.Add(b);
                _warnLines.Add(l);
            }
            LayoutLeft();

            // 가운데 단(3~8열): 탭 아래 판.
            _assemblyPanel = UiKit.Node(content, "Assembly", CenterX, BoardY, CenterW, 760f);
            _storageRoot = UiKit.Node(_assemblyPanel, "Storage", 0f, UiLayout.SectionH, StorageColumns * (StorageCell + StorageGap), 2 * StorageCell + StorageGap);
            float benchX = StorageColumns * (StorageCell + StorageGap) + StorageGap;
            var storageTitle = UiKit.Node(_assemblyPanel, "StorageTitle", 0f, 0f, benchX - StorageGap * 2f, UiLayout.SectionH);
            UiLayout.Section(Ctx, storageTitle, 0f, 0f, benchX - StorageGap * 2f, T("str.camp.storage"));
            var benchTitle = UiKit.Node(_assemblyPanel, "BenchTitle", benchX, 0f, CenterW - benchX, UiLayout.SectionH);
            UiLayout.Section(Ctx, benchTitle, 0f, 0f, CenterW - benchX, T("str.camp.bench"));
            _benchRoot = UiKit.Node(_assemblyPanel, "Bench", benchX, UiLayout.SectionH, CenterW - benchX, 2 * StorageCell + StorageGap);
            _wandListRoot = UiKit.Node(_assemblyPanel, "WandList", 0f, 0f, CenterW, 48f);
            _libraryRoot = UiKit.Node(_assemblyPanel, "Library", 0f, 88f, CenterW, 2 * LibCellH + LibGap + 24f);
            _libCounter = UiKit.Label(Ctx, _assemblyPanel, string.Empty, UiFontId.F16, 0f, 56f, 400f, 24f);
            float descY = WorldDescY;
            _descRoot = UiKit.Node(_assemblyPanel, "Desc", 0f, descY, CenterW, DescH);
            float opsY = BoardY + descY + DescH + 24f;
            _opsRoot = UiKit.Node(content, "Ops", CenterX, opsY, CenterW, UiLayout.SecondaryH);
            float opW = (CenterW - 3f * UiLayout.ColGap) / 4f;
            _insert = UiLayout.SecondaryButton(Ctx, _opsRoot, T("str.camp.insert"), 0f, 0f, opW, OnInsertButton, Focus);
            _remove = UiLayout.SecondaryButton(Ctx, _opsRoot, T("str.camp.remove"), (opW + UiLayout.ColGap), 0f, opW, OnRemoveButton, Focus);
            _swap = UiLayout.SecondaryButton(Ctx, _opsRoot, T("str.camp.swap"), (opW + UiLayout.ColGap) * 2f, 0f, opW, OnSwapButton, Focus);
            _removeAll = UiLayout.SecondaryButton(Ctx, _opsRoot, T("str.camp.remove_all"), (opW + UiLayout.ColGap) * 3f, 0f, opW, OnRemoveAll, Focus);
            _notice = UiKit.Label(Ctx, content, string.Empty, UiFontId.F16, CenterX, opsY + UiLayout.SecondaryH + 16f, CenterW, 24f, UiTheme.Amber);
            UiKit.MakeDropTarget(_assemblyPanel.gameObject, p => p is int src && _assembly != null && _assembly.CanMove(src, SlotRefs.None),
                p => { if (p is int src) DoMove(src, SlotRefs.None); });

            _trie = new ArtTrieView(Ctx, content, TrieX, BoardY, Focus);
            _trie.Picked = OnArtPicked;

            // 아래 줄: 주 버튼(출발)은 PrimaryRect, 보조 행동(회복·연습)은 그 왼쪽 같은 줄에 조용한 꼴로.
            float subY = UiLayout.SecondaryYBesidePrimary(Top);
            float healX = UiLayout.PrimaryRect.x - UiLayout.ColGap - HealW;
            _heal = UiLayout.SecondaryButton(Ctx, content, T("str.camp.heal"), healX, subY, HealW, OnHeal, Focus);
            _dawn = UiKit.Label(Ctx, content, string.Empty, UiFontId.F16, healX, subY + UiLayout.SecondaryH + 2f, HealW, 24f, UiTheme.Good, TextAnchor.MiddleCenter);
            _practice = UiLayout.SecondaryButton(Ctx, content, T("str.camp.practice"), healX - UiLayout.ColGap - PracticeW, subY, PracticeW, OnPractice, Focus);

            // 오른쪽 단(9~11열): 구역 "주문 수치"(이름표 ― 값 줄), 구역 "다음 웨이브"(또는 서고 거르기), 가방 미리보기.
            float sy = UiLayout.Section(Ctx, content, RightX, StatsY, ColW, T("str.camp.section.stats"));
            _statHeat = UiLayout.StatRow(Ctx, content, RightX, sy, ColW, T("str.camp.stat.heat_per_shot"), string.Empty);
            sy += UiLayout.RowH + UiLayout.Rhythm;
            _statCrit = UiLayout.StatRow(Ctx, content, RightX, sy, ColW, T("str.camp.stat.crit_shots"), string.Empty);
            sy += UiLayout.RowH;
            _critWarn = UiKit.Label(Ctx, content, string.Empty, UiFontId.F16, RightX, sy, ColW, 24f, UiTheme.Amber, TextAnchor.MiddleRight);
            sy += 24f + UiLayout.Rhythm;
            _statDps = UiLayout.StatRow(Ctx, content, RightX, sy, ColW, T("str.camp.stat.dummy_dps"), string.Empty);

            _nextWaveRoot = UiKit.Node(content, "NextWave", RightX, NextWaveY, ColW, 520f);
            _filterRoot = UiKit.Node(content, "LibraryFilter", RightX, NextWaveY, ColW, 200f);
            _bag = UiKit.Label(Ctx, content, string.Empty, UiFontId.F16, RightX, NextWaveY + 400f, ColW, 48f, UiTheme.SlateBlue, TextAnchor.UpperLeft, true);
            _depart = UiLayout.PrimaryButton(Ctx, content, T("str.camp.depart"), OnDepart, Focus, Top);
            var pr = UiLayout.PrimaryRect;
            _departHint = UiKit.LabelKey(Ctx, content, "str.camp.depart_hint", UiFontId.F16, pr.x, pr.y + pr.height - Top, pr.width, 24f, UiTheme.SlateBlue, TextAnchor.MiddleCenter);
        }

        const float DescH = 176f;
        /// <summary>설명 판 y(판 기준): 판 중 = 보관함 두 줄 밑, 서고 = 서고 칸 두 줄 밑.</summary>
        const float WorldDescY = UiLayout.SectionH + 2 * StorageCell + StorageGap + 24f, LibraryDescY = 88f + 2 * LibCellH + LibGap + 24f + 24f;
        RectTransform _treeSection;

        /// <summary>왼쪽 단 세로 배치: 회로 높이 → 구역 제목 → 주문 흐름 판(내용 높이) → 경고 줄. 다시 그릴 때마다 맞춘다.</summary>
        void LayoutLeft()
        {
            float y = UiLayout.Snap(CircuitY + _circuit.Height + 24f);
            UiKit.Place(_treeSection, LeftX, y, ColW, UiLayout.SectionH);
            y += UiLayout.SectionH;
            UiKit.Place(_tree.Root, LeftX, y, ColW, _tree.Height);
            y += _tree.Height + UiLayout.Rhythm;
            for (int i = 0; i < _warnButtons.Count; i++)
            {
                UiKit.Place(_warnButtons[i].Rect, LeftX, y, ColW, WarnH);
                y += WarnH;
            }
        }

        /// <summary>위 띠 값 알약 하나(UiLayout.Chip). right = 오른쪽 끝 x(왼쪽으로 쌓는다). 반환 = 알약의 글자.</summary>
        UiLabel HeaderChip(ref float right, string text, Color color)
        {
            var chip = UiLayout.Chip(Ctx, _headerChips, 0f, 0f, text, null, color);
            float w = chip.rect.width;
            right -= w;
            UiKit.Place(chip, right, 0f, w, UiLayout.ChipH);
            right -= UiLayout.Rhythm;
            return chip.GetComponentInChildren<UiLabel>(true);
        }

        // ---------------------------------------------------------------- 들어오기

        public override void OnEnter(ScreenArgs args)
        {
            base.OnEnter(args);
            var mode = Args.Get(ScreenArgKeys.CampMode, CampMode.Camp);
            _layout = CampModeLayout.For(mode);
            SetTitle(T(_layout.TitleKey));
            _held = _selected = SlotRefs.None;
            _treeBracketSlot = -1;
            _warnBracketSlot = -1;
            _noticeKey = null;
            _highlight.Reset();
            _sig = null;

            _tabKeys = new string[_layout.Tabs.Count];
            for (int i = 0; i < _tabKeys.Length; i++) _tabKeys[i] = T(CampModeLayout.TabKey(_layout.Tabs[i]));
            BuildTabs();

            if (_layout.UsesWorld)
            {
                var s = Ctx != null ? Ctx.Session : null;
                _world = s != null ? (s.MainWorld ?? s.World) : null;
                _player = CampRules.MeOf(_world); // 협동: 내 캐릭터(내 보관함·내 비기 차례)
                _assembly = new WorldAssembly(_world, _player);
                _character = _player != null ? Ids.Char.NumberOf(_player.CharId) : 0;
            }
            else
            {
                _world = null;
                _player = null;
                var profile = Ctx != null ? Ctx.Profile : null;
                if (mode == CampMode.LoadoutEdit)
                {
                    _wandId = Args.Get(ScreenArgKeys.WandId, 0L);
                    var wand = profile != null ? profile.Wand(_wandId) : null;
                    _character = wand != null ? wand.Character : 1;
                }
                else
                {
                    _character = Args.Get(ScreenArgKeys.Character, profile != null ? profile.LastCharacter : 1);
                    if (_character < 1 || _character > Ids.Char.Count) _character = 1;
                    _wandId = EquippedWand(_character);
                }
                _assembly = new LibraryAssembly(Ctx.Content, Ctx.Wands, profile, _wandId);
            }

            _tab = CampTab.Assembly;
            // 해금 차례가 남아 있으면 비기 탭(고르는 판)부터(04 11.3절).
            if (mode == CampMode.Camp && _world != null && CampRules.ArtPickPending(CampRules.RewardOf(_world, _player))) _tab = CampTab.Arts;
            if (_tabsWidget != null) _tabsWidget.Select(_layout.Tabs.IndexOf(_tab), false);
            ApplyModeVisibility();
            RebuildAll();
            PlayUi("ui.camp.open");
            if (mode == CampMode.Camp && _world != null && _world.Run.DawnCamp) PlayUi("ui.camp.free_heal");
        }

        long EquippedWand(int character)
        {
            var p = Ctx != null ? Ctx.Profile : null;
            var rec = p != null ? p.Character(character) : null;
            return rec != null ? rec.EquippedWandId : 0;
        }

        void BuildTabs()
        {
            if (Tabs != null)
            {
                Tabs = null;
            }
            if (_tabsWidget != null)
            {
                foreach (var b in _tabsWidget.Buttons) Focus.Remove(b);
                if (Application.isPlaying) Object.Destroy(_tabsWidget.gameObject); else Object.DestroyImmediate(_tabsWidget.gameObject);
                _tabsWidget = null;
            }
            if (_tabKeys.Length <= 1) return;
            var tabs = UiKit.Tabs(Ctx, Content, _tabKeys, CenterX, RowY, 200f, 48f, OnTab, 0, false, 4f, _layout.QeSwitchesCharacter ? Focus : null);
            // Library는 Q/E가 캐릭터라 탭은 마우스로만 바꾼다(ScreenBase는 Tabs가 있으면 Q/E를 먼저 준다).
            Tabs = _layout.QeSwitchesCharacter ? null : tabs;
            _tabsWidget = tabs;
        }

        UiTabs _tabsWidget;

        void OnTab(int i)
        {
            if (i < 0 || i >= _layout.Tabs.Count) return;
            _tab = _layout.Tabs[i];
            _held = SlotRefs.None;
            ApplyModeVisibility();
            RebuildAll();
        }

        void ApplyModeVisibility()
        {
            bool world = _layout.UsesWorld;
            bool arts = _tab == CampTab.Arts;
            bool lib = _tab == CampTab.Library;
            _assemblyPanel.gameObject.SetActive(!arts);
            _opsRoot.gameObject.SetActive(!arts && !lib);
            _trie.SetActive(arts);
            _storageRoot.gameObject.SetActive(world);
            _benchRoot.gameObject.SetActive(world && _layout.ShowBench);
            foreach (Transform t in _assemblyPanel) if (t.name == "BenchTitle") t.gameObject.SetActive(world && _layout.ShowBench);
            SetStorageTitleVisible(world);
            _libraryRoot.gameObject.SetActive(!world);
            _libCounter.gameObject.SetActive(!world);
            _wandListRoot.gameObject.SetActive(!world && _layout.ShowWandPicker && !lib);
            _heal.gameObject.SetActive(_layout.ShowHeal);
            _dawn.gameObject.SetActive(_layout.ShowHeal);
            _practice.gameObject.SetActive(_layout.ShowPractice);
            _depart.gameObject.SetActive(_layout.ShowDepart);
            _departHint.gameObject.SetActive(_layout.ShowDepart);
            // 판 밖(서고)은 서고 칸이 두 줄이라 설명·조작 줄을 그 밑으로 내린다.
            float descY = world ? WorldDescY : LibraryDescY;
            UiKit.Place(_descRoot, 0f, descY, CenterW, DescH);
            float opsY = BoardY + descY + DescH + 24f;
            UiKit.Place(_opsRoot, CenterX, opsY, CenterW, UiLayout.SecondaryH);
            UiKit.Place(_notice.Rect, CenterX, opsY + UiLayout.SecondaryH + 16f, CenterW, 24f);
            _nextWaveRoot.gameObject.SetActive(_layout.ShowNextWave);
            _filterRoot.gameObject.SetActive(_layout.ShowLibraryFilter);
            _bag.gameObject.SetActive(_layout.ShowBagPreview);
            _headerChips.gameObject.SetActive(_layout.Mode == CampMode.Camp);
        }

        void SetStorageTitleVisible(bool on)
        {
            foreach (Transform t in _assemblyPanel) if (t.name == "StorageTitle") t.gameObject.SetActive(on);
        }

        // ---------------------------------------------------------------- 다시 그리기

        void RebuildAll()
        {
            RebuildHeader();
            RebuildAssembly();
            RebuildPreview();
            RebuildRight();
            if (_tab == CampTab.Arts) RebuildTrie();
            _notice.SetText(_noticeKey != null ? T(_noticeKey) : string.Empty);
        }

        void RebuildHeader()
        {
            if (_layout.Mode != CampMode.Camp || _world == null || _player == null) return;
            int wave = CampRules.HeaderWave(_world.Run);
            CampViews.Clear(_headerChips);
            float right = _headerChips.rect.width;
            bool hurt = _player.HpCells < _player.MaxHpCells;
            _headerHp = HeaderChip(ref right, F("str.camp.hp", "hp", _player.HpCells, "max", _player.MaxHpCells), hurt ? UiTheme.Amber : UiTheme.Chalk);
            _headerPurse = HeaderChip(ref right, F("str.format.gold_amount", "n", N(_world.Run.Purse)), UiTheme.Gold);
            _header = HeaderChip(ref right, F("str.camp.header_chip", "day", DayCycle.Day(wave < 1 ? 1 : wave), "wave", wave), UiTheme.Chalk);
        }

        void RebuildAssembly()
        {
            foreach (var b in _dynamicCells) if (b != null) Focus.Remove(b);
            _dynamicCells.Clear();
            _cellRefs.Clear();
            CampViews.Clear(_storageRoot);
            CampViews.Clear(_benchRoot);
            CampViews.Clear(_libraryRoot);
            CampViews.Clear(_wandListRoot);
            CampViews.Clear(_descRoot);
            CampViews.Clear(_filterRoot);

            _circuit.Rebuild(_assembly, Focus, OnCellClick, r => !_assembly.At(r).IsEmpty, (a, b) => _assembly.CanMove(a, b), DoMove, _held, _treeBracketSlot);
            foreach (var kv in _circuit.Refs) _cellRefs[kv.Key] = kv.Value;
            _wandName.SetText(WandTitle());

            if (_tab == CampTab.Arts) return;
            if (_layout.UsesWorld)
            {
                int cap = _assembly.StorageCap;
                for (int i = 0; i < cap; i++)
                {
                    var cell = UiKit.GridCell(i, StorageColumns, StorageCell, StorageCell, StorageGap, StorageGap);
                    AddCell(_storageRoot, SlotRefs.Storage(i), cell.x, cell.y, StorageCell, i < _assembly.StorageCount);
                }
                if (_layout.ShowBench)
                    for (int i = 0; i < _assembly.BenchCount; i++)
                    {
                        var cell = UiKit.GridCell(i, BenchColumns, StorageCell, StorageCell, StorageGap * 0.5f, StorageGap);
                        AddCell(_benchRoot, SlotRefs.Bench(i), cell.x, cell.y, StorageCell, true);
                    }
            }
            else BuildLibrary();
            BuildDescription();
            UpdateOps();
        }

        void AddCell(RectTransform parent, int r, float x, float y, float size, bool real)
        {
            var item = real ? _assembly.At(r) : default(AssemblyItem);
            int myRef = r;
            var b = CampViews.BlockCell(Ctx, parent, x, y, size, item, () => OnCellClick(myRef), Focus);
            b.Picked = _held == myRef;
            if (real)
            {
                UiKit.MakeDraggable(b.gameObject, myRef, null, () => !_assembly.At(myRef).IsEmpty);
                UiKit.MakeDropTarget(b.gameObject, p => p is int src && _assembly.CanMove(src, myRef), p => { if (p is int src) DoMove(src, myRef); });
            }
            _cellRefs[b] = myRef;
            _dynamicCells.Add(b);
        }

        void BuildLibrary()
        {
            var lib = _assembly as LibraryAssembly;
            if (lib == null) return;
            int used, cap; bool full;
            lib.LibraryCounter(out used, out cap, out full);
            _libCounter.SetText(F("str.format.library_counter", "used", N(used), "cap", N(cap)) + (full ? " " + T("error.library_full") : string.Empty));
            _libCounter.Color = full ? UiTheme.Amber : UiTheme.Chalk;

            if (_layout.ShowWandPicker && _tab == CampTab.Assembly) BuildWandList();

            var cells = lib.VisibleCells();
            for (int k = 0; k < cells.Count; k++)
            {
                var c = cells[k];
                var pos = UiKit.GridCell(k, LibraryColumns, LibCellW, LibCellH, LibGap, LibGap);
                int myRef = SlotRefs.Library(k);
                var b = UiKit.Button(Ctx, _libraryRoot, null, pos.x, pos.y, LibCellW, LibCellH, () => OnCellClick(myRef), UiButtonStyle.Plain, Focus, "");
                b.name = "LibCell." + c.BlockId;
                var cr = b.ContentRoot;
                Color rc = CampViews.RarityColor(c.Rarity);
                UiKit.Rect(cr, 0f, 0f, LibCellW, LibCellH, UiTheme.WithAlpha(UiTheme.WoodDark, 0.9f));
                UiFrame.Create(cr, c.ZeroState ? UiTheme.SlateBlue : rc, CampViews.RarityFrameThickness(c.Rarity));
                var icon = UiKit.Icon(Ctx, cr, c.BlockId, 48, (LibCellW - 48f) * 0.5f, 8f);
                if (c.ZeroState && icon != null) icon.color = UiTheme.SlateBlue;
                UiKit.Label(Ctx, cr, T(c.BlockId), UiFontId.F16, 2f, 56f, LibCellW - 4f, 20f, UiTheme.Chalk, TextAnchor.MiddleCenter);
                string count = F("str.camp.library_block_count", "n", N(c.Qty - c.Socketed));
                if (c.BoundQty > 0) count += F("str.format.bound_count", "n", c.BoundQty);
                UiKit.Label(Ctx, cr, count, UiFontId.F16, 2f, 72f, LibCellW - 4f, 20f, c.ZeroState ? UiTheme.SlateBlue : UiTheme.Chalk, TextAnchor.MiddleCenter);
                if (c.Socketed > 0)
                    UiKit.Label(Ctx, cr, T("str.camp.library_equipped") + " " + N(c.Socketed), UiFontId.F16, 2f, 88f, LibCellW - 4f, 20f, UiTheme.SlateBlue, TextAnchor.MiddleCenter);
                b.Picked = _held == myRef;
                UiKit.MakeDraggable(b.gameObject, myRef, null, () => !_assembly.At(myRef).IsEmpty);
                UiKit.MakeDropTarget(b.gameObject, p => p is int src && _assembly.CanMove(src, myRef), p => { if (p is int src) DoMove(src, myRef); });
                _cellRefs[b] = myRef;
                _dynamicCells.Add(b);
            }
            if (_layout.ShowLibraryFilter) BuildFilters(lib);
        }

        void BuildWandList()
        {
            var wands = Ctx.Wands != null ? Ctx.Wands.List(_character, WandSort.Recent) : new List<WandListEntry>();
            UiKit.Label(Ctx, _wandListRoot, T(Ids.Char.Of(_character)) + " " + T("str.camp.hint.character"), UiFontId.F16, 0f, 12f, 260f, 24f);
            float x = 264f;
            for (int i = 0; i < wands.Count; i++)
            {
                var e = wands[i];
                long id = e.Wand.Id;
                var b = UiKit.Button(Ctx, _wandListRoot, F("str.format.wand_stage", "n", e.Wand.Stage), x, 0f, 88f, 48f, () => SelectWand(id), UiButtonStyle.Small, Focus);
                b.On = id == _wandId;
                _dynamicCells.Add(b);
                x += 96f;
                if (x + 88f > CenterW) break;
            }
        }

        void BuildFilters(LibraryAssembly lib)
        {
            float fy = UiLayout.Section(Ctx, _filterRoot, 0f, 0f, ColW, T("str.camp.library.filter"));
            float fw = (ColW - 3f * UiLayout.Rhythm) / 4f;
            string[] rarities = { null, Ids.Rarity.Common, Ids.Rarity.Rare, Ids.Rarity.Hero };
            for (int i = 0; i < rarities.Length; i++)
            {
                string r = rarities[i];
                var b = UiKit.Button(Ctx, _filterRoot, r == null ? T("str.common.all") : T("str.block.grade." + r), i * (fw + UiLayout.Rhythm), fy, fw, 40f,
                    () => { lib.RarityFilter = r; RebuildAll(); }, UiButtonStyle.Small, Focus);
                b.On = lib.RarityFilter == r;
                _dynamicCells.Add(b);
            }
            BlockKind?[] kinds = { null, BlockKind.Projectile, BlockKind.Control, BlockKind.Modifier };
            string[] kindKeys = { "str.common.all", "str.block.kind.projectile", "str.block.kind.control", "str.block.kind.modifier" };
            for (int i = 0; i < kinds.Length; i++)
            {
                BlockKind? k = kinds[i];
                var b = UiKit.Button(Ctx, _filterRoot, T(kindKeys[i]), i * (fw + UiLayout.Rhythm), fy + 48f, fw, 40f,
                    () => { lib.KindFilter = k; RebuildAll(); }, UiButtonStyle.Small, Focus);
                b.On = lib.KindFilter == k;
                _dynamicCells.Add(b);
            }
        }

        void BuildDescription()
        {
            int r = _held != SlotRefs.None ? _held : _selected;
            var item = r != SlotRefs.None && _assembly != null ? _assembly.At(r) : default(AssemblyItem);
            UiLayout.Surface(Ctx, _descRoot, 0f, 0f, CenterW, DescH, 0);
            if (item.IsEmpty) return;
            var card = LootPickModel.Card(Ctx.Content, item.BlockId, CampViews.RarityOf(Ctx.Content, item.BlockId), item.Borrowed);
            UiKit.Icon(Ctx, _descRoot, item.BlockId, 96, 24f, 24f);
            UiKit.Label(Ctx, _descRoot, T(item.BlockId), UiFontId.F24, 144f, 16f, 400f, 32f);
            string tag = (card.KindKey != null ? T(card.KindKey) : string.Empty) + T("str.ui.hint.sep") + T(card.GradeKey);
            if (item.Borrowed) tag += T("str.ui.hint.sep") + T("str.block.borrowed");
            UiKit.Label(Ctx, _descRoot, tag, UiFontId.F16, 144f, 48f, 400f, 24f, CampViews.RarityColor(card.Rarity));
            CampViews.ElementGlyph(Ctx, _descRoot, card.Element, 552f, 24f);
            for (int i = 0; i < card.StatLines.Count; i++)
                UiKit.Label(Ctx, _descRoot, T(card.StatLines[i].Key) + " " + card.StatLines[i].Value, UiFontId.F16, 144f + i * 168f, 76f, 160f, 24f);
            // 효과 한 줄 + 붙는 규칙, 판 한정 별(★)이면 그 몫(BlockDescriber — 블록 데이터에서 만든다).
            var def = Ctx.Content != null ? Ctx.Content.Block(item.BlockId) : null;
            string desc = BlockDescriber.Text(Ctx.Names, def, true);
            // 코어가 기본 공격 유형의 핵이면 연결 블록이 그 핵에서 하는 일("이 핵에서는 …", Docs/12-attack-styles.md 2.2절)
            if (r != SlotRefs.Core && _assembly != null)
            {
                var coreId = _assembly.At(SlotRefs.Core).BlockId;
                var coreDef = coreId != null && Ctx.Content != null ? Ctx.Content.Block(coreId) : null;
                var on = BlockDescriber.OnCore(def, coreDef);
                if (on.Key != null) desc += T("str.ui.hint.sep") + on.Format(Ctx.Names);
            }
            if (item.Stars > 0)
            {
                var sl = BlockDescriber.StarLine(def, item.Stars, BlockDescriber.Stars(Ctx.Content));
                if (sl.Key != null) desc += T("str.ui.hint.sep") + sl.Format(Ctx.Names);
                CampViews.StarBadge(Ctx, _descRoot, 24f, 128f, item.Stars, 18f);
            }
            UiKit.Label(Ctx, _descRoot, desc, UiFontId.F16, 144f, 104f, CenterW - 168f, 64f, UiTheme.Chalk, TextAnchor.UpperLeft, wrap: true);
        }

        void RebuildPreview()
        {
            SpellPreviewResult r;
            var rules = Ctx != null ? Ctx.Rules : null;
            var db = Ctx != null ? Ctx.Content : null;
            if (_layout.UsesWorld)
            {
                r = SpellPreview.ForPlayer(rules, db, _player);
            }
            else
            {
                var lib = _assembly as LibraryAssembly;
                var wand = lib != null ? lib.Wand : null;
                var ch = db != null ? db.Character(Ids.Char.Of(_character)) : null;
                var muts = new List<string>();
                if (wand != null) foreach (var m in wand.Mutations) muts.Add(m.Id);
                string core;
                var links = new List<string>();
                _assembly.Snapshot(out core, links);
                r = SpellPreview.ForWand(rules, db, ch, wand != null ? wand.Stage : 0, muts, _assembly.LinkCount, core, links);
            }
            float decay = db != null && db.Const != null ? db.Const.DepthDecayDefault : 0f;
            var tree = TreePreviewModel.Build(db, r.Spell, decay);
            _lastTree = tree;
            _tree.Rebuild(tree, Focus, slot => { _treeBracketSlot = slot; RebuildCircuitOnly(); }, _warnBracketSlot);
            _warnSlots.Clear();
            _warnSlots.AddRange(tree.WarningSlots);
            for (int i = 0; i < _warnLines.Count; i++)
            {
                bool has = i < tree.WarningKeys.Count;
                _warnLines[i].SetText(has ? T(tree.WarningKeys[i]) : string.Empty);
                if (i < _warnButtons.Count) _warnButtons[i].gameObject.SetActive(has);
            }
            LayoutLeft();
            ApplyStats(r.Stats);
        }

        TreePreviewModel _lastTree;
        int _warnBracketSlot = -1;

        void OnWarningLine(int line)
        {
            if (line < 0 || line >= _warnSlots.Count) return;
            _warnBracketSlot = _warnSlots[line];
            _treeBracketSlot = _warnBracketSlot;
            if (_lastTree != null) _tree.Rebuild(_lastTree, Focus, slot => { _treeBracketSlot = slot; RebuildCircuitOnly(); }, _warnBracketSlot);
            RebuildCircuitOnly();
        }

        void RebuildCircuitOnly()
        {
            _circuit.Rebuild(_assembly, Focus, OnCellClick, r => !_assembly.At(r).IsEmpty, (a, b) => _assembly.CanMove(a, b), DoMove, _held, _treeBracketSlot);
            foreach (var kv in _circuit.Refs) _cellRefs[kv.Key] = kv.Value;
        }

        void ApplyStats(SpellStats s)
        {
            string heat = CampFormat.OneDecimal(s.HeatPerShot);
            string crit = s.CritShots < 0 ? T("str.camp.stat.infinite") : s.CritShots.ToString(System.Globalization.CultureInfo.InvariantCulture);
            string dps = CampFormat.OneDecimal(s.DummyDps);
            bool lit = false;
            lit |= _highlight.Set("heat", heat);
            lit |= _highlight.Set("crit", crit);
            lit |= _highlight.Set("dps", dps);
            if (lit) PlayUi("ui.camp.stat_highlight");
            _statHeat.SetText(heat);
            _statCrit.SetText(crit);
            _statDps.SetText(dps);
            _critWarn.SetText(CampRules.CritZeroWarning(s) ? T("str.camp.warn.crit_zero") : string.Empty);
            ApplyHighlightColors();
        }

        void ApplyHighlightColors()
        {
            _statHeat.Color = _highlight.IsLit("heat") ? UiTheme.SelectYellow : UiTheme.Chalk;
            _statCrit.Color = _highlight.IsLit("crit") ? UiTheme.SelectYellow : !string.IsNullOrEmpty(_critWarn.FullText) ? UiTheme.Amber : UiTheme.Chalk;
            _statDps.Color = _highlight.IsLit("dps") ? UiTheme.SelectYellow : UiTheme.Chalk;
        }

        void RebuildRight()
        {
            if (_layout.ShowHeal && _world != null)
            {
                var hs = CampRules.Heal(_world, _player);
                _heal.SetText(F("str.camp.heal_with_cost", "gold", N(hs.Cost)));
                if (hs.Enabled) _heal.SetEnabled(); else _heal.SetDisabled(hs.DisabledReasonKey);
                _dawn.SetText(hs.DawnLine ? T("str.camp.heal_dawn") : string.Empty);
            }
            if (_layout.ShowBagPreview && _world != null)
            {
                var bag = CampRules.BagPreview(Ctx.Settlement, Ctx.Content, _world.Run.ClearedDirect);
                _bag.SetText(F("str.camp.bag_preview", "slots", bag.Slots, "hero", bag.HeroCap));
            }
            if (_layout.ShowNextWave) BuildNextWave();
        }

        void BuildNextWave()
        {
            CampViews.Clear(_nextWaveRoot);
            if (_world == null) return;
            // 틀 없는 구역: 제목(Section) + 줄들. 가방 미리보기는 마지막 줄 바로 밑에 붙인다.
            float y = UiLayout.Section(Ctx, _nextWaveRoot, 0f, 0f, ColW, T("str.camp.next_wave"));
            var v = CampRules.NextWave(Ctx.Content, _world.Run.NextPlan, _world.Run.AffinityKnown);
            if (v.Wave > 0)
            {
                string line = F("str.format.wave", "n", v.Wave);
                if (v.TodId != null) line += T("str.ui.hint.sep") + T(v.TodId);
                if (v.FrameId != null) line += T("str.ui.hint.sep") + T(v.FrameId);
                // 첫 줄: 웨이브·시간대·틀(F24) + 오른쪽 끝에 틀·시간대 아이콘(보충 그림 icons frame.* / tod.*, 32×32 그대로).
                const int WaveIconPx = 32;
                float ix = ColW - WaveIconPx;
                if (v.FrameId != null) { UiKit.Icon(Ctx, _nextWaveRoot, v.FrameId, WaveIconPx, ix, y); ix -= WaveIconPx + 4f; }
                if (v.TodId != null) { UiKit.Icon(Ctx, _nextWaveRoot, v.TodId, WaveIconPx, ix, y); ix -= 8f; }
                UiKit.Label(Ctx, _nextWaveRoot, line, UiFontId.F24, 0f, y, ix, 32f);
                y += 40f;
                // 변덕: 이름(호박색) + 무엇이 달라지는지 한 줄(Data.WhimDescriber — 수치는 modifiers.json에서).
                for (int i = 0; i < v.Whims.Count; i++)
                {
                    UiKit.Label(Ctx, _nextWaveRoot, T(v.Whims[i]), UiFontId.F16, 0f, y, ColW, 24f, UiTheme.Amber);
                    y += 24f;
                    string effect = global::Wizard.Solo.Data.WhimDescriber.OneLine(Ctx.Names, Ctx.Content, v.Whims[i]);
                    if (effect == null) continue;
                    UiKit.Label(Ctx, _nextWaveRoot, effect, UiFontId.F16, 0f, y, ColW, 48f, UiTheme.SlateBlue, TextAnchor.UpperLeft, true);
                    y += 48f;
                }
                if (v.Whims.Count > 0) y += UiLayout.Rhythm;
                if (v.BossId != null)
                {
                    UiKit.Label(Ctx, _nextWaveRoot, T(v.BossId), UiFontId.F24, 0f, y, ColW, 32f);
                    y += 40f;
                }
                for (int i = 0; i < v.Species.Count && y < 400f; i++)
                {
                    var sp = v.Species[i];
                    UiKit.Label(Ctx, _nextWaveRoot, T(sp.SpeciesId), UiFontId.F16, 0f, y, ColW - 128f, 24f);
                    float gx = ColW - 120f;
                    if (sp.KnownWeak != null)
                    {
                        UiKit.LabelKey(Ctx, _nextWaveRoot, "str.camp.next_wave.weak_mark", UiFontId.F16, gx, y, 20f, 24f, UiTheme.Good);
                        CampViews.ElementGlyph(Ctx, _nextWaveRoot, sp.KnownWeak, gx + 20f, y + 4f);
                    }
                    if (sp.KnownResist != null)
                    {
                        UiKit.LabelKey(Ctx, _nextWaveRoot, "str.camp.next_wave.resist_mark", UiFontId.F16, gx + 64f, y, 20f, 24f, UiTheme.SlateBlue);
                        CampViews.ElementGlyph(Ctx, _nextWaveRoot, sp.KnownResist, gx + 84f, y + 4f);
                    }
                    y += 24f;
                }
                if (v.GuaranteedMaterials.Count > 0)
                {
                    string ml = T("str.camp.next_wave.materials");
                    for (int i = 0; i < v.GuaranteedMaterials.Count; i++)
                        ml += " " + F("str.camp.material_count", "name", T(v.GuaranteedMaterials[i].Key), "n", v.GuaranteedMaterials[i].Value);
                    UiKit.Label(Ctx, _nextWaveRoot, ml, UiFontId.F16, 0f, y + 4f, ColW, 48f, UiTheme.Chalk, TextAnchor.UpperLeft, true);
                    y += 52f;
                }
            }
            y += UiLayout.Rhythm * 2f;
            // 복수 중이면 추적기 큰 꼴(13.2절, 432×96) — 값은 RevengeTracker.Build 하나로.
            if (_world.Run.RevengeActive)
            {
                var t = RevengeTracker.Build(Ctx.Profile, Ctx.Content, _world.Run.Wave, metThisRun: _world.Run.RevengeActive);
                if (t.Visible)
                {
                    var box = UiKit.Node(_nextWaveRoot, "Tracker", 0f, y, ColW, 96f);
                    y += 108f;
                    UiLayout.Surface(Ctx, box, 0f, 0f, ColW, 96f, 0);
                    UiKit.Label(Ctx, box, T("revenge.tracker.goal"), UiFontId.F16, 12f, 4f, ColW - 24f, 24f);
                    UiKit.Label(Ctx, box, F("revenge.tracker.progress", "n", t.Progress), UiFontId.F16, 12f, 32f, ColW - 24f, 24f);
                    UiKit.ProgressBar(Ctx, box, 12f, 58f, ColW - 24f, 6f, t.ProgressMax > 0 ? (float)t.Progress / t.ProgressMax : 0f);
                    if (t.HasDent) UiKit.Label(Ctx, box, F("revenge.tracker.dent", "percent", t.DentPercent, "tries", t.Tries), UiFontId.F16, 12f, 68f, ColW - 24f, 24f, UiTheme.SlateBlue);
                }
            }
            UiKit.Place(_bag.Rect, RightX, NextWaveY + y, ColW, 48f);
        }

        void RebuildTrie()
        {
            if (_world == null || _player == null) { _trie.Rebuild(null, false); return; }
            var offer = CampRules.RewardOf(_world, _player).ArtOffer;
            var cands = offer != null ? offer.Candidates : null;
            var m = ArtTrieModel.Build(Ctx.Content, Ctx.Rules != null ? Ctx.Rules.Arts : null, _player.CharId, _player.UnlockedArts, cands);
            bool pick = cands != null && cands.Count > 0;
            _trie.Rebuild(m, pick);
            if (pick) PlayUi("ui.trie.unlock_candidate");
        }

        string WandTitle()
        {
            int stage = 0;
            if (_player != null && _player.Wand != null) stage = _player.Wand.Stage;
            else if (_assembly is LibraryAssembly lib && lib.Wand != null) stage = lib.Wand.Stage;
            string kind = Ids.Char.WandKindOf(_character);
            string nick = kind + ".stage." + stage.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".nickname";
            string s = T(kind) + " " + F("str.format.wand_stage", "n", stage);
            if (Ctx != null && Ctx.Names != null && Ctx.Names.Has(nick)) s += T("str.ui.hint.sep") + T(nick);
            return s;
        }

        void UpdateOps()
        {
            bool heldNonWand = _held != SlotRefs.None && !SlotRefs.IsWand(_held, _assembly.LinkCount);
            int r = _held != SlotRefs.None ? _held : _selected;
            bool wandWithBlock = r != SlotRefs.None && SlotRefs.IsWand(r, _assembly.LinkCount) && !_assembly.At(r).IsEmpty;
            bool sourceWithBlock = r != SlotRefs.None && !SlotRefs.IsWand(r, _assembly.LinkCount) && !_assembly.At(r).IsEmpty;
            if (sourceWithBlock || heldNonWand) _insert.SetEnabled(); else _insert.SetDisabled(null);
            if (wandWithBlock && _assembly.CanMove(r, SlotRefs.None)) _remove.SetEnabled(); else _remove.SetDisabled(null);
            if (r != SlotRefs.None && !_assembly.At(r).IsEmpty) _swap.SetEnabled(); else _swap.SetDisabled(null);
        }

        // ---------------------------------------------------------------- 조작

        int RefOf(IUiFocusable f)
        {
            var b = f as UiButton;
            int r;
            return b != null && _cellRefs.TryGetValue(b, out r) ? r : SlotRefs.None;
        }

        void OnCellClick(int r)
        {
            _selected = r;
            if (_held != SlotRefs.None)
            {
                if (_held == r) { _held = SlotRefs.None; RebuildAssembly(); return; }
                if (_assembly.CanMove(_held, r)) { int src = _held; _held = SlotRefs.None; DoMove(src, r); return; }
                _held = _assembly.At(r).IsEmpty ? SlotRefs.None : r;
                RebuildAssembly();
                return;
            }
            var item = _assembly.At(r);
            if (item.IsEmpty) { RebuildAssembly(); return; }
            if (!SlotRefs.IsWand(r, _assembly.LinkCount))
            {
                // J = 끼우기: 빈 코어(투사체) → 첫 빈 연결 칸. 빈 칸이 없으면 들어 두고 대상을 고른다.
                int target = _assembly.AutoInsertTarget(item.BlockId);
                if (target != SlotRefs.None && _assembly.CanMove(r, target)) { DoMove(r, target); return; }
            }
            _held = r;
            RebuildAssembly();
        }

        void DoMove(int src, int dst)
        {
            if (_assembly == null) return;
            bool srcWand = SlotRefs.IsWand(src, _assembly.LinkCount);
            bool dstWand = SlotRefs.IsWand(dst, _assembly.LinkCount);
            if (_assembly.Move(src, dst))
            {
                _noticeKey = null;
                PlayUi(srcWand && dstWand ? "ui.camp.slot_swap" : dstWand ? "ui.camp.slot_insert" : "ui.camp.slot_remove");
                PlayUi("ui.camp.tree_rebuild");
            }
            else _noticeKey = _assembly.LastErrorKey;
            _held = SlotRefs.None;
            AfterChange();
        }

        void AfterChange()
        {
            // 판 중이면 명령은 다음 틱에 적용된다 → Tick의 서명 비교가 다시 그린다. 판 밖은 바로 다시 그린다.
            if (!_layout.UsesWorld) RebuildAll();
            else { RebuildAssembly(); _notice.SetText(_noticeKey != null ? T(_noticeKey) : string.Empty); }
        }

        void OnInsertButton()
        {
            int r = _held != SlotRefs.None ? _held : _selected;
            if (r == SlotRefs.None || SlotRefs.IsWand(r, _assembly.LinkCount)) return;
            var item = _assembly.At(r);
            int target = _assembly.AutoInsertTarget(item.BlockId);
            if (target != SlotRefs.None) DoMove(r, target);
            else { _held = r; RebuildAssembly(); }
        }

        void OnRemoveButton()
        {
            int r = _held != SlotRefs.None ? _held : _selected;
            if (r != SlotRefs.None && SlotRefs.IsWand(r, _assembly.LinkCount)) DoMove(r, SlotRefs.None);
        }

        void OnSwapButton()
        {
            int r = _selected != SlotRefs.None ? _selected : _held;
            if (r == SlotRefs.None || _assembly.At(r).IsEmpty) return;
            _held = r;
            RebuildAssembly();
        }

        void OnRemoveAll()
        {
            if (_assembly != null && _assembly.RemoveAll()) { PlayUi("ui.camp.slot_remove"); PlayUi("ui.camp.tree_rebuild"); _noticeKey = null; }
            else if (_assembly != null) _noticeKey = _assembly.LastErrorKey;
            _held = SlotRefs.None;
            AfterChange();
        }

        void OnHeal()
        {
            if (_world == null || _player == null) return;
            var hs = CampRules.Heal(_world, _player);
            if (!hs.Enabled) return;
            _world.Enqueue(new RunCommand(RunCommandKind.CampHeal, 0, 0, null, _player.Slot));
            PlayUi("ui.camp.heal");
        }

        bool IsOnlineRun => Ctx != null && Ctx.Session is IOnlineSession os && os.IsOnline;

        void OnDepart()
        {
            if (_world == null) return;
            if (CampRules.ArtPickPending(CampRules.RewardOf(_world, _player)))
            {
                // 남은 해금 차례를 먼저 고른다(04 11.3절 [임시 U-22]).
                _noticeKey = "str.camp.depart_need_pick";
                SelectTab(CampTab.Arts);
                return;
            }
            // 협동: 내 출발 준비. 모두 준비되면 떠나고, 아니면 화면이 닫히고 전장에서 동료를 기다린다.
            _world.Enqueue(new RunCommand(RunCommandKind.CampDepart, slot: _player != null ? _player.Slot : 0));
            PlayUi("ui.camp.depart");
        }

        void SelectTab(CampTab t)
        {
            int i = _layout.Tabs.IndexOf(t);
            if (i < 0) return;
            if (_tabsWidget != null) _tabsWidget.Select(i, false);
            _tab = t;
            ApplyModeVisibility();
            RebuildAll();
        }

        void OnPractice()
        {
            if (Screens == null) return;
            if (_layout.Mode == CampMode.Camp)
                Screens.Push(ScreenId.DummyPractice, ScreenArgs.From(ScreenId.Camp).With(ScreenArgKeys.PracticeEntry, PracticeEntry.Camp));
            else if (_layout.Mode == CampMode.Library)
                Screens.Push(ScreenId.DummyPractice, ScreenArgs.From(ScreenId.Camp).With(ScreenArgKeys.PracticeEntry, PracticeEntry.MainMenu)
                    .With(ScreenArgKeys.Character, _character));
        }

        void OnArtPicked(string artId)
        {
            if (_world == null || _player == null || artId == null) return;
            var offer = CampRules.RewardOf(_world, _player).ArtOffer;
            if (offer == null || !offer.Candidates.Contains(artId)) return;
            _world.Enqueue(new RunCommand(RunCommandKind.ArtPick, 0, 0, artId, _player.Slot));
            // 비기 해금은 계정에 캐릭터별로 바로 남긴다(04 11.3절 "오프라인 프로필에 즉시 저장"). 온라인 판은 오프라인 프로필에 남기지 않는다.
            if (Ctx.Settlement != null && !IsOnlineRun) Ctx.Settlement.RecordArtUnlock(_character, artId);
            PlayUi("ui.trie.unlock");
            _noticeKey = null;
        }

        void SelectWand(long wandId)
        {
            _wandId = wandId;
            _assembly = new LibraryAssembly(Ctx.Content, Ctx.Wands, Ctx.Profile, _wandId);
            _held = _selected = SlotRefs.None;
            _highlight.Reset();
            RebuildAll();
        }

        void SwitchCharacter(int delta)
        {
            int n = Ids.Char.Count;
            _character = ((_character - 1 + delta) % n + n) % n + 1;
            SelectWand(EquippedWand(_character));
        }

        // ---------------------------------------------------------------- 키

        public override bool OnMenuKey(MenuKey key)
        {
            if (_layout == null) return base.OnMenuKey(key);
            if ((key == MenuKey.PrevTab || key == MenuKey.NextTab) && _layout.QeSwitchesCharacter)
            {
                SwitchCharacter(key == MenuKey.PrevTab ? -1 : 1);
                return true;
            }
            if (key == MenuKey.Cancel)
            {
                // K: 들고 있으면 내려놓기 → 커서 칸(지팡이)에 블록이 있으면 빼기 → 아니면 닫기.
                if (_held != SlotRefs.None) { _held = SlotRefs.None; RebuildAssembly(); return true; }
                int r = RefOf(Focus.Current);
                if (r != SlotRefs.None && SlotRefs.IsWand(r, _assembly.LinkCount) && !_assembly.At(r).IsEmpty)
                {
                    DoMove(r, SlotRefs.None);
                    return true;
                }
                if (!OnBack()) Close();
                return true;
            }
            bool used = base.OnMenuKey(key);
            int cur = RefOf(Focus.Current);
            if (cur != _selected && cur != SlotRefs.None) { _selected = cur; CampViews.Clear(_descRoot); BuildDescription(); UpdateOps(); }
            if (_tab == CampTab.Arts) _trie.SelectFocused(Focus.Current);
            return used;
        }

        public override bool OnBack()
        {
            if (_layout != null && _layout.CloseByCommand)
            {
                // 판 중 야영: 닫기는 CampClose 명령. 화면을 Pop 하는 것은 Battle(CampChanged).
                if (_world != null) _world.Enqueue(new RunCommand(RunCommandKind.CampClose, slot: _player != null ? _player.Slot : 0));
                return true;
            }
            return false;
        }

        // ---------------------------------------------------------------- 틱

        public override void Tick(float dt)
        {
            _highlight.Advance(dt);
            ApplyHighlightColors();
            if (_layout == null || !_layout.UsesWorld || _world == null) return;
            string sig = Signature();
            if (sig == _sig) return;
            bool first = _sig == null;
            _sig = sig;
            if (first) return;
            RebuildAll();
        }

        string Signature()
        {
            var run = _world.Run;
            var mine = CampRules.RewardOf(_world, _player);
            var sb = new System.Text.StringBuilder();
            sb.Append(run.Purse).Append('|').Append(mine.HealsThisCamp).Append('|').Append(run.Phase).Append('|');
            if (_player != null)
            {
                sb.Append(_player.Hp).Append('|').Append(_player.Loadout.Core.BlockId).Append(_player.Loadout.Core.WildUid);
                for (int i = 0; i < _player.Loadout.Links.Length; i++) sb.Append(',').Append(_player.Loadout.Links[i].BlockId).Append(_player.Loadout.Links[i].WildUid);
                sb.Append('|').Append(_player.UnlockedArts.Count);
            }
            for (int i = 0; i < mine.Storage.Count; i++) sb.Append(mine.Storage[i].Uid).Append(';');
            sb.Append('|');
            for (int i = 0; i < mine.LibraryBench.Count; i++) sb.Append(mine.LibraryBench[i].Uid).Append(';');
            sb.Append('|').Append(mine.ArtTurns.Count).Append(mine.ArtOffer != null ? mine.ArtOffer.TurnWave : -1);
            return sb.ToString();
        }

        protected override UiKeyHint[] KeyHints => new[]
        {
            new UiKeyHint(Key.J, "str.camp.insert"),
            new UiKeyHint(Key.K, "str.camp.remove"),
            new UiKeyHint(Key.Escape, "str.common.close")
        };
    }
}
