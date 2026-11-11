# 11. 광장 · 사회 기능 · 거래 — 구현 명세서

게임: 엄청엄청 위대한 마법사 키우기. 대상: 광장 인스턴스와 이동(10틱), 시설, 채팅 채널, 친구, 살펴보기, 직접 거래, 장터, 우편함, 사기·복제 방지.

## 0. 이 문서를 읽는 법

### 0.1 원본과 우선순위

| 원본 | 이 문서가 옮긴 범위 |
|---|---|
| `docs/DECISIONS.md` | 전부 읽음. 1.5, 1.9, 1.10, 1.14~1.16, 1.22, 1.25, 2.1, 2.4, 2.6, 2.7이 이 영역에 닿는다. **다른 문서와 다르면 이것이 맞다** |
| `docs/online.md` 5절(5.1~5.6) | 전부 |
| `docs/world-characters.md` 2.5절 | 시설 배치 규칙만 |
| `docs/ui-assets.md` 4.20~4.24절 | 동작 규칙만(픽셀 배치는 그림 단계로 넘기고, 동작에 닿는 수치만 옮긴다) |
| 교차 확인 | `online.md` 1.1, 1.3, 1.4, 1.6, 2.4, 2.5, 3.2, 4.6~4.10, 7.3, 7.5, 7.7~7.10, 8.1, 9.1, 10, 11, 14절 / `effects.md` 1.3, 8, 9.3, 18, 19절 / `ui-assets.md` 4.7절 / `GDD.md` 3절, 개발자 확인 46번 / `world-characters.md` 5.1, 10.3절 / `docs/sim/enh.py` |

### 0.2 표시

- 출처 표기: `(online 5.5)`는 `docs/online.md` 5.5절, `(wc 2.5)`는 `world-characters.md`, `(ui 4.23)`은 `ui-assets.md`, `(fx 1.3)`은 `effects.md`, `(DEC 2.7)`은 `DECISIONS.md`.
- **[확정]**: DECISIONS의 [개발자] 항목이나 원본의 [개발자] 표시.
- **[조율]**: DECISIONS 2절(공통 규격)이나 원본의 [조율] 표시.
- **[제안]**: 원본의 설계안. 원본은 "표시가 없는 수치는 모두 [제안]이고 QA에서 조정하는 시작값"이라고 정한다(online 머리말). 이 문서도 그대로 따른다.
- **[임시]**: 원본이 정하지 않아 이 명세서가 넣은 기본값. 구현을 막지 않으려고 넣었고 19절(미정)에 모아 두었다. 개발자가 바꾸면 그 값만 고친다.
- 숫자는 원문 그대로(단위 포함) 적고, 칸·틱으로 바꿀 수 있는 것은 `⇒` 뒤에 환산값을 붙인다.

### 0.3 단위와 환산 규칙

| 단위 | 정의 | 출처 |
|---|---|---|
| 칸 | 1칸 = 64px. 게임 수치(거리, 반경, 속도)는 칸 단위 그대로 | (DEC 2.1) [조율] |
| 코어 틱 | 고정 60틱(1틱 = 16.667ms). 실수 대신 Q32.32 고정소수점 | (online 3.1) [제안] |
| 광장 틱 | 서버 권위 10틱(1광장 틱 = 0.1초 = 코어 6틱) | (online 5.1) [제안] |
| Q32.32 | 정수 `long`. 실수 x의 원시값 = `x × 2^32`(버림). 1.0 = 4,294,967,296 | (online 3.1) |
| 서버 시각 | 모든 시간 판정은 서버 시각. 클라이언트 시계는 믿지 않는다 | (online 7.8) [제안] |

- 광장은 결정론 재실행 대상이 아니지만(무한 웨이브·결투와 달리 다시 돌려 검증하지 않는다), 서버와 클라이언트 예측이 같은 값을 내도록 **광장 이동도 Q32.32로 계산한다** [임시]. 이렇게 하면 `Wizard.Sim`의 고정소수점 수학 함수를 그대로 쓴다.
- 시간 상수는 `config`에 **밀리초 정수**로, 비율은 **만분율(basis point, bp) 정수**로 둔다(5% = 500bp, 1% = 100bp, 0.3배 = 3,000bp, 5배 = 50,000bp) [임시]. 수치는 코드가 아니라 `config`와 버전이 붙은 데이터 파일에 둔다(online 7.9) [제안].

### 0.4 이 영역의 범위 밖(다른 명세서 몫)

강화 규칙과 강화 화면, 결투 규칙·리그·IL 계산식 본체, 무한 웨이브 정산·검증, 시즌 점수, 계정 인증, 결제. 이 문서는 그것들을 **부르는 쪽의 인터페이스**만 적는다(예: `IlService.RecalcWand`, `RunVerify.IsPending`).

---

## 1. 상수 표 (config 키)

모든 값은 `config` 표(`key, value, updated_at`)에 둔다(online 7.9). 아래 "기본값"은 원문 값이다.

### 1.1 광장

| 키 | 기본값 | 환산 | 출처·표시 |
|---|---|---|---|
| `plaza.instance_capacity` | 40명 | | (online 5.1) [제안] |
| `plaza.instance_open_threshold` | 35명(이 수를 **넘으면** 새 인스턴스) | 36명째부터 | (online 5.1) [제안] |
| `plaza.width_tiles` / `plaza.height_tiles` | 40 / 24칸 | 2,560×1,536px | (online 5.1, wc 2.5) [제안] |
| `plaza.move_speed` | 5.0 (칸/초) | 광장 틱당 0.5칸, 코어 틱당 0.083333칸(Q32.32 원시 357,913,941). 대각선 성분 3.5355339칸/초 = 광장 틱당 0.35355339칸(원시 1,518,500,249) | (online 5.1) [제안] |
| `plaza.tick_hz` | 10 | 100ms | (online 5.1) [제안] |
| `plaza.remote_interp_ms` | 100 (남은 0.1초 보간) | 1광장 틱 | (online 5.1) [제안] |
| `plaza.day_cycle_ms` | 24분 = 1,440,000 | 14,400 광장 틱, 86,400 코어 틱 | (online 5.1, wc 2.5) [제안] |
| `plaza.phase_day_ms` | 12분 = 720,000 | 7,200 광장 틱 | 같음 |
| `plaza.phase_dusk_ms` | 4분 = 240,000 | 2,400 광장 틱 | 같음 |
| `plaza.phase_night_ms` | 8분 = 480,000 | 4,800 광장 틱 | 같음 |
| `plaza.emote_base_count` | 8종 | | (online 5.1) [제안] |
| `plaza.interact_radius` | 2.0칸 | | [임시] |
| `plaza.body_radius` | 0.30칸 | | [임시] |
| `plaza.empty_instance_close_ms` | 300,000 (5분) | | [임시] |
| `plaza.trade_distance` | 5칸 | 거리 제곱 25 | (online 5.5) [제안] |
| `plaza.challenge_accept_ms` | 20초 = 20,000 | 200 광장 틱 | (online 4.6) [제안] |
| `plaza.request_note_ms` | 20,000 (거래 신청 수락 대기) | | [임시](도전의 20초에 맞춤) |
| `plaza.bubble_line_chars` / `plaza.bubble_max_lines` | 20자 / 4줄 | | (ui 4.20) [제안] |
| `plaza.bubble_fade_ms` | 4초 = 4,000 | | (ui 4.20) [제안] |
| `plaza.other_sound_minus12db_tiles` / `plaza.other_sound_cutoff_tiles` | 8칸 / 16칸 | | (fx 19) [제안] |

### 1.2 채팅

| 키 | 기본값 | 환산 | 출처·표시 |
|---|---|---|---|
| `chat.nearby_radius` | 8칸 | 거리 제곱 64 | (online 5.3) [제안] |
| `chat.cooldown.nearby_ms` | 2초에 1회 = 2,000 | 20 광장 틱 | (online 5.3) [제안] |
| `chat.cooldown.plaza_ms` | 5초에 1회 = 5,000 | 50 광장 틱 | 같음 |
| `chat.cooldown.trade_ms` | 30초에 1회 = 30,000 | 300 광장 틱 | 같음 |
| `chat.cooldown.whisper_ms` / `friend_ms` / `duelroom_ms` | 1,000 | | [임시](원문 빈칸) |
| `chat.max_len` | 80자 | | (online 5.3) [제안] |
| `chat.repeat_block_at` | 3회째부터 막음 | | (online 5.3) [제안] |
| `chat.repeat_window_ms` | 600,000 (10분) | | [임시] |
| `chat.newbie_unlock_wave` | 무한 웨이브 10 | | (online 5.3) [제안] |
| `chat.report_excerpt_lines` | 30줄 | | (online 5.3) [제안] |
| `chat.report_auto_mute_accounts` | 서로 다른 계정 5개 이상 | | (online 5.3) [제안] |
| `chat.report_auto_mute_window_ms` | 24시간 = 86,400,000 | | 같음 |
| `chat.report_auto_mute_ms` | 1시간 = 3,600,000 | | 같음 |
| `chat.block_max` | 200명 | | (online 5.3) [제안] |
| `chat.duel_preset_count` | 8개 | | (online 5.3) [제안] |
| `chat.log_retention_days` | 90일 | | (online 7.9, 10) [제안] |
| `chat.report_excerpt_retention_days` | 처리 뒤 1년 | | (online 10) [제안] |
| `chat.whisper_default` | 친구만 | | (online 10) [제안] |
| `chat.sanction_ladder` | 경고 → 채팅 1일 → 7일 → 30일 → 영구 | | (online 10) [제안] |

### 1.3 친구·살펴보기

| 키 | 기본값 | 출처·표시 |
|---|---|---|
| `friend.max` | 100명 | (online 5.4) [제안] |
| `friend.recent_met_max` | 20명(최근 결투 상대와 거래 상대) | (online 5.4) [제안] |
| `friend.request_expire_ms` | 7일 | [임시] |
| `friend.pending_out_max` | 50건 | [임시] |

### 1.4 거래·장터·우편

| 키 | 기본값 | 환산 | 출처·표시 |
|---|---|---|---|
| `trade.unlock_wave` | 무한 웨이브 10 도달 | | (online 5.5, 4.9) [제안] |
| `trade.unlock_play_s` | 누적 플레이 2시간 = 7,200초 | | 같음 |
| `trade.slots_per_side` | 8칸(물건). 골드는 칸 밖 입력 | | (online 5.5, ui 4.23) [제안] |
| `trade.changed_blink_ms` | 3초 = 3,000 | | (online 5.5) [제안] |
| `trade.changed_blink_max_hz` | 초당 2회 이하 | | (ui 4.23) [제안] |
| `trade.final_confirm_delay_ms` | 5초 = 5,000 | 50 광장 틱 | (online 5.5) [제안] |
| `trade.session_idle_timeout_ms` | 300,000 (5분) | | [임시] |
| `trade.fee_bp` | 5% = 500 | | (online 5.5, 2.4) [제안] |
| `trade.wand_fee_bp` | 10% = 1,000 | | (online 5.5, 2.4) [제안] |
| `trade.gift_friend_days` | 7일 | | (online 5.5) [제안] |
| `trade.gold_give_daily_ratio_bp` | 계정 누적 획득 골드의 20% = 2,000 | | (online 5.5) [제안] |
| `trade.loss_warning_ratio` | 감정가 차이 5배 초과 | | (online 5.6) [제안] |
| `trade.new_device_block_ms` | 24시간 | | (online 7.3) [제안] |
| `market.duration_ms` | 48시간 = 172,800,000 | | (online 5.5) [제안] |
| `market.max_active_listings` | 계정당 동시 20건(모든 품목 합계) | | (online 5.5) [제안] |
| `market.deposit_bp` | 값의 1% = 100 | | (online 5.5) [제안] |
| `market.fee_bp` / `market.wand_fee_bp` | 판매가의 5% / 지팡이 10% | | (online 2.4) [제안] |
| `market.median_days` | 7일 | | (online 5.5) [제안] |
| `market.price_min_bp` / `market.price_max_bp` | 0.3배 = 3,000 / 5배 = 50,000 | | (online 5.5) [제안] |
| `market.price_range_min_history` | 30건 | | (online 5.5) [제안] |
| `market.wand_min_stage` | +1 | | (online 5.5, 1.6) [제안] |
| `wand.trade_lock_ms` | 7일 | | (online 5.5) [제안] |
| `wand.appraisal_plus0` | 250골드 | | (online 5.5) [제안] |
| `wand.appraisal_factor_bp` | × 0.5 = 5,000 | | (online 5.5) [제안] |
| `wand.release_refund_bp` | 감정가의 10% = 1,000 | | (online 1.6) [제안] |
| `wand.shop_price` | 500골드 | | (online 1.6, 2.4) [제안] |
| `il.drop_delay_days` | 14일 | | (online 4.7) [제안] |
| `mail.note_retention_days` | 30일 | | [임시] |
| `mail.attachment_retention` | 없음(만료 없음) | | [임시] |
| `request.retention_days` | 30일 | | (online 5.6) [제안] |
| `library.base_capacity` | 블록 100개 | | (online 2.4) [제안] |
| `library.expand_step` / `library.expand_price` | 50개 / 2,000골드 | | (online 2.4) [제안] |
| `shop.charm_price` / `shop.charm_weekly_limit` | 10,000골드 / 프로필당 주 3개 | | (online 2.4) [제안] |
| `shop.nickname_change_price` | 5,000골드 | | (online 2.4) [제안] |
| `shop.mutation_remove_price` | 변이 1개당 3,000골드 | | (online 2.4) 참고(강화 화면 몫) |

---

## 2. 광장 인스턴스와 이동

### 2.1 들어가는 조건

- 광장은 **서버 프로필에서만** 열린다(online 5 머리말, wc 2.5, DEC 2.6) [조율]. 오프라인 프로필에서는 메뉴에 묶어 두고 "서버에 접속하면 열립니다" 한 줄(online 8.1, wc 2.5) [조율]. 오프라인 프로필에는 혼자 걷는 빈 야영지도 없다(GDD 개발자 확인 46번 지금 값) [제안].
- 전투는 없다. 같은 서버의 사람들이 만나고, 거래하고, 결투를 신청하고, 친구를 맺고, 채팅한다(online 5, DEC 1.5) [확정].
- 서버에 연결하지 못했을 때 광장은 열리지 않는다. "까마귀가 아직 안 왔다"는 서버에 닿지 못했을 때의 안내 문구로만 쓴다(wc 2.5) [제안].
- 들어가는 순서: 메인 메뉴의 광장 카드 → 채널 고르기 → 불러오는 화면 → 광장(ui 4.7) [제안].

### 2.2 데이터 구조

```csharp
// 메모리 상태가 정본이다. DB의 plaza_instance는 "메모리 상태의 그림자"(online 7.9).
public sealed class PlazaInstance {
    public int InstanceId;            // 1부터. 화면의 "채널 번호"
    public long StartedAtMs;          // 서버 시각
    public int Population => Members.Count;  // 0..40
    public SortedDictionary<long, PlazaMember> Members; // key = account_id (반복 순서 고정)
    public long EmptySinceMs;         // 0 = 비어 있지 않음
    public long Tick;                 // 광장 틱 번호(인스턴스 안 단조 증가)
}

public sealed class PlazaMember {
    public long AccountId;
    public string Nickname;           // 2~8자(online 7.3)
    public CharacterId Character;     // 들고 있는 캐릭터(5종 중 하나)
    public long EquippedWandId;       // 그 캐릭터의 "든 지팡이"(online 1.6)
    public FixVec2 Pos;               // Q32.32, 칸 단위. 0 <= x <= 40, 0 <= y <= 24
    public Dir8 Facing;               // 8방향
    public Dir8OrNone MoveInput;      // 마지막으로 받은 입력(없음 포함)
    public uint LastInputSeq;         // 클라이언트가 붙인 입력 순번(마지막 처리분)
    public MemberActivity Activity;   // 아래 enum
    public EmoteId? Emote; public long EmoteUntilTick;
    public long ChatNextAllowedMs[ChatChannel]; // 채널별 다음 발언 가능 시각
}

public enum MemberActivity { Walking, Idle, InMenu, Trading, Emoting, Sitting, WatchingPractice, Practicing }
// InMenu: 강화 화면, 서고, 옷장, 우편함 등 메뉴를 보고 있다 → 머리 위 말줄임표. 강화 화면만의 표시는 없다(online 1.4, ui 4.20).

public enum Dir8 { N, NE, E, SE, S, SW, W, NW }  // 화면 y는 아래로 증가(돌마당 좌표 규칙과 같다, wc 2.6)
```

- 좌표계: 왼쪽 위 (0,0) ~ 오른쪽 아래 (40,24). 칸 (x, y)는 가로 x~x+1, 세로 y~y+1을 차지한다. 위치는 같은 축의 실수(Q32.32)로 적는다. 돌마당의 좌표 규칙(wc 2.6)과 같게 맞춘다 [임시].

### 2.3 인스턴스 배정 (알고리즘)

원문: 인스턴스(채널)당 40명. 35명이 넘으면 새 인스턴스를 연다. 자동 배정. 채널 목록에서 고를 수 있고, 친구가 있는 채널이 먼저 보인다(online 5.1) [제안]. 채널 고르기 판 맨 위에 [알아서 들어가기], 줄마다 채널 번호·인원 "23 / 40"·친구 수(ui 4.7) [제안].

해석 [임시]: 40은 **하드 상한**(누구도 넘지 못함), 35는 **자동 배정 상한**(자동 배정은 35명 이하인 곳에만 넣는다). 직접 고르기와 친구 따라가기는 40까지 들어간다.

```
function AutoAssign(account):
    friends = FriendsOnlineInPlaza(account)               // 인스턴스별 친구 수
    candidates = [i for i in instances if i.Population < 36]   // 35 이하
    if candidates is empty:
        return OpenNewInstance()                           // 모든 곳이 36명 이상
    // 1순위: 친구가 많은 곳, 2순위: 사람이 많은 곳(광장을 붐비게), 3순위: 번호가 작은 곳
    return argmax(candidates, key = (friends[i], i.Population, -i.InstanceId))

function Join(account, instanceId, reason):   // reason: Auto | Manual | FollowFriend
    i = instances[instanceId]
    if i.Population >= 40: return Error(InstanceFull)
    if account already in some instance: Leave(account)
    i.Members.Add(account, NewMember(spawn = SpawnPoint(reason)))
    if after adding, every instance has Population >= 36: OpenNewInstance()   // 미리 연다
    Broadcast(i, MemberJoined)

function OnTickHousekeeping(now):
    for i in instances:
        if i.Population == 0:
            if i.EmptySinceMs == 0: i.EmptySinceMs = now
            elif now - i.EmptySinceMs >= 300_000 and instances.Count > 1: Close(i)   // [임시] 5분
        else: i.EmptySinceMs = 0
```

- 채널 번호는 열린 인스턴스 중 가장 작은 빈 번호를 다시 쓴다 [임시].
- 채널 옮기기: 광장 메뉴(Esc)의 "채널 옮기기"가 같은 채널 고르기 판을 연다(ui 4.20) [제안]. 옮기면 그 인스턴스의 Join 규칙을 다시 탄다.
- 친구의 채널로 따라가기: 친구 줄의 [따라가기], 상호작용 목록의 "따라가기"(online 5.4, ui 4.20·4.22) [제안]. 대상이 광장에 없으면 실패("지금 광장에 없다") [임시]. 대상 인스턴스가 40명이면 실패.
- 동시 접속: 계정당 게임 접속 하나, 새 접속이 기존 접속을 끊는다(online 7.3) [제안] ⇒ 한 계정은 동시에 두 인스턴스에 있을 수 없다.
- 접속자 수 상한 `config` 시작값 400. 넘으면 대기열 화면(online 7.5) [제안]. 광장 인원과 별개다.

### 2.4 이동과 동기화

원문: WASD 8방향, 속도 5.0. 발사·비기 없음. 서버 권위 10틱. 클라이언트는 자기 이동을 예측하고 남은 0.1초 보간한다. 서버가 속도와 벽을 검사한다(online 5.1) [제안]. 전투와 같은 조작(WASD)으로 걷는다. 발사와 비기는 꺼진다. 메뉴는 마우스를 쓸 수 있다(wc 2.5) [제안].

방식 [임시, 원문의 "서버 권위 + 클라이언트 예측 + 보간"을 구체화]: **입력 전송 방식**. 클라이언트는 위치가 아니라 방향 입력을 보내고, 서버가 위치를 계산한다. 그러면 "속도 검사"는 서버 계산에 들어 있고, 위치를 거짓으로 보내는 길이 처음부터 없다.

```
// 클라이언트 → 서버, 광장 틱마다(10Hz) 하나. 입력이 바뀌지 않아도 보낸다(유실 대비 최근 3개를 겹쳐 실음).
struct PlazaInputMsg { uint Seq; Dir8OrNone Move; }   // Seq는 1씩 증가

// 서버, 광장 틱마다
function ServerPlazaTick(instance):
    instance.Tick += 1
    for m in instance.Members (account_id 순):
        input = m.PendingInputs 중 Seq가 가장 큰 것(이번 틱에 여러 개 오면 마지막 것만 쓴다)
        if m.Activity in {InMenu, Trading, Sitting, Practicing}: input = None   // 메뉴·거래 중에는 서 있다
        if input != None:
            step = DirUnit(input) * (5.0 * 0.1)       // Q32.32. 직교 0.5칸, 대각 0.35355339칸(성분)
            m.Pos = MoveAndSlide(m.Pos, step, plaza.Walls, radius = 0.30)
            m.Facing = input
        m.LastInputSeq = max seq processed
    Broadcast snapshot: 각 member의 (account_id, pos, facing, activity, emote) + 수신자별 ack(LastInputSeq)

// 클라이언트
- 자기 캐릭터: 매 렌더 프레임 같은 MoveAndSlide를 0.1초 단위 고정 스텝으로 예측. 스냅샷의 ack 이후 입력을 다시 적용해 맞춘다(되감기).
- 남: 스냅샷 두 개 사이를 100ms 늦게 선형 보간(남은 0.1초 보간).
```

- `DirUnit`: 직교는 (±1, 0)/(0, ±1), 대각은 (±1/√2, ±1/√2). 1/√2의 Q32.32 원시값 = 3,037,000,499(버림) [임시] → **[조율: dev CONTRACTS 0.3] 코어와 같은 3,037,000,500 하나를 쓴다.**
- 벽: 광장 바깥 경계(0~40, 0~24)와 시설의 몸통 칸(4절 표)은 막혀 있다. 캐릭터끼리는 막지 않는다(서로 지나간다) [임시].
- `MoveAndSlide`: x축 먼저, 그다음 y축으로 따로 움직이고, 막히면 그 축만 막힌 면에 붙인다(벽에 비벼 미끄러짐) [임시].
- 입력 지연 보호: 서버가 0.5초(5 광장 틱) 동안 입력을 못 받으면 그 사람의 MoveInput을 None으로 둔다 [임시].
- 광장 회선: 초당 10회 × (39명 × 약 10바이트 + 머리 60바이트) = 약 36kbps 내림. 인스턴스 하나(40명)에 1.5Mbps(online 7.5) [제안]. ⇒ 스냅샷 한 사람 몫은 약 10바이트가 목표다. 제안 포맷 [임시]: 인스턴스 안 자리 번호 1바이트 + x, y를 1/64칸 단위 `ushort` 2개(4바이트, 0~2,560 / 0~1,536) + facing·activity 1바이트 + emote 1바이트 + 예비 = 10바이트. 정밀 좌표(Q32.32)는 서버 안에서만 쓴다.

### 2.5 키 배치

| 키 | 동작 | 출처 |
|---|---|---|
| WASD | 8방향 이동 | (online 5.1) [제안] |
| J | 상호작용(가장 가까운 사람 또는 시설) | (online 5.1) [제안] |
| K | 감정표현 바퀴(8칸, 방향키로 고르고 J) | (online 5.1, ui 4.20) [제안] |
| Enter | 채팅 입력 줄 열기 | (online 5.1, ui 4.20) [제안] |
| Tab | 주변 사람 목록(휘장, 닉네임, 캐릭터 초상, 거리) | (online 5.1, ui 4.20) [제안] |
| Esc | 광장 메뉴: 강화, 서고, 옷장, 우편함, 순위표, 채널 옮기기, 설정, 메인 메뉴로 | (online 5.1은 강화·서고·옷장·우편함·설정, ui 4.20이 순위표·채널 옮기기·메인 메뉴로를 더함. 18절 충돌 C-05) [제안] |

- 메뉴는 마우스를 쓸 수 있고 키보드만으로도 된다(wc 2.5, GDD 2) [확정: GDD 조작].
- J 대상 고르기 [임시]: 반경 2.0칸 안의 사람과 시설 중 가장 가까운 것. 같은 거리면 사람이 먼저. 대상 머리 위 또는 시설 앞에 "J 살펴보기", "J 좌판 거리", "J 결투 게시판" 같은 안내(ui 4.20) [제안].
- Tab 목록과 대화 줄의 누름 목록에서는 거리와 무관하게 그 인스턴스의 사람을 고를 수 있다. 다만 거래 신청은 5칸 조건(9.2절)을 따로 검사한다 [임시].

### 2.6 시간대

- 서버 시각으로 24분이 하루다(낮 12분, 해질녘 4분, 밤 8분). 모든 인스턴스에서 같이 흐르고 색만 바뀐다(online 5.1, wc 2.5) [제안]. 같은 서버의 모두가 같은 시간대를 본다(wc 2.5).
- 팔레트는 전장과 같은 3벌(낮, 해질녘, 밤)을 색 교체로 쓴다(wc 10.3) [제안]. 모닥불은 주황~노랑으로 그린다(위험색 범위 밖)(wc 2.5) [조율: DEC 2.2].

```
function PlazaPhase(serverUnixMs):
    t = serverUnixMs mod 1_440_000          // 기준점 = 유닉스 시각 0 [임시]
    if t < 720_000:  return (Day,   t)               // 0 ~ 12분
    if t < 960_000:  return (Dusk,  t - 720_000)     // 12 ~ 16분
    return              (Night, t - 960_000)          // 16 ~ 24분
```

- 클라이언트는 접속할 때 서버 시각 차이를 받아 위 식을 스스로 돌린다(시간대 전환만 위해 패킷을 쓰지 않는다) [임시]. 전환의 색 보간 길이는 표시 계층의 일이다.

### 2.7 머리 위 표시

| 요소 | 규칙 | 출처 |
|---|---|---|
| 닉네임 판 | 왼쪽에 리그 휘장, 닉네임, 오른쪽에 상태 아이콘. **친구는 닉네임 색이 "좋음 초록"**, **차단한 사람은 판 전체가 없다**. 나 = 분필 흰색 테 | (ui 4.20) [제안] |
| 칭호 띠 | 지팡이 칭호(wc 6.1) 또는 고른 칭호. +13 칭호는 금 띠, 후계자는 전용 띠, 수제자 띠 | (ui 4.20) [제안] |
| 말줄임표 | 메뉴를 보고 있는 사람(강화 화면 포함). "강화 중"이라는 표시는 없다 | (ui 4.20, online 1.4, fx 1.1) [확정 계열: DEC 1.7, 2.4] |
| 말풍선 | 주변 채널만. 한 줄 20자, 최대 4줄(80자). 4초 뒤 흐려진다. **귓속말은 풍선으로 뜨지 않는다** | (ui 4.20) [제안] |
| 닉네임 길이 | 2~8자(online 7.3). 넘으면 "…" | (ui 4.20) [제안] |
| 지팡이 | 들고 있는 지팡이(그 캐릭터의 든 지팡이)는 **실제 강화 단계의 모양과 효과 그대로** 보인다 | (online 5.1, wc 2.5) [확정: DEC 1.3] |
| 강화 표시 | 강화 화면을 열고 있다는 표시나 강화 결과의 표시는 광장에 없다 | (online 5.1, 1.4) [확정] |
| 입자 | `effects.md`의 광장 예산(이 문서 17.5절 표) | (online 5.1, fx 9.3) [조율: DEC 2.8] |

```csharp
public struct OverheadView {          // 서버가 Join/변경 때 보내는 정적 정보(스냅샷과 별도)
    long AccountId; string Nickname; LeagueId League; TitleId? Title; TitleBand Band; // Normal|Gold13|Heir|Disciple
    CharacterId Character; WandAppearance Wand;  // 단계, 겉모습, 흔적 공개 여부에 따른 겹그림 플래그
    StatusIcon Status;                // None|InQueue|Busy [임시]
}
```

- 흔적 겹그림: 남의 화면(광장)에서는 주인이 그 지팡이의 "흔적 공개"를 켠 경우에만 보인다(fx 1.3) [확정: DEC 1.15]. `WandAppearance.ShowScars = wand.show_scars`.
- 차단 판정은 **보는 사람 기준**이다. A가 B를 차단하면 A의 화면에서 B의 판이 없다. 캐릭터 몸은 그대로 보인다 [임시](원문은 판만 없앤다고 적음).

### 2.8 감정표현

- 8종으로 시작: 인사, 박수, 도발, 절, 앉기, 춤, 지팡이 들어 올리기(단계 효과 재생), 좌절. 추가 감정표현은 꾸미기로 얻는다(online 5.1) [제안].

| id | 이름 | 길이 [임시] | 비고 |
|---|---|---|---|
| `emote_greet` | 인사 | 2,000ms | |
| `emote_clap` | 박수 | 2,000ms | |
| `emote_taunt` | 도발 | 2,000ms | |
| `emote_bow` | 절 | 2,000ms | |
| `emote_sit` | 앉기 | 이동 입력이 올 때까지 | 큰 모닥불의 앉기 동작과 같다(4절) |
| `emote_dance` | 춤 | 이동 입력이 올 때까지 | |
| `emote_raise_wand` | 지팡이 들어 올리기 | 3,000ms | 단계 효과 재생 |
| `emote_despair` | 좌절 | 2,000ms | |

- 감정표현 중 이동 입력이 오면 즉시 끝난다 [임시]. 감정표현 간격 제한 1,000ms [임시](도배 방지).
- 추가 감정표현은 `item_instance`(꾸미기)로 가진다. 바퀴는 8칸이므로 "바퀴에 거는 8개"를 계정 설정으로 고른다 [임시].

---

## 3. 시설

### 3.1 시설 표 (이름·자리·생김새는 wc 2.5, 기능은 online 5.2가 기준)

시설은 **10곳**이다. 광장의 시설 이름은 wc 2.5의 표가 유일한 기준이고 "장터 NPC", "대마법사 NPC", "후계자 동상", "원정 문", "허수아비 연습장" 같은 다른 이름은 쓰지 않는다(wc 2.5) [제안]. 이름은 작업용이며 코드는 id로만 가리킨다(DEC 1.25) [확정].

| id | 이름 | 자리와 생김새 (wc 2.5) | J를 눌렀을 때 (online 5.2) | 임시 좌표·몸통 [임시] |
|---|---|---|---|---|
| `fac_bonfire` | 큰 모닥불 | 광장 한가운데. 둘레에 통나무 의자 | 모이는 자리. 앉기 동작, 주변 채팅. 화면 없음 | 중심 (20.0, 12.0), 몸통 2×2칸(19~21, 11~13). 통나무 의자 6개를 반경 2.5칸 둘레에(앉기 자리) |
| `fac_stall_street` | 좌판 거리 | 왼쪽. 플레이어가 펴는 돗자리와 나무 궤짝 | 직접 거래 신청(상대가 가까이 있을 때), 장터 등록·구매, 시세 조회. 거래가 열리는 조건은 9.1절 | 구역 x 2~10, y 8~16. 안내 기준점 (6.0, 12.0). 몸통 없음(걸어 들어간다) |
| `fac_peddler_cart` | 행상 수레 | 왼쪽 아래. 느루가 버린 물건을 주워다 파는 떠돌이의 손수레 | 골드 상점: 하락 방지 부적, 새 지팡이(+0), 골드 꾸미기, 서고 확장. 값은 1.4절 표 | 중심 (5.0, 20.0), 몸통 2×1.5칸 |
| `fac_duel_board` | 결투 게시판 | 오른쪽. 쪽지가 잔뜩 꽂힌 나무판 | 자동 매치 등록, 결투 방 목록·생성, 관전, 리그와 결투 점수 확인 | 중심 (32.0, 11.0), 몸통 2×1칸 |
| `fac_stone_path` | 돌마당 가는 돌길 | 오른쪽 끝. 선돌 사이로 난 돌길 | 결투가 잡힌 두 사람이 함께 걸어 들어가는 길(연출). 광장 도전(online 4.6)도 수락되면 이 길로 간다. **기능 화면은 없다** | 중심 (38.5, 12.0), 선돌 몸통 (38,10)·(38,14) 1×1칸 2개 |
| `fac_heir_monument` | 후계자 비석 | 위쪽 가운데. 이름이 새겨진 선돌 | 시즌 순위표, 역대 후계자 명단(시즌 1위의 닉네임이 새겨진다) | 중심 (20.0, 4.0), 몸통 1.5×2칸 |
| `fac_nuru_chair` | 느루의 흔들의자 | 후계자 비석 옆. 느루가 졸고 있다 | 시즌 안내, 후계 점수 내역, 시즌 보상 수령, 시즌 상점, 한 줄 잡담 | 중심 (23.0, 4.5), 몸통 1.5×1.5칸 |
| `fac_practice_dummy` | 연습 허수아비 | 아래쪽 풀밭 | 허수아비 연습(GDD의 연습 기능) + 결투 입력 지연 체험(online 4.12). 다른 사람의 연습을 구경할 수 있다 | 중심 (28.0, 19.5), 몸통 1×1칸 |
| `fac_crow_mailbox` | 까마귀 우편함 | 입구(한뎃벌 출입문 옆). 새집이 달린 말뚝 | 친구 목록, 쪽지, 우편(장터 대금, 산 물건, 돌아온 물건, 보상) | 중심 (17.0, 22.0), 몸통 1×1칸 |
| `fac_wilds_gate` | 한뎃벌 출입문 | 아래쪽 가운데. 덤불이 벌어진 틈 | 무한 웨이브 출발(캐릭터·지팡이·조립·시작 웨이브 확인) | 중심 (20.0, 23.0), 몸통 없음(바깥 경계의 틈) |

- 처음 들어오는 위치(스폰) [임시]: 한뎃벌 출입문 앞 (20.0, 21.0)을 중심으로 반경 1.5칸 안의 빈 자리. 친구 따라가기면 그 친구 옆 1칸. 결투·원정에서 돌아오면 각각 돌마당 가는 돌길 앞 (36.5, 12.0), 한뎃벌 출입문 앞.
- 탑 밑동(광장 쪽 모습)은 위쪽 배경(wc 10.3)이며 판정은 바깥 벽과 같다 [임시]: y < 2.0은 막힌 칸.
- 시설 이름표: 좌판 거리, 결투 게시판, 후계자 비석, 느루의 흔들의자, 연습 허수아비, 행상 수레, 까마귀 우편함, 한뎃벌 출입문, 돌마당 가는 돌길(ui 4.20). 큰 모닥불은 이름표가 없다 [제안: ui 목록 그대로].

### 3.2 강화와 메뉴 화면

- **강화용 장소와 NPC는 없다** [조율: DEC 2.4]. 강화, 서고, 옷장은 메뉴(Esc)에서 여는 화면이다. 메인 메뉴에서도 같은 화면이 열린다(online 5.2, wc 2.5).
- 강화 화면의 배경은 느루의 작업대이며 광장에서 걸어서 갈 수 있는 곳이 아니다(wc 2.5) [제안].
- 시설의 기능도 전부 화면이다(online 5.2) [제안].

| 구분 | 목록 | 출처 |
|---|---|---|
| 광장에 들어오지 않고 메인 메뉴에서 열 수 있는 것 | 원정, 강화, 서고, 옷장, 상점, 우편함, 친구 목록, 순위표, 시즌 화면, 장터, 자동 매치 대기열 | (online 5.2) [제안] |
| 광장에 와야만 되는 것 | 다른 사람 만나기, 직접 거래, 광장 도전, 주변·광장 채팅, 남의 연습 구경 | (online 5.2) [제안] |

```csharp
public enum FacilityId { Bonfire, StallStreet, PeddlerCart, DuelBoard, StonePath, HeirMonument, NuruChair, PracticeDummy, CrowMailbox, WildsGate }
public enum ScreenId { Market, DirectTradeRequest, PriceLookup, GoldShop, DuelBoard, SeasonInfo, HeirScore, SeasonRewards, SeasonShop, NuruChatter,
                       Leaderboard, HeirList, Expedition, Practice, LatencyDemo, PracticeSpectate, Mailbox, FriendList, Enhance, Library, Wardrobe, Settings, ChannelSelect }
// 시설 → 열리는 화면은 데이터 표(facility_screens.json)로 둔다. 메인 메뉴도 같은 ScreenId를 연다.
```

### 3.3 서버 전체 알림을 광장에서 받을 때

- +10 이상 강화 성공과 +13 달성은 서버 전체에 알린다(DEC 2.4) [조율]. 광장에서는 화면 위 가장자리에 알림 띠, 느루의 까마귀가 그 띠를 물고 지나가고, 느루의 흔들의자에서 느루가 한쪽 눈을 뜬다(wc 2.5, fx 8) [제안]. 남에게 전해지는 것은 이 알림뿐이고 광장에서 남의 강화를 구경하는 연출은 없다(wc 2.5, online 1.4) [확정 계열].
- 알림 큐 규칙(fx 8) [제안]: +10~+12 알림은 6초에 하나, +13 알림은 10초에 하나씩 차례로. +13이 줄 맨 앞. 밀린 +10~+12 알림이 10개를 넘으면 "방금 +10 이상 성공 14건"처럼 한 줄로 묶는다. 대성공으로 두 단계를 올라 +10 이상이 되면 도착한 단계로 한 번만. 띠 길이: +10~+12는 4초, +13은 6초(펼쳐진 때부터).
- 이 큐는 **서버 쪽 하나**로 두고 모든 연결에 같은 순서로 보낸다(시스템 채널, 5.2절) [임시]. 받는 쪽 설정 "서버 강화 알림 보기"(전체 / +13만 / 끔)는 클라이언트가 거른다(fx 8) [제안].
- 알림에 내 닉네임을 올릴지는 계정 설정 `show_name_in_alerts`(켬/끔, 기본 켬). 끄면 "어느 제자의 …"(online 1.4, fx 8) [제안].
- 알림 문구: "○○○의 {지팡이 이름}이 +11이 되었다." / "○○○ — 엄청엄청 위대한 {캐릭터 표시 이름}가 나왔다. 느루: …그건 나도 못 해 봤다."(fx 8) [제안]. 이름은 이름 표에서 읽는다(DEC 1.25) [확정].

---

## 4. 채팅

### 4.1 채널

| 채널 id | 이름 | 범위 | 제한 | 출처 |
|---|---|---|---|---|
| `nearby` | 주변 | 반경 8칸, 말풍선 | 2초에 1회 | (online 5.3) [제안] |
| `plaza` | 광장 | 같은 인스턴스 전체 | 5초에 1회 | 같음 |
| `trade` | 거래 | 같은 인스턴스 전체, 아이템 링크 가능 | 30초에 1회 | 같음 |
| `whisper` | 귓속말 | 접속 중인 한 사람 | 차단한 사람에게서는 오지 않는다. 기본 "친구만"(online 10) | 같음 |
| `friend` | 친구 | 접속 중인 친구 전체 | (원문 빈칸) [임시 1초] | 같음 |
| `duelroom` | 결투방 | 방 안. **경기 중에는 정해진 문구 8개만** | (원문 빈칸) [임시 1초] | 같음 |
| `system` | 시스템 | 강화 알림(+10 이상 성공, +13 달성), 운영 공지. 읽기 전용. 서버 전체에 간다 | 보낼 수 없다 | 같음 |

- **서버 전체 채팅 채널은 두지 않는다** [조율: DEC 2.7]. 시스템 알림만 서버 전체에 간다(online 5.3).
- 거래 채널 "아이템 링크": 대화 줄 안의 물건 이름표. 등급색 테. 누르면 툴팁으로 상세(ui 4.20) [제안]. 링크는 `ItemLink { ref_type: stack|instance|wand, item_code or id, snapshot }`로 서버가 발언 시점의 스냅샷을 붙인다 [임시](받는 쪽이 남의 인벤토리를 조회하지 않게). 지팡이 링크의 흔적 표시는 살펴보기와 같은 공개 규칙을 따른다 [임시].
- 결투방 채널은 결투 방 화면(4.21)과 공유되며 경기 중 자유 채팅은 없다(online 10) [제안].
- 대화창 탭: 주변, 광장, 거래, 귓속말, 친구, 결투방, 시스템(7개). 꺼 둔 채널은 탭이 회청(ui 4.20) [제안].
- 채널별 글자색(표시 데이터): 주변 분필 흰색, 광장 회청, 거래 놋쇠색, 귓속말 연보라, 친구 좋음 초록, 결투방 하늘, 시스템 금색(강화 알림)·분필 흰색(공지). 색과 함께 줄 머리에 채널 아이콘(ui 4.20) [제안].

### 4.2 메시지 처리 절차 (서버)

```
function HandleChat(sender, channel, rawText, target?, itemLinks?):
    // 0. 자격
    if channel == system: reject(ReadOnly)
    if sender.chat_sanction_until > now: reject(Muted, until)
    if sender.best_wave_ever < 10 and channel != nearby: reject(NewbieNearbyOnly)  // 무한 웨이브 10에 닿기 전
    if channel in {nearby, plaza, trade} and sender not in a plaza instance: reject(NotInPlaza)
    if channel == duelroom and sender.room.InMatch and not IsPresetPhrase(rawText): reject(PresetOnly)
    // 1. 길이
    text = NormalizeWhitespace(rawText)          // 앞뒤 공백 제거, 연속 공백 1개
    if text.Length == 0: reject(Empty)
    if CharCount(text) > 80: reject(TooLong)      // 한 줄 최대 80자. 자르지 않고 거절 [임시]
    // 2. 간격
    if now < sender.ChatNextAllowedMs[channel]: reject(Cooldown, remainingMs)
    // 3. 반복
    key = FilterNormalize(text)
    if CountSame(sender.recentKeys within 10분, key) >= 2: reject(Repeated)   // 같은 문장 3회째부터 막는다
    // 4. 필터
    if ContainsContactPattern(key or text): reject(ContactBlocked)   // 주소·전화·계좌·메신저 아이디 모양은 통째로 막는다
    masked = MaskBannedWords(text, key)                                 // 걸린 단어만 "＊"로
    // 5. 전달 대상
    recipients = channel switch {
        nearby   => instance members with Dist2(sender, m) <= 64,       // 8칸, 경계 포함 [임시]
        plaza    => instance members,
        trade    => instance members,
        whisper  => [target] if target online,
        friend   => online friends of sender,
        duelroom => room members (+ spectators),
    }
    recipients = recipients.Where(r => !r.Blocks(sender) && !r.ChannelMuted(channel))
    if channel == whisper:
        if target.Blocks(sender): silently drop (보낸 쪽에는 "보냈다"로 보인다) [임시]
        if target.whisper_policy == FriendsOnly and !Friends(sender, target): reject(WhisperFriendsOnly)
    // 6. 기록과 발송 (발송이 실패해도 기록은 남긴다)
    sender.ChatNextAllowedMs[channel] = now + cooldown[channel]
    sender.recentKeys.Push(key, now)
    chat_log.Insert(channel, instance_id, sender.id, masked, filtered = masked != text, at = now)
    Send(recipients, ChatMsg{channel, sender, masked, itemLinks, at})
    if channel == nearby: 말풍선은 받는 클라이언트가 그린다(20자×4줄, 4초)
```

- 간격은 **성공한 발언에만** 갱신한다(거절된 시도는 간격을 다시 걸지 않는다) [임시].
- 입력 줄: 남은 글자 수 "12/80", 다음에 보낼 수 있을 때까지 대기 표시(ui 4.20) [제안]. 서버가 `Cooldown` 거절과 함께 `remainingMs`를 준다.

### 4.3 필터

- 금칙어 사전 + 정규화(자모 분리, 사이에 낀 기호·공백, 비슷한 글자 치환)를 **서버에서** 한다. 걸리면 그 단어만 가린다. 주소(URL), 전화번호, 계좌번호 모양, 메신저 아이디 모양은 통째로 막는다(현금 거래 유도와 낚시 차단)(online 5.3) [제안].
- 채팅 필터는 끌 수 없다(online 10) [제안]. 금칙어 버전은 `config`에 둔다(online 7.9).
- 닉네임에도 금칙어 필터와 운영자·NPC 사칭 금지어를 쓴다(online 10) [제안] — 같은 `FilterNormalize`를 쓴다.

```
function FilterNormalize(s):
    s = NFKC(s); s = ToLower(s)
    s = DecomposeHangulToJamo(s)                 // 자모 분리: "시발" → ㅅㅣㅂㅏㄹ
    s = RemoveInterleavedSymbolsAndSpaces(s)     // "ㅅ.ㅂ", "시 발", "시_발" → 같은 열
    s = MapLookalikes(s)                         // 0→o, 1→l/i, @→a, $→s, ㅆ→ㅅㅅ 등. 표는 데이터 파일
    return s

ContainsContactPattern 패턴 [임시, 데이터 파일 chat_contact_patterns.json]:
    URL: (https?://|www\.|[a-z0-9-]+\.(com|net|kr|co|io|gg|me|xyz|org)(/|\b))
    전화: 숫자(정규화 뒤) 9~11자리가 연속(구분 기호 제거 후), 또는 01[016789] 로 시작하는 10~11자리
    계좌: 숫자 10~14자리 연속, 또는 2~6자리-2~6자리-2~7자리 꼴
    메신저 아이디: (카톡|카카오|톡|텔레|텔레그램|디코|디스코드|라인|kakao|telegram|discord|line)\s*[:：]?\s*[a-z0-9_.@#-]{3,}
    숫자는 한글 숫자("공일공", "영일영")와 전각 숫자도 정규화한 뒤 센다
```

### 4.4 신고·차단·끄기·제재

- 신고: 채팅 줄이나 캐릭터를 골라 신고. 사유 5종(욕설, 사기, 현금 거래, 부정 프로그램, 기타). 서버가 그 사람의 **최근 채팅 30줄**을 자동으로 붙인다. 서로 다른 계정 5개 이상에게 24시간 안에 신고당하면 검토 전까지 채팅이 자동으로 1시간 막힌다(online 5.3) [제안]. 신고 창에는 한 줄 설명과 "최근 대화 30줄이 함께 간다" 안내(ui 4.22) [제안].
- 차단: 상대의 채팅, 귓속말, 도전, 거래 신청, 친구 신청이 모두 오지 않는다. 계정에 저장된다(최대 200명)(online 5.3) [제안]. 차단 확인 창에 막히는 것(대화, 귓속말, 도전, 거래 신청, 친구 신청)을 적는다(ui 4.22) [제안].
- 채팅 끄기: 채널별로 끌 수 있다. 전체 끄기 설정도 둔다(online 5.3) [제안]. 끄기는 **받기만** 막는다(보내기는 된다) [임시]. 시스템 채널의 강화 알림은 "서버 강화 알림 보기" 설정이 따로 다룬다.
- 제재 단계: 경고 → 채팅 1일 → 7일 → 30일 → 영구(online 10) [제안]. 자동 1시간 막힘은 이 단계에 세지 않는다 [임시]. 운영자 제재는 `sanction`과 `audit_log`에 남는다(online 7.10, 10).

```csharp
public enum ReportReason { Abuse = 1, Scam = 2, RealMoneyTrade = 3, Cheating = 4, Other = 5 }
public sealed class ReportRow { long Id; long ReporterId; long TargetId; ReportReason Reason; string Note; // 한 줄 설명 [임시 최대 80자]
    string ChatExcerptJson;   // 대상의 최근 30줄(채널, 문장, 시각). 서버가 붙인다
    string Ref;               // 신고한 줄의 chat_log.id 또는 "character"
    ReportStatus Status; long? HandledBy; DateTime At; }

function OnReport(reporter, target, reason, note, ref):
    if reporter == target: reject
    if exists report(reporter, target) within 24h: 기록만 갱신하고 자동 막힘 계산에는 1회로 센다 [임시]
    insert report(..., chat_excerpt = last 30 chat_log rows of target)
    n = count distinct reporter_id from report where target_id = target and at > now - 24h
    if n >= 5 and target.chat_sanction_until < now + 1h and no pending review lock:
        target.chat_sanction_until = now + 1h; sanction.Insert(kind = chat, reason = "auto_report", from = now, until = now + 1h, by = system)
```

- "검토 전까지 1시간": 운영자가 검토해 풀거나 늘리기 전이라도 1시간이 지나면 풀린다 [임시]. 같은 24시간 창에서 다시 5명을 넘으면 다시 1시간 [임시].
- 신고 남용 방지 [임시]: 한 계정이 하루에 보낼 수 있는 신고 20건.

### 4.5 데이터

| 표 | 열 | 메모 |
|---|---|---|
| `chat_log` | id, channel, instance_id, account_id, text, filtered, at | 90일 보관 뒤 삭제(online 7.9) |
| `block` | account_id, other_id, state, at | 계정당 최대 200행(online 5.3, 7.9) |
| `report` | id, reporter_id, target_id, reason, chat_excerpt, ref, status, handled_by, at | (online 7.9). 발췌는 처리 뒤 1년(online 10) |
| `sanction` | id, account_id, kind(채팅/거래/결투/접속), reason, from, until, by | (online 7.9) |
| `account_chat_settings` [임시] | account_id, muted_channels(bitset), mute_all, whisper_policy(친구만/모두), show_alerts(전체/+13만/끔) | 새 표 |

---

## 5. 친구, 최근 만난 사람, 살펴보기

### 5.1 친구

- 신청과 수락, 최대 100명. 접속 여부와 지금 있는 곳(광장 채널 / 원정 중 / 결투 중)을 본다. 친구의 채널로 따라가기, 결투 초대, 귓속말(online 5.4) [제안].
- 친구 창 탭: 친구 / 최근 만난 사람 / 받은 신청 / 차단. 친구 수 "37 / 100". 친구 찾기: 닉네임으로 신청(ui 4.22) [제안].

상태 기계 (`friend` 표의 `state`):

```
 (없음) ──신청(A→B)──▶ Pending(A→B) ──B 수락──▶ Friends(양쪽 행 2개, since = now)
                          │  ├─B 거절──▶ (없음)
                          │  ├─A 취소──▶ (없음)
                          │  └─7일 지남 [임시]──▶ (없음)
                          └─B가 A에게 신청──▶ 즉시 Friends (서로 신청은 수락으로 본다) [임시]
 Friends ──어느 쪽이든 삭제──▶ (없음, 양쪽 행 삭제)
 (어느 상태든) ──한쪽이 차단──▶ 친구·신청 행 삭제 + block 행
```

조건:
- 신청 거절 조건: 대상이 신청자를 차단했다(조용히 실패, 신청자에게는 "보냈다") [임시: 차단 사실을 드러내지 않음], 어느 쪽이든 100명이 찼다, 이미 친구, 이미 신청 중, 자기 자신, 대상의 친구 신청이 50건 이상 쌓여 있다 [임시].
- 수락 때 두 계정 행을 계정 번호 순으로 잠그고 100명 한도를 다시 센다(9.7절과 같은 잠금 규칙) [임시].
- `since`는 선물 조건(친구가 된 지 7일, 9.5절)에 쓴다. **친구를 끊었다가 다시 맺으면 since는 새로 시작한다** [임시].

```csharp
public enum PresenceKind { Offline, MainMenu, Plaza, Expedition, Duel }   // MainMenu는 [임시] 추가
public struct Presence { PresenceKind Kind; int? PlazaInstanceId; }        // 광장이면 채널 번호
```

- 친구 줄의 [따라가기]는 `Plaza`일 때만 켜진다. [결투 초대]는 결투 방 초대로 이어진다(결투 명세서) [임시]. [귓속말]은 4절 규칙.
- 접속 상태 알림: 친구가 접속하거나 나가면 친구에게 `friend_presence` 이벤트 [임시]. 미성년자 기본값 정책 때문에 "접속 숨기기" 설정을 둔다(기본 끔) [임시].

### 5.2 최근 만난 사람

- 최근 결투 상대와 거래 상대 20명(online 5.4) [제안]. 거래 완료(직접 거래) 또는 결투 경기(세 경로 모두)가 끝날 때 양쪽에 넣는다. 같은 사람은 맨 앞으로 옮긴다. 21번째가 들어오면 가장 오래된 것을 지운다 [임시]. 장터 거래 상대는 넣지 않는다 [임시](장터는 상대를 고르지 않는다).

### 5.3 살펴보기

- 다른 사람에게 상호작용 → 프로필(online 5.4) [제안].
- 보이는 것: 캐릭터, 지팡이(단계, 변이, 슬롯, 조립한 블록), 장착 IL, 리그와 점수, 시즌 최고 웨이브, 칭호(online 5.4). 화면 구성으로는 왼쪽(캐릭터, 닉네임, 칭호, 리그 휘장, 결투 점수, 시즌 최고 웨이브), 가운데(지팡이 이름, 단계, 변이, 확장 슬롯, "주인 n번째", 지팡이 문서가 주는 단계 별명 한 줄), 오른쪽(조립한 블록, 장착 IL, 단추: [귓속말] [친구 신청] [거래 신청] [결투 도전] [차단] [신고])(ui 4.22) [제안].
- 조립 공개는 설정에서 끌 수 있다(**단계와 겉모습은 항상 보인다**). 비공개면 "조립 비공개"(online 5.4, ui 4.22) [제안].
- **하락의 흔적(흔적 겹그림, 역대 최고 단계, 하락 횟수)은 주인이 그 지팡이의 "흔적 공개"를 켠 경우에만 보인다** [확정: DEC 1.15, online 5.4]. 지팡이마다 따로 저장하고 기본은 끔(fx 1.3) [제안: 기본값은 fx 개발자 확인 2번].
- 큰 그림 회전(좌우 키)은 회전용 그림을 내주는 지팡이만(ui 4.22) [제안] — 데이터 `wand_art.has_rotation`.

```csharp
public sealed class InspectView {          // 서버가 보는 사람 기준으로 걸러서 만든다
    long AccountId; string Nickname; TitleId? Title; LeagueId League; int DuelRating; int SeasonBestWave;
    CharacterId Character; int EquippedIl;
    WandPublic Wand;                       // 든 지팡이
    LoadoutPublic? Loadout;                // show_loadout = false면 null + LoadoutHidden = true
    bool LoadoutHidden;
}
public sealed class WandPublic {
    long WandId; CharacterId Character; int Stage; MutationId[] Mutations; int SlotsExtra; int OwnerCount; SkinId? Skin;
    // 흔적 칸: show_scars가 켜졌을 때만 채운다. 꺼졌으면 null(값 자체를 보내지 않는다)
    int? RecordPeak; int? DropCount; bool? HasScarOverlay;
}
```

- **서버가 가린 값을 아예 보내지 않는다**(클라이언트 표시만 끄는 것이 아니다) [임시]. 흔적 공개를 끈 지팡이의 `record_peak`, `drop_count`는 살펴보기 응답에 없다.
- 살펴보기는 광장 밖에서도 된다(순위표 프로필, 친구 목록) [임시]. 같은 `InspectView`를 쓴다.

---

## 6. 거래 공통

### 6.1 거래 수단

- 직접 거래(광장에서 만난 두 사람)와 장터(즉시 구매가 등록), 그리고 선물(한쪽만 주는 거래)(online 5.5) [제안]. 거래 가능 품목: 주문 블록, 꾸미기 아이템, 강화 재료와 부적, 강화된 지팡이 [확정: DEC 1.10].
- 서버 프로필에서만 있다. 오프라인 프로필에는 거래, 장터, 우편이 없다(online 8.1) [조율: DEC 2.6]. 오프라인 진행은 거래에 반영되지 않는다 [조율].

### 6.2 거래가 열리는 조건 (`TradeGate`)

```
function TradeGate(account, now) -> (ok, reasons[]):
    r = []
    if account.best_wave_ever < 10:            r += NeedWave10           // 무한 웨이브 10 도달
    if account.play_seconds < 7200:            r += NeedPlay2h           // 누적 플레이 2시간
    if exists account_debt where cleared_at is null: r += HasDebt       // "갚을 것"이 남아 있다(online 3.2 6번)
    if sanction(kind = 거래) active:           r += Sanctioned
    if now < account.trade_block_until:        r += SecurityHold         // 새 기기·이메일 변경·비밀번호 재설정 뒤 24시간
    return (r empty, r)
```

| 조건 | 값 | 출처 |
|---|---|---|
| 직접 거래, 장터(등록·구매), 선물이 열리는 때 | 무한 웨이브 10 도달 + 누적 플레이 2시간(자동 매치와 같은 조건) | (online 5.5, 4.9) [제안] |
| 그 전의 계정 | 좌판 거리에서 **시세 조회만** | (online 5.5) [제안] |
| 갚을 것이 남아 있는 동안 | 직접 거래, 장터 등록·구매, 선물, 지팡이 떠나보내기 불가. 원정, 강화, 결투는 된다 | (online 5.5, 3.2) [제안] |
| 처음 보는 기기 로그인 | 24시간 동안 **거래와 장터 등록**을 막는다 | (online 7.3) [제안] |
| 이메일 주소를 바꾼 뒤 | 24시간 거래와 장터 등록을 막는다 | (online 7.3) [제안] |
| 비밀번호 재설정 성공 뒤 | 24시간 거래와 장터 등록을 막는다 | (online 7.3) [제안] |

- 막는 이유: 갓 만든 계정이 까마귀 보따리(monsters 표 6.3-A)나 시즌 참가 보상만 받아 다른 계정으로 넘기는 일을 막는다(online 5.5).
- "도달"의 정의 [임시]: 서버 프로필에서 검증 1차를 통과한 판 기준으로 웨이브 10에 들어선 적이 있다(`account`에 `best_wave_ever` 칸을 둔다). 허상과의 첫 만남(웨이브 10)에서 쓰러져도 판이 이어지므로 도달로 친다.
- 보안 잠금(24시간) 동안 막히는 범위 [임시]: 원문은 "거래와 장터 등록". 따라서 **장터 구매와 우편 수령은 된다**. 직접 거래와 선물은 막힌다.
- 화면: 조건을 못 채우면 좌판 거리에서 시세 조회만 켜고, 무엇이 모자란지(웨이브, 시간, 갚을 것, 보안 잠금 남은 시간) 적는다 [임시].

### 6.3 품목과 귀속 규칙

[개발자: 품목 / 제안: 규칙] (online 5.5)

| 품목 | `TradeItemKind` | 직접 거래 | 장터 | 수수료 | 귀속·제한 |
|---|---|---|---|---|---|
| 주문 블록 | `Block` | 가능 | 가능 | 5% | 출발 조립에 끼워 둔 블록은 빼야 거래 가능. 프로필의 마지막 투사체 블록은 올릴 수 없다(online 1.1). 받는 쪽 서고 한도(online 2.4) 안에서만 받는다 |
| 강화석 | `EnhanceStone` | 가능 | 가능 | 5% | |
| 하락 방지 부적 | `Charm` | 가능 | 가능 | 5% | 상점에서 산 부적도 거래 가능 |
| 꾸미기(골드·보상으로 얻은 것) | `Cosmetic` | 가능 | 가능 | 5% | |
| 지팡이(강화된 것과 +0 모두) | `Wand` | 가능 | 가능(+0은 불가) | 10% | 8절 지팡이 규칙. 받는 쪽의 개수 제한은 없다 |
| 시즌 한정 꾸미기(시즌 상점, 순위 보상) | `SeasonCosmetic` | 가능 | 가능 | 5% | 후계자 전용만 예외 [조율: DEC 2.7] |
| 후계자 칭호와 후계자 전용 겉모습 | — | 불가 | 불가 | | 계정에 묶인다 [조율: DEC 2.7] |
| 복수 보상(monsters 3.9.4): 머리 장식 "금 간 거울 조각", 귀속 강화석과 귀속 부적 | — | 불가 | 불가 | | 계정에 묶인다. 꾸미기는 `item_instance.bound = true`, 재료는 `item_stack.qty_bound`로 따로 센다. 귀속 재료는 선물과 우편 첨부로도 넘길 수 없고, 강화·슬롯 새기기에서 같은 재료의 거래 가능한 수량보다 **먼저** 쓰인다 |
| 칭호, 기록, 리그 휘장 | — | 불가 | 불가 | | 계정 기록 |
| 골드 | `Gold` | 직접 거래에서 물건과 함께 | — | 5% | 하루에 주는 골드 합계 상한 = 계정 누적 획득 골드의 20% |
| 검증 대기 중인 보상(online 3.2) | — | 불가 | 불가 | | 검증이 통과로 끝나면 풀린다. 실패하면 회수된다 |
| 유료 재화·유료 구매품(결제 연결 뒤) | — | 불가 | 불가 | | `origin = 유료`, `bound = true`(online 9.1) |
| 시즌 주화, 별빛 | — | 불가 | 불가 | | (online 9.1) [제안] |

**거래 가능 수량** (겹쳐지는 물건, `item_stack`):

```
tradable_qty(stack) = stack.qty - stack.qty_pending - stack.qty_socketed - stack.qty_bound - stack.qty_listed - stack.qty_in_trade
```

- `qty_listed`(장터에 올린 수량)와 `qty_in_trade`(열린 직접 거래 창에 올린 수량)는 원문 표에 없는 칸이다 [임시]. 원문은 `wand.lock_state`(없음/원정/결투/장터/우편/거래)로 지팡이만 잠그므로, 겹쳐지는 물건은 장터 등록 때 **수량을 `item_stack`에서 빼서 `market_listing.qty`로 옮기는 방식**을 택한다(예약이 아니라 이동) [임시]. 그러면 `qty_listed` 칸이 필요 없다. 직접 거래 창은 커밋 때 다시 확인하므로 `qty_in_trade`도 저장하지 않고 메모리에서만 센다 [임시]. 최종 식:

```
tradable_qty(stack) = stack.qty - stack.qty_pending - stack.qty_socketed - stack.qty_bound   // 메모리의 거래 창 예약분은 따로 뺀다
```

- 쓸 때(강화·슬롯 새기기) 귀속 수량부터 뺀다(online 5.5, 7.9) — 강화 명세서 몫이지만 같은 `item_stack` 규칙이다.
- **마지막 투사체 블록 보호**: 서고와 조립을 합쳐 투사체 블록이 1개뿐이면 그 블록은 제물 칸, 거래 창, 장터에 올릴 수 없다(online 1.1) [제안]. 판정: `Σ(투사체 블록의 qty, 끼운 것 포함) - 이번에 내보내는 투사체 블록 수 >= 1`이어야 한다 [임시: 여러 개를 한 번에 내보낼 때도 남는 것이 1개 이상].
- **끼워 둔 블록**: 출발 조립에 끼운 블록은 빼야 거래 가능(online 5.5). `qty_socketed`는 거래 가능 수량에서 빠진다.
- **서고 한도**: 서고 한도는 블록 개수 합계(지팡이에 끼워 둔 블록은 빼고 센다). 100개에서 시작, 50개당 2,000골드, 횟수 제한 없음. 거래와 장터로 받는 블록은 한도를 넘으면 **받을 수 없고(거래 창에 올릴 수 없다)**, 우편으로 온 블록은 자리가 날 때까지 우편함에 머문다(online 2.4) [제안].
  - 직접 거래: 상대가 블록을 올리는 순간 서버가 "받는 쪽 서고 여유 ≥ 올린 블록 수 합계"를 검사하고, 넘으면 그 칸을 거절한다 [임시: "거래 창에 올릴 수 없다"의 구현]. 커밋 때 다시 검사한다.
  - 장터 구매: 산 블록은 우편으로 오므로 구매 자체는 된다. 우편 수령 때 서고 여유만큼만 받고 나머지는 우편에 남는다 [임시: 부분 수령].

### 6.4 감정가 (`Appraise`)

원문: 물건마다 감정가(7일 중앙값, 지팡이는 아래 공식)를 매긴다. **지팡이 감정가 = 그 단계까지 평균 강화 골드(제물 +15 기준, online 2.5 표의 값을 단계별로 표로 둔다) × 0.5. +0 지팡이의 감정가는 250골드(상점 값의 절반)**. 수수료 계산, 떠나보낼 때 돌려주는 골드(online 1.6), 이상 거래 탐지에 쓴다(online 5.5) [제안].

지팡이 감정가 표 `wand_appraisal.json` [제안 공식 / 표의 중간 단계 값은 이 명세서가 `docs/sim/enh.py`의 같은 모형(제물 +15, 씨앗 7, 2만 번)으로 다시 계산한 것. 10골드 단위 반올림은 [임시]]:

| 단계 | 평균 강화 골드(제물 +15, +0부터) | 감정가 = × 0.5 | 떠나보내기 환급 = 감정가 × 10% |
|---|---|---|---|
| +0 | 0 | **250**(고정) | **0**(원문: +0은 0골드) |
| +1 | 100 | 50 | 5 |
| +2 | 521 | 260 | 26 |
| +3 | 1,370 | 690 | 69 |
| +4 | 3,162 | 1,580 | 158 |
| +5 | 6,784 (원문 표 6,800) | 3,390 | 339 |
| +6 | 13,561 | 6,780 | 678 |
| +7 | 24,992 (원문 표 25,000) | 12,500 | 1,250 |
| +8 | 44,172 | 22,090 | 2,209 |
| +9 | 76,231 | 38,120 | 3,812 |
| +10 | 129,891 (원문 표 130,000) | 64,950 | 6,495 |
| +11 | 214,817 | 107,410 | 10,741 |
| +12 | 340,677 | 170,340 | 17,034 |
| +13 | 517,208 (원문 표 517,000) | 258,600 | 25,860 |

- 원문 표(online 2.5)에 있는 단계(+5, +7, +10, +13)는 원문 값을 써도 된다(차이 1% 이하). **구현은 위 표 하나만 데이터로 읽는다.** 감정가에는 변이·확장 슬롯·흔적을 더하지 않는다(원문 공식에 없다) [제안 그대로].
- +1 감정가(50)가 +0 감정가(250)보다 낮다. 원문 공식 그대로 옮긴 결과이고 19절 미정 U-07에 적었다.
- 겹쳐지는 물건과 꾸미기: `appraise(item_code) = 7일 거래 중앙값(market_sale.price/qty 기준)`. 7일 거래가 0건이면 [임시] 감정가 = `config`의 품목별 기본 감정가(`item_appraisal_default.json`, 처음에는 상점 값이 있으면 그 절반, 없으면 0).
- 골드의 감정가는 액면 그대로.

```
function Appraise(item, now):
    if item.Kind == Wand:  return item.Stage == 0 ? 250 : WandAppraisalTable[item.Stage]
    if item.Kind == Gold:  return item.Amount
    med = Median7d(item.ItemCode, now)        // 낱개 값의 중앙값
    unit = med ?? DefaultAppraisal(item.ItemCode)
    return unit * item.Qty
```

- 7일 중앙값은 `market_sale`에서 매시 정각에 다시 계산해 `market_price_stat(item_code, median_unit, volume_7d, n_7d, at)`에 둔다 [임시]. 지팡이는 캐릭터·단계별로 같은 표를 둔다(시세 조회용).

### 6.5 수수료 계산 (`ComputeTradeFee`)

원문:
- "직접 거래에서 물건끼리만 바꾸면 수수료를 피할 수 있으므로, 물건마다 감정가를 매겨 **주고받은 값이 큰 쪽의 5%를 양쪽이 반씩** 낸다."(online 5.5)
- "직접 거래 수수료: 오간 값의 5% (지팡이가 끼면 감정가의 10%)"(online 2.4)
- 품목 표의 수수료: 지팡이 10%, 나머지 5%(online 5.5)
- "오갈 때마다 감정가의 10% 수수료"(online 4.9, 지팡이 맡기기)

둘을 하나로 합친 식 [임시, 18절 충돌 C-01]:

```
function ComputeTradeFee(sideA_items, sideB_items):
    // 한쪽이 내놓은 것의 "수수료 기준액" = 물건마다 감정가 × 그 물건의 요율
    baseA = Σ over A's items: Appraise(i) * (i.Kind == Wand ? 0.10 : 0.05)   // 골드도 5%
    baseB = Σ over B's items: Appraise(i) * (i.Kind == Wand ? 0.10 : 0.05)
    fee = max(baseA, baseB)                 // "주고받은 값이 큰 쪽"
    each = Ceil(fee / 2)                    // 양쪽이 반씩. 각자 올림 [임시]
    return (feeA = each, feeB = each)
```

- 지팡이가 없을 때 이 식은 원문 "큰 쪽의 5%를 반씩"과 똑같다. 지팡이가 끼면 그 지팡이 감정가의 10%가 기준이 되어 online 2.4·4.9와 맞는다.
- 수수료는 각자의 **골드 잔액에서** 커밋 때 뺀다. 거래 창에 올린 골드와는 별도이고, 잠금을 누를 때 "올린 골드 + 내 수수료 ≤ 거래 가능 골드"를 검사한다 [임시]. 모자라면 잠글 수 없다.
- 수수료로 낸 골드는 사라진다(원장 `reason = trade_fee`, 상대에게 가지 않는다)(online 2.4 "골드가 사라지는 곳") [제안].
- 수수료 줄: 거래 창에 "양쪽이 낼 수수료"를 항상 보여 준다(ui 4.23) [제안].
- 정수 계산: 감정가 × bp ÷ 10,000을 먼저 물건마다 하지 않고 **합계를 만분율로 모은 뒤 한 번만 나눈다**(`fee = Ceil(max(ΣA_bp, ΣB_bp) / 10000)` 후 반으로) [임시]. 반올림 오차를 줄이고 테스트가 같은 값을 내게 한다.

### 6.6 하루 골드 주기 상한

- 하루에 주는 골드 합계 상한 = 계정 누적 획득 골드의 20%(online 5.5) [제안].
- "주는 골드" = 직접 거래 창에 올려 상대에게 넘어간 골드 + 선물로 준 골드(선물에 골드를 허용하는 경우, 9.5절) [임시]. 수수료와 장터 구매 대금은 세지 않는다 [임시].
- "누적 획득 골드" = `currency_ledger`에서 `bucket = 획득`이고 `delta > 0`이며 `reason`이 플레이 수입(판 정산, 결투 보상, 시즌 보상, 장터 판매 대금)인 것의 합 [임시]. 직접 거래로 받은 골드는 넣지 않는다 [임시](돌려막기로 상한을 키우지 못하게). 검증 대기분은 넣지 않는다.
- "하루" = 서버 시각 기준 하루. 경계는 한국 시각 0시 [임시](주간 한도도 서버 시각 기준이다, online 7.7).

---

## 7. 직접 거래

### 7.1 절차 (원문)

1. 상대에게 거래 신청 → 수락. 두 사람이 5칸 안에 있어야 한다.
2. 각자 물건과 골드를 올린다(각 8칸).
3. 양쪽이 **잠금**을 누른다. 한쪽이 내용을 바꾸면 양쪽 잠금이 풀리고, 바뀐 칸이 3초 동안 깜박인다.
4. 양쪽이 잠근 뒤 5초가 지나야 **최종 확인**이 켜진다. 확인 화면에 "내가 주는 것 / 받는 것"을 글자로 다시 보여 주고, 지팡이는 단계·변이·슬롯과 **역대 최고 단계·하락 횟수를 항상** 크게 적는다(흔적 공개 설정과 무관). 이 거래로 어느 쪽의 리그가 올라가면 그것도 적는다.
5. 양쪽이 확인하면 서버가 한 번의 트랜잭션으로 옮긴다.

(online 5.5) [제안]. 흔적이 항상 보이는 것은 [확정: DEC 1.15]. 확인 화면의 바꿔치기 방지(잠금 해제, 깜박임, 5초 대기)는 이 절차 그대로다(online 5.6).

부가 규칙(ui 4.23) [제안]:
- 지팡이를 올리면 그 칸 아래에 단계, 변이, 슬롯, 역대 최고 단계와 하락 횟수를 **항상** 적는다. 올린 사람 화면에는 "거래 상대에게는 흔적이 보인다" 한 줄(fx 1.3도 같다).
- 내 가방 탭: 블록 / 재료 / 꾸미기 / 지팡이 / 시즌 한정. 거래할 수 없는 것은 "놓을 수 없음" 칸 + 이유(끼워 둔 블록, 거래 잠금, 마지막 한 자루, 계정에 묶임). 계정에 묶인 재료는 같은 아이콘의 따로 된 칸 + 자물쇠.
- 최종 확인: 지팡이는 한 자루당 세 줄(이름과 단계 / 변이와 슬롯 / 역대 최고 +N · 하락 n번). 한쪽에 지팡이가 둘 이상이면 고른 줄의 큰 그림만 보이고 나머지는 아이콘.
- 손해 경고: 감정가 차이가 5배를 넘으면 "크게 손해 보는 거래입니다"를 띄우고 확인을 한 번 더 받는다(online 5.6). 호박색(빨강 아님)(ui 4.23).
- [그만두기]는 어느 상태에서든 거래를 끝낸다.

### 7.2 데이터 구조

```csharp
public sealed class TradeSession {          // 메모리 상태(wizard-api 안). DB에는 커밋 결과만 남는다(trade 표)
    Guid TradeId;
    long AId, BId;                          // AId < BId 가 아니어도 된다. 잠금 순서는 커밋 때 정한다
    TradeState State;
    TradeSide A, B;
    long BothLockedAtMs;                    // 0 = 아직
    long LastActivityMs;
    int Revision;                           // 내용이 바뀔 때마다 +1. 확인 요청은 이 번호를 같이 보낸다
}
public sealed class TradeSide {
    TradeSlot?[8] Slots;                    // 물건 8칸
    long Gold;                              // 0 이상. 칸을 쓰지 않는다
    bool Locked; bool Confirmed; bool LossWarningAcked;
    long BlinkUntilMs[8]; long GoldBlinkUntilMs;   // 바뀐 칸 3초 깜박임(표시용, 서버가 시각을 준다)
}
public sealed class TradeSlot {
    TradeItemKind Kind;                     // Block|EnhanceStone|Charm|Cosmetic|SeasonCosmetic|Wand
    string? ItemCode; int Qty;              // 겹쳐지는 물건
    long? InstanceId; long? WandId; int? ExpectedVersion;  // 개체 물건·지팡이: 올릴 때의 version
}
public enum TradeState { Requested, Open, OneLocked, BothLocked, ConfirmReady, Committing, Completed, Cancelled }
```

### 7.3 상태 기계

```
Requested ──(B 수락, 20초 안 [임시], 둘 다 5칸 안, 둘 다 TradeGate 통과)──▶ Open
Requested ──(B 거절 / 20초 지남 / A 취소 / 한쪽 광장 떠남)──▶ Cancelled

Open ──(한쪽 잠금)──▶ OneLocked ──(다른 쪽 잠금)──▶ BothLocked (BothLockedAtMs = now)
OneLocked/BothLocked/ConfirmReady ──(어느 쪽이든 내용 변경)──▶ Open
      · 양쪽 Locked = false, Confirmed = false, LossWarningAcked = false
      · 바뀐 칸 BlinkUntilMs = now + 3000
      · Revision += 1
OneLocked ──(잠근 쪽이 잠금 해제)──▶ Open
BothLocked ──(now - BothLockedAtMs >= 5000)──▶ ConfirmReady      // 5초 대기
ConfirmReady ──(양쪽 Confirmed, 손해 경고 대상이면 그 쪽 LossWarningAcked)──▶ Committing
Committing ──(트랜잭션 성공)──▶ Completed
Committing ──(검사 실패)──▶ Open  (양쪽 잠금 해제, 실패 사유를 두 사람에게)   [임시: 거래 창은 닫지 않는다]
(어느 상태든) ──(그만두기 / 5분 동안 아무 동작 없음 [임시] / 한쪽 접속 끊김 / 한쪽 광장 떠남)──▶ Cancelled
```

조건 세부:
- 신청 때와 수락 때 **둘 다 같은 인스턴스, 거리 ≤ 5칸**(`Dist2 <= 25`)이어야 한다 [제안 5칸, 경계 포함 [임시]]. 창이 열린 뒤에는 두 사람 모두 `Activity = Trading`이라 움직이지 않으므로 거리를 다시 재지 않는다 [임시].
- 한 계정은 동시에 거래 창 하나만 연다. 신청 중이거나 거래 중인 사람에게 온 신청은 "지금 거래 중" [임시].
- 차단 관계(어느 쪽이든)면 신청이 오지 않는다(online 5.3). 보낸 쪽에는 "상대가 받을 수 없다" [임시].
- 결투 대기열에 있어도 거래는 된다. 매치가 잡히면 거래 창은 Cancelled [임시](결투로 넘어가므로).
- 잠금 버튼은 그 쪽 내용이 검사(7.4)를 통과할 때만 눌린다.
- **확인 요청에는 `Revision`을 싣는다.** 서버의 Revision과 다르면 거절한다(그 사이 바뀐 내용을 확인한 것으로 처리하지 않는다) [임시].
- 5초 대기 시각은 서버가 잰다. 클라이언트 표시용으로 `ConfirmReadyAtMs = BothLockedAtMs + 5000`을 보낸다.
- 손해 경고 판정: `max(VA, VB) > 5 × min(VA, VB)`이고 `min > 0`일 때, 값이 작은 쪽을 **받는** 사람(손해 보는 쪽)에게 경고를 띄운다 [임시: 원문은 "차이가 5배를 넘으면"]. VA = A가 주는 것의 감정가 합(골드 포함). `min = 0`이면(한쪽이 비었다) 선물이므로 9.5절로 간다.
- 리그 변화 문구: 커밋 전 미리 계산 `IlService.PreviewHoldingIl(account, after = 이 거래 반영)`으로 **오르는** 경우에만 "이 거래로 리그가 맹수 → 후계로 올라갑니다"(online 4.7, 5.5) [제안]. 내준 쪽은 14일 뒤 내려가고 리그는 다음 시즌에 내려가므로 "리그 변화 없음(14일 뒤 보유 IL 재계산)" [임시 문구].

### 7.4 칸에 올릴 때의 검사 (`ValidateOffer`)

```
function ValidateOffer(owner, counterparty, slot):
    switch slot.Kind:
      Block/EnhanceStone/Charm/stacked Cosmetic:
          need tradable_qty(owner, code) - reserved_in_this_trade(owner, code) >= slot.Qty
          if Block and IsProjectile(code): need ProjectileCountAfter(owner) >= 1          // 마지막 투사체 블록
          if Block: need LibraryFree(counterparty) >= BlocksOfferedTo(counterparty) + slot.Qty   // 받는 쪽 서고 한도
      Cosmetic/SeasonCosmetic (instance):
          need inst.owner_id == owner, inst.bound == false, inst.origin != 유료, not pending, not equipped-on-wand(벗겨서 옷장에 있어야) [임시]
          if HeirOnly(inst.item_code): reject(BoundHeir)
      Wand: WandTransferable(owner, wand)  (8.1절)
    gold: need slot.Gold + FeeEstimate(owner) <= tradable_gold(owner)
          need GoldGivenToday(owner) + slot.Gold <= 0.20 × LifetimeEarnedGold(owner)
    // 공통
    need not pending (검증 대기 중인 보상이 아니다: qty_pending, wand.pending_run_id is null, currency amount_pending 제외)
```

- 거래 가능 골드 = `currency_balance(골드, 획득).amount - amount_pending` [임시].
- 끼워 둔 블록이 걸리면 거래 창이 "전부 빼기"를 대신 해 준다 — 지팡이를 올릴 때 그 지팡이의 끼운 블록 전부를 서고로 돌린다(online 5.5 지팡이 규칙) [제안]. 이 동작은 별도 요청(`LoadoutClearAll(wand_id)`)이고 거래 커밋과 다른 트랜잭션이다 [임시]. 서고 한도와 무관하게 돌아온다(끼운 블록은 원래 서고 개수에서 빠져 있었으므로 한도를 넘을 수 있다. 넘은 상태에서는 새로 받기만 막힌다) [임시].

### 7.5 커밋 (트랜잭션 의사코드)

원문(online 5.6) [제안]: 양쪽 계정 행을 계정 번호 순으로 잠그고(교착 방지), 양쪽 물건과 골드의 소유·수량·잠금 상태를 다시 확인한 뒤 옮기고, 원장에 적고, 커밋한다. 하나라도 어긋나면 전체가 취소된다.

```sql
BEGIN;  -- 격리 수준 READ COMMITTED + 명시적 행 잠금 [임시]
-- 0. 멱등: 두 사람이 같은 TradeId로 확인을 보내도 커밋은 한 번. trade.id = TradeId (PK)
SELECT 1 FROM trade WHERE id = :trade_id;           -- 있으면 저장된 결과를 돌려주고 끝
-- 1. 계정 잠금: 번호 작은 쪽부터
SELECT ... FROM account WHERE id IN (:lo, :hi) ORDER BY id FOR UPDATE;
-- 2. 물건 행 잠금 (항상 같은 순서: currency_balance → item_stack(account_id, item_code 순) → item_instance(id 순) → wand(id 순))
SELECT ... FROM currency_balance WHERE account_id IN (:lo,:hi) AND currency='gold' ORDER BY account_id FOR UPDATE;
SELECT ... FROM item_stack WHERE (account_id, item_code) IN (...) ORDER BY account_id, item_code FOR UPDATE;
SELECT ... FROM item_instance WHERE id IN (...) ORDER BY id FOR UPDATE;
SELECT ... FROM wand WHERE id IN (...) ORDER BY id FOR UPDATE;
-- "마지막 한 자루" 검사를 위해 내놓는 지팡이 캐릭터의 그 계정 지팡이 전부도 잠근다
SELECT ... FROM wand WHERE owner_id = :giver AND character = :c ORDER BY id FOR UPDATE;
-- 3. 다시 검사 (TradeGate 둘 다, ValidateOffer 전부, wand.version == ExpectedVersion, lock_state = 없음, 하루 골드 상한, 서고 한도, 수수료 골드)
-- 4. 옮기기
--   골드: A -gA -feeA, B +gA ; B -gB -feeB, A +gB   (currency_balance 갱신 + currency_ledger 4~6줄, reason = trade_give / trade_receive / trade_fee)
--   겹쳐지는 물건: item_stack qty 증감 (qty >= 0 제약이 마지막 방어선) + item_ledger 2줄씩 (reason = trade)
--   개체 물건: item_instance.owner_id 변경, version += 1 + item_ledger
--   지팡이: 8.2절 TransferWand (owner_id, 상태 초기화, version += 1) + item_ledger
-- 5. 기록
INSERT INTO trade (id, a_id, b_id, a_gives, b_gives, fee, appraised_a, appraised_b, at) VALUES (...);
--   든 지팡이 자동 교체(8.2), 보유 IL 재계산 표시(IlService.OnWandReceived / OnWandGiven), 최근 만난 사람 갱신
COMMIT;
```

- `fee` 칸에는 두 사람의 수수료를 json `{a: feeA, b: feeB}`로 적는다 [임시]. `appraised_a/b`는 각자 내놓은 것의 감정가 합이다.
- 커밋 실패 시 어떤 행도 바뀌지 않는다. 서버는 실패 이유(`NotOwner`, `QtyChanged`, `WandVersionChanged`, `GateClosed`, `LibraryFull`, `GoldCapExceeded`, `InsufficientGoldForFee`, `LastWand`, `LastProjectileBlock`, `Locked`)를 두 사람에게 보낸다.
- 거래 화면 도중 서버가 꺼지면 커밋 전이면 없던 일이 된다(트랜잭션)(online 7.7) [제안].
- 보유 IL과 리그 갱신은 같은 트랜잭션 안에서 `il_history`와 `account.league`에 반영한다 [임시](8.3절).

---

## 8. 지팡이 거래 규칙

각 캐릭터는 전용 지팡이만 쓴다. 모든 계정이 캐릭터 5명을 다 가지고 있으므로, 암흑 지팡이를 사면 내 그믐이 쓴다(online 5.5) [조율: DEC 2.4 "거래로 받은 지팡이는 해당 캐릭터로 써야 한다"].

### 8.1 내보낼 수 있는가 (`WandTransferable`)

```
function WandTransferable(owner, wand, channel /* Direct | Market | Gift */):
    require wand.owner_id == owner
    require wand.lock_state == None                 // 원정 중·결투 중·장터·우편·거래 중이면 불가
    require wand.pending_run_id is null             // 검증 대기 중 보상으로 강화했으면 통과로 끝날 때까지 잠김(online 3.2 5번)
    require now >= wand.trade_locked_until          // 받은 뒤 7일
    require not AccountHasDebt(owner)               // 갚을 것이 남아 있으면 무효 보상으로 강화한 지팡이는 0이 될 때까지 잠김
    require InHandCount(owner, wand.character) - 1 >= 1     // 마지막 한 자루
    if channel == Market: require wand.stage >= 1           // +0은 장터 불가(직접 거래와 선물은 된다)
    require wand.loadout is empty   // 끼워 둔 블록은 전부 빼야 한다(거래 창·장터 등록 화면이 "전부 빼기"를 대신 해 준다)

function InHandCount(owner, character):
    // "손에 있는 지팡이" = 보관 중이거나 원정·결투 중인 것.
    // 장터에 올려 둔 것과 우편함에서 아직 꺼내지 않은 것은 세지 않는다(online 1.6)
    return count wand where owner_id = owner and character = character and lock_state in (None, 원정, 결투)
           and wand is not attached to an unclaimed mail
```

| 항목 | 거래되면 | 출처 |
|---|---|---|
| 받는 쪽의 자리 | 필요 없다. 개수 제한이 없으므로 언제든 받는다 | (online 5.5, 1.6) [확정: DEC 1.22] |
| 주는 쪽의 마지막 한 자루 | 캐릭터의 마지막 지팡이는 팔거나 줄 수 없다(손에 있는 지팡이가 캐릭터마다 1자루는 남아야 한다). 먼저 골드 상점에서 +0 지팡이(500골드)를 사서 두 자루로 만든 뒤에 판다. 그 지팡이가 든 지팡이였다면 남은 지팡이 중 단계가 가장 높은 것이 자동으로 든 지팡이가 된다 | (online 5.5) [제안] |
| 강화 단계, 변이, 확장 슬롯 | 그대로 넘어간다 | 같음 |
| 장인의 기운, 슬롯 새기기의 누적 확률 | 0이 된다 | 같음 |
| 하락 바닥의 기준 | 현재 단계로 다시 잡힌다(바닥이 현재 −2가 된다). 새 주인이 그 뒤에 올린 최고 단계부터 다시 따라간다 | 같음 |
| 흔적 기록 | 원래의 역대 최고 단계와 하락 횟수는 지워지지 않고 지팡이를 따라간다. 거래해도 흔적은 사라지지 않는다. 새 주인이 원래의 역대 최고 단계에 다시 닿으면 흔적 겹그림이 사라지는 것은 지팡이 문서의 규칙 그대로다 | 같음 |
| 흔적 공개 설정 | 끔으로 돌아간다. 새 주인이 다시 고른다. **거래 창, 장터 목록과 상세, 최종 확인에서는 이 설정과 무관하게 흔적이 항상 보인다** | 같음 [확정: DEC 1.15] |
| 끼워 둔 블록 | 거래 전에 전부 빼야 한다. 거래 창과 장터 등록 화면이 "전부 빼기"를 대신 해 준다 | 같음 |
| 겉모습 | 단계 모양은 그대로. 덧씌운 꾸미기 겉모습은 벗겨져 주는 쪽의 옷장으로 돌아가고, 따로 거래한다 | 같음 |
| 거래 잠금 | 받은 뒤 7일 동안 다시 거래할 수 없고 떠나보낼 수도 없다. 쓰고 강화하는 것은 바로 된다 | 같음 |
| 이력 | 지팡이 정보에 "주인 n번째"가 보인다 | 같음 |
| 원정 중·결투 중·검증 대기 중 | 거래 불가 | 같음 |
| 보유 IL과 리그 | 받은 쪽은 즉시 오르고 리그도 즉시 올라갈 수 있다. 준 쪽의 보유 IL은 14일 뒤에 내려가고 리그는 다음 시즌 시작에 내려간다. 거래 확인 창에 리그가 바뀌는지를 적는다 | (online 5.5, 4.7, 4.8) [확정: DEC 1.9 리그 기준 / 제안: 시점] |
| 비기 해금, 기록 | 넘어가지 않는다. 캐릭터에 묶인 것이다 | (online 5.5, 1.6) [조율: DEC 2.8] |

### 8.2 옮기기 (`TransferWand`, 트랜잭션 안)

```
function TransferWand(wand, from, to, now):
    skin = wand.skin_id
    wand.owner_id          = to
    wand.aura              = 0                 // 장인의 기운
    wand.carve_bonus       = 0                 // 슬롯 새기기 누적 확률
    wand.floor_peak        = wand.stage        // 하락 바닥 기준 = 현재 단계 → 바닥 = stage - 2 (+3 이하 하락 없음은 그대로)
    // wand.record_peak, wand.drop_count 는 그대로 (흔적 기록)
    wand.show_scars        = false
    wand.loadout           = []                // 이미 비어 있어야 한다(검사 통과 조건). 방어적으로 비운다
    wand.skin_id           = null
    if skin != null: WardrobeReturn(from, skin)   // 주는 쪽 옷장으로
    wand.owner_count      += 1                 // "주인 n번째"
    wand.trade_locked_until = now + 7d
    wand.origin            = 거래              // 원문 enum(시작/상점/거래)
    wand.lock_state        = None              // 장터 구매는 우편 첨부라 '우편'(8.4)
    wand.version          += 1
    if character_state(from, wand.character).equipped_wand_id == wand.id:
        next = argmax over from's remaining in-hand wands of that character by (stage desc, record_peak desc, id asc)   // 동률 규칙 [임시]
        character_state(from, wand.character).equipped_wand_id = next.id
    item_ledger += (from, wand, -1, reason = trade_out), (to, wand, +1, reason = trade_in)
```

- `floor_peak`의 뜻: 하락 바닥 = `floor_peak − 2`(online 1.3) — 새 주인이 올리면 `floor_peak = max(floor_peak, 새 단계)`로 따라간다(강화 명세서 몫).
- 받은 쪽의 든 지팡이는 바꾸지 않는다 [임시](받는 순간 손에 든 것이 바뀌면 혼란). 받은 쪽에 그 캐릭터의 지팡이가 원래 1자루 이상 있으므로(캐릭터마다 시작 지팡이) 든 지팡이는 항상 있다.

### 8.3 보유 IL과 리그 (이 영역이 부르는 쪽)

| 일 | 보유 IL | 리그 | 출처 |
|---|---|---|---|
| 지팡이를 받았다(샀다) | 거래가 확정된 순간 다시 계산한다. 장터에서 산 것은 우편함에서 꺼내기 전이라도 산 순간부터 센다 | 경계를 넘으면 **즉시** 위 리그로 올라간다. 아래 리그의 자동 매치 대기열에 있었다면 대기열에서 빠진다. 이미 시작한 경기는 그대로 끝낸다 | (online 4.7) [제안] |
| 지팡이를 내줬다(팔았다, 줬다, 떠나보냈다) | 그 지팡이가 14일 최고값을 만들고 있었다면 14일 뒤에 남은 지팡이와 블록으로 다시 계산한 값으로 내려간다 | 14일 동안 그대로. 값이 내려가도 리그가 내려가는 것은 다음 시즌 시작 | 같음 |
| 장터에 올렸다 | 변하지 않는다(올려 둔 동안은 내 것이다). 팔린 순간부터 "내줬다" | 변하지 않는다 | 같음 |
| 블록을 사고팔았다 | 지팡이와 같은 규칙 | 같다 | 같음 |

- 우편함에 있는 것, 장터에 올려 둔 것, 거래 잠금 중인 것, 검증 대기 중인 것도 보유 IL에 넣는다(online 4.7) [제안].
- 인터페이스 [임시]: `IlService.OnHoldingsChanged(accountId, tx)` — 같은 트랜잭션 안에서 그 계정의 지팡이별 값을 다시 계산하고 `il_history(account_id, day, holding_il)`의 오늘 행을 `max`로 갱신한다. 리그 상향은 `LeagueService.RaiseIfNeeded(accountId, tx)`가 하고, 자동 매치 대기열 제거는 커밋 뒤 이벤트(`league_raised`)로 매치메이커에 알린다.

### 8.4 장터와 우편에서의 지팡이

- 장터 등록: `lock_state = 장터`. 팔리면 구매자에게 우편으로 오고(`owner_id = buyer`, `lock_state = 우편`), 산 순간부터 구매자의 것으로 센다(online 1.6, 4.7) [제안]. 우편에서 꺼내면 `lock_state = None`.
- 7일 거래 잠금은 **산 순간**부터 센다 [임시](우편 수령 시각이 아니다).
- 안 팔린 지팡이는 우편함으로 돌아온다(`lock_state = 우편`, owner는 그대로).
- 판 지팡이 쪽의 "마지막 한 자루" 검사는 **등록할 때** 한다(등록된 지팡이는 손에 있는 것으로 치지 않으므로, 등록 뒤 손에 1자루 이상이 남아야 한다) [임시: online 1.6의 계산 규칙을 등록 시점에 적용].

---

## 9. 장터

### 9.1 규칙 (원문)

| 규칙 | 값 | 출처 |
|---|---|---|
| 방식 | 파는 사람이 값을 정해 올리고, 사는 사람이 그 값에 산다. **입찰은 두지 않는다** | (online 5.5) [제안] |
| 등록 기간 | 48시간 | 같음 |
| 동시 등록 | 계정당 20건(모든 품목 합계). 화면 "12 / 20" | (online 5.5, ui 4.23) [제안] |
| 보증금 | 등록할 때 값의 1%. 팔리면 돌려받는다. 안 팔리면 물건은 우편함으로 돌아오고 보증금은 사라진다 | (online 5.5) [제안] |
| 지팡이 하한 | +1 이상만. +0 지팡이는 상점에서 500골드에 언제든 살 수 있으므로 장터에 올리지 못한다(직접 거래와 선물은 된다) | (online 5.5, 1.6) [제안] |
| 묶음 | 겹쳐지는 물건(블록, 강화석, 부적)은 묶음으로 올리고 **일부만 살 수 있다** | (online 5.5) [제안] |
| 시세 | 종류별로 최근 7일 거래 중앙값과 거래량을 보여 준다 | 같음 |
| 가격 범위 | 등록가는 7일 중앙값의 0.3배~5배 안이어야 한다. 거래 이력이 30건 미만인 물건과 지팡이는 범위 없음, 대신 감사 대상 | 같음 |
| 지팡이 흔적 | 목록과 상세 모두에 흔적 겹그림, 역대 최고 단계, 하락 횟수가 **항상** 보인다 | 같음 [확정: DEC 1.15] |
| 수수료 | 판매가의 5% (지팡이는 10%) | (online 2.4) [제안] |
| 서버가 꺼져 있던 때 | 서버가 꺼져 있던 시간만큼 모든 등록의 만료를 늦춘다 | (online 7.7) [제안] |
| 왜 이 한도인가 | 지팡이를 개수 제한 없이 갖게 되었으므로 이 등록 한도와 보증금이 장터가 지팡이로 넘치는 것을 막는 장치다 | (online 5.5, 1.6) |

화면 동작(ui 4.23) [제안]:
- 탭: 사기 / 팔기 / 내 등록 / 시세. 분류: 블록(종류와 등급), 강화석, 부적, 꾸미기, 지팡이(캐릭터 다섯), 시즌 한정. 찾기와 정렬(값, 남은 시간, 단계).
- 목록 줄: 아이콘, 이름, 수량, 낱개 값, 남은 시간, 파는 사람. 지팡이 줄에는 단계 묶음 테와 흔적 딱지가 **항상**.
- 상세: 지팡이는 큰 그림(흔적 겹그림 포함), "역대 최고 +N, 지금 +M", 하락 횟수, 주인 n번째, 거래 잠금 안내. 최근 7일 거래 중앙값과 거래량, 시세 그림, 수량 조절, [사기].
- 팔기 탭: +0 지팡이, 마지막 한 자루, 거래 잠금 지팡이는 "놓을 수 없음" + 이유 툴팁("+0 지팡이는 장터에 올릴 수 없다. 직접 거래는 된다"). 값 입력, 보증금과 수수료, 받을 금액, 등록 기간, 값 범위 안내. 범위를 벗어나면 입력 칸이 오류 상태.
- 내 등록 탭: 남은 시간, [내리기].
- 구매 확인: 물건, 수량, 합계. 지팡이면 흔적 줄을 한 번 더. 리그가 오르면 "이 거래로 리그가 … 올라갑니다"(online 4.7).

### 9.2 데이터

```csharp
// market_listing (online 7.9: id, seller_id, item_ref, qty, unit_price, deposit, expires_at, status) + [임시] 칸
public sealed class MarketListing {
    long Id; long SellerId;
    ItemRef Item;            // {kind, item_code} 또는 {kind = Wand, wand_id} 또는 {kind = Cosmetic, instance_id}
    int QtyInitial;          // [임시] 등록 수량
    int Qty;                 // 남은 수량. 0이면 다 팔림
    long UnitPrice;          // 낱개 값(골드, ≥ 1)
    long Deposit;            // 보증금 전체(등록 때 낸 것)
    long DepositRefunded;    // [임시] 지금까지 돌려준 보증금
    DateTime ListedAt;       // [임시]
    DateTime ExpiresAt;      // ListedAt + 48h + 서버 정지 보정
    ListingStatus Status;    // Active | SoldOut | Expired | Cancelled
    bool AuditFlag;          // [임시] 가격 범위 없이 올라간 등록(이력 30건 미만, 지팡이)
    // 지팡이 등록은 등록 시점의 표시 스냅샷을 같이 둔다(목록 성능용): stage, record_peak, drop_count, mutations, slots_extra, owner_count
    string? WandSnapshotJson;
}
// market_sale (online 7.9): listing_id, buyer_id, qty, price, fee, at
public enum ListingStatus { Active, SoldOut, Expired, Cancelled }
```

### 9.3 등록 (`MarketList`)

```
function MarketList(seller, item, qty, unitPrice, requestId):
    Idempotent(seller, requestId)
    BEGIN
      lock account(seller); lock the item rows (item_stack / item_instance / wand)
      require TradeGate(seller).ok               // 보안 잠금 24시간 포함
      require count(listing where seller_id = seller and status = Active) < 20
      require unitPrice >= 1, qty >= 1
      if item is Wand:  require WandTransferable(seller, wand, Market) and wand.stage >= 1 and qty == 1
      if stacked:       require tradable_qty(seller, code) >= qty ; if projectile block: last-projectile rule
      if instance:      require not bound, not 유료, not heir-only
      stat = market_price_stat(item key)
      if item is not Wand and stat.n_7d >= 30:
          require 0.3 × stat.median_unit <= unitPrice <= 5 × stat.median_unit     // 정수: unitPrice × 10000 >= 3000 × median 등
          audit = false
      else: audit = true                                                          // 범위 없음, 감사 대상
      total   = unitPrice × qty
      deposit = Ceil(total × 1%)  (최소 1골드) [임시]
      require tradable_gold(seller) >= deposit
      gold -= deposit (currency_ledger reason = market_deposit)
      move item: stacked → item_stack.qty -= qty (item_ledger reason = market_list) ; wand/instance → lock_state = 장터
      insert listing(Status = Active, ExpiresAt = now + 48h, ...)
    COMMIT
```

- "거래 이력 30건"의 단위 [임시]: 그 물건 종류(`item_code`)의 최근 7일 `market_sale` 건수. "7일 중앙값"과 같은 창을 쓴다.
- 지팡이는 항상 범위가 없다(원문). 지팡이 시세는 캐릭터·단계별 중앙값을 시세 탭에 보여만 준다.
- 묶음으로 올린 겹쳐지는 물건은 낱개 값(`unit_price`)으로 등록한다(ui 4.23 "낱개 값") [제안].

### 9.4 구매 (`MarketBuy`)

원문(online 5.6) [제안]: 등록 행(`market_listing`)을 잠근 뒤 남은 수량을 확인하고, 수량을 줄이고, 대금을 우편으로 만들고, 원장에 적는다. **두 사람이 마지막 한 개를 동시에 사도 한 명만 성공한다.**

```
function MarketBuy(buyer, listingId, qty, expectedUnitPrice, requestId):
    Idempotent(buyer, requestId)
    BEGIN
      // 잠금 순서: listing → 계정(번호 순) → 잔액
      L = SELECT * FROM market_listing WHERE id = :listingId FOR UPDATE
      require L.Status == Active and now < L.ExpiresAt
      require L.SellerId != buyer                         // 자기 물건은 못 산다 [임시]
      require 1 <= qty <= L.Qty
      require L.UnitPrice == expectedUnitPrice            // 화면에서 본 값과 같아야 한다 [임시]
      if L.Item is Wand: require qty == 1
      lock account rows (buyer, seller) ORDER BY id
      require TradeGate(buyer).ok except SecurityHold     // 보안 잠금은 "거래와 장터 등록"만 막으므로 구매는 된다 [임시]
      price = L.UnitPrice × qty
      require tradable_gold(buyer) >= price
      fee_bp = L.Item is Wand ? 1000 : 500
      fee = Ceil(price × fee_bp / 10000) [임시: 올림]
      depositBack = (L.Qty - qty == 0) ? (L.Deposit - L.DepositRefunded)
                                       : Floor(L.Deposit × qty / L.QtyInitial)          // 팔린 만큼 비례 환급 [임시]
      L.Qty -= qty; L.DepositRefunded += depositBack; if L.Qty == 0: L.Status = SoldOut
      buyer gold -= price                                 (ledger: market_buy)
      mail to buyer:  payload = 물건(qty) 또는 지팡이(wand_id)    reason = market_bought
          wand: owner_id = buyer, lock_state = 우편, TransferWand 규칙 적용(8.2), trade_locked_until = now + 7d
      mail to seller: payload = gold(price - fee + depositBack)   reason = market_proceeds   (fee는 사라진다, ledger: market_fee)
      insert market_sale(listing_id, buyer_id, qty, price, fee, at = now)
      IlService.OnHoldingsChanged(buyer) ; IlService.OnHoldingsChanged(seller)  // 산 순간부터 센다 / 판 쪽은 14일 규칙
    COMMIT
```

- 판매 대금은 판 사람의 우편으로 간다(online 5.2 "우편(장터 대금, 산 물건, 돌아온 물건, 보상)") [제안]. 대금 골드는 우편에서 받기 전에는 잔액이 아니다 [임시].
- 판매 대금의 골드는 "누적 획득 골드"(6.6)에 넣는다 [임시].
- 일부만 살 수 있는 것은 겹쳐지는 물건뿐이다. 지팡이와 개체 꾸미기는 수량 1.

### 9.5 내리기와 만료

```
function MarketCancel(seller, listingId):        // [내리기]
    lock listing; require seller == L.SellerId and L.Status == Active
    L.Status = Cancelled
    mail to seller: 남은 물건(L.Qty) reason = market_returned
    // 보증금: 남은 수량 몫은 사라진다 [임시](원문 "안 팔리면 보증금은 사라진다"를 내린 경우에도 적용)

function MarketExpireSweep(now):                 // 1분마다 [임시]
    for L in listing where Status = Active and ExpiresAt <= now (FOR UPDATE SKIP LOCKED):
        L.Status = Expired; mail to seller: 남은 물건 reason = market_returned
        // 남은 보증금(L.Deposit - L.DepositRefunded)은 사라진다 (ledger 기록 없음: 등록 때 이미 빠졌다)

function OnServerResume(downtimeMs):             // 서버가 꺼져 있던 시간만큼 만료를 늦춘다(online 7.7)
    UPDATE market_listing SET expires_at = expires_at + downtimeMs WHERE status = 'Active'
```

- 서버가 꺼져 있던 시간 = 마지막 하트비트(`config.last_heartbeat_at`, 10초마다 갱신 [임시])와 다시 켠 시각의 차이.

### 9.6 시세 조회

- 좌판 거리의 시세 조회는 거래 조건(6.2)과 무관하게 누구나(online 5.5) [제안]. 내용: 종류별 최근 7일 거래 중앙값과 거래량(online 5.5), 시세 그림(꺾은선과 막대는 코드로 그린다)(ui 4.23).
- 중앙값 정의 [임시]: 최근 7일 `market_sale`의 낱개 값(`price / qty`)을 수량 가중 없이 건별로 모은 중앙값. 건수가 짝수면 가운데 둘의 평균을 버림.

---

## 10. 선물

- 한쪽만 주는 거래(선물)는 **친구가 된 지 7일이 지난 사이에서만** 되고 수수료는 감정가의 5%다(online 5.5) [제안]. 지팡이 선물은 10% [임시: online 4.9 "오갈 때마다 감정가의 10%"와 품목 표의 지팡이 10%에 맞춤, 충돌 C-02].
- 선물도 6.2절 조건을 따른다(online 5.5 "직접 거래, 장터, 선물은 … 뒤에 열린다").
- 귀속 재료는 선물과 우편 첨부로도 넘길 수 없다(online 5.5).

경로 [임시, 원문이 화면을 정하지 않음 — 19절 U-10]:
1. **직접 거래 창에서 한쪽만 올린 경우**: 커밋 전 선물로 다룬다. 친구 7일 조건을 검사하고, 수수료는 주는 쪽이 전부 낸다(`fee = Ceil(Σ 감정가 × 요율)`), 받는 쪽 수수료 0.
2. **우편 선물**: 친구 창의 [선물 보내기] → 물건·골드 고르기 → 우편으로 간다(`mail.reason = gift`). 광장에 있지 않아도 된다. 수수료는 주는 쪽이 낸다.

```
function SendGift(giver, receiverId, items, gold, requestId):
    Idempotent(giver, requestId)
    BEGIN lock accounts (id 순), item rows
      require TradeGate(giver).ok and TradeGate(receiver).ok [임시: 받는 쪽도 조건을 채워야 한다 — 갓 만든 계정으로 몰아주기를 막는 원래 목적]
      require Friends(giver, receiver) and now - friend.since >= 7d
      require not Blocks(receiver, giver)
      validate each item as ValidateOffer (마지막 한 자루, 마지막 투사체, 귀속, 대기, 끼움)
      fee = Ceil(Σ Appraise(i) × rate(i))  (골드 포함 5%, 지팡이 10%)
      require GoldGivenToday(giver) + gold <= 20% LifetimeEarned(giver)
      require tradable_gold(giver) >= gold + fee
      move: 물건은 giver에서 빼서 mail payload로 (wand: lock_state = 우편, TransferWand)
      insert mail(account_id = receiver, payload, reason = gift, sender = giver)
      insert trade(a = giver, b = receiver, a_gives = items+gold, b_gives = [], fee = {a: fee, b: 0}, ...)
    COMMIT
```

- 받는 쪽의 블록 서고 한도: 우편으로 오므로 자리가 날 때까지 우편함에 머문다(online 2.4).

---

## 11. 우편함

### 11.1 구성

- 까마귀 우편함(시설)과 메뉴(Esc), 메인 메뉴에서 연다(online 5.2, ui 4.24) [제안]. 내용: 친구 목록, 쪽지, 우편(장터 대금, 산 물건, 돌아온 물건, 보상)(online 5.2).
- 탭: 우편 / 쪽지 / 보낸 쪽지. 우편 줄: 봉투 상태, 보낸 이(장터, 느루, 운영자, 친구), 제목, 남은 보관 기간(ui 4.24) [제안].
- 첨부: 물건과 골드. [받기] [모두 받기]. **검증 대기 중인 보상은 잠김 칸 + "검증이 끝나면 받는다"**(ui 4.24) [제안].
- 쪽지 쓰기: 받는 이(친구에서 고르기), 한 줄 제목, 본문(ui 4.24) [제안]. 쪽지는 친구에게만 [임시: "친구에서 고르기"].

### 11.2 데이터

```csharp
// mail (online 7.9: id, account_id, payload(json), reason, expires_at, claimed_at) + [임시] 칸
public sealed class Mail {
    long Id; long AccountId;
    MailReason Reason;         // MarketProceeds | MarketBought | MarketReturned | Reward | SeasonReward | Gift | Operator | Note | Refund [임시]
    MailSender Sender;         // Market | Nuru | Operator | Friend(account_id)
    long? SenderAccountId;
    string Title; string Body; // 쪽지: 제목 1줄 [임시 최대 24자], 본문 [임시 최대 400자], 같은 채팅 필터를 거친다
    MailPayload Payload;       // { gold: long, stacks: [{item_code, qty}], instances: [id], wands: [id] }
    bool PendingVerify;        // 검증 대기 보상. true면 받을 수 없다 [임시: run_id 참조]
    long? PendingRunId;
    DateTime CreatedAt; DateTime? ExpiresAt;   // 쪽지 30일 [임시]. 첨부 있는 우편은 null(만료 없음) [임시]
    DateTime? ReadAt; DateTime? ClaimedAt;
}
```

### 11.3 받기 (`ClaimMail`)

원문(online 5.6) [제안]: `UPDATE mail SET claimed_at = now() WHERE id = ? AND claimed_at IS NULL`이 한 행을 바꿨을 때만 지급한다.

```
function ClaimMail(account, mailId, requestId):
    Idempotent(account, requestId)
    BEGIN
      lock account(account)
      m = SELECT * FROM mail WHERE id = :mailId AND account_id = :account FOR UPDATE
      require m.PendingVerify == false
      // 블록 서고 한도: 받을 수 있는 만큼만 [임시: 부분 수령]
      free = LibraryFree(account)
      blocks = m.Payload.stacks where IsBlock
      if Σ blocks.qty > free:
          // 블록은 들어갈 만큼만 받고 나머지는 같은 우편에 남긴다. 다른 첨부는 다 받는다
          take = SplitBlocks(blocks, free) ; m.Payload = remaining ; grant(take + non-block parts)
          (claimed_at은 그대로 NULL, payload 갱신, 버전 갱신)
      else:
          n = UPDATE mail SET claimed_at = now() WHERE id = :mailId AND claimed_at IS NULL
          if n != 1: return AlreadyClaimed (저장된 결과)
          grant(all): gold → currency_balance + ledger(reason = mail_claim, ref = mail id)
                      stacks → item_stack + item_ledger
                      wands → lock_state = None (owner는 이미 account)
                      instances → owner 확인
    COMMIT

function ClaimAll(account): 받을 수 있는 우편마다 ClaimMail을 하나의 요청 번호 아래 차례로(각 우편은 각자의 트랜잭션) [임시]
```

- 검증 대기 표시가 풀리면(검증 통과) `PendingVerify = false`로 바꾼다. 실패하면 우편째 회수한다(online 3.2 6번) [임시: 우편 행 삭제 + ledger 회수].
- 계정에 "갚을 것"이 있으면 받은 골드·물건에서 먼저 뺀다(online 3.2 6번 "다음 획득분에서 먼저 뺀다") [제안]. 단, 판매 대금처럼 플레이 수입이 아닌 것도 "획득분"으로 볼지는 19절 U-14 [임시: 본다].
- 우편함의 지팡이는 손에 있는 1자루로 세지 않는다(online 1.6) [제안]. 보유 IL에는 센다(online 4.7) [제안].

---

## 12. 사기와 복제 방지

### 12.1 원칙 (원문 그대로)

| 원칙 | 내용 | 출처 |
|---|---|---|
| 인벤토리 위치 | **서버 프로필의 인벤토리는 서버에만 있다.** 클라이언트는 화면에 그릴 뿐이고, 모든 변경은 서버 함수 호출이다 | (online 5.6) [제안] |
| 트랜잭션 | **서버 함수는 전부 C#이고, 재화와 물건을 건드리는 함수는 PostgreSQL 트랜잭션 하나(`BEGIN … COMMIT`) 안에서 돈다.** 바꿀 행은 `SELECT … FOR UPDATE`로 먼저 잠근다. 강화, 거래, 장터, 정산, 우편 수령, 슬롯 새기기, 상점 구매가 모두 이 규칙을 따른다 | (online 5.6, 7.1) [제안] |
| 원장 | **원장은 하나다.** 재화는 `currency_ledger`, 물건은 `item_ledger`에만 적는다. 다른 지갑이나 저장소 기능은 없다. 둘 다 추가만 한다 | (online 5.6, 7.9) [제안] |
| 직접 거래 한 건 | 양쪽 계정 행을 계정 번호 순으로 잠그고(교착 방지), 양쪽 물건과 골드의 소유·수량·잠금 상태를 다시 확인한 뒤 옮기고, 원장에 적고, 커밋한다. 하나라도 어긋나면 전체가 취소된다 | (online 5.6) |
| 장터 구매 | 등록 행을 잠근 뒤 남은 수량을 확인하고, 수량을 줄이고, 대금을 우편으로 만들고, 원장에 적는다. 두 사람이 마지막 한 개를 동시에 사도 한 명만 성공한다 | (online 5.6) |
| 우편 수령 | `UPDATE mail SET claimed_at = now() WHERE id = ? AND claimed_at IS NULL`이 한 행을 바꿨을 때만 지급한다 | (online 5.6) |
| 멱등 키 | 모든 변경 요청에는 클라이언트가 만든 요청 번호가 붙는다. `request(account_id, request_id)`에 유일 제약을 걸고 처음 결과를 저장한다. 같은 번호가 다시 오면 저장된 결과를 돌려준다. 30일 보관한다. 판 정산은 `run_id`, 결제는 `order_id`가 같은 역할을 한다 | (online 5.6, 7.9) |
| 개체 | 지팡이와 개체 꾸미기는 개체마다 고유 번호와 소유자, 버전을 가진다. 겹쳐지는 물건은 `수량 >= 0` 제약을 건다 | (online 5.6) |
| 바꿔치기 방지 | 잠금 해제, 깜박임, 5초 대기는 직접 거래 절차 그대로 | (online 5.6) |
| 손해 경고 | 감정가 차이가 5배를 넘으면 "크게 손해 보는 거래입니다"를 띄우고 확인을 한 번 더 받는다 | (online 5.6) |
| 강화 | 강화 결과는 서버가 난수를 뽑는다. 요청에서 결과까지가 한 트랜잭션이고, 시도마다 확률 내역과 뽑힌 값을 `enhance_log`에 남긴다 | (online 5.6) — 강화 명세서 몫 |
| 클라이언트 계정 번호 | `wizard-api`의 모든 요청은 접근 토큰을 확인한 뒤에야 계정 번호를 얻는다. 클라이언트가 보낸 계정 번호는 믿지 않는다 | (online 7.3) |

### 12.2 공통 래퍼

```csharp
public async Task<TResult> Mutate<TResult>(long accountId, Guid requestId, string kind, Func<NpgsqlTransaction, Task<TResult>> body) {
    await using var tx = await db.BeginTransactionAsync();
    // 1. 멱등: 행을 먼저 넣는다. 충돌하면 저장된 결과를 돌려준다
    var inserted = await tx.Exec("INSERT INTO request(account_id, request_id, kind, result, at) VALUES (@a, @r, @k, NULL, now()) ON CONFLICT DO NOTHING");
    if (inserted == 0) {
        var row = await tx.QuerySingle("SELECT kind, result FROM request WHERE account_id=@a AND request_id=@r FOR UPDATE");
        if (row.kind != kind) throw new RequestIdReused();      // 같은 번호를 다른 동작에 쓰면 거절 [임시]
        await tx.Rollback(); return Deserialize<TResult>(row.result);   // result가 NULL이면(앞 요청이 진행 중이었다가 롤백) 여기 올 수 없다: 같은 트랜잭션에서 넣었으므로
    }
    var result = await body(tx);                                   // 실패하면 예외 → 롤백 → request 행도 사라진다
    await tx.Exec("UPDATE request SET result=@json WHERE account_id=@a AND request_id=@r", result);
    await tx.Commit(); return result;
}
```

- **실패한 요청은 저장하지 않는다** [임시]: 롤백되므로 같은 번호로 다시 보내면 다시 시도된다. 성공한 결과와 "업무 규칙으로 거절됨"(예: 수량 부족)은 저장한다 — 거절도 `result`에 담아 커밋한다 [임시]. 그래야 네트워크 재전송이 두 번 실행되지 않는다.
- 보관 30일: 매일 `DELETE FROM request WHERE at < now() - interval '30 days'`.
- 잠금 순서 규칙(전 서버 공통) [임시]: `market_listing` → `account`(id 순) → `currency_balance`(account_id 순) → `item_stack`(account_id, item_code 순) → `item_instance`(id 순) → `wand`(id 순) → `mail`(id 순). 교착이 나면 PostgreSQL이 하나를 취소하고, 래퍼가 최대 3번 다시 시도한다 [임시].

### 12.3 원장 이유 코드 (이 영역)

```csharp
public enum LedgerReason {
    TradeGive, TradeReceive, TradeFee,                 // 직접 거래
    GiftGive, GiftFee,                                 // 선물
    MarketDeposit, MarketBuy, MarketFee, MarketProceeds, MarketReturn, MarketListMove,
    MailClaim, MailRevoke,                             // 우편 수령, 검증 실패 회수
    ShopBuy, LibraryExpand, NicknameChange,            // 행상 수레(골드 상점)
    WandRelease,                                        // 떠나보내기 환급
    DebtRepay                                           // 갚을 것
}
```

- `currency_ledger(id, account_id, currency, bucket, delta, balance_after, pending, reason, ref_type, ref_id, request_id, at)`, `item_ledger(id, account_id, item_ref, delta, pending, reason, ref_type, ref_id, request_id, at)` — 원문 열 그대로(online 7.9).
- 장터 등록·우편 이동처럼 **소유자는 같고 장소만 바뀌는 이동**도 `item_ledger`에 −/+ 두 줄로 적는다 [임시]. 그래야 야간 대조의 "생성 − 소멸 = 현재 수량"에서 장터·우편에 있는 수량을 따로 셀 수 있다.

### 12.4 야간 대조 (`NightlyReconcile`)

매일 밤 대조한다. 어긋나면 운영자에게 알린다(online 5.6) [제안]. 서버가 다시 켜질 때도 원장 대조를 한 번 한다(online 7.7).

| 대조 | 식 | 출처 |
|---|---|---|
| 재화 | 원장의 합 = 잔액 (`Σ currency_ledger.delta` per (account, currency, bucket) = `currency_balance.amount`) | (online 5.6) |
| 겹쳐지는 물건 | 생성 − 소멸 = 현재 수량(품목별). 현재 수량 = `Σ item_stack.qty` + `Σ market_listing.qty(Active)` + `Σ 우편 payload 안 수량(미수령)` [임시: 장소 합] | (online 5.6) |
| 지팡이 | 생성 − 소멸 = 현재 개체 수 | (online 5.6) |
| 잠금 | "원정 중"인데 진행 중인 판이 없는 지팡이, "장터"인데 등록이 없는 물건 | (online 5.6) |
| 추가 [임시] | "우편"인데 미수령 우편에 없는 지팡이, 같은 지팡이가 두 우편 payload에 있는 경우, `lock_state = 거래`가 남은 지팡이(직접 거래는 커밋 순간에만 옮기므로 이 값은 남으면 안 된다) | |

```sql
-- 재화
SELECT b.account_id, b.currency, b.bucket, b.amount, COALESCE(SUM(l.delta),0) AS ledger
FROM currency_balance b LEFT JOIN currency_ledger l USING (account_id, currency, bucket)
GROUP BY 1,2,3,4 HAVING b.amount <> COALESCE(SUM(l.delta),0);
-- 장터 잠금
SELECT w.id FROM wand w WHERE w.lock_state = '장터'
  AND NOT EXISTS (SELECT 1 FROM market_listing m WHERE m.status='Active' AND m.item_ref->>'wand_id' = w.id::text);
```

- 사고 대응: 복제가 발견되면 원장으로 해당 물건의 이동 경로를 따라가 회수한다. 전체 되돌리기는 마지막 수단(online 7.10) [제안].
- 이상 탐지(online 7.10, 10) [제안]: 원장 대조 불일치, 감정가 대비 이상 거래, 짧은 시간 대량 획득, 신고 누적, 한쪽만 주는 거래, 새 계정으로 몰리는 골드, 한 계정에서 여러 계정으로 퍼지는 물건, 같은 기기 계정 사이 거래 표시. 장터의 범위 없는 등록(`AuditFlag`)도 여기로 간다.

### 12.5 현금 거래 대응 (online 10) [제안]

- 약관으로 금지. 억제: 수수료, 하루 골드 상한, 선물 조건, 장터 가격 범위, 지팡이 7일 재거래 잠금. 제재: 양쪽 계정의 해당 물건 회수와 거래 정지.
- 채팅의 연락처 모양 차단(4.3)도 같은 목적이다.

---

## 13. 메시지·API 목록 (구현 인터페이스)

요청·응답은 HTTPS(JSON), 광장·채팅·알림·매치는 웹소켓(online 7.2) [제안]. 이름은 [임시].

### 13.1 웹소켓 (광장)

| 방향 | 메시지 | 필드 |
|---|---|---|
| C→S | `plaza.join` | instance_id? (없으면 자동), reason(auto/manual/follow), follow_account_id? |
| C→S | `plaza.leave` | |
| C→S | `plaza.input` | seq, move(Dir8 또는 none), 최근 3개 겹침 |
| C→S | `plaza.emote` | emote_id |
| C→S | `plaza.activity` | activity(InMenu/Idle 등) |
| C→S | `plaza.interact` | target(account_id 또는 facility_id) |
| S→C | `plaza.welcome` | instance_id, tick, server_time_ms, members[OverheadView], self_slot |
| S→C | `plaza.snapshot` | tick, ack_seq, entries[{slot, x16, y16, facing_activity, emote}] |
| S→C | `plaza.member_joined` / `member_left` / `member_changed` | OverheadView / account_id |
| S→C | `notice.server` | kind(+10~+12 / +13 / 묶음 / 공지), payload |
| C→S | `chat.send` | channel, text, target?, item_links? |
| S→C | `chat.msg` | channel, from, text(가린 것), item_links, at |
| S→C | `chat.reject` | reason(Muted/NewbieNearbyOnly/Cooldown/TooLong/Repeated/ContactBlocked/PresetOnly/WhisperFriendsOnly), remaining_ms? |
| S→C | `request.note` | kind(friend/trade/challenge), from, expires_at_ms |
| C→S | `request.respond` | request_id, accept(bool) |
| S→C | `friend.presence` | account_id, presence |

### 13.2 거래 (웹소켓, 세션형)

| 방향 | 메시지 | 필드 |
|---|---|---|
| C→S | `trade.request` | target_account_id |
| S→C | `trade.opened` | trade_id, a, b, revision |
| C→S | `trade.set_slot` | trade_id, slot_index(0~7), item(TradeSlot) 또는 null |
| C→S | `trade.set_gold` | trade_id, gold |
| C→S | `trade.lock` / `trade.unlock` | trade_id |
| C→S | `trade.confirm` | trade_id, revision, loss_warning_ack(bool), request_id |
| C→S | `trade.cancel` | trade_id |
| S→C | `trade.state` | trade_id, state, revision, sides(A,B: slots, gold, locked, confirmed, blink_until_ms[]), fees{a,b}, appraised{a,b}, confirm_ready_at_ms?, loss_warning_for?, league_change{a?, b?} |
| S→C | `trade.result` | trade_id, ok, reason? |

### 13.3 HTTPS (요청 번호 필수)

| 함수 | 입력 | 출력 |
|---|---|---|
| `MarketSearch` | 분류, 이름, 정렬(값/남은 시간/단계), 페이지 | 목록 줄(지팡이 흔적 포함) |
| `MarketPriceStat` | item key | median_unit, volume_7d, n_7d, 시세 그림 점들 |
| `MarketList` | item, qty, unit_price, request_id | listing_id, deposit, fee 예정, 받을 금액 |
| `MarketBuy` | listing_id, qty, expected_unit_price, request_id | mail_id, league_change? |
| `MarketCancel` | listing_id, request_id | mail_id |
| `MailList` / `MailClaim` / `MailClaimAll` / `NoteSend` | | |
| `GiftSend` | receiver, items, gold, request_id | mail_id, fee |
| `FriendRequest` / `FriendRespond` / `FriendRemove` / `FriendList` / `RecentMet` | | |
| `Block` / `Unblock` / `BlockList` | | |
| `Report` | target, reason, note, ref | report_id |
| `Inspect` | account_id | InspectView |
| `ChatSettings` | muted_channels, mute_all, whisper_policy, show_alerts | |
| `LoadoutClearAll` | wand_id, request_id | (거래 창·장터 등록 화면이 부른다) |

---

## 14. 의존 순서

일정이 아니라 의존 순서다. 범위는 줄이지 않는다(DEC 1.16) [확정]. 이 영역은 online 11절의 5단계(광장)와 6단계(거래)에 해당하고, 그 앞의 1~4단계(결정론 코어, `IBackend`/`Wizard.Rules`/`OfflineBackend`, 서버 기반: 계정·원장·멱등 키·강화 서버 함수, 무한 웨이브 제출과 검증)가 먼저다(online 11).

1. 공통 래퍼(12.2 `Mutate`, 멱등 키, 잠금 순서), 원장 이유 코드 — 서버 기반 단계의 원장에 얹는다.
2. 광장 인스턴스 배정과 이동(2절), 시간대, 머리 위 표시 데이터, 감정표현.
3. 시설 상호작용과 화면 id 표(3절), 서버 알림 큐(3.3).
4. 채팅(4절): 채널, 간격, 필터, 차단, 신고, 제재.
5. 친구와 최근 만난 사람, 살펴보기(5절) — 차단 표가 먼저 있어야 한다.
6. 거래 공통(6절): `TradeGate`, 거래 가능 수량, 감정가 표, 수수료 식 — `account_debt`, 검증 대기 표시(무한 웨이브 검증 단계)가 먼저다.
7. 우편함(11절) — 장터와 선물이 우편을 쓰므로 먼저.
8. 직접 거래(7절)와 지팡이 거래 규칙(8절) — IL 서비스의 `OnHoldingsChanged` 자리(결투 단계의 IL·리그 계산이 붙기 전에는 빈 구현으로 두고 나중에 채운다).
9. 장터(9절), 선물(10절).
10. 야간 대조(12.4), 이상 탐지 표시.

---

## 15. 테스트 벡터

형식: **Given** 상태 / **When** 동작 / **Then** 기대. 단위 테스트(순수 함수)와 통합 테스트(DB 트랜잭션)를 구분하지 않고 번호를 매긴다. 번호는 `TV-11-xxx`.

### 15.1 인스턴스와 이동

- **TV-11-001** Given 인스턴스 1개(인원 35) / When 새 사람이 자동 배정 / Then 그 인스턴스로 들어가 36명, 모든 인스턴스가 36명 이상이 되었으므로 새 인스턴스 2가 열린다.
- **TV-11-002** Given 인스턴스 1(36명), 2(0명) / When 자동 배정 / Then 인스턴스 2로 간다(1은 35명을 넘었다).
- **TV-11-003** Given 인스턴스 1(40명) / When 채널 목록에서 1을 직접 고른다 / Then `InstanceFull`로 거절.
- **TV-11-004** Given 인스턴스 1(38명) / When 친구 따라가기로 1에 들어간다 / Then 39명으로 들어간다(하드 상한 40 안).
- **TV-11-005** Given 인스턴스 1(30명, 친구 0), 2(20명, 친구 2) / When 자동 배정 / Then 2(친구가 많은 곳 우선).
- **TV-11-006** Given 인스턴스 1(30명), 2(20명), 둘 다 친구 0 / When 자동 배정 / Then 1(사람이 많은 곳).
- **TV-11-007** Given 인스턴스 3이 0명이 된 지 4분 59초, 인스턴스 2개 이상 / When 정리 틱 / Then 닫지 않는다. 5분 0초에 닫는다.
- **TV-11-008** Given 위치 (10.0, 10.0), 입력 E, 벽 없음 / When 광장 1틱 / Then (10.5, 10.0). Q32.32 원시 x = 45,097,156,608.
- **TV-11-009** Given 위치 (10.0, 10.0), 입력 SE / When 광장 1틱 / Then x, y 각각 +0.35355339칸(원시 +1,518,500,249 이상 1,518,500,250 이하). 이동 거리 0.5칸 ± 1e-6.
- **TV-11-010** Given 위치 (39.6, 5.0), 몸 반지름 0.30, 입력 E / When 1틱 / Then x = 39.70(바깥 벽 40.0 − 0.30)에 붙는다.
- **TV-11-011** Given 위치 (39.6, 5.0), 입력 NE / When 1틱 / Then x는 39.70에 막히고 y는 5.0 − 0.35355339 = 4.64644661로 미끄러진다.
- **TV-11-012** Given 한 광장 틱 안에 seq 7(E), 8(W) 두 입력이 도착 / When 서버 틱 / Then seq 8(W)만 적용, ack = 8.
- **TV-11-013** Given 입력 E를 계속 누름 / When 10틱(1초) / Then 5.0칸 이동(속도 5.0).
- **TV-11-014** Given `Activity = Trading`, 입력 E / When 1틱 / Then 위치가 변하지 않는다.
- **TV-11-015** Given 0.5초 동안 입력 패킷이 없음, 마지막 입력 E / When 다음 틱 / Then MoveInput = None, 위치 고정.
- **TV-11-016** Given 40명 인스턴스 / When 스냅샷 크기 계산 / Then 한 사람 내림 = 10회/초 × (39 × 10 + 60)바이트 = 4,500바이트/초 = 36kbps, 인스턴스 합계 40 × 36kbps = 1.44Mbps(원문 "1.5Mbps", 반올림).

### 15.2 시간대

- **TV-11-017** Given 서버 시각 t mod 1,440,000 = 0 / Then 낮, 경과 0.
- **TV-11-018** Given t mod 1,440,000 = 719,999 / Then 낮. 720,000 / Then 해질녘 경과 0.
- **TV-11-019** Given t mod 1,440,000 = 959,999 / Then 해질녘. 960,000 / Then 밤 경과 0.
- **TV-11-020** Given t mod 1,440,000 = 1,439,999 / Then 밤. 다음 1ms는 낮.
- **TV-11-021** Given 같은 서버 시각, 인스턴스 1과 7 / Then 같은 시간대(모든 인스턴스가 같이 흐른다).
- **TV-11-022** Given 하루 길이 / Then 720,000 + 240,000 + 480,000 = 1,440,000ms = 24분 = 14,400 광장 틱 = 86,400 코어 틱.

### 15.3 머리 위 표시와 감정표현

- **TV-11-023** Given A가 B를 차단 / When A의 화면에 B의 OverheadView / Then 닉네임 판·칭호 띠가 없다. B의 화면에서 A는 평소대로.
- **TV-11-024** Given A와 B가 친구 / Then 서로의 닉네임 색 = 좋음 초록.
- **TV-11-025** Given B가 강화 화면을 연다 / Then 인스턴스의 다른 사람에게 `Activity = InMenu`(말줄임표)만 가고, 강화 결과·실패·하락·변이·도전 중 정보는 어떤 메시지에도 없다.
- **TV-11-026** Given B가 +10 성공 / Then 같은 인스턴스에 개별 연출 메시지 없음, 서버 전체 `notice.server`(+10)만 간다.
- **TV-11-027** Given 주변 채팅 80자 / When 말풍선 줄 나누기 / Then 20자 × 4줄. 4,000ms 뒤 흐려짐 시작.
- **TV-11-028** Given 귓속말 수신 / Then 말풍선 이벤트 없음(대화창에만).
- **TV-11-029** Given 감정표현 바퀴 / Then 칸 8개, 기본 id 8종(`emote_greet, emote_clap, emote_taunt, emote_bow, emote_sit, emote_dance, emote_raise_wand, emote_despair`).
- **TV-11-030** Given `emote_dance` 중 / When 이동 입력 / Then 감정표현이 끝나고 이동.
- **TV-11-031** Given 지팡이 show_scars = false, record_peak 12, stage 10 / When 남이 광장에서 봄 / Then `WandAppearance`에 흔적 겹그림 플래그 없음, 현재 단계(+10) 모양만.

### 15.4 시설

- **TV-11-032** Given 시설 목록 / Then 10곳: 큰 모닥불, 좌판 거리, 행상 수레, 결투 게시판, 돌마당 가는 돌길, 후계자 비석, 느루의 흔들의자, 연습 허수아비, 까마귀 우편함, 한뎃벌 출입문. 강화용 시설 없음.
- **TV-11-033** Given 돌마당 가는 돌길 앞에서 J / Then 열리는 화면 없음.
- **TV-11-034** Given 행상 수레 J / Then 골드 상점 화면(메인 메뉴 "상점"과 같은 ScreenId).
- **TV-11-035** Given 오프라인 프로필 / When 광장 진입 시도 / Then 진입 불가, "서버에 접속하면 열립니다".
- **TV-11-036** Given 메인 메뉴(광장 밖) / When 장터, 우편함, 친구 목록, 자동 매치 대기열 열기 / Then 열린다. When 직접 거래 신청, 광장 도전, 주변·광장 채팅, 남의 연습 구경 / Then 광장에 있어야 한다는 이유로 거절.
- **TV-11-037** Given +10 알림 3건과 +13 알림 1건이 동시에 큐에 / When 내보내기 / Then +13이 먼저, 이후 +10~+12는 6초 간격, +13끼리는 10초 간격.
- **TV-11-038** Given 밀린 +10~+12 알림 11건 / Then 한 줄로 묶어 "방금 +10 이상 성공 11건".
- **TV-11-039** Given 대성공으로 +9 → +11 / Then 알림 1회(+11).
- **TV-11-040** Given `show_name_in_alerts = false` / Then 알림 문구가 "어느 제자의 …".

### 15.5 채팅

- **TV-11-041** Given A와 B 거리 8.0칸 / When A가 주변 채팅 / Then B가 받는다. 8.01칸이면 받지 않는다.
- **TV-11-042** Given A가 주변 채팅을 t=0에 성공 / When t=1,999ms에 다시 / Then `Cooldown`, remaining 1ms. t=2,000ms면 성공.
- **TV-11-043** Given 광장 채널 t=0 성공 / When t=4,999 / Then 거절. t=5,000 성공.
- **TV-11-044** Given 거래 채널 t=0 성공 / When t=29,999 / Then 거절. t=30,000 성공.
- **TV-11-045** Given 주변 채널 간격이 남아 있음 / When 광장 채널로 발언 / Then 성공(간격은 채널별).
- **TV-11-046** Given 80자 문장 / Then 통과. 81자 / Then `TooLong`.
- **TV-11-047** Given 같은 문장(정규화 뒤 같음)을 1·2회 보냄 / When 3회째 / Then `Repeated`. "안녕" → "안 녕" → "안.녕"도 같은 문장으로 센다.
- **TV-11-048** Given 계정의 최고 웨이브 9 / When 광장 채널 / Then `NewbieNearbyOnly`. 주변 채널은 성공. 웨이브 10 도달 뒤 광장 채널 성공.
- **TV-11-049** Given "www.example.com 오세요" / Then `ContactBlocked`(통째로 막음).
- **TV-11-050** Given "010-1234-5678로 연락" / Then `ContactBlocked`. "공일공 일이삼사 오육칠팔"도 `ContactBlocked`.
- **TV-11-051** Given "123-456-789012" 꼴 / Then `ContactBlocked`(계좌 모양).
- **TV-11-052** Given "카톡: abc123" / Then `ContactBlocked`(메신저 아이디 모양).
- **TV-11-053** Given 금칙어가 든 문장 "너 ㅅ.ㅂ 뭐야"(사전에 해당 단어) / Then 그 단어만 "＊"로 가리고 나머지는 보낸다. `chat_log.filtered = true`.
- **TV-11-054** Given B가 A를 차단 / When A가 광장 채널 발언 / Then B는 받지 않는다. 다른 사람은 받는다.
- **TV-11-055** Given B가 A를 차단 / When A가 B에게 귓속말 / Then B는 받지 않는다.
- **TV-11-056** Given B의 귓속말 설정 "친구만"(기본), A와 B는 친구 아님 / When A가 귓속말 / Then `WhisperFriendsOnly`.
- **TV-11-057** Given 결투방에서 경기 중 / When 정해진 문구 8개 밖의 문장 / Then `PresetOnly`. 문구 8개 중 하나는 성공.
- **TV-11-058** Given 시스템 채널로 발언 시도 / Then `ReadOnly`.
- **TV-11-059** Given 서로 다른 계정 5개가 24시간 안에 C를 신고 / Then C의 채팅이 1시간 막힌다(`sanction kind = 채팅, until = now + 1h`). 4개면 막히지 않는다.
- **TV-11-060** Given 같은 계정이 C를 5번 신고 / Then 자동 막힘 없음(서로 다른 계정 수 1).
- **TV-11-061** Given 25시간 전 신고 1건 + 최근 24시간 신고 4건(모두 다른 계정) / Then 자동 막힘 없음.
- **TV-11-062** Given 신고 / Then `report.chat_excerpt`에 대상의 최근 채팅 30줄(30줄 미만이면 있는 만큼).
- **TV-11-063** Given 차단 목록 200명 / When 201번째 차단 / Then 거절.
- **TV-11-064** Given B가 A를 차단 / Then A의 도전, 거래 신청, 친구 신청이 B에게 가지 않는다.
- **TV-11-065** Given 거래 채널 꺼 둠 / Then 거래 채널 메시지를 받지 않는다. 내가 거래 채널로 보내는 것은 된다.
- **TV-11-066** Given 서버 전체 채팅 채널 요청(channel = "global") / Then 알 수 없는 채널로 거절(그런 채널은 없다).

### 15.6 친구·살펴보기

- **TV-11-067** Given A의 친구 100명 / When A가 신청 또는 수락 / Then 거절. B의 친구가 100명일 때도 같다.
- **TV-11-068** Given A→B 신청 대기 중 / When B→A 신청 / Then 즉시 친구.
- **TV-11-069** Given 친구 / When 상태 조회 / Then 광장이면 채널 번호, 원정 중, 결투 중, 접속 안 함 중 하나.
- **TV-11-070** Given 최근 만난 사람 20명 / When 새 거래 상대 / Then 가장 오래된 1명이 빠지고 20명 유지. 이미 있는 사람이면 맨 앞으로 이동.
- **TV-11-071** Given 조립 공개 끔 / When 남이 살펴보기 / Then `Loadout = null, LoadoutHidden = true`, 지팡이 단계와 겉모습은 보인다.
- **TV-11-072** Given 든 지팡이 show_scars = false, record_peak 12, drop_count 3 / When 남이 살펴보기 / Then 응답 JSON에 `record_peak`, `drop_count` 키가 없다.
- **TV-11-073** Given 같은 지팡이 show_scars = true / Then `record_peak = 12, drop_count = 3, has_scar_overlay = true`.
- **TV-11-074** Given 내 지팡이, show_scars = false / When 내가 살펴보기 / Then 흔적은 항상 보이고 "남에게는 안 보인다" 표시(fx 1.3).

### 15.7 거래 조건

- **TV-11-075** Given 최고 웨이브 9, 플레이 3시간 / Then `TradeGate` 닫힘(NeedWave10). 시세 조회는 된다.
- **TV-11-076** Given 최고 웨이브 10, 플레이 7,199초 / Then 닫힘(NeedPlay2h). 7,200초면 열림.
- **TV-11-077** Given 조건 충족, `account_debt` 미해결 1행 / Then 닫힘(HasDebt). 장터 등록·구매, 선물, 지팡이 떠나보내기도 불가. 원정·강화·결투는 된다.
- **TV-11-078** Given 새 기기 로그인 23시간 전 / When 직접 거래 신청 / Then 거절(SecurityHold). 장터 등록도 거절. 장터 구매는 된다. 24시간 뒤 열림.
- **TV-11-079** Given 비밀번호 재설정 1시간 전 / Then 거래와 장터 등록 막힘.

### 15.8 품목 검사

- **TV-11-080** Given 강화석 qty 10, qty_bound 3, qty_pending 2 / Then 거래 가능 수량 5. 6개를 올리면 거절.
- **TV-11-081** Given 블록 X qty 4, qty_socketed 1 / Then 거래 가능 3.
- **TV-11-082** Given 서고와 조립을 합쳐 투사체 블록이 1개뿐(마력탄 1, 지팡이에 끼움) / When 그 블록을 거래 창에 / Then 거절(마지막 투사체 블록, 그리고 끼워 둔 블록).
- **TV-11-083** Given 투사체 블록 2개(서고 2) / When 2개를 한 번에 거래 창에 / Then 거절. 1개는 된다.
- **TV-11-084** Given 받는 쪽 서고 100/100(한도 100) / When 상대가 블록 1개를 올린다 / Then 그 칸 거절. 강화석은 된다.
- **TV-11-085** Given 받는 쪽 서고 98/100 / When 블록 2개 / Then 된다. 3개 / Then 거절.
- **TV-11-086** Given 서고 100/100 / When 우편으로 블록 5개가 옴 / Then 우편 수령 때 블록은 받지 못하고 우편에 남는다. 같은 우편의 골드는 받는다.
- **TV-11-087** Given `item_instance.bound = true`(금 간 거울 조각) / When 거래 창·장터·선물 / Then 모두 거절.
- **TV-11-088** Given 후계자 전용 겉모습 / Then 거래 불가. 같은 시즌의 일반 시즌 한정 꾸미기는 직접 거래·장터 가능.
- **TV-11-089** Given 별빛으로 산 꾸미기(`origin = 유료, bound = true`) / Then 거래 서버 함수가 거절(결제가 꺼져 있어도 검사가 돈다).
- **TV-11-090** Given 시즌 주화 / Then 거래 품목으로 올릴 수 없다.
- **TV-11-091** Given 누적 획득 골드 100,000, 오늘 준 골드 15,000 / When 6,000골드를 올린다 / Then 거절(상한 20,000). 5,000은 된다.

### 15.9 감정가와 수수료

- **TV-11-092** Given +0 지팡이 / Then 감정가 250. 떠나보내기 환급 0.
- **TV-11-093** Given +5 / +7 / +10 / +13 지팡이 / Then 감정가 3,390 / 12,500 / 64,950 / 258,600(표). 원문 표 값으로 검산: 6,800 × 0.5 = 3,400, 25,000 × 0.5 = 12,500, 130,000 × 0.5 = 65,000, 517,000 × 0.5 = 258,500(차이 1% 이하).
- **TV-11-094** Given +13 지팡이 떠나보내기 / Then 환급 25,860골드(감정가의 10%) = 평균 강화 골드의 5%(517,208 × 0.05 = 25,860).
- **TV-11-095** Given A: 강화석 3개(7일 중앙값 1,000) / B: 골드 2,000 / Then V_A 3,000, V_B 2,000. 기준 A 150, B 100 → 수수료 150, 각자 75.
- **TV-11-096** Given A: 블록 5개(중앙값 400 → 2,000) / B: 부적 1개(중앙값 2,000) / Then 수수료 = 5% × 2,000 = 100, 각자 50(물건끼리만 바꿔도 수수료가 붙는다).
- **TV-11-097** Given A: +7 지팡이(12,500) / B: 골드 10,000 / Then 기준 A 1,250(10%), B 500 → 수수료 1,250, 각자 625.
- **TV-11-098** Given 수수료 151 / Then 각자 Ceil(75.5) = 76.
- **TV-11-099** Given A가 골드 10,000을 올리고 거래 가능 골드 10,050, 예상 수수료 75 / When 잠금 / Then 거절(10,000 + 75 > 10,050).
- **TV-11-100** Given 7일 거래가 0건인 물건 / Then 감정가 = 기본 감정가 표의 값.

### 15.10 직접 거래 상태 기계

- **TV-11-101** Given A·B 거리 5.0칸 / When 거래 신청 / Then 신청이 간다. 5.01칸 / Then `TooFar`.
- **TV-11-102** Given 신청 후 20초 동안 응답 없음 / Then Cancelled.
- **TV-11-103** Given A가 거래 중 / When C가 A에게 신청 / Then "지금 거래 중"으로 거절.
- **TV-11-104** Given 양쪽 잠금(BothLocked, t=0) / When t=4,999ms에 확인 / Then 거절(아직 ConfirmReady 아님). t=5,000ms에 ConfirmReady.
- **TV-11-105** Given BothLocked t=3,000ms / When A가 칸 2의 수량을 바꿈 / Then 양쪽 Locked = false, 상태 Open, 칸 2 `BlinkUntil = now + 3,000`, Revision +1. 다시 양쪽 잠그면 5초를 처음부터 센다.
- **TV-11-106** Given ConfirmReady, Revision 7 / When A가 Revision 6으로 확인 / Then 거절.
- **TV-11-107** Given A가 줄 것의 감정가 600, B가 줄 것 3,500 / Then 비율 5.83 > 5 → B(3,500을 주고 600을 받는 쪽, 값이 작은 것을 받는 쪽)에게 손해 경고, B가 경고를 확인해야 커밋된다. A에게는 경고가 없다. 비율이 정확히 5.0(600 대 3,000)이면 경고 없음.
- **TV-11-108** Given 커밋 직전에 A의 지팡이가 다른 요청으로 version이 올라감 / When 커밋 / Then `WandVersionChanged`, 어떤 행도 바뀌지 않음, 거래 창은 Open으로.
- **TV-11-109** Given A·B 동시에 확인 요청(같은 trade_id) / Then 커밋 1회, `trade` 행 1개, 원장 한 벌.
- **TV-11-110** Given 직접 거래 커밋 / Then `currency_ledger`와 `item_ledger`의 합이 0이 아닌 것은 수수료 줄뿐이다(골드: A −g −fee, B +g; 물건: −1/+1).
- **TV-11-111** Given 계정 7과 3이 거래 / Then 행 잠금 순서는 3 → 7(교착 방지). 동시에 7↔3, 3↔7 거래가 커밋되어도 교착 없이 하나씩.
- **TV-11-112** Given 커밋 중 서버가 꺼짐(COMMIT 전) / Then 아무것도 옮겨지지 않음.
- **TV-11-113** Given 맹수 리그 계정이 +13 지팡이를 받는 거래 / Then 확인 창에 "이 거래로 리그가 맹수 → 후계로 올라갑니다". 커밋 뒤 즉시 후계, 맹수 자동 매치 대기열에 있었다면 빠진다.
- **TV-11-114** Given 결투 대기열에 있는 A가 거래 창을 연 상태 / When 매치 성사 / Then 거래 Cancelled.

### 15.11 지팡이 거래

- **TV-11-115** Given A의 달구 지팡이 1자루(든 지팡이) / When 거래 창·장터·선물 / Then 거절(마지막 한 자루). A가 상점에서 +0을 500골드에 사 2자루가 되면 된다.
- **TV-11-116** Given A의 달구 지팡이 2자루 중 1자루가 장터 등록 중 / When 나머지 1자루를 거래 창에 / Then 거절(손에 있는 것이 1자루).
- **TV-11-117** Given A의 그믐 지팡이 2자루 중 1자루가 미수령 우편에 있음 / When 보관 중인 1자루를 거래 / Then 거절(우편의 것은 세지 않는다).
- **TV-11-118** Given A의 지팡이 3자루: +9(든 것), +11, +4 / When +9를 내보냄 / Then 든 지팡이 = +11.
- **TV-11-119** Given 지팡이 stage 10, record_peak 12, drop_count 4, floor_peak 12, aura 2, carve_bonus 40, show_scars true, owner_count 1, skin S / When 거래로 넘김 / Then 새 주인 쪽: stage 10, record_peak 12, drop_count 4, floor_peak 10(바닥 +8), aura 0, carve_bonus 0, show_scars false, owner_count 2, skin null(S는 주는 쪽 옷장으로), trade_locked_until = now + 7일, 변이·확장 슬롯 그대로.
- **TV-11-120** Given 받은 지 6일 23시간 / When 다시 거래 또는 떠나보내기 / Then 거절. 강화와 원정 출발은 된다. 7일 0초 뒤 거래 가능.
- **TV-11-121** Given 지팡이 lock_state = 원정 / Then 거래 불가. 결투 중도 불가.
- **TV-11-122** Given 지팡이 `pending_run_id` 있음(검증 대기 보상으로 강화) / Then 거래 불가. 검증 통과 뒤 가능. 실패 후 갚을 것이 남으면 0이 될 때까지 불가.
- **TV-11-123** Given 끼워 둔 블록 3개인 지팡이를 거래 창에 / Then "전부 빼기" 뒤 올라간다. 블록 3개는 서고로.
- **TV-11-124** Given +0 지팡이 / When 장터 등록 / Then 거절("+0 지팡이는 장터에 올릴 수 없다. 직접 거래는 된다"). 직접 거래와 선물은 된다.
- **TV-11-125** Given +3 이하 지팡이를 넘긴 뒤 새 주인이 실패 / Then 하락 없음(+3 이하 하락 없음은 그대로).
- **TV-11-126** Given 지팡이를 받은 쪽 / Then 비기 해금과 기록은 넘어오지 않는다.

### 15.12 장터

- **TV-11-127** Given 활성 등록 20건 / When 21번째 등록 / Then 거절. 화면 "20 / 20".
- **TV-11-128** Given 강화석 10개를 낱개 1,000에 등록 / Then 보증금 = 10,000 × 1% = 100골드, 만료 = 등록 + 48시간.
- **TV-11-129** Given 낱개 30골드 × 1개 등록 / Then 보증금 Ceil(0.3) = 1골드.
- **TV-11-130** Given 7일 중앙값 1,000, 7일 거래 30건 / When 등록가 300 / Then 된다. 299 / Then 거절. 5,000 / Then 된다. 5,001 / Then 거절.
- **TV-11-131** Given 7일 거래 29건 / When 등록가 50(중앙값 1,000) / Then 된다(범위 없음), `AuditFlag = true`.
- **TV-11-132** Given 지팡이 등록 / Then 가격 범위 없음, `AuditFlag = true`.
- **TV-11-133** Given 강화석 10개 등록 / When 3개 구매 / Then 남은 수량 7, 판 사람 우편 = 3,000 − 150(5%) + 30(보증금 3/10) = 2,880골드, 산 사람 우편 = 강화석 3개.
- **TV-11-134** Given 위 등록에서 나머지 7개 구매 / Then 판 사람 우편 = 7,000 − 350 + 70 = 6,720골드, 상태 SoldOut. 보증금 100 전부 환급.
- **TV-11-135** Given +10 지팡이를 100,000골드에 등록 / When 팔림 / Then 수수료 10% = 10,000, 판 사람 우편 = 90,000 + 보증금 1,000 = 91,000.
- **TV-11-136** Given 남은 수량 1 / When 두 사람이 동시에 1개씩 구매 / Then 한 명만 성공, 다른 한 명은 `SoldOut`, 골드 변동 없음.
- **TV-11-137** Given 48시간 동안 안 팔림 / When 만료 / Then 물건은 우편함으로(`market_returned`), 보증금은 사라진다.
- **TV-11-138** Given 10개 중 4개 팔린 뒤 만료 / Then 남은 6개 우편으로, 팔린 4개 몫 보증금(40)은 이미 환급, 남은 60은 사라진다.
- **TV-11-139** Given 등록 후 서버가 3시간 꺼져 있었음 / When 다시 켬 / Then 모든 활성 등록의 만료가 3시간 늦춰진다.
- **TV-11-140** Given 자기 등록 / When 자기가 구매 / Then 거절.
- **TV-11-141** Given 화면에서 본 값 1,000, 실제 등록가 1,000 / Then 구매. `expected_unit_price` 900이면 거절.
- **TV-11-142** Given 장터에 +13 지팡이를 올림 / Then 올린 사람의 보유 IL 변화 없음. 팔린 순간 "내줬다" 규칙(14일 뒤 내려감), 산 사람은 우편에서 꺼내기 전에도 즉시 반영.
- **TV-11-143** Given 장터 지팡이 목록·상세 / Then show_scars와 무관하게 흔적 겹그림, 역대 최고 단계, 하락 횟수가 응답에 있다.
- **TV-11-144** Given 거래 조건 미달 계정 / When 좌판 거리 시세 조회 / Then 된다. 등록·구매는 거절.

### 15.13 선물·우편

- **TV-11-145** Given 친구가 된 지 6일 / When 선물 / Then 거절. 7일 지나면 된다.
- **TV-11-146** Given 강화석 2개(감정가 2,000) 선물 / Then 주는 쪽 수수료 100(5%), 받는 쪽 0.
- **TV-11-147** Given +7 지팡이 선물 / Then 수수료 1,250(감정가의 10%) [임시 값 기준].
- **TV-11-148** Given 직접 거래 창에서 B가 아무것도 올리지 않고 A만 올림 / Then 선물로 다뤄 친구 7일 조건 검사, 수수료는 A만 낸다.
- **TV-11-149** Given 귀속 강화석(`qty_bound`) / When 우편 첨부·선물 / Then 거절.
- **TV-11-150** Given 미수령 우편 / When 같은 우편에 `ClaimMail` 두 번(다른 요청 번호) / Then 첫 번째만 지급, 두 번째는 `AlreadyClaimed`(UPDATE가 0행).
- **TV-11-151** Given 같은 요청 번호로 `ClaimMail` 두 번 / Then 두 번째는 첫 결과를 그대로 돌려준다.
- **TV-11-152** Given 검증 대기 보상 우편 / When 받기 / Then 거절("검증이 끝나면 받는다"). 검증 통과 뒤 받을 수 있다. 검증 실패면 우편이 회수된다.
- **TV-11-153** Given 갚을 것 골드 500, 장터 대금 우편 2,000 / When 받기 / Then 잔액 +1,500, 갚을 것 0 [임시 규칙 기준].

### 15.14 멱등·원장·대조

- **TV-11-154** Given 요청 번호 R로 장터 구매 성공 / When 같은 R로 다시 / Then 같은 결과, 골드 한 번만 빠짐.
- **TV-11-155** Given 요청 번호 R을 31일 전에 썼음(보관 30일 지나 삭제) / When R로 다시 / Then 새 요청으로 처리.
- **TV-11-156** Given 요청 번호 R을 구매에 썼음 / When 같은 R로 등록 / Then `RequestIdReused`로 거절.
- **TV-11-157** Given 원장 합 10,000, 잔액 10,100 / When 야간 대조 / Then 불일치로 운영자 알림.
- **TV-11-158** Given `lock_state = 장터`인데 활성 등록이 없는 지팡이 / Then 야간 대조에 걸린다.
- **TV-11-159** Given `lock_state = 원정`인데 진행 중인 판이 없는 지팡이 / Then 야간 대조에 걸린다.
- **TV-11-160** Given 강화석 생성 합 500, 소멸 합 120, item_stack 합 300, 활성 등록 50, 미수령 우편 30 / Then 380 = 300 + 50 + 30, 일치.
- **TV-11-161** Given `item_stack.qty`를 음수로 만드는 갱신 / Then DB 제약(`qty >= 0`)으로 실패, 트랜잭션 롤백.

---

## 16. 오프라인 프로필에서의 동작

`IBackend`에는 "이 기능을 지원하는가"가 아니라 프로필 종류가 있고, 화면은 프로필 종류를 보고 메뉴를 정한다(online 8.3) [조율].

| 기능 | 오프라인 프로필 | 출처 |
|---|---|---|
| 광장, 채팅, 친구 | 없다 | (online 8.1) [조율: DEC 2.6] |
| 거래, 장터, 우편 | 없다 | (online 8.1) |
| 골드 상점(부적, 새 지팡이, 골드 꾸미기) | 된다. 같은 값, 같은 주간 한도(기기 시계 기준). 지팡이 개수 제한이 없는 것도 같다 | (online 8.1, 1.6) |
| 허수아비 연습 | 된다(메인 메뉴의 연습) | (online 8.1) |
| 옷장 | 된다 | (ui 4.24, online 8.1) |
| +10 이상 강화 알림 | 없다 | (online 8.1, fx 8) |
| 메뉴 표시 | 서버 전용 항목(광장, 결투, 시즌, 우편함, 친구, 순위표, 장터)을 흐리게 두지 않고 한 상자에 묶어 "서버에 접속하면 열립니다" 한 줄 | (wc 2.5, online 8.1) |

- `OfflineBackend`는 거래·장터·우편·채팅 함수를 구현하지 않는다(호출 자체가 없다). 상점 규칙 코드는 `Wizard.Rules`를 같은 소스로 쓴다(online 8.3) [제안].
- **TV-11-165** Given 오프라인 프로필, 네트워크 끊김 / When 메인 메뉴 → 상점 → +0 지팡이 구입 / Then 500골드가 빠지고 지팡이가 생긴다. 서버 주소로 나가는 호출이 없다(상태 파일 읽기 시도는 예외).
- **TV-11-166** Given 오프라인 프로필 / When 메뉴 구성 / Then 장터·우편함·친구가 묶음 상자 안에 있고 누를 수 있는 단추가 아니다.

## 17. 시각/소리 연결점

그림·이펙트·소리가 나중에 붙을 이벤트 이름 목록이다. 구현은 하지 않고, 클라이언트의 표시 계층이 이 이름으로 구독한다. 이름은 [임시]. 괄호 안은 원본이 정한 연출 단서(그릴 때 참고).

### 17.1 광장 공간

| 이벤트 | 때 | 단서 |
|---|---|---|
| `plaza_enter` | 내가 인스턴스에 들어옴 | 불러오는 화면 뒤(ui 4.7) |
| `plaza_leave` | 내가 나감 | |
| `plaza_channel_switch` | 채널 옮기기 완료 | |
| `plaza_member_spawn` / `plaza_member_despawn` | 남이 들어오고 나감 | |
| `plaza_footstep` | 걷는 동안 걸음마다 | 남의 소리는 8칸에서 −12dB, 16칸에서 들리지 않는다(fx 19) |
| `plaza_time_phase_changed(phase)` | 낮/해질녘/밤 전환 | 팔레트 3벌 색 교체(wc 10.3) |
| `plaza_interact_prompt_show(target)` / `_hide` | J 대상이 생김/사라짐 | "J 살펴보기" 등(ui 4.20) |
| `plaza_facility_open(facility_id)` | 시설 화면 열림 | |
| `plaza_menu_ellipsis_on(account)` / `_off` | 메뉴를 보는 사람 | 말줄임표 3프레임(ui S-2004) |
| `plaza_emote_wheel_open` / `_close` | K | 바퀴 360×360(ui S-2003) |
| `plaza_emote_play(account, emote_id)` | 감정표현 시작 | `emote_raise_wand`는 단계 효과 재생 |
| `plaza_sit(account, seat?)` | 앉기 | 큰 모닥불 통나무 의자 |
| `plaza_wand_idle_fx(account, wand)` | 지팡이 대기 효과 | 광장 예산(17.5) |
| `plaza_mailbox_has_letter(bool)` | 내 우편함에 새 우편 | 까마귀 우편함 "편지 있음" 2프레임(wc 10.3) |
| `plaza_practice_hit(account)` | 허수아비를 침 | 치는 본인 화면에만, 분류 2와 4 각 60(fx 9.3) |
| `plaza_duel_walk(account_a, account_b)` | 결투가 잡혀 돌길로 함께 걸어감 | 돌마당 가는 돌길(연출) |

### 17.2 알림

| 이벤트 | 단서 |
|---|---|
| `notice_band_show(kind = enhance10, wand, nickname?)` | 알림 띠 4초, 종 한 번(fx 8) |
| `notice_band_show(kind = enhance13, ...)` | 알림 띠 6초 금 테두리, 큰 종 + 속성 주제 선율 2마디 |
| `notice_crow_flyby` | 까마귀가 1.2초에 왼→오, 0.6초에 쪽지(fx 8) |
| `notice_nuru_eye_open` / `notice_nuru_eye_close` | 흔들의자의 느루 한쪽 눈(fx 8, wc 2.5) |
| `notice_sky_band_13(element)` | 광장 하늘 속성색 빛띠 6초 |
| `notice_bonfire_tint_13(element)` | 모닥불이 그 속성색으로 6초 |
| `notice_bundle(count)` | "방금 +10 이상 성공 n건" |

### 17.3 채팅·사회

| 이벤트 | 단서 |
|---|---|
| `chat_bubble_show(account, text)` / `chat_bubble_fade` | 말풍선, 4초 뒤 흐려짐 |
| `chat_message_received(channel)` | 채널 아이콘·글자색(ui 4.20) |
| `chat_send_rejected(reason)` | 입력 줄 대기 표시 |
| `chat_word_masked` | 가린 말 "＊" |
| `request_note_received(kind)` | 받은 신청 쪽지, 남은 시간 고리(ui 4.20) |
| `request_note_expired(kind)` | |
| `friend_request_received` / `friend_added` / `friend_online` / `friend_offline` | |
| `block_added` / `report_sent` | |
| `inspect_open(account)` | 큰 그림 1배(fx 1.2) |
| `inspect_wand_rotate(dir)` | 회전 그림이 있는 지팡이만 |

### 17.4 거래·장터·우편

| 이벤트 | 단서 |
|---|---|
| `trade_open` / `trade_cancel` / `trade_complete` | 거래 돗자리 열림·잠김(ui S-2301) |
| `trade_item_placed(side, slot)` / `trade_item_removed(side, slot)` | |
| `trade_lock_on(side)` / `trade_lock_released_all` | 자물쇠 3프레임(ui S-2302) |
| `trade_slot_changed_blink(side, slot)` | 3초, 초당 2회 이하 밝기만(ui S-2303) |
| `trade_confirm_ready` | 5초 대기 끝 |
| `trade_loss_warning_show` | 호박색 경고(ui 4.23) |
| `trade_scar_notice_shown` | "거래 상대에게는 흔적이 보인다" |
| `market_listed` / `market_cancelled` / `market_purchase_ok` / `market_purchase_failed(reason)` | |
| `market_sold` / `market_expired_returned` | 우편 도착과 같이 |
| `mail_new(reason)` / `mail_claimed` / `mail_claim_all_done` | 봉투 상태(ui 4.24) |
| `shop_purchase_ok(item)` | 행상 수레 손짓(wc 10.3) |

### 17.5 입자 예산 (데이터만, fx 9.3 [조율: DEC 2.8 effects.md가 유일한 기준])

| 분류 | 전체 | 줄임 | 최소 |
|---|---|---|---|
| 지팡이 대기 합계 | 400 | 200 | 60 |
| 환경(모닥불, 반딧불, 날씨) | 200 | 100 | 60 |
| 감정표현, 알림 띠, +13 하늘 빛띠 | 200 | 100 | 30 |
| 합계 | 800 | 400 | 150 |

지팡이 대기 400 나누기: 나 = 요청량 전부(60까지), 가까운 순으로 8명 = 요청량의 절반(올림, 30까지), 나머지 31명 = 입자 0(본체 그림과 본체에 딸린 부품만). 40명 전원 +13일 때 합계 300(요청 합계 40 × 60 = 2,400 중). 실제 최악은 성에 +13의 18 기준으로 나 18 + 8명 × 9 = 90. "남의 지팡이 효과" 옵션(전부/줄임/끔, 기본 전부)을 줄임으로 하면 8명의 몫이 0(fx 9.3, 18). 표시 계층은 `PlazaParticleBudget.Allocate(selfRequest, othersSortedByDistance)`를 둔다 [임시].

- **TV-11-162** Given 40명 전원이 60을 요청, 옵션 "전부" / When `Allocate` / Then 나 60 + 8명 × 30 + 31명 × 0 = 300.
- **TV-11-163** Given 성에 +13 18 요청 40명 / Then 18 + 8 × 9 = 90.
- **TV-11-164** Given 옵션 "줄임" / Then 8명 몫 0, 나 60.

### 17.6 에셋 식별자 (그림 단계에서 꽂을 자리)

ui S-2001(닉네임 판), S-2002(칭호 띠), S-2003(감정표현 바퀴), S-2004(말줄임표), S-2005(말풍선), S-2006(물건 이름표), S-2007(시설 팻말), S-2301~S-2307(거래·장터·상점), S-2401~S-2407(우편함·옷장·흔들의자), I-F3(사람 관계 아이콘 10종), I-F8(채팅 채널 아이콘 7종). 광장 시설 그림은 wc 10.3(큰 모닥불 128×128 6장, 결투 게시판 128×128 등).

---

## 18. 충돌

문서 사이 불일치와 택한 쪽. DECISIONS가 직접 답하지 않는 것은 "주인 문서" 원칙(시설 이름·자리 = wc 2.5, 기능 = online 5.2, 화면 = ui)으로 풀었다.

| # | 내용 | 택한 쪽 |
|---|---|---|
| C-01 | 직접 거래 수수료: online 2.4 "오간 값의 5% (지팡이가 끼면 감정가의 10%)" vs online 5.5 "주고받은 값이 큰 쪽의 5%를 양쪽이 반씩". 지팡이가 끼었을 때 10%를 "반씩"인지 "전체"인지, 두 식을 어떻게 합칠지 원문이 정하지 않았다 | DECISIONS는 말하지 않음. 6.5절의 합친 식 [임시]: 물건마다 요율(지팡이 10%, 나머지·골드 5%)을 곱해 쪽마다 합하고, 큰 쪽을 양쪽이 반씩. 지팡이 없는 거래는 5.5와 똑같고, 지팡이 낀 거래는 2.4·4.9의 "감정가의 10%"와 같다 |
| C-02 | 선물 수수료: online 5.5 "감정가의 5%" vs 지팡이 요율 10%(5.5 표, 2.4, 4.9 "오갈 때마다 감정가의 10%") | 지팡이 선물은 10%, 나머지 5% [임시]. 4.9가 지팡이를 맡기는 길(선물 포함)에 10%를 전제로 샌드배깅 대응을 짰으므로 그쪽을 따른다 |
| C-03 | 광장 이동 속도: online 5.1 "속도 5.0" vs 캐릭터마다 다른 이동 속도(wc 5.1: 달구 4.7, 초롱 5.6, 갈피 5.0, 성에 4.5, 그믐 5.2; GDD 3 기준값 5.0) | online 5.1(광장의 주인)을 택해 **광장에서는 전원 5.0** [제안 그대로]. 광장은 전투가 없고 함께 걷는 공간이므로 같은 속도가 맞다. 개발자가 캐릭터 속도를 원하면 `plaza.move_speed`를 캐릭터별 표로 바꾼다(19절 U-02) |
| C-04 | 광장 시설 수: wc 2.5 "시설 10곳" / online 5.2 표 10줄 / ui 4.20 시설 이름표 9개(큰 모닥불 없음) | 시설은 10곳(wc 2.5). 큰 모닥불은 화면이 없으므로 이름표만 없다고 본다(ui 그대로). 충돌 아님으로 정리 |
| C-05 | 광장 메뉴(Esc): online 5.1 "강화, 서고, 옷장, 우편함, 설정" vs ui 4.20 "강화, 서고, 옷장, 우편함, 순위표, 채널 옮기기, 설정, 메인 메뉴로"(8개) | ui 4.20의 8개(상위 집합). 화면 구성은 ui가 주인이고 online의 목록과 모순되지 않는다 |
| C-06 | 행상 수레 품목: online 5.2 "부적, 새 지팡이, 골드 꾸미기, 서고 확장" vs ui 4.23 탭 "부적 / 새 지팡이 / 꾸미기 / 그 밖(서고 확장, 닉네임 바꾸기)" | ui 4.23(닉네임 바꾸기 포함). online 2.4가 닉네임 변경 5,000골드를 지출로 적고 있어 근거가 있다 |
| C-07 | 연습 허수아비 기능: wc 2.5 "허수아비 연습. 다른 사람의 연습을 구경" vs online 5.2 "허수아비 연습 + 결투 입력 지연 체험(4.12절)" | online 5.2(기능의 주인, wc 2.5 스스로 "세부는 online.md 5.2절"이라고 적음). 지연 체험을 포함 |
| C-08 | 흔적이 보이는 곳: online 1.3 "거래 화면과 장터 상세에서는 항상" vs online 5.5 "거래 창, 장터 목록과 상세, 최종 확인" vs fx 1.3 "직접 거래 창, 장터 목록과 미리보기" | 가장 넓은 쪽(거래 창, 최종 확인, 장터 목록, 장터 상세, 구매 확인)에서 항상 보인다. DEC 1.15 "거래할 때는 상대에게 항상 보인다"에 맞는다 [확정] |
| C-09 | 서버 전체 채팅 채널 목록: DEC 2.7 "주변, 광장, 거래, 귓속말, 친구, 결투방" vs online 5.3·ui 4.20의 "시스템" 채널 추가 | DEC 2.7은 사람이 말하는 채널을 나열한 것이고 시스템은 읽기 전용 알림이다. 시스템 채널을 둔다(서버 전체에 가는 것은 시스템 알림뿐이라는 DEC 2.7·online 5.3과 맞는다) |
| C-10 | "+0 지팡이는 장터 불가"의 표시: online 5.5에서 [제안], online 14절 10번에서 "기본값으로 본문에 적용" | 규칙으로 구현([제안] 그대로). 개발자 확인 대상 |
| C-11 | 강화 알림 이름 설정: online 1.4 "강화 알림에 내 이름 표시" vs fx 8 "online.md의 공개 범위 설정" | 같은 설정 하나(`account.show_name_in_alerts`, online 7.9 열 이름) |
| C-12 | 마지막 한 자루 계산과 보유 IL: online 1.6 "장터에 올린 것·미수령 우편은 1자루로 치지 않는다" vs online 4.7 "우편함·장터의 것도 보유 IL에 넣는다" | 서로 다른 계산이므로 둘 다 그대로. `InHandCount`(8.1)와 보유 IL(8.3)을 따로 구현 |
| C-13 | 장터 보증금: "팔리면 돌려받는다 / 안 팔리면 사라진다"(online 5.5) vs "일부만 살 수 있다"(같은 절) — 일부만 팔린 경우가 정해지지 않음 | 팔린 수량 비례 환급, 남은 몫은 만료·내리기 때 사라짐 [임시](9.4~9.5). 19절 U-08 |
| C-14 | 귓속말: online 5.3 "접속 중인 한 사람, 차단한 사람에게서는 오지 않는다" vs online 10 "귓속말은 기본 친구만" | 둘 다 적용(차단 검사 + 기본 친구만, 설정으로 "모두" 가능) [제안] |
| C-15 | 감정표현 "앉기": online 5.1 감정표현 8종의 하나 vs online 5.2 큰 모닥불 "앉기 동작" | 같은 동작으로 본다. 모닥불 둘레에서는 통나무 의자에 앉는 자리 맞춤만 더한다 [임시] |
| C-16 | 거래 열림 조건의 "도달": online 5.5 "무한 웨이브 10 도달" vs online 5.3 채팅 "무한 웨이브 10에 닿기 전까지" | 같은 값(`best_wave_ever >= 10`)으로 구현 [임시] |

---

## 19. 미정

원문이 정하지 않았거나 "개발자 확인 필요"인 것. 임시 기본값은 본문에 이미 넣었다. 번호는 `U-xx`.

### 19.1 원본의 개발자 확인 항목 중 이 영역에 닿는 것

| # | 원본 | 내용 | 지금 적용한 값 |
|---|---|---|---|
| U-01 | online 14절 10번 | 개수 제한이 없어진 뒤의 딸린 값: +0 지팡이 500골드, +0은 장터 불가·직접 거래만, 장터 등록 한도 계정당 20건, 떠나보내면 감정가의 10% | 원문 기본값 그대로 [제안] |
| U-01b | online 14절 5번 | 이메일을 확인하지 않은 계정도 거래까지 되는가 | 된다(원문 기본값). 개발자가 바꾸면 `TradeGate`에 `NeedEmailVerified` 한 줄 추가 |
| U-01c | online 14절 11번 | 지팡이를 팔아도 그 시즌에는 리그가 내려가지 않는 규칙 | 원문 기본값(시즌 안에서 올라가기만) |
| U-01d | fx 개발자 확인 2번 | 하락 흔적 공개의 기본값 | 끔(비공개) |
| U-01e | GDD 개발자 확인 46번 | 오프라인 프로필의 야영지 | 없음(메인 메뉴에서 모든 화면) |
| U-01f | wc 개발자 확인 7번, GDD 19번 | 행상 수레를 느루의 흔들의자에 합칠지 | 따로 둔다 |
| U-01g | wc 개발자 확인 3번, GDD 17번 | 같은 캐릭터가 광장에 여럿 보이는 이유(설정) | 기능 영향 없음 |
| U-01h | ui 개발자 확인 2번 | 경고에 빨강을 쓰지 않고 호박색 | 호박색(손해 경고) |

### 19.2 원문이 정하지 않은 것 (이 명세서의 [임시])

| # | 무엇 | [임시] 기본값 | 이유 |
|---|---|---|---|
| U-02 | 광장 이동 속도를 캐릭터별로 할지 | 전원 5.0 | online 5.1 그대로(C-03) |
| U-03 | 40명/35명의 해석 | 40 = 하드 상한, 35 = 자동 배정 상한(36명 이상이면 자동 배정 안 함, 모두 36 이상이면 새 인스턴스) | "35명이 넘으면 새 인스턴스"를 자동 배정 규칙으로 읽음 |
| U-04 | 자동 배정 우선순위 | 친구 수 → 인원 → 채널 번호 | 광장을 붐비게, 친구와 만나게 |
| U-05 | 빈 인스턴스 닫기 | 5분 비면 닫음(마지막 1개는 남김) | |
| U-06 | 시설 좌표, 몸통 크기, 스폰 위치, 상호작용 반경 2.0칸, 몸 반지름 0.30칸 | 3.1절 표 | wc 2.5는 상대 위치만 준다. 그림이 나오면 맞춘다 |
| U-07 | 지팡이 감정가 표의 중간 단계 값과 반올림 | `enh.py` 같은 모형으로 다시 계산, 10골드 반올림(6.4절 표). **+1 감정가 50이 +0 감정가 250보다 낮다** — 원문 공식("그 단계까지 평균 강화 골드 × 0.5"에 +0 값이 없음) 그대로 둠 | 개발자가 고치려면 `감정가 = 250 + 표 × 0.5` 같은 식으로 바꿀 수 있다(떠나보내기 환급과 수수료가 같이 바뀐다) |
| U-08 | 장터 일부 판매 때 보증금, 내리기 때 보증금 | 팔린 수량 비례 환급, 내리기·만료 때 남은 몫은 사라짐 | C-13 |
| U-09 | 장터 수수료·보증금의 반올림 | 수수료 올림, 보증금 올림(최소 1골드), 비례 환급 버림 | 골드가 새로 생기지 않는 쪽 |
| U-10 | 선물의 경로(화면) | 직접 거래 창에서 한쪽만 올린 경우 + 친구 창의 [선물 보내기](우편) | 원문은 조건과 수수료만 정함. "우편 첨부로도 넘길 수 없다"(online 5.5)가 우편 선물의 존재를 시사 |
| U-11 | 선물을 받는 쪽도 거래 조건(웨이브 10 + 2시간)을 채워야 하는가 | 채워야 한다 | 갓 만든 계정으로 몰아주기 차단이 조건의 목적 |
| U-12 | 하루 골드 상한의 "하루" 경계, "누적 획득 골드"의 범위 | 한국 시각 0시 / 플레이 수입 + 장터 판매 대금, 직접 거래·선물로 받은 골드는 제외 | 돌려막기로 상한을 키우지 못하게 |
| U-13 | 손해 경고의 판정식과 대상 | `max > 5 × min`(min > 0), 값이 작은 것을 받는 쪽에게 | "차이가 5배를 넘으면" |
| U-14 | 갚을 것이 있을 때 우편으로 받는 장터 대금도 "획득분"으로 먼저 빼는가 | 뺀다 | online 3.2 "다음 획득분에서 먼저" |
| U-15 | 거래 신청·친구 신청의 수락 대기 시간 | 거래 신청 20초(도전과 같게), 친구 신청 7일 | 받은 신청 쪽지에 "남은 시간 고리"(ui 4.20)가 있다 |
| U-16 | 거래 창이 열린 뒤 거리 재검사, 유휴 시간 | 다시 재지 않음(거래 중 이동 불가), 5분 동작 없으면 닫힘 | |
| U-17 | 결투방 경기 중 정해진 문구 8개의 내용 | `duel_preset_phrases.json`: "잘 부탁해", "좋은 경기", "한 판 더?", "와!", "아깝다", "고마워", "잠깐만", "다음에 또" | 원문은 개수만 정함 |
| U-18 | 친구·결투방·귓속말 채널의 발언 간격 | 1초 | 원문 빈칸 |
| U-19 | 같은 문장 반복의 범위 | 정규화 뒤 같은 문장, 채널 무관, 최근 10분 안 3회째 | "같은 문장 반복은 3회째부터 막는다" |
| U-20 | 80자 초과 메시지 | 자르지 않고 거절 | 입력 줄이 "12/80"으로 막으므로 서버까지 오면 비정상 |
| U-21 | 연락처 모양 정규식의 세부 | 4.3절 패턴(데이터 파일) | QA에서 오탐을 보고 조정 |
| U-22 | 주변 채널 8칸 경계, 거래 5칸 경계 | 경계 포함(≤) | |
| U-23 | 신고 자동 막힘의 반복, 신고 남용 | 1시간 지나면 풀림, 다시 5명이면 다시 1시간. 하루 신고 20건 | |
| U-24 | 차단한 사람에게 보낸 귓속말·신청이 보낸 쪽에 어떻게 보이나 | 귓속말은 "보냈다"처럼, 신청은 "상대가 받을 수 없다" | 차단 사실을 너무 드러내지 않는다 |
| U-25 | 친구 상태에 "메인 메뉴"를 둘지, 접속 숨기기 | 둔다 / 접속 숨기기 설정(기본 끔) | 원문 셋(광장 채널/원정 중/결투 중) 밖의 상태가 실제로 있다 |
| U-26 | 친구 끊고 다시 맺을 때 선물 7일 | 새로 센다 | 맡기기 남용 방지 |
| U-27 | 쪽지 길이, 쪽지·우편 보관 기간 | 제목 24자, 본문 400자, 쪽지 30일, 첨부 있는 우편은 만료 없음 | ui 4.24의 "남은 보관 기간"은 쪽지에만 표시. 물건이 사라지면 대조와 분쟁이 어렵다 |
| U-28 | 우편 블록 부분 수령 | 서고 여유만큼만 받고 나머지는 남김 | online 2.4 "자리가 날 때까지 우편함에 머문다" |
| U-29 | 겹쳐지는 물건의 장터 등록 방식 | 수량을 `item_stack`에서 빼서 등록 행으로 옮긴다(예약이 아니라 이동) | 원문 `item_stack` 칸에 "장터분"이 없다 |
| U-30 | 7일 중앙값의 계산 방법, 거래가 없을 때 감정가 | 건별 낱개 값의 중앙값, 매시 갱신 / 기본 감정가 표 | |
| U-31 | 장터 만료 정리 주기, 서버 정지 시간 측정 | 1분 / 10초 하트비트 | |
| U-32 | 든 지팡이 자동 교체의 동률 | 단계 → 역대 최고 → id 순 | 원문 "단계가 가장 높은 것" |
| U-33 | 장터 산 지팡이의 7일 잠금 시작 | 산 순간 | |
| U-34 | 보안 잠금(24시간) 동안 장터 구매·우편 수령 | 된다 | 원문은 "거래와 장터 등록"만 막는다 |
| U-35 | 거래 창 커밋 실패 뒤 창을 닫을지 | 닫지 않고 Open으로(잠금 해제) | |
| U-36 | 멱등 키에서 업무 규칙 거절을 저장할지 | 저장한다(재전송이 같은 답을 받게). 예외(롤백)는 저장하지 않는다 | |
| U-37 | 교착 시 재시도 | 최대 3번 | |
| U-38 | 광장 스냅샷 포맷 | 1/64칸 `ushort` 좌표 등 10바이트 | online 7.5의 "약 10바이트" 추정에 맞춤 |
| U-39 | 광장 이동 계산의 수 표현 | Q32.32(코어와 같은 수학 함수) | 예측과 서버가 같은 값을 내게 |
| U-40 | 시간대 기준점 | 유닉스 시각 0 | 어느 기준이든 모두가 같으면 된다 |
| U-41 | 감정표현 길이·간격, 바퀴에 거는 8개 | 2~3초, 1초 간격, 계정 설정 | |
| U-42 | 채팅 끄기의 뜻 | 받기만 막는다 | |
| U-43 | 살펴보기 응답에서 가린 값 | 서버가 아예 보내지 않는다 | 클라이언트 조작으로 흔적을 보는 길을 막음 |
| U-44 | 거래 채널 아이템 링크 | 발언 시점 스냅샷을 서버가 붙인다 | |
| U-45 | 장터 등록가 하한·자기 물건 구매 | 1골드 이상 / 자기 등록은 못 산다 | |
| U-46 | 시설 J 안내가 사람과 겹칠 때 | 가까운 것, 같으면 사람 | |

---

## 20. 출처 대조 체크리스트 (빠뜨림 확인용)

online 5.1: 40명, 35명 초과 새 인스턴스, 자동 배정·채널 목록·친구 채널 먼저, 40×24칸, WASD 8방향, 속도 5.0, 발사·비기 없음, 서버 권위 10틱, 예측·0.1초 보간, 속도·벽 검사, 키(J, K, Enter, Tab, Esc와 메뉴 항목), 24분 하루(12/4/8분), 모든 인스턴스 같이·색만, 머리 위(닉네임, 칭호, 리그 휘장), 지팡이 실제 단계, 강화 표시 없음, 광장 입자 예산, 감정표현 8종 + 꾸미기 → 2.3~2.8절, 17.5절.
online 5.2: 시설 10곳의 기능, 강화 장소·NPC 없음, 메인 메뉴에서 되는 것 11가지, 광장에서만 되는 것 5가지 → 3절.
online 5.3: 채널 7개와 범위·제한, 서버 전체 채팅 없음, 필터(사전 + 정규화 3종, 단어만 가림, 주소·전화·계좌·메신저 통째로), 새 계정 주변만(웨이브 10), 80자, 반복 3회째, 신고(5종, 30줄, 5계정·24시간·1시간), 차단(5가지, 200명), 채널 끄기·전체 끄기 → 4절.
online 5.4: 친구 100명, 상태 3종, 따라가기·결투 초대·귓속말, 최근 만난 사람 20명, 살펴보기 항목, 조립 공개 끄기(단계·겉모습 항상), 흔적은 공개 켠 때만·지팡이마다·기본 끔 → 5절.
online 5.5: 열림 조건(웨이브 10 + 2시간), 그 전 시세 조회만, 갚을 것, 직접 거래 5단계(5칸, 8칸, 잠금·해제·3초, 5초·글자 확인·흔적 항상·리그 상승, 한 트랜잭션), 장터(즉시 구매, 입찰 없음, 48시간, 20건, 1% 보증금, +1 이상, 묶음 일부 구매, 7일 중앙값·거래량, 0.3~5배·30건 미만·지팡이 범위 없음·감사, 흔적 항상), 품목 표 12줄, 수수료 계산(큰 쪽 5% 반씩, 선물 친구 7일·5%), 지팡이 규칙 표 16줄, 감정가 공식·+0 250골드·쓰임 → 6~10절.
online 5.6: 서버 인벤토리, C# + 트랜잭션 + FOR UPDATE + 적용 함수 7종, 원장 하나, 직접 거래 잠금 순서, 장터 구매 동시성, 우편 조건부 UPDATE, 멱등 키(30일, run_id, order_id), 개체 번호·소유자·버전, 수량 ≥ 0, 야간 대조 4종, 바꿔치기 방지, 5배 경고, 강화 난수·enhance_log → 12절.
wc 2.5: 서버 자체 계정만, 위치, 40×24, 조작, 시간대, 색(모닥불 주황~노랑), 대화 채널, 시설 10곳 표(이름 유일 기준, 금지 이름 5개), 강화 장소 없음·작업대는 배경, 서버 알림 연출(띠·까마귀·느루), 지팡이 현재 단계로 보임, 오프라인 때 안내 → 2.1, 2.6, 2.7, 3절.
ui 4.20~4.24의 동작 규칙: 닉네임 판(친구 초록, 차단 판 없음), 칭호 띠 4종, 말줄임표, 말풍선(20자 × 4줄, 4초, 귓속말 풍선 없음), 닉네임 2~8자, 대화창 접기·11줄, 채널 탭 7개·꺼 둔 탭, 채널 글자색, 물건 이음, 남은 글자 수, 가린 말, 줄 누름 목록(귓속말·살펴보기·차단·신고), 상호작용 목록 8개, 감정표현 바퀴, Tab 목록, 광장 메뉴 8개, 받은 신청 쪽지 3종, 시설 이름표 9개 / 광장 도전(수락 남은 시간, 리그 규칙 체크) / 친구 창 탭 4개·"37 / 100"·닉네임 신청, 살펴보기 구성, 신고 창, 차단 확인 / 거래 창(8칸, 흔적 항상, 자물쇠, 3초·초당 2회, 가방 탭 5개, 놓을 수 없음 이유 4종, 귀속 칸, 수수료 줄, 5초 확인, 최종 확인 세 줄, 손해 경고 호박색), 장터(탭 4개, 분류 6개, 정렬 3개, 목록·상세 흔적 항상, 팔기 탭 놓을 수 없음, "12 / 20", 구매 확인), 상점(탭, 유료 탭 0개면 없음, 주간 한도 표시, "지금 n자루") / 우편함(탭 3개, 보낸 이 4종, 받기·모두 받기, 검증 대기 잠김, 쪽지 쓰기) → 2.7, 2.8, 4, 5, 7, 9, 11절.
