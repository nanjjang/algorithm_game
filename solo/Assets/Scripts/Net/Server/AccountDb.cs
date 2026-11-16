using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;

namespace Wizard.Solo.Net
{
    /// <summary>계정 저장소 전체(서버가 시작할 때 한 번 읽어 메모리에 둔다).</summary>
    public sealed class AccountData
    {
        public long NextId = 1;
        public List<AccountRecord> Accounts = new List<AccountRecord>();
        public long NextListingId = 1;
        public List<MarketListing> Listings = new List<MarketListing>();
        /// <summary>서버 전체 상태(시즌·역대 후계자·신고·판 기록). Postgres는 world_state 표에 한 덩어리로 둔다.</summary>
        public SeasonState Season;
        public List<SeasonArchive> Hall = new List<SeasonArchive>();
        public List<ReportEntry> Reports = new List<ReportEntry>();
        public long NextReportId = 1;
        public List<RunRecord> Runs = new List<RunRecord>();
        public long NextRunId = 1;
    }

    /// <summary>AccountData 가운데 서버 전체 상태만(저장용 묶음).</summary>
    public sealed class WorldState
    {
        public SeasonState Season;
        public List<SeasonArchive> Hall;
        public List<ReportEntry> Reports;
        public long NextReportId = 1;
        public List<RunRecord> Runs;
        public long NextRunId = 1;

        public static WorldState Of(AccountData d) => new WorldState { Season = d.Season, Hall = d.Hall, Reports = d.Reports, NextReportId = d.NextReportId, Runs = d.Runs, NextRunId = d.NextRunId };

        public void Into(AccountData d)
        {
            d.Season = Season;
            d.Hall = Hall ?? new List<SeasonArchive>();
            d.Reports = Reports ?? new List<ReportEntry>();
            d.NextReportId = Math.Max(1, NextReportId);
            d.Runs = Runs ?? new List<RunRecord>();
            d.NextRunId = Math.Max(1, NextRunId);
        }
    }

    /// <summary>강화 기록 한 줄(enhance_log 표).</summary>
    public sealed class EnhanceLogEntry
    {
        public long AccountId;
        public int Character;
        public int StageBefore, StageAfter;
        public string Outcome;
        public long AtTicks;
    }

    /// <summary>한 번의 Save()에서 쓸 바뀜.</summary>
    public sealed class AccountDbChanges
    {
        public long NextId, NextListingId;
        public readonly List<AccountRecord> Accounts = new List<AccountRecord>();
        public readonly List<MarketListing> AddedListings = new List<MarketListing>();
        public readonly List<long> RemovedListings = new List<long>();
        public readonly List<EnhanceLogEntry> EnhanceLog = new List<EnhanceLogEntry>();
        /// <summary>서버 전체 상태(시즌·신고·판 기록)가 바뀌었다.</summary>
        public bool WorldDirty;
        public bool IsEmpty => Accounts.Count == 0 && AddedListings.Count == 0 && RemovedListings.Count == 0 && EnhanceLog.Count == 0 && !WorldDirty;
    }

    /// <summary>계정 저장소. Commit은 바뀜 전부를 한꺼번에 쓰거나(성공) 하나도 쓰지 않는다(예외).</summary>
    public interface IAccountDb : IDisposable
    {
        AccountData Load();
        void Commit(AccountData all, AccountDbChanges changes);
    }

    /// <summary>
    /// JSON 파일 하나(개발·테스트용, 예전 서버 저장 방식). Commit마다 전체를 임시 파일에 쓰고 바꿔치기한다. path = null이면 메모리에만.
    /// 강화 기록은 남기지 않는다(Postgres만).
    /// </summary>
    public sealed class JsonAccountDb : IAccountDb
    {
        readonly string _path;

        public JsonAccountDb(string path) { _path = path; }

        public AccountData Load()
        {
            if (_path == null || !File.Exists(_path)) return new AccountData();
            try { return JsonConvert.DeserializeObject<AccountData>(File.ReadAllText(_path, Encoding.UTF8)) ?? new AccountData(); }
            catch (Exception) { return new AccountData(); }
        }

        public void Commit(AccountData all, AccountDbChanges changes)
        {
            if (_path == null) return;
            var dir = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            string tmp = _path + ".tmp";
            File.WriteAllText(tmp, JsonConvert.SerializeObject(all, Formatting.Indented), Encoding.UTF8);
            if (File.Exists(_path)) File.Replace(tmp, _path, null);
            else File.Move(tmp, _path);
        }

        public void Dispose() { }
    }
}
