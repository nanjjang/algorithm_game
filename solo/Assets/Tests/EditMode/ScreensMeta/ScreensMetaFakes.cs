using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Wizard.Solo.Data;
using Wizard.Solo.Meta;
using Wizard.Solo.UI;

namespace Wizard.Solo.Tests.ScreensMeta
{
    /// <summary>Save·Changed 횟수를 세는 설정 저장소(진짜 저장소처럼 Save 때 Changed가 난다).</summary>
    public sealed class CountingSettingsStore : ISettingsStore
    {
        public SettingsData Current { get; set; } = new SettingsData();
        public bool ExistedAtLoad => true;
        public int Saves, ChangedCount;
        public event Action Changed;
        public CountingSettingsStore() { Changed += () => ChangedCount++; }
        public void Load() { }
        public void Save() { Saves++; Changed?.Invoke(); }
    }

    /// <summary>Save 횟수, 저장 폴더, RestorePrevious의 결과·호출 수를 정하는 프로필 저장소.</summary>
    public sealed class FakeProfileStore : IProfileStore
    {
        public ProfileData Current { get; set; } = new ProfileData();
        public int Saves, RestoreCalls;
        public bool RestoreResult;
        public bool RestoreThrows;
        public string Dir = "C:/fake/profiles/offline";
        public event Action Saved;
        public void LoadOrCreate(out bool created, out bool restored) { created = false; restored = false; }
        public void Save() { Saves++; Saved?.Invoke(); }
        public string Directory => Dir;
        public bool RestorePrevious()
        {
            RestoreCalls++;
            if (RestoreThrows) throw new InvalidOperationException("broken");
            return RestoreResult;
        }
    }

    /// <summary>값을 정하는 가짜 상점. Buy는 07 8절의 규칙대로 골드·한도만 본다(호출을 기록).</summary>
    public sealed class FakeShop : IShopService
    {
        public ShopState State = new ShopState
        {
            CharmPrice = 10000, CharmBoughtThisWeek = 0, CharmWeeklyLimit = 3, WandPrice = 500,
            LibraryStepPrice = 2000, LibraryStepBlocks = 50, LibraryCap = 100, Gold = 0
        };
        public readonly List<string> Calls = new List<string>();
        public ShopState GetState() => State;
        public Result<ShopState> Buy(string sku, int qty, int character)
        {
            Calls.Add(sku + "x" + qty + "@" + character);
            long price = sku == Ids.Item.Charm ? State.CharmPrice : sku == Ids.Item.WandBox ? State.WandPrice : State.LibraryStepPrice;
            if (sku == Ids.Item.Charm && State.CharmBoughtThisWeek + qty > State.CharmWeeklyLimit) return Result<ShopState>.Fail(ErrorCode.WeeklyLimit);
            if (State.Gold < price * qty) return Result<ShopState>.Fail(ErrorCode.NotEnoughGold);
            State.Gold -= price * qty;
            if (sku == Ids.Item.Charm) State.CharmBoughtThisWeek += qty;
            return Result<ShopState>.Success(State);
        }
    }

    /// <summary>열쇠를 그대로 돌려주는 이름 표("{name}" 채우기는 열쇠 뒤에 "|이름=값"을 붙인다 — 글자 검사용).</summary>
    public sealed class KeyNames : INameTable
    {
        public string Get(string key) => key;
        public bool Has(string key) => false;
        public string Format(string key, params object[] pairs)
        {
            var sb = new StringBuilder(key);
            for (int i = 0; i + 1 < pairs.Length; i += 2) sb.Append('|').Append(pairs[i]).Append('=').Append(pairs[i + 1]);
            return sb.ToString();
        }
        public string Number(long value) => value.ToString();
    }

    /// <summary>등록만 기록하는 화면 관리자.</summary>
    public sealed class RecordingScreens : IScreenManager
    {
        public readonly List<ScreenId> Registered = new List<ScreenId>();
        public readonly Dictionary<ScreenId, Func<IScreen>> Factories = new Dictionary<ScreenId, Func<IScreen>>();
        public readonly List<string> Calls = new List<string>();
        public ScreenArgs LastArgs;
        public void Register(ScreenId id, Func<IScreen> factory) { Registered.Add(id); Factories[id] = factory; }
        public void Push(ScreenId id, ScreenArgs args = null) { Calls.Add("push " + id); LastArgs = args; }
        public void Replace(ScreenId id, ScreenArgs args = null) { Calls.Add("replace " + id); }
        public void Pop(object result = null) { Calls.Add("pop"); }
        public void PopTo(ScreenId id, ScreenArgs args = null) { Calls.Add("popto " + id); }
        public ScreenId Top => ScreenId.None;
        public bool IsOpen(ScreenId id) => false;
        public void Close(IScreen screen, object result = null) { Calls.Add("close " + result); }
        public void Confirm(string titleKey, string bodyKey, bool caution, Action<bool> onResult) { Calls.Add("confirm " + bodyKey); }
        public void ConfirmText(string titleKey, string bodyText, bool caution, Action<bool> onResult) { Calls.Add("confirmtext"); }
        public void Toast(string text, float seconds = 3f) { Calls.Add("toast " + text); }
        public void ShowTooltip(string text, RectTransform anchor) { }
        public void HideTooltip(RectTransform anchor = null) { }
        public bool CaptureKeys { get; set; }
    }

    /// <summary>테스트용 프로필 만들기.</summary>
    public static class MetaProfiles
    {
        /// <summary>캐릭터 다섯, 캐릭터마다 지팡이 한 자루(단계 stages[i]).</summary>
        public static ProfileData Five(params int[] stages)
        {
            var p = new ProfileData();
            for (int c = 1; c <= Ids.Char.Count; c++)
            {
                long id = c;
                p.Wands.Add(new WandData { Id = id, Character = c, Stage = stages != null && stages.Length >= c ? stages[c - 1] : 0 });
                p.Characters.Add(new CharacterRecord { Id = c, EquippedWandId = id });
            }
            return p;
        }

        public static RunRecordData Run(string runId, int character, int reached, string date = "2026-11-01T10:00:00")
        {
            return new RunRecordData { RunId = runId, Character = character, ReachedWave = reached, StartWave = 1, Date = date };
        }
    }

    /// <summary>화면 트리의 글자를 모은다.</summary>
    public static class TreeText
    {
        public static List<string> All(GameObject root)
        {
            var list = new List<string>();
            foreach (var t in root.GetComponentsInChildren<UnityEngine.UI.Text>(true)) list.Add(t.text ?? string.Empty);
            // 한 줄 칸은 넘치면 "…"로 자르므로 전체 글도 넣는다.
            foreach (var l in root.GetComponentsInChildren<UiLabel>(true)) list.Add(l.FullText ?? string.Empty);
            return list;
        }

        public static bool AnyContains(List<string> texts, string part)
        {
            for (int i = 0; i < texts.Count; i++) if (texts[i].Contains(part)) return true;
            return false;
        }
    }
}
