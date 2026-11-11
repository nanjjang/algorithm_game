using System;
using Wizard.Content;

namespace Wizard.Client
{
    // CONTRACTS 6.1, T-26. 화면과 게임 로직은 이것만 부른다(ARCHITECTURE P7)
    public interface IBackend : IDisposable
    {
        ProfileKind Kind { get; }
        BackendFeatures Features { get; }
        IProfileService Profile { get; }
        IWandService Wands { get; }
        IEnhanceService Enhance { get; }
        IShopService Shop { get; }
        IRunService Runs { get; }
        IRecordService Records { get; }
        IStoreService Store { get; }
        ISettingsService Settings { get; }
    }
}
