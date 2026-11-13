using System.Collections.Generic;

namespace Wizard.ViewLogic.Presentation
{
    // CONTRACTS 13. 코어 밖에서 생기는 표시 사건의 문자열 열쇠 카탈로그. 열쇠는 추가만 한다
    public static class UiEventKeys
    {
        // 구역: "ui.*"(화면 조작, 강화 의식), "net.*"(연결, 대기열, 회선), "duel.*"(매치·점수·입력 버려짐·여유 띠),
        //       "plaza.*", "notice.*"(서버 전체 알림), "trade.*", "season.*", "run.*"(제출·검증 상태)
        // 아래 다섯은 spec 10 23절의 예. 나머지(spec 09 22, 10 23, 11 17, 12 17, 13 21절의 이름)는 M9-PRS-01 이 채운다.
        public const string DuelMatchFound = "duel.match_found";
        public const string DuelQueueEnter = "duel.queue_enter";
        public const string DuelRatingChanged = "duel.rating_changed";
        public const string DuelInputDropped = "duel.input_dropped";
        public const string DuelMarginBandChanged = "duel.margin_band_changed";

        // M9-PRS-01: 각 명세 연결점 절의 코어 밖 사건 이름을 구역.snake_case 열쇠로 옮긴 것(CONTRACTS 3.3·13절).
        // 명세 이름 → 열쇠의 규칙과 대조는 tests/Wizard.ViewLogic.Tests/Presentation/UiEvents/UiEventCatalogSpecTests.cs 가 명세 파일을 읽어서 한다.
        // 작업용 이름은 열쇠에 쓰지 않는다(CLAUDE.md 5절): spec 11 17.2의 흔들의자 눈 뜨기는 notice.master_eye_*.

        // duel.* — spec 10 23절(코어 밖), spec 09 22절, spec 12 17절
        public const string DuelAimLineUpdate = "duel.aim_line_update";
        public const string DuelAppliedStageLabel = "duel.applied_stage_label";
        public const string DuelForfeit = "duel.forfeit";
        public const string DuelHitConfirmed = "duel.hit_confirmed";
        public const string DuelHitPredicted = "duel.hit_predicted";
        public const string DuelHitRollback = "duel.hit_rollback";
        public const string DuelIntroShown = "duel.intro_shown";
        public const string DuelLeaguePromoted = "duel.league.promoted";
        public const string DuelLeagueTradeWarning = "duel.league.trade_warning";
        public const string DuelNetInputDropped = "duel.net.input_dropped";
        public const string DuelNetMarginLevel = "duel.net.margin_level";
        public const string DuelNetRecommendWired = "duel.net.recommend_wired";
        public const string DuelOpponentDisconnected = "duel.opponent_disconnected";
        public const string DuelOpponentProjectileImpact = "duel.opponent_projectile_impact";
        public const string DuelOpponentProjectileSpawn = "duel.opponent_projectile_spawn";
        public const string DuelOpponentReconnected = "duel.opponent_reconnected";
        public const string DuelPlacementProgress = "duel.placement_progress";
        public const string DuelQueueBanned = "duel.queue_banned";
        public const string DuelQueueLeave = "duel.queue_leave";
        public const string DuelRatingSoftReset = "duel.rating_soft_reset";
        public const string DuelRollbackStallBegin = "duel.rollback_stall_begin";
        public const string DuelRollbackStallEnd = "duel.rollback_stall_end";
        public const string DuelSpectateStart = "duel.spectate_start";
        public const string DuelSproutUpgradeHint = "duel.sprout_upgrade_hint";
        public const string DuelStateResync = "duel.state_resync";
        public const string DuelVoid = "duel.void";
        public const string DuelVoidedByServer = "duel.voided_by_server";
        public const string DuelWalkToArena = "duel.walk_to_arena";

        // net.* — spec 12 17절
        public const string NetAdmissionAdmitted = "net.admission.admitted";
        public const string NetAdmissionPositionChanged = "net.admission.position_changed";
        public const string NetAdmissionQueued = "net.admission.queued";
        public const string NetAuthAccountDeletionCancelled = "net.auth.account.deletion_cancelled";
        public const string NetAuthAccountDeletionRequested = "net.auth.account.deletion_requested";
        public const string NetAuthEmailChanged = "net.auth.email.changed";
        public const string NetAuthEmailCodeExpired = "net.auth.email.code_expired";
        public const string NetAuthEmailCodeSent = "net.auth.email.code_sent";
        public const string NetAuthEmailCodeWrong = "net.auth.email.code_wrong";
        public const string NetAuthEmailResendCooldownTick = "net.auth.email.resend_cooldown_tick";
        public const string NetAuthEmailUnverifiedBadge = "net.auth.email.unverified_badge";
        public const string NetAuthEmailVerified = "net.auth.email.verified";
        public const string NetAuthLoginFailed = "net.auth.login.failed";
        public const string NetAuthLoginStarted = "net.auth.login.started";
        public const string NetAuthLoginSucceeded = "net.auth.login.succeeded";
        public const string NetAuthLoginThrottled = "net.auth.login.throttled";
        public const string NetAuthNewDeviceTradeHold = "net.auth.new_device.trade_hold";
        public const string NetAuthPasswordChanged = "net.auth.password.changed";
        public const string NetAuthRecoveryMailOff = "net.auth.recovery.mail_off";
        public const string NetAuthRecoveryRequested = "net.auth.recovery.requested";
        public const string NetAuthRecoveryResetFailed = "net.auth.recovery.reset_failed";
        public const string NetAuthRecoveryResetSucceeded = "net.auth.recovery.reset_succeeded";
        public const string NetAuthSignupFieldInvalid = "net.auth.signup.field_invalid";
        public const string NetAuthSignupSucceeded = "net.auth.signup.succeeded";
        public const string NetClientVersionTooOld = "net.client.version_too_old";
        public const string NetOfflineSuggested = "net.offline.suggested";
        public const string NetSecurityRefreshReuseDetected = "net.security.refresh_reuse_detected";
        public const string NetServerConnected = "net.server.connected";
        public const string NetServerDisconnected = "net.server.disconnected";
        public const string NetServerMaintenance = "net.server.maintenance";
        public const string NetServerReconnectAttempt = "net.server.reconnect_attempt";
        public const string NetServerReconnected = "net.server.reconnected";
        public const string NetSessionKicked = "net.session.kicked";
        public const string NetStatusFileInvalidSignature = "net.status_file.invalid_signature";
        public const string NetStatusFileLoaded = "net.status_file.loaded";
        public const string NetStatusFileUnreachable = "net.status_file.unreachable";

        // notice.* — spec 08 16.4절, spec 11 17.2절, spec 12 17절
        public const string NoticeBandShow = "notice.band_show";
        public const string NoticeBonfireTint13 = "notice.bonfire_tint_13";
        public const string NoticeBundle = "notice.bundle";
        public const string NoticeCrowFlyby = "notice.crow_flyby";
        public const string NoticeEnhanceBundled = "notice.enhance.bundled";
        public const string NoticeEnhancePlus1012 = "notice.enhance.plus10_12";
        public const string NoticeEnhancePlus13 = "notice.enhance.plus13";
        public const string NoticeMaintenanceShown = "notice.maintenance.shown";
        public const string NoticeMasterEyeClose = "notice.master_eye_close";
        public const string NoticeMasterEyeOpen = "notice.master_eye_open";
        public const string NoticeOperatorShown = "notice.operator.shown";
        public const string NoticeSkyBand13 = "notice.sky_band_13";
        public const string NoticeSystemBroadcastEnhance10plus = "notice.system.broadcast.enhance_10plus";
        public const string NoticeSystemBroadcastEnhance13 = "notice.system.broadcast.enhance_13";
        public const string NoticeSystemBroadcastNotice = "notice.system.broadcast.notice";

        // plaza.* — spec 11 17절, spec 04 18.2절
        public const string PlazaBlockAdded = "plaza.block_added";
        public const string PlazaChannelSwitch = "plaza.channel_switch";
        public const string PlazaChatBubbleFade = "plaza.chat_bubble_fade";
        public const string PlazaChatBubbleShow = "plaza.chat_bubble_show";
        public const string PlazaChatMessageReceived = "plaza.chat_message_received";
        public const string PlazaChatSendRejected = "plaza.chat_send_rejected";
        public const string PlazaChatWordMasked = "plaza.chat_word_masked";
        public const string PlazaDuelWalk = "plaza.duel_walk";
        public const string PlazaEmotePlay = "plaza.emote_play";
        public const string PlazaEmoteRaiseWand = "plaza.emote_raise_wand";
        public const string PlazaEmoteWheelClose = "plaza.emote_wheel_close";
        public const string PlazaEmoteWheelOpen = "plaza.emote_wheel_open";
        public const string PlazaEnter = "plaza.enter";
        public const string PlazaFacilityOpen = "plaza.facility_open";
        public const string PlazaFootstep = "plaza.footstep";
        public const string PlazaFriendAdded = "plaza.friend_added";
        public const string PlazaFriendOffline = "plaza.friend_offline";
        public const string PlazaFriendOnline = "plaza.friend_online";
        public const string PlazaFriendRequestReceived = "plaza.friend_request_received";
        public const string PlazaInspectOpen = "plaza.inspect_open";
        public const string PlazaInspectWandRotate = "plaza.inspect_wand_rotate";
        public const string PlazaInteractPromptHide = "plaza.interact_prompt_hide";
        public const string PlazaInteractPromptShow = "plaza.interact_prompt_show";
        public const string PlazaLeave = "plaza.leave";
        public const string PlazaMailClaimAllDone = "plaza.mail_claim_all_done";
        public const string PlazaMailClaimed = "plaza.mail_claimed";
        public const string PlazaMailNew = "plaza.mail_new";
        public const string PlazaMailboxHasLetter = "plaza.mailbox_has_letter";
        public const string PlazaMemberDespawn = "plaza.member_despawn";
        public const string PlazaMemberSpawn = "plaza.member_spawn";
        public const string PlazaMenuEllipsisOff = "plaza.menu_ellipsis_off";
        public const string PlazaMenuEllipsisOn = "plaza.menu_ellipsis_on";
        public const string PlazaPracticeHit = "plaza.practice_hit";
        public const string PlazaReportSent = "plaza.report_sent";
        public const string PlazaRequestNoteExpired = "plaza.request_note_expired";
        public const string PlazaRequestNoteReceived = "plaza.request_note_received";
        public const string PlazaShopPurchaseOk = "plaza.shop_purchase_ok";
        public const string PlazaSit = "plaza.sit";
        public const string PlazaTimePhaseChanged = "plaza.time_phase_changed";
        public const string PlazaWandIdleFx = "plaza.wand_idle_fx";

        // run.* — spec 09 22절, spec 12 17절
        public const string RunAccountFlagged = "run.account_flagged";
        public const string RunCheckpointDeferred = "run.checkpoint_deferred";
        public const string RunCheckpointSaved = "run.checkpoint_saved";
        public const string RunDebtCleared = "run.debt_cleared";
        public const string RunDebtCreated = "run.debt_created";
        public const string RunPersonalBest = "run.personal_best";
        public const string RunPhantomFlagCleared = "run.phantom_flag_cleared";
        public const string RunPrimaryRejected = "run.primary_rejected";
        public const string RunReplayOpened = "run.replay_opened";
        public const string RunRevengeRewardGranted = "run.revenge_reward_granted";
        public const string RunRevengeRewardReverted = "run.revenge_reward_reverted";
        public const string RunRewardPendingBadge = "run.reward_pending_badge";
        public const string RunSettleBagAutofill = "run.settle_bag_autofill";
        public const string RunSettleMaterialsReveal = "run.settle_materials_reveal";
        public const string RunStartBlockedUnsettled = "run.start_blocked_unsettled";
        public const string RunStartIssued = "run.start_issued";
        public const string RunSubmitAck = "run.submit.ack";
        public const string RunSubmitLateUnranked = "run.submit.late_unranked";
        public const string RunSubmitQueuedOffline = "run.submit.queued_offline";
        public const string RunSubmitSent = "run.submit.sent";
        public const string RunTidyCompleted = "run.tidy_completed";
        public const string RunTidyConfirmPrompt = "run.tidy_confirm_prompt";
        public const string RunTidyNoCheckpoint = "run.tidy_no_checkpoint";
        public const string RunVerifyFailedReclaimed = "run.verify_failed_reclaimed";
        public const string RunVerifyPassed = "run.verify_passed";
        public const string RunWandTradeLockPending = "run.wand_trade_lock_pending";
        public const string RunWandTradeLockReleased = "run.wand_trade_lock_released";

        // season.* — spec 09 22절, spec 12 17절
        public const string SeasonCoinsConverted = "season.coins_converted";
        public const string SeasonDeadlineSoon = "season.deadline_soon";
        public const string SeasonDiscipleAwarded = "season.disciple_awarded";
        public const string SeasonEndExtended = "season.end_extended";
        public const string SeasonExtended = "season.extended";
        public const string SeasonHeirCrowned = "season.heir_crowned";
        public const string SeasonHeirEstimateUpdated = "season.heir_estimate_updated";
        public const string SeasonLeaderboardRankChanged = "season.leaderboard_rank_changed";
        public const string SeasonLeaderboardTop3 = "season.leaderboard_top3";
        public const string SeasonLeagueTierReward = "season.league_tier_reward";
        public const string SeasonResultReady = "season.result_ready";
        public const string SeasonRollover = "season.rollover";
        public const string SeasonSanctionRankRemoved = "season.sanction_rank_removed";
        public const string SeasonSettlingStarted = "season.settling_started";
        public const string SeasonWeeklyBagDisabled = "season.weekly_bag_disabled";
        public const string SeasonWeeklyChallengeLocked = "season.weekly_challenge_locked";
        public const string SeasonWeeklyChallengeUnlocked = "season.weekly_challenge_unlocked";
        public const string SeasonWeeklyRewardMail = "season.weekly_reward_mail";

        // trade.* — spec 11 17.4절, spec 12 17절
        public const string TradeCancel = "trade.cancel";
        public const string TradeCancelledByDisconnect = "trade.cancelled_by_disconnect";
        public const string TradeComplete = "trade.complete";
        public const string TradeConfirmReady = "trade.confirm_ready";
        public const string TradeItemPlaced = "trade.item_placed";
        public const string TradeItemRemoved = "trade.item_removed";
        public const string TradeLockOn = "trade.lock_on";
        public const string TradeLockReleasedAll = "trade.lock_released_all";
        public const string TradeLossWarningShow = "trade.loss_warning_show";
        public const string TradeMarketListingExtendedByDowntime = "trade.market.listing.extended_by_downtime";
        public const string TradeMarketCancelled = "trade.market_cancelled";
        public const string TradeMarketExpiredReturned = "trade.market_expired_returned";
        public const string TradeMarketListed = "trade.market_listed";
        public const string TradeMarketPurchaseFailed = "trade.market_purchase_failed";
        public const string TradeMarketPurchaseOk = "trade.market_purchase_ok";
        public const string TradeMarketSold = "trade.market_sold";
        public const string TradeOpen = "trade.open";
        public const string TradeScarNoticeShown = "trade.scar_notice_shown";
        public const string TradeSlotChangedBlink = "trade.slot_changed_blink";

        // ui.* — spec 13 21절, spec 08 16절, spec 04 18.3절, spec 06 17절·07 20절의 정산 화면, spec 12 17절
        public const string UiAdminAlertBackupFailed = "ui.admin.alert.backup_failed";
        public const string UiAdminAlertDiskLow = "ui.admin.alert.disk_low";
        public const string UiAdminAlertMailFailureRate = "ui.admin.alert.mail_failure_rate";
        public const string UiAdminAlertReconcileMismatch = "ui.admin.alert.reconcile_mismatch";
        public const string UiAdminAlertServerJitter = "ui.admin.alert.server_jitter";
        public const string UiAdminAlertVerifyQueueBacklog = "ui.admin.alert.verify_queue_backlog";
        public const string UiAuthCodeOk = "ui.auth.code_ok";
        public const string UiAuthCodeSent = "ui.auth.code_sent";
        public const string UiAuthCodeWrong = "ui.auth.code_wrong";
        public const string UiAuthLockoutTick = "ui.auth.lockout_tick";
        public const string UiAuthLoginFail = "ui.auth.login_fail";
        public const string UiAuthLoginOk = "ui.auth.login_ok";
        public const string UiAuthResendReady = "ui.auth.resend_ready";
        public const string UiAuthResetDone = "ui.auth.reset_done";
        public const string UiCampDepart = "ui.camp.depart";
        public const string UiCampDepartHold = "ui.camp.depart_hold";
        public const string UiCampFreeHeal = "ui.camp.free_heal";
        public const string UiCampHeal = "ui.camp.heal";
        public const string UiCampOpen = "ui.camp.open";
        public const string UiCampSlotInsert = "ui.camp.slot_insert";
        public const string UiCampSlotRemove = "ui.camp.slot_remove";
        public const string UiCampSlotSwap = "ui.camp.slot_swap";
        public const string UiCampStatHighlight = "ui.camp.stat_highlight";
        public const string UiCampTreeRebuild = "ui.camp.tree_rebuild";
        public const string UiChatBlocked = "ui.chat.blocked";
        public const string UiChatCooldown = "ui.chat.cooldown";
        public const string UiChatSend = "ui.chat.send";
        public const string UiCommonConfirmCheck = "ui.common.confirm_check";
        public const string UiCommonCursorBracketPulse = "ui.common.cursor_bracket_pulse";
        public const string UiCommonCursorMove = "ui.common.cursor_move";
        public const string UiCommonDisabledPress = "ui.common.disabled_press";
        public const string UiCommonHoverEnter = "ui.common.hover_enter";
        public const string UiCommonHoverExit = "ui.common.hover_exit";
        public const string UiCommonInputError = "ui.common.input_error";
        public const string UiCommonPress = "ui.common.press";
        public const string UiCommonRelease = "ui.common.release";
        public const string UiCommonScreenClose = "ui.common.screen_close";
        public const string UiCommonScreenOpen = "ui.common.screen_open";
        public const string UiCommonToggleOff = "ui.common.toggle_off";
        public const string UiCommonToggleOn = "ui.common.toggle_on";
        public const string UiCommonTooltipShow = "ui.common.tooltip_show";
        public const string UiCurrencyStarlightBlockedNegative = "ui.currency.starlight.blocked_negative";
        public const string UiDuelIntro = "ui.duel.intro";
        public const string UiDuelResult = "ui.duel.result";
        public const string UiDuelRoundBreak = "ui.duel.round_break";
        public const string UiDuelBoardMatchFound = "ui.duel_board.match_found";
        public const string UiDuelBoardQueueJoin = "ui.duel_board.queue_join";
        public const string UiDuelBoardQueueTick = "ui.duel_board.queue_tick";
        public const string UiDummyJustStar = "ui.dummy.just_star";
        public const string UiDummyLateFail = "ui.dummy.late_fail";
        public const string UiEnhanceAttemptChargeBegin = "ui.enhance.attempt.charge.begin";
        public const string UiEnhanceAttemptChargeEnd = "ui.enhance.attempt.charge.end";
        public const string UiEnhanceAttemptHold = "ui.enhance.attempt.hold";
        public const string UiEnhanceAttemptJudged = "ui.enhance.attempt.judged";
        public const string UiEnhanceAttemptPaid = "ui.enhance.attempt.paid";
        public const string UiEnhanceAttemptPressed = "ui.enhance.attempt.pressed";
        public const string UiEnhanceAttemptResultLate = "ui.enhance.attempt.result_late";
        public const string UiEnhanceAttemptResultLateTimeout = "ui.enhance.attempt.result_late_timeout";
        public const string UiEnhanceAttemptSacrificeOffered = "ui.enhance.attempt.sacrifice_offered";
        public const string UiEnhanceAuraConsumed = "ui.enhance.aura.consumed";
        public const string UiEnhanceAuraGained = "ui.enhance.aura.gained";
        public const string UiEnhanceAuraEmber = "ui.enhance.aura_ember";
        public const string UiEnhanceButtonDisabledShake = "ui.enhance.button.disabled_shake";
        public const string UiEnhanceCarveAttempted = "ui.enhance.carve.attempted";
        public const string UiEnhanceChargeBegin = "ui.enhance.charge_begin";
        public const string UiEnhanceChargeProgress = "ui.enhance.charge_progress";
        public const string UiEnhanceCharmSlotFolded = "ui.enhance.charm.slot_folded";
        public const string UiEnhanceCharmSlotted = "ui.enhance.charm.slotted";
        public const string UiEnhanceCharmUnslotted = "ui.enhance.charm.unslotted";
        public const string UiEnhanceCharmBurn = "ui.enhance.charm_burn";
        public const string UiEnhanceCharmCatch = "ui.enhance.charm_catch";
        public const string UiEnhanceCharmSet = "ui.enhance.charm_set";
        public const string UiEnhanceClose = "ui.enhance.close";
        public const string UiEnhanceContinueTallyChanged = "ui.enhance.continue.tally_changed";
        public const string UiEnhanceCostShake = "ui.enhance.cost_shake";
        public const string UiEnhanceDropRiskTag = "ui.enhance.drop_risk.tag";
        public const string UiEnhanceEngraveFail = "ui.enhance.engrave_fail";
        public const string UiEnhanceEngraveOk = "ui.enhance.engrave_ok";
        public const string UiEnhanceEngraveTry = "ui.enhance.engrave_try";
        public const string UiEnhanceFailLightOut = "ui.enhance.fail_light_out";
        public const string UiEnhanceFailStart = "ui.enhance.fail_start";
        public const string UiEnhanceGreatCrackSpread = "ui.enhance.great_crack_spread";
        public const string UiEnhanceGreatShatter = "ui.enhance.great_shatter";
        public const string UiEnhanceHold = "ui.enhance.hold";
        public const string UiEnhanceLateNotice = "ui.enhance.late_notice";
        public const string UiEnhanceLateWait = "ui.enhance.late_wait";
        public const string UiEnhanceMasterWakes = "ui.enhance.master_wakes";
        public const string UiEnhanceMilestone = "ui.enhance.milestone";
        public const string UiEnhanceMilestoneReached = "ui.enhance.milestone.reached";
        public const string UiEnhanceMutationAccepted = "ui.enhance.mutation.accepted";
        public const string UiEnhanceMutationDeclined = "ui.enhance.mutation.declined";
        public const string UiEnhanceMutationOffered = "ui.enhance.mutation.offered";
        public const string UiEnhanceMutationRemoved = "ui.enhance.mutation.removed";
        public const string UiEnhanceMutationAccept = "ui.enhance.mutation_accept";
        public const string UiEnhanceMutationCard = "ui.enhance.mutation_card";
        public const string UiEnhanceMutationReject = "ui.enhance.mutation_reject";
        public const string UiEnhanceMutationRemove = "ui.enhance.mutation_remove";
        public const string UiEnhanceMutationVine = "ui.enhance.mutation_vine";
        public const string UiEnhanceNoDrop = "ui.enhance.no_drop";
        public const string UiEnhanceOffer = "ui.enhance.offer";
        public const string UiEnhanceOpen = "ui.enhance.open";
        public const string UiEnhancePay = "ui.enhance.pay";
        public const string UiEnhancePlus13Reached = "ui.enhance.plus13.reached";
        public const string UiEnhancePlus13Silence = "ui.enhance.plus13_silence";
        public const string UiEnhancePlus13Slam = "ui.enhance.plus13_slam";
        public const string UiEnhancePlus13Theme = "ui.enhance.plus13_theme";
        public const string UiEnhancePortraitReact = "ui.enhance.portrait_react";
        public const string UiEnhancePress = "ui.enhance.press";
        public const string UiEnhanceProbChanged = "ui.enhance.prob.changed";
        public const string UiEnhanceProbRoll = "ui.enhance.prob_roll";
        public const string UiEnhanceQuickResult = "ui.enhance.quick.result";
        public const string UiEnhanceRelease = "ui.enhance.release";
        public const string UiEnhanceResultCardShown = "ui.enhance.result.card_shown";
        public const string UiEnhanceResultCharmBlocked = "ui.enhance.result.charm_blocked";
        public const string UiEnhanceResultFailDrop = "ui.enhance.result.fail_drop";
        public const string UiEnhanceResultFailNoDrop = "ui.enhance.result.fail_no_drop";
        public const string UiEnhanceResultFakeFailOpen = "ui.enhance.result.fake_fail_open";
        public const string UiEnhanceResultGreatSuccess = "ui.enhance.result.great_success";
        public const string UiEnhanceResultSlotAlreadyFull = "ui.enhance.result.slot_already_full";
        public const string UiEnhanceResultSlotExpanded = "ui.enhance.result.slot_expanded";
        public const string UiEnhanceResultSuccess = "ui.enhance.result.success";
        public const string UiEnhanceResultCard = "ui.enhance.result_card";
        public const string UiEnhanceResultRecovered = "ui.enhance.result_recovered";
        public const string UiEnhanceSacrificeAdded = "ui.enhance.sacrifice.added";
        public const string UiEnhanceSacrificeRemoved = "ui.enhance.sacrifice.removed";
        public const string UiEnhanceSacrificePut = "ui.enhance.sacrifice_put";
        public const string UiEnhanceSacrificeTake = "ui.enhance.sacrifice_take";
        public const string UiEnhanceScarAdded = "ui.enhance.scar.added";
        public const string UiEnhanceScarPieceRemoved = "ui.enhance.scar.piece_removed";
        public const string UiEnhanceScarVisibilityChanged = "ui.enhance.scar_visibility.changed";
        public const string UiEnhanceScreenClosed = "ui.enhance.screen.closed";
        public const string UiEnhanceScreenOpened = "ui.enhance.screen.opened";
        public const string UiEnhanceShortResult = "ui.enhance.short_result";
        public const string UiEnhanceSlotExtend = "ui.enhance.slot_extend";
        public const string UiEnhanceStageDown = "ui.enhance.stage_down";
        public const string UiEnhanceSuccess = "ui.enhance.success";
        public const string UiEnhanceTitleBand = "ui.enhance.title_band";
        public const string UiEnhanceTraceShed = "ui.enhance.trace_shed";
        public const string UiEnhanceTraceToggle = "ui.enhance.trace_toggle";
        public const string UiEnhanceWandSwitched = "ui.enhance.wand.switched";
        public const string UiEnhanceWandDrop = "ui.enhance.wand_drop";
        public const string UiEnhanceWandSwitch = "ui.enhance.wand_switch";
        public const string UiFirstRunChoice = "ui.first_run.choice";
        public const string UiFxFlashBorderHighlight = "ui.fx.flash.border_highlight";
        public const string UiFxFlashFull = "ui.fx.flash.full";
        public const string UiFxHitstop = "ui.fx.hitstop";
        public const string UiFxShakeAdd = "ui.fx.shake.add";
        public const string UiHudBossFinalRage = "ui.hud.boss_final_rage";
        public const string UiHudBossPhaseTick = "ui.hud.boss_phase_tick";
        public const string UiHudBossRageStack = "ui.hud.boss_rage_stack";
        public const string UiHudBossShow = "ui.hud.boss_show";
        public const string UiHudCircuitStep = "ui.hud.circuit_step";
        public const string UiHudDamageNumber = "ui.hud.damage_number";
        public const string UiHudDayWaveChange = "ui.hud.day_wave_change";
        public const string UiHudDuelCountdown = "ui.hud.duel.countdown";
        public const string UiHudDuelInputDropped = "ui.hud.duel.input_dropped";
        public const string UiHudDuelMarginLevel = "ui.hud.duel.margin_level";
        public const string UiHudDuelPhraseWheel = "ui.hud.duel.phrase_wheel";
        public const string UiHudDuelRoundDot = "ui.hud.duel.round_dot";
        public const string UiHudDuelThornsStart = "ui.hud.duel.thorns_start";
        public const string UiHudHpChange = "ui.hud.hp_change";
        public const string UiHudHpLastCell = "ui.hud.hp_last_cell";
        public const string UiHudNotice = "ui.hud.notice";
        public const string UiHudOverheatLock = "ui.hud.overheat_lock";
        public const string UiHudPhantomLine = "ui.hud.phantom_line";
        public const string UiHudPortraitHit = "ui.hud.portrait_hit";
        public const string UiHudRecentInput = "ui.hud.recent_input";
        public const string UiHudRevengeTag = "ui.hud.revenge_tag";
        public const string UiHudRevengeWavesLeft = "ui.hud.revenge_waves_left";
        public const string UiHudTutorialStep = "ui.hud.tutorial_step";
        public const string UiHudWaveProgress = "ui.hud.wave_progress";
        public const string UiLeaguePromoteConfirm = "ui.league.promote_confirm";
        public const string UiLoadingProgress = "ui.loading.progress";
        public const string UiLootCardFlip = "ui.loot.card_flip";
        public const string UiLootDiscard = "ui.loot.discard";
        public const string UiLootPick = "ui.loot.pick";
        public const string UiLootReroll = "ui.loot.reroll";
        public const string UiLootUnpick = "ui.loot.unpick";
        public const string UiMailClaim = "ui.mail.claim";
        public const string UiMailReceive = "ui.mail.receive";
        public const string UiMainMenuBgTimeChange = "ui.main_menu.bg_time_change";
        public const string UiMainMenuMailDot = "ui.main_menu.mail_dot";
        public const string UiMainMenuModeCardFocus = "ui.main_menu.mode_card_focus";
        public const string UiMarketBuy = "ui.market.buy";
        public const string UiMarketDelist = "ui.market.delist";
        public const string UiMarketList = "ui.market.list";
        public const string UiMasterChairIdleLine = "ui.master_chair.idle_line";
        public const string UiMonumentEngrave = "ui.monument.engrave";
        public const string UiNoticeBonfireTint = "ui.notice.bonfire_tint";
        public const string UiNoticeMenuLine = "ui.notice.menu_line";
        public const string UiNoticePlazaBand = "ui.notice.plaza_band";
        public const string UiNoticePlazaSkyBand = "ui.notice.plaza_sky_band";
        public const string UiNoticeSystemChat = "ui.notice.system_chat";
        public const string UiOfflineGroupBoxOpen = "ui.offline.group_box_open";
        public const string UiPlazaBubbleFade = "ui.plaza.bubble_fade";
        public const string UiPlazaBubbleShow = "ui.plaza.bubble_show";
        public const string UiPlazaEllipsisOff = "ui.plaza.ellipsis_off";
        public const string UiPlazaEllipsisOn = "ui.plaza.ellipsis_on";
        public const string UiPlazaEmote = "ui.plaza.emote";
        public const string UiPlazaInteractHint = "ui.plaza.interact_hint";
        public const string UiPlazaRequestIn = "ui.plaza.request_in";
        public const string UiPlazaRequestTimeout = "ui.plaza.request_timeout";
        public const string UiPrepCharacterConfirm = "ui.prep.character_confirm";
        public const string UiPrepDepart = "ui.prep.depart";
        public const string UiPrepDepartBlocked = "ui.prep.depart_blocked";
        public const string UiPrepHeldWandSet = "ui.prep.held_wand_set";
        public const string UiPrepWandSelect = "ui.prep.wand_select";
        public const string UiProfileSaveRestoredFromBackup = "ui.profile.save.restored_from_backup";
        public const string UiPurchaseCheckoutOpened = "ui.purchase.checkout.opened";
        public const string UiPurchaseOrderCancelled = "ui.purchase.order.cancelled";
        public const string UiPurchaseOrderCreated = "ui.purchase.order.created";
        public const string UiPurchaseOrderGranted = "ui.purchase.order.granted";
        public const string UiPurchaseOrderRefunded = "ui.purchase.order.refunded";
        public const string UiPurchaseRedeemFailed = "ui.purchase.redeem.failed";
        public const string UiPurchaseRedeemSucceeded = "ui.purchase.redeem.succeeded";
        public const string UiRevengeComplete = "ui.revenge.complete";
        public const string UiRevengeNoteDrop = "ui.revenge.note_drop";
        public const string UiRevengeNoteLift = "ui.revenge.note_lift";
        public const string UiRevengeSettlementRow = "ui.revenge.settlement_row";
        public const string UiRevengeTrackerComplete = "ui.revenge.tracker_complete";
        public const string UiRevengeTrackerUpdate = "ui.revenge.tracker_update";
        public const string UiSettlementBagFull = "ui.settlement.bag_full";
        public const string UiSettlementBagOpen = "ui.settlement.bag_open";
        public const string UiSettlementBagPut = "ui.settlement.bag_put";
        public const string UiSettlementBundleOpen = "ui.settlement.bundle_open";
        public const string UiSettlementNewBest = "ui.settlement.new_best";
        public const string UiSettlementPage = "ui.settlement.page";
        public const string UiSettlementRevengeReward = "ui.settlement.revenge_reward";
        public const string UiShopBuy = "ui.shop.buy";
        public const string UiShopPurchased = "ui.shop.purchased";
        public const string UiStorePaidTabVisible = "ui.store.paid_tab.visible";
        public const string UiTitleStart = "ui.title.start";
        public const string UiTitleStartPromptPulse = "ui.title.start_prompt_pulse";
        public const string UiTradeFinalReady = "ui.trade.final_ready";
        public const string UiTradeLock = "ui.trade.lock";
        public const string UiTradeLossWarning = "ui.trade.loss_warning";
        public const string UiTradeSlotChanged = "ui.trade.slot_changed";
        public const string UiTradeUnlockAll = "ui.trade.unlock_all";
        public const string UiTrieUnlock = "ui.trie.unlock";
        public const string UiTrieUnlockCandidate = "ui.trie.unlock_candidate";
        public const string UiWandEnhanceChargeBegin = "ui.wand.enhance_charge_begin";
        public const string UiWandEnhanceHoldBreath = "ui.wand.enhance_hold_breath";
        public const string UiWandEnhanceResult = "ui.wand.enhance_result";
        public const string UiWandEnhanceSacrificeOffered = "ui.wand.enhance_sacrifice_offered";
        public const string UiWandEquippedChanged = "ui.wand.equipped_changed";
        public const string UiWandMilestoneScene = "ui.wand.milestone_scene";
        public const string UiWandMutationAccepted = "ui.wand.mutation_accepted";
        public const string UiWandMutationEvicted = "ui.wand.mutation_evicted";
        public const string UiWandMutationRemoved = "ui.wand.mutation_removed";
        public const string UiWandNewNicknameShown = "ui.wand.new_nickname_shown";
        public const string UiWandPurchased = "ui.wand.purchased";
        public const string UiWandReleased = "ui.wand.released";
        public const string UiWandScarAdded = "ui.wand.scar_added";
        public const string UiWandScarRemoved = "ui.wand.scar_removed";
        public const string UiWandServerBroadcast = "ui.wand.server_broadcast";
        public const string UiWandSlotExtended = "ui.wand.slot_extended";
        public const string UiWandStage13Achieved = "ui.wand.stage13_achieved";
        public const string UiWandStageDropPieces = "ui.wand.stage_drop_pieces";
        public const string UiWandStageTransition = "ui.wand.stage_transition";

        // 열쇠의 서수 오름차순
        public static IReadOnlyList<string> All { get; } = new[]
        {
            DuelAimLineUpdate,
            DuelAppliedStageLabel,
            DuelForfeit,
            DuelHitConfirmed,
            DuelHitPredicted,
            DuelHitRollback,
            DuelInputDropped,
            DuelIntroShown,
            DuelLeaguePromoted,
            DuelLeagueTradeWarning,
            DuelMarginBandChanged,
            DuelMatchFound,
            DuelNetInputDropped,
            DuelNetMarginLevel,
            DuelNetRecommendWired,
            DuelOpponentDisconnected,
            DuelOpponentProjectileImpact,
            DuelOpponentProjectileSpawn,
            DuelOpponentReconnected,
            DuelPlacementProgress,
            DuelQueueBanned,
            DuelQueueEnter,
            DuelQueueLeave,
            DuelRatingChanged,
            DuelRatingSoftReset,
            DuelRollbackStallBegin,
            DuelRollbackStallEnd,
            DuelSpectateStart,
            DuelSproutUpgradeHint,
            DuelStateResync,
            DuelVoid,
            DuelVoidedByServer,
            DuelWalkToArena,
            NetAdmissionAdmitted,
            NetAdmissionPositionChanged,
            NetAdmissionQueued,
            NetAuthAccountDeletionCancelled,
            NetAuthAccountDeletionRequested,
            NetAuthEmailChanged,
            NetAuthEmailCodeExpired,
            NetAuthEmailCodeSent,
            NetAuthEmailCodeWrong,
            NetAuthEmailResendCooldownTick,
            NetAuthEmailUnverifiedBadge,
            NetAuthEmailVerified,
            NetAuthLoginFailed,
            NetAuthLoginStarted,
            NetAuthLoginSucceeded,
            NetAuthLoginThrottled,
            NetAuthNewDeviceTradeHold,
            NetAuthPasswordChanged,
            NetAuthRecoveryMailOff,
            NetAuthRecoveryRequested,
            NetAuthRecoveryResetFailed,
            NetAuthRecoveryResetSucceeded,
            NetAuthSignupFieldInvalid,
            NetAuthSignupSucceeded,
            NetClientVersionTooOld,
            NetOfflineSuggested,
            NetSecurityRefreshReuseDetected,
            NetServerConnected,
            NetServerDisconnected,
            NetServerMaintenance,
            NetServerReconnectAttempt,
            NetServerReconnected,
            NetSessionKicked,
            NetStatusFileInvalidSignature,
            NetStatusFileLoaded,
            NetStatusFileUnreachable,
            NoticeBandShow,
            NoticeBonfireTint13,
            NoticeBundle,
            NoticeCrowFlyby,
            NoticeEnhanceBundled,
            NoticeEnhancePlus1012,
            NoticeEnhancePlus13,
            NoticeMaintenanceShown,
            NoticeMasterEyeClose,
            NoticeMasterEyeOpen,
            NoticeOperatorShown,
            NoticeSkyBand13,
            NoticeSystemBroadcastEnhance10plus,
            NoticeSystemBroadcastEnhance13,
            NoticeSystemBroadcastNotice,
            PlazaBlockAdded,
            PlazaChannelSwitch,
            PlazaChatBubbleFade,
            PlazaChatBubbleShow,
            PlazaChatMessageReceived,
            PlazaChatSendRejected,
            PlazaChatWordMasked,
            PlazaDuelWalk,
            PlazaEmotePlay,
            PlazaEmoteRaiseWand,
            PlazaEmoteWheelClose,
            PlazaEmoteWheelOpen,
            PlazaEnter,
            PlazaFacilityOpen,
            PlazaFootstep,
            PlazaFriendAdded,
            PlazaFriendOffline,
            PlazaFriendOnline,
            PlazaFriendRequestReceived,
            PlazaInspectOpen,
            PlazaInspectWandRotate,
            PlazaInteractPromptHide,
            PlazaInteractPromptShow,
            PlazaLeave,
            PlazaMailClaimAllDone,
            PlazaMailClaimed,
            PlazaMailNew,
            PlazaMailboxHasLetter,
            PlazaMemberDespawn,
            PlazaMemberSpawn,
            PlazaMenuEllipsisOff,
            PlazaMenuEllipsisOn,
            PlazaPracticeHit,
            PlazaReportSent,
            PlazaRequestNoteExpired,
            PlazaRequestNoteReceived,
            PlazaShopPurchaseOk,
            PlazaSit,
            PlazaTimePhaseChanged,
            PlazaWandIdleFx,
            RunAccountFlagged,
            RunCheckpointDeferred,
            RunCheckpointSaved,
            RunDebtCleared,
            RunDebtCreated,
            RunPersonalBest,
            RunPhantomFlagCleared,
            RunPrimaryRejected,
            RunReplayOpened,
            RunRevengeRewardGranted,
            RunRevengeRewardReverted,
            RunRewardPendingBadge,
            RunSettleBagAutofill,
            RunSettleMaterialsReveal,
            RunStartBlockedUnsettled,
            RunStartIssued,
            RunSubmitAck,
            RunSubmitLateUnranked,
            RunSubmitQueuedOffline,
            RunSubmitSent,
            RunTidyCompleted,
            RunTidyConfirmPrompt,
            RunTidyNoCheckpoint,
            RunVerifyFailedReclaimed,
            RunVerifyPassed,
            RunWandTradeLockPending,
            RunWandTradeLockReleased,
            SeasonCoinsConverted,
            SeasonDeadlineSoon,
            SeasonDiscipleAwarded,
            SeasonEndExtended,
            SeasonExtended,
            SeasonHeirCrowned,
            SeasonHeirEstimateUpdated,
            SeasonLeaderboardRankChanged,
            SeasonLeaderboardTop3,
            SeasonLeagueTierReward,
            SeasonResultReady,
            SeasonRollover,
            SeasonSanctionRankRemoved,
            SeasonSettlingStarted,
            SeasonWeeklyBagDisabled,
            SeasonWeeklyChallengeLocked,
            SeasonWeeklyChallengeUnlocked,
            SeasonWeeklyRewardMail,
            TradeCancel,
            TradeCancelledByDisconnect,
            TradeComplete,
            TradeConfirmReady,
            TradeItemPlaced,
            TradeItemRemoved,
            TradeLockOn,
            TradeLockReleasedAll,
            TradeLossWarningShow,
            TradeMarketListingExtendedByDowntime,
            TradeMarketCancelled,
            TradeMarketExpiredReturned,
            TradeMarketListed,
            TradeMarketPurchaseFailed,
            TradeMarketPurchaseOk,
            TradeMarketSold,
            TradeOpen,
            TradeScarNoticeShown,
            TradeSlotChangedBlink,
            UiAdminAlertBackupFailed,
            UiAdminAlertDiskLow,
            UiAdminAlertMailFailureRate,
            UiAdminAlertReconcileMismatch,
            UiAdminAlertServerJitter,
            UiAdminAlertVerifyQueueBacklog,
            UiAuthCodeOk,
            UiAuthCodeSent,
            UiAuthCodeWrong,
            UiAuthLockoutTick,
            UiAuthLoginFail,
            UiAuthLoginOk,
            UiAuthResendReady,
            UiAuthResetDone,
            UiCampDepart,
            UiCampDepartHold,
            UiCampFreeHeal,
            UiCampHeal,
            UiCampOpen,
            UiCampSlotInsert,
            UiCampSlotRemove,
            UiCampSlotSwap,
            UiCampStatHighlight,
            UiCampTreeRebuild,
            UiChatBlocked,
            UiChatCooldown,
            UiChatSend,
            UiCommonConfirmCheck,
            UiCommonCursorBracketPulse,
            UiCommonCursorMove,
            UiCommonDisabledPress,
            UiCommonHoverEnter,
            UiCommonHoverExit,
            UiCommonInputError,
            UiCommonPress,
            UiCommonRelease,
            UiCommonScreenClose,
            UiCommonScreenOpen,
            UiCommonToggleOff,
            UiCommonToggleOn,
            UiCommonTooltipShow,
            UiCurrencyStarlightBlockedNegative,
            UiDuelIntro,
            UiDuelResult,
            UiDuelRoundBreak,
            UiDuelBoardMatchFound,
            UiDuelBoardQueueJoin,
            UiDuelBoardQueueTick,
            UiDummyJustStar,
            UiDummyLateFail,
            UiEnhanceAttemptChargeBegin,
            UiEnhanceAttemptChargeEnd,
            UiEnhanceAttemptHold,
            UiEnhanceAttemptJudged,
            UiEnhanceAttemptPaid,
            UiEnhanceAttemptPressed,
            UiEnhanceAttemptResultLate,
            UiEnhanceAttemptResultLateTimeout,
            UiEnhanceAttemptSacrificeOffered,
            UiEnhanceAuraConsumed,
            UiEnhanceAuraGained,
            UiEnhanceAuraEmber,
            UiEnhanceButtonDisabledShake,
            UiEnhanceCarveAttempted,
            UiEnhanceChargeBegin,
            UiEnhanceChargeProgress,
            UiEnhanceCharmSlotFolded,
            UiEnhanceCharmSlotted,
            UiEnhanceCharmUnslotted,
            UiEnhanceCharmBurn,
            UiEnhanceCharmCatch,
            UiEnhanceCharmSet,
            UiEnhanceClose,
            UiEnhanceContinueTallyChanged,
            UiEnhanceCostShake,
            UiEnhanceDropRiskTag,
            UiEnhanceEngraveFail,
            UiEnhanceEngraveOk,
            UiEnhanceEngraveTry,
            UiEnhanceFailLightOut,
            UiEnhanceFailStart,
            UiEnhanceGreatCrackSpread,
            UiEnhanceGreatShatter,
            UiEnhanceHold,
            UiEnhanceLateNotice,
            UiEnhanceLateWait,
            UiEnhanceMasterWakes,
            UiEnhanceMilestone,
            UiEnhanceMilestoneReached,
            UiEnhanceMutationAccepted,
            UiEnhanceMutationDeclined,
            UiEnhanceMutationOffered,
            UiEnhanceMutationRemoved,
            UiEnhanceMutationAccept,
            UiEnhanceMutationCard,
            UiEnhanceMutationReject,
            UiEnhanceMutationRemove,
            UiEnhanceMutationVine,
            UiEnhanceNoDrop,
            UiEnhanceOffer,
            UiEnhanceOpen,
            UiEnhancePay,
            UiEnhancePlus13Reached,
            UiEnhancePlus13Silence,
            UiEnhancePlus13Slam,
            UiEnhancePlus13Theme,
            UiEnhancePortraitReact,
            UiEnhancePress,
            UiEnhanceProbChanged,
            UiEnhanceProbRoll,
            UiEnhanceQuickResult,
            UiEnhanceRelease,
            UiEnhanceResultCardShown,
            UiEnhanceResultCharmBlocked,
            UiEnhanceResultFailDrop,
            UiEnhanceResultFailNoDrop,
            UiEnhanceResultFakeFailOpen,
            UiEnhanceResultGreatSuccess,
            UiEnhanceResultSlotAlreadyFull,
            UiEnhanceResultSlotExpanded,
            UiEnhanceResultSuccess,
            UiEnhanceResultCard,
            UiEnhanceResultRecovered,
            UiEnhanceSacrificeAdded,
            UiEnhanceSacrificeRemoved,
            UiEnhanceSacrificePut,
            UiEnhanceSacrificeTake,
            UiEnhanceScarAdded,
            UiEnhanceScarPieceRemoved,
            UiEnhanceScarVisibilityChanged,
            UiEnhanceScreenClosed,
            UiEnhanceScreenOpened,
            UiEnhanceShortResult,
            UiEnhanceSlotExtend,
            UiEnhanceStageDown,
            UiEnhanceSuccess,
            UiEnhanceTitleBand,
            UiEnhanceTraceShed,
            UiEnhanceTraceToggle,
            UiEnhanceWandSwitched,
            UiEnhanceWandDrop,
            UiEnhanceWandSwitch,
            UiFirstRunChoice,
            UiFxFlashBorderHighlight,
            UiFxFlashFull,
            UiFxHitstop,
            UiFxShakeAdd,
            UiHudBossFinalRage,
            UiHudBossPhaseTick,
            UiHudBossRageStack,
            UiHudBossShow,
            UiHudCircuitStep,
            UiHudDamageNumber,
            UiHudDayWaveChange,
            UiHudDuelCountdown,
            UiHudDuelInputDropped,
            UiHudDuelMarginLevel,
            UiHudDuelPhraseWheel,
            UiHudDuelRoundDot,
            UiHudDuelThornsStart,
            UiHudHpChange,
            UiHudHpLastCell,
            UiHudNotice,
            UiHudOverheatLock,
            UiHudPhantomLine,
            UiHudPortraitHit,
            UiHudRecentInput,
            UiHudRevengeTag,
            UiHudRevengeWavesLeft,
            UiHudTutorialStep,
            UiHudWaveProgress,
            UiLeaguePromoteConfirm,
            UiLoadingProgress,
            UiLootCardFlip,
            UiLootDiscard,
            UiLootPick,
            UiLootReroll,
            UiLootUnpick,
            UiMailClaim,
            UiMailReceive,
            UiMainMenuBgTimeChange,
            UiMainMenuMailDot,
            UiMainMenuModeCardFocus,
            UiMarketBuy,
            UiMarketDelist,
            UiMarketList,
            UiMasterChairIdleLine,
            UiMonumentEngrave,
            UiNoticeBonfireTint,
            UiNoticeMenuLine,
            UiNoticePlazaBand,
            UiNoticePlazaSkyBand,
            UiNoticeSystemChat,
            UiOfflineGroupBoxOpen,
            UiPlazaBubbleFade,
            UiPlazaBubbleShow,
            UiPlazaEllipsisOff,
            UiPlazaEllipsisOn,
            UiPlazaEmote,
            UiPlazaInteractHint,
            UiPlazaRequestIn,
            UiPlazaRequestTimeout,
            UiPrepCharacterConfirm,
            UiPrepDepart,
            UiPrepDepartBlocked,
            UiPrepHeldWandSet,
            UiPrepWandSelect,
            UiProfileSaveRestoredFromBackup,
            UiPurchaseCheckoutOpened,
            UiPurchaseOrderCancelled,
            UiPurchaseOrderCreated,
            UiPurchaseOrderGranted,
            UiPurchaseOrderRefunded,
            UiPurchaseRedeemFailed,
            UiPurchaseRedeemSucceeded,
            UiRevengeComplete,
            UiRevengeNoteDrop,
            UiRevengeNoteLift,
            UiRevengeSettlementRow,
            UiRevengeTrackerComplete,
            UiRevengeTrackerUpdate,
            UiSettlementBagFull,
            UiSettlementBagOpen,
            UiSettlementBagPut,
            UiSettlementBundleOpen,
            UiSettlementNewBest,
            UiSettlementPage,
            UiSettlementRevengeReward,
            UiShopBuy,
            UiShopPurchased,
            UiStorePaidTabVisible,
            UiTitleStart,
            UiTitleStartPromptPulse,
            UiTradeFinalReady,
            UiTradeLock,
            UiTradeLossWarning,
            UiTradeSlotChanged,
            UiTradeUnlockAll,
            UiTrieUnlock,
            UiTrieUnlockCandidate,
            UiWandEnhanceChargeBegin,
            UiWandEnhanceHoldBreath,
            UiWandEnhanceResult,
            UiWandEnhanceSacrificeOffered,
            UiWandEquippedChanged,
            UiWandMilestoneScene,
            UiWandMutationAccepted,
            UiWandMutationEvicted,
            UiWandMutationRemoved,
            UiWandNewNicknameShown,
            UiWandPurchased,
            UiWandReleased,
            UiWandScarAdded,
            UiWandScarRemoved,
            UiWandServerBroadcast,
            UiWandSlotExtended,
            UiWandStage13Achieved,
            UiWandStageDropPieces,
            UiWandStageTransition,
        };
    }
}
