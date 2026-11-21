using System;
using System.Collections.Generic;
using Wizard.Solo.Data;
using Wizard.Solo.Sim;
using Wizard.Solo.UI;

namespace Wizard.Solo.Screens.Battle
{
    /// <summary>BattleFlowModel이 정한 화면 쪽 할 일.</summary>
    public enum BattleFlowActionKind
    {
        /// <summary>LootPick 화면을 Push(PhaseChanged → LootPick).</summary>
        PushLootPick,
        /// <summary>LootPick 단계가 끝났다 → 열려 있는 LootPick을 PopTo(Battle).</summary>
        CloseLootPick,
        /// <summary>Camp 화면을 Push(CampMode.Camp). 모닥불 옆 J의 판정은 Run이 하고 화면은 CampSub만 본다(00 문서 5.5절).</summary>
        PushCamp,
        /// <summary>CampSub가 CampScreen이 아니게 됐거나 야영을 벗어났다 → 열려 있는 Camp·ArtTrie를 PopTo(Battle).</summary>
        CloseCamp,
        /// <summary>웨이브 5의 배수를 넘겼다 → ISettlementService.SaveCheckpoint(00 문서 D-23).</summary>
        SaveCheckpoint,
        /// <summary>판 중 해금한 비기 → ISettlementService.RecordArtUnlock(int 캐릭터, artId).</summary>
        RecordArtUnlock,
        /// <summary>판이 끝났다 → (HpZero면 사망 문구) 지연 뒤 Settlement로 Replace.</summary>
        BeginSettlement
    }

    public struct BattleFlowAction
    {
        public BattleFlowActionKind Kind;
        public int Wave;
        /// <summary>캐릭터 번호 1~5(RecordArtUnlock).</summary>
        public int Character;
        public string ArtId;
        public RunEndReason Reason;
        /// <summary>BeginSettlement: 정산 화면까지 기다릴 초(HpZero = camp.json downed_to_settlement_seconds, 그 밖 0).</summary>
        public float DelaySeconds;
    }

    /// <summary>
    /// 판 진행 화면(Battle)의 전이 규칙. 엔진 없는 순수 클래스. 본판 세계의 이벤트와 상태만 보고 화면 쪽 할 일을 정한다.
    /// - 연출 길이를 재지 않는다: WaveClearing의 길이(보통 60틱, 보스 2.0초, 복수 264틱)는 Run이 센다(D-58). 화면은 PhaseChanged만 본다.
    /// - 야영 화면은 Run.CampSub == CampScreen일 때만 연다(D-49). 화면은 CampOpen 명령을 넣지 않는다.
    /// - 사망(HpZero)이면 사망 문구를 downed_to_settlement_seconds(D-21) 보인 뒤 정산, 포기(Abandon)는 바로 정산(D-19).
    /// </summary>
    public sealed class BattleFlowModel
    {
        /// <summary>중간 지점 간격 기본값(00 문서 D-23 "웨이브 5의 배수"). DataSolo/G20.json checkpoint_wave_interval이 있으면 그것.</summary>
        public const int DefaultCheckpointInterval = 5;
        public const string ExtraName = "G20";

        public int CheckpointInterval = DefaultCheckpointInterval;
        /// <summary>체력 0 뒤 정산 화면까지(초). camp.json downed_to_settlement_seconds(D-21 솔로 임시 2.0).</summary>
        public float DownedToSettlementSeconds;

        public bool Ended { get; private set; }
        public RunEndReason EndReason { get; private set; }
        /// <summary>사망 문구(str.battle_hud.death_line)를 보이는 중인가.</summary>
        public bool DeathLineVisible { get; private set; }
        public bool SettlementPending { get; private set; }
        public float SettlementRemaining { get; private set; }

        /// <summary>콘텐츠에서 수치를 읽는다(없으면 그대로 둔다).</summary>
        public void Configure(IContentDb content)
        {
            if (content == null) return;
            var camp = content.Camp;
            if (camp != null) DownedToSettlementSeconds = camp.DownedToSettlementSeconds;
            Newtonsoft.Json.Linq.JObject extra = null;
            try { extra = content.SoloExtra(ExtraName); } catch (Exception) { extra = null; }
            if (extra != null)
            {
                var t = extra["checkpoint_wave_interval"];
                if (t != null && t.Type == Newtonsoft.Json.Linq.JTokenType.Integer)
                {
                    int v = (int)t;
                    if (v > 0) CheckpointInterval = v;
                }
            }
        }

        /// <summary>새 판(또는 다시 연 화면)마다 상태를 비운다. 수치 설정은 그대로.</summary>
        public void Reset()
        {
            Ended = false;
            EndReason = RunEndReason.None;
            DeathLineVisible = false;
            SettlementPending = false;
            SettlementRemaining = 0f;
        }

        /// <summary>웨이브 번호가 중간 지점(5의 배수)인가.</summary>
        public bool IsCheckpointWave(int wave) => wave > 0 && CheckpointInterval > 0 && wave % CheckpointInterval == 0;

        /// <summary>야영 화면이 열려 있어야 하는 상태인가(Camp·StartCamp이고 내 CampSub == CampScreen). 협동에서는 사람마다 따로 연다.</summary>
        public static bool CampScreenWanted(SimWorld w) => w != null && w.InCamp && w.Run.Rewards[w.Me.Slot].CampSub == CampSubState.CampScreen;

        static bool IsCampPhase(RunPhase p) => p == RunPhase.Camp || p == RunPhase.StartCamp;

        /// <summary>
        /// 본판 세계의 이벤트 하나. 보조 판의 이벤트는 부르는 쪽이 거른다(첫 인자가 MainWorld가 아니면 무시).
        /// isOpen = 화면 관리자의 IsOpen. 결정은 into에 더한다(부르는 쪽이 이벤트마다 바로 실행한다).
        /// </summary>
        public void OnEvent(SimWorld world, in SimEvent e, Func<ScreenId, bool> isOpen, List<BattleFlowAction> into)
        {
            if (world == null || into == null) return;
            if (isOpen == null) isOpen = _ => false;
            switch (e.Type)
            {
                case SimEventType.PhaseChanged:
                {
                    if (Ended) return;
                    var now = (RunPhase)e.A;
                    var prev = (RunPhase)e.B;
                    if (prev == RunPhase.LootPick && now != RunPhase.LootPick && isOpen(ScreenId.LootPick))
                        into.Add(new BattleFlowAction { Kind = BattleFlowActionKind.CloseLootPick });
                    if (now == RunPhase.LootPick && !isOpen(ScreenId.LootPick))
                        into.Add(new BattleFlowAction { Kind = BattleFlowActionKind.PushLootPick });
                    ApplyCamp(world, IsCampPhase(now), isOpen, into);
                    break;
                }
                case SimEventType.CampChanged:
                    if (Ended) return;
                    ApplyCamp(world, true, isOpen, into);
                    break;
                case SimEventType.WaveCleared:
                    if (IsCheckpointWave(e.A))
                        into.Add(new BattleFlowAction { Kind = BattleFlowActionKind.SaveCheckpoint, Wave = e.A });
                    break;
                case SimEventType.ArtUnlocked:
                {
                    if (string.IsNullOrEmpty(e.Id)) return;
                    if (e.Slot >= 0 && e.Slot != world.ViewSlot) return; // 협동: 동료의 해금은 내 기록이 아니다
                    string charId = null;
                    if (e.Slot >= 0 && e.Slot < world.Players.Length) charId = world.Players[e.Slot].CharId;
                    if (charId == null && world.Run.Config != null) charId = world.Run.Config.CharId;
                    int n = Ids.Char.NumberOf(charId);
                    if (n <= 0) return;
                    into.Add(new BattleFlowAction { Kind = BattleFlowActionKind.RecordArtUnlock, Character = n, ArtId = e.Id });
                    break;
                }
                case SimEventType.RunEnded:
                {
                    if (Ended) return;
                    Ended = true;
                    EndReason = (RunEndReason)e.A;
                    DeathLineVisible = EndReason == RunEndReason.HpZero;
                    SettlementPending = true;
                    SettlementRemaining = DeathLineVisible ? Math.Max(0f, DownedToSettlementSeconds) : 0f;
                    into.Add(new BattleFlowAction
                    {
                        Kind = BattleFlowActionKind.BeginSettlement, Reason = EndReason, DelaySeconds = SettlementRemaining
                    });
                    break;
                }
            }
        }

        /// <summary>
        /// 야영 화면 열고 닫기. mayOpen = 여는 이벤트(PhaseChanged가 Camp·StartCamp로, 또는 CampChanged)인가.
        /// 닫기는 CampSub가 CampScreen이 아니게 됐거나 야영을 벗어났을 때 언제나.
        /// </summary>
        void ApplyCamp(SimWorld world, bool mayOpen, Func<ScreenId, bool> isOpen, List<BattleFlowAction> into)
        {
            bool wanted = CampScreenWanted(world);
            if (wanted)
            {
                if (mayOpen && !isOpen(ScreenId.Camp))
                    into.Add(new BattleFlowAction { Kind = BattleFlowActionKind.PushCamp });
            }
            else if (isOpen(ScreenId.Camp) || isOpen(ScreenId.ArtTrie))
            {
                into.Add(new BattleFlowAction { Kind = BattleFlowActionKind.CloseCamp });
            }
        }

        /// <summary>정산까지의 시간을 흘린다. 정산 화면으로 넘어갈 때가 되면 한 번만 true.</summary>
        public bool Advance(float dt)
        {
            if (!SettlementPending) return false;
            if (dt > 0f) SettlementRemaining -= dt;
            if (SettlementRemaining > 0f) return false;
            SettlementRemaining = 0f;
            SettlementPending = false;
            return true;
        }

        /// <summary>
        /// 일시정지를 받아도 되는가(00 문서 4.1절, D-20): 이미 일시정지가 아니고, 본판이 있고, 판이 끝나지 않았고,
        /// 되비침·쓰러짐(활동 중인 플레이어의 InputLocked, 첫 만남 단계 PhantomFinale·PhantomDowned)이 아니다.
        /// </summary>
        public bool CanRequestPause(SimWorld main, bool hasRun, bool paused)
        {
            if (paused || !hasRun || main == null || Ended) return false;
            var phase = main.Run.Phase;
            if (phase == RunPhase.RunEnded || phase == RunPhase.NotStarted) return false;
            if (phase == RunPhase.PhantomFinale || phase == RunPhase.PhantomDowned) return false;
            for (int i = 0; i < main.Players.Length; i++)
            {
                var p = main.Players[i];
                if (p != null && p.Active && p.InputLocked) return false;
            }
            return true;
        }
    }
}
