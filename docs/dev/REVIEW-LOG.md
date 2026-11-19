# 설계 검토 기록 — 엄청엄청 위대한 마법사 키우기

검토자 세 명(결정론·넷코드, 명세 대응, 빌드 가능성·병렬성)이 dev 문서 묶음과 `tasks.json`에 낸 문제 76건과 기술 총괄의 판단이다. 판단은 **반영**(고쳤다), **반영(변형)**(문제는 맞고 고쳤지만 제안과 다른 방법을 골랐다. 이유를 적었다), **거절** 셋이다.

## 결과

| | 건수 |
|---|---|
| 낸 문제 | 76 (막는 문제 10, 큰 문제 44, 작은 문제 22) |
| 반영 | 62 |
| 반영(변형) | 14 |
| 통째로 거절 | 0 |
| 문제는 받아들이되 제안의 일부를 거절한 것 | 8 (아래 "거절한 세부 제안") |

통째로 거절한 것이 없는 이유: 명세의 해당 줄이나 이 기기의 실제 상태(Unity 모듈 목록, bash 판, Docker, Rosetta)로 확인할 수 있는 것은 확인했고, 확인한 것은 각 줄의 "이유"에 "확인했다"로 적었다. 나머지는 dev 문서 안의 모순이나 빠짐이라 문서를 읽고 판단했다. 틀린 지적은 찾지 못했다. "확인했다"가 없는 줄은 명세를 다시 대조하지 않은 것이다.

검사: `node tools/check-tasks.mjs` 통과 — 작업 271개, 위상정렬 됨, 같은 묶음의 경로 겹침 0, 경로가 겹치는 38쌍 전부 `deps`로 순서 있음, 명세 벡터 2,855개 전부 정확히 한 작업에 배정(미배정 0, 이중 배정 0), 마일스톤 집계 일치.

### 거절한 세부 제안

| 문제 | 거절한 제안 | 이유 | 대신 한 것 |
|---|---|---|---|
| B07 | 작업 트리를 저장소 밖(`../wizard-wt/<id>`)으로 옮긴다 | 에이전트가 쓸 수 있는 폴더가 작업 디렉터리 아래로 묶여 있다. 저장소 밖은 호출마다 허가를 물을 수 있다 | `.claude/worktrees/`에 두고 금지 규칙의 문구를 고쳤다. 저장소를 훑는 도구는 `src/`, `server/`, `tests/`, `tools/`, `data/`, `unity/Assets/`를 뿌리로 훑어 사본을 세지 않는다 |
| B18 | `ErrorCode`·`Result`를 새 어셈블리 `Wizard.Common`으로 옮긴다 | 열 번째 공유 패키지(asmdef, package.json, 의존 그림)가 늘어난다. 해시의 범위만 줄이면 된다 | 폴더로 가른다: `Core/Errors/`와 `Content/Rules/`를 해시에서 빼고 분석기가 전투 쪽의 참조를 막는다 |
| D07 | 보정 이벤트의 시각을 "현재 틱 + D, 오프셋 0"으로 | 같은 틱에 이미 낸 이벤트(오프셋 > 0)보다 앞서게 되어 한 패킷 안의 순서가 역행한다. 새 검증 규칙에서 역행은 패킷 거부다 | 만드는 순간의 입력 시계 시각으로 찍고 직전 이벤트보다 앞서지 않게 올린다 |
| D15, S15 | 이 검토에서 루트 `CLAUDE.md`와 `.claude/agents/`를 바로 고친다 | 두 곳은 모든 에이전트의 지시문이라 사용자 승인이 있어야 한다(PIPELINE 5.5절). 에이전트의 말로는 승인이 되지 않는다 | 원본(`docs/dev/agent-config/`)을 고쳤다. 설치는 M0의 첫 작업 M0-TL-02(사용자 승인)다. 그 전의 작업 카드에는 규칙을 붙인다 |
| S06 | 허브(M12)가 생기기 전에 HTTP 활동으로 `play_seconds`를 어림한다 | 이 값을 읽는 거래(M13)와 자동 매치(M14)가 허브보다 뒤다. 어림값을 쌓으면 나중에 두 셈이 섞인다 | M12-API-01이 처음부터 센다 |
| B05 | 비어 있는 상태 절은 전부 해시에서 뺀다 | 스칼라 상태를 가진 절은 "비었다"를 정의하려면 초기값 비교가 필요하고, 그 비교가 틀리면 해시가 조용히 갈린다 | 개체 풀·목록만으로 이루어진 절만 개수 0일 때 생략한다. 절마다 꼬리표를 붙여 생략이 모호하지 않게 했다 |
| S03 | 작업마다 벡터 id를 하나하나 배열로 적는다 | 벡터가 2,855개다. 절 전체를 맡은 작업까지 id를 늘어놓으면 명세에 벡터를 더할 때마다 배열이 어긋난다 | 선택자를 쓴다: 절 전체(`S07§17.3`), 하나(`S13:TV-051`), 같은 절 안의 범위(`S13:TV-026..TV-028`). 검사기가 명세에서 id를 뽑아 미배정·이중 배정을 0으로 강제한다 |
| D04, D13 | `wizard-api`가 요약(JSON)을 정규 바이트로 바꿔 저장한다 | 바꾸는 코드가 지금 버전의 서버에 있으면 옛 코어의 워커가 쓴 바이트와 같다는 보장이 서버 버전에 기댄다 | 클라이언트가 그 판의 코어로 쓴 facts 바이트를 그대로 올리고 서버는 저장·읽기만 한다. 워커도 같은 코어의 같은 함수로 쓴다 |

## 표

머리글자: D = 결정론·넷코드, S = 명세 대응, B = 빌드 가능성·병렬성. 심각도: 막 = 막는 문제, 큰 = 큰 문제, 작 = 작은 문제.

### 결정론·넷코드

| # | 심각도 | 문제 | 판단 | 이유와 반영한 곳 |
|---|---|---|---|---|
| D01 | 막 | 결투 입력 검증: 봉인된 틱의 이벤트와 순번 건너뜀을 패킷 버림으로 처리해 정직한 플레이어가 몰수패한다 | 반영 | spec 10 14.8절을 확인했다. 표의 "늦은 시각: 그 이벤트를 버린다"와 의사코드의 `reject Packet`이 명세 안에서 모순이고, TV-N15·N16이 의사코드 쪽을 따랐다. 패킷이 8틱을 겹쳐 싣기 때문에 늦은 이벤트 하나가 뒤따르는 패킷을 줄줄이 버리게 한다. CONTRACTS 10.6절에 검증 순서를 정본으로 적었다(패킷 거부는 패킷 안의 역행뿐, 순번 공백은 유실로 소비, `base_seq`). spec 10 14.8절의 표·의사코드와 TV-N15·N16을 `[조율]` 표시와 함께 고쳐 적었다. 통합 표, T-45, M2-NET-04·06의 수락 조건, NetSim의 연속 유실 모형. (보충, M1-TE-01) 같은 규칙의 벡터인 spec 01 T-I13("순번이 1씩 늘지 않는 패킷 → 그 패킷을 버린다")이 빠져 있어 TV-N16과 같은 꼴로 고쳐 적었다. T-I06·T-I13은 서버 수신의 벡터라 M2-NET-04로 옮겼다 |
| D02 | 막 | 입력 전송이 틱 진행에 묶여 마감 여유에서 프레임 한 장이 빠진다 | 반영 | ARCHITECTURE 6.1절의 루프를 "Drain → Quantize → OnLocalInput → 즉시 송신 → 틱 루프"로 바꿨다. CONTRACTS 6.5절을 `Quantize`와 `TickInputQueue.TakeForTick`으로 나누고 여유 식 `(D + 1 − f)틱 − 편도 − 전송 대기`를 적었다. D의 표는 데이터(`NetRuleSet`)에서만 읽는다. 호스트의 수신 시각은 커널 타임스탬프, 루프는 "다 비운 뒤 Advance". M2-NET-06에 "오프셋 균등 분포 입력의 폐기율 0", 못 넘으면 수치 조정 작업(OPEN-QUESTIONS Q-06) |
| D03 | 막 | 재실행의 시작 조건(`unlocked_arts`, `season_tag_on`)을 서버가 갖고 있지 않다 | 반영 | `run.setup_canonical`(= `.wzrc` 머리말 바이트)을 더했다. 서버가 출발 때 만들어 저장하고 같은 바이트를 내려 준다. 워커는 DB의 값으로만 머리말을 만들고 파일의 머리말은 1차 검사에서 바이트 대조만 한다. CONTRACTS 3.2·8.3·12.1절, M11-RUN-01·02·03, M11-VER-01, M11-TE-01 |
| D04 | 막 | 2차 검증이 무엇과 무엇을 비교하는지 없다 | 반영(변형) | T-23을 고쳐 적었다. 비교 대상 = `RunResultFacts`의 바이트(주장: `run.claimed_facts`, 재실행: `verify_queue.result_facts`). 일치 = 바이트 동등 + 받아 둔 중간 지점 전부 일치. "정산에 쓰이는 모든 필드는 facts 안에 있다"를 불변식으로 적었다. 변형: 요약을 서버가 바꾸지 않고 클라이언트가 바이트를 올린다(위 표). M11-TE-01에 필드별 변조 사례와 다시 하기 사례 |
| D05 | 큰 | 왕복 지연과 D를 서버가 잴 수 없다 | 반영 | CONTRACTS 10.2·10.4·10.5절: 서버 시각의 에코와 붙들어 둔 시간, `server_seq`, 서버가 D를 정해 `HelloAck`로만 내려 줌, `DuelSetup`에서 D 제거(`SetInputDelay`, WZDR의 별도 칸), 대기열 진입 전 UDP 탐침, 시작 전 취소. 7.3절에 내부 API 넷, T-45 |
| D06 | 큰 | 결투 재접속 계약이 없고 nonce가 재사용될 수 있다 | 반영 | `duel.rejoin`, 키는 (경기, 사람, epoch)마다, 같은 키에서 패킷 번호는 되돌아가지 않는다, 응답 주소 규칙, 서버가 만든 뗌 이벤트의 순번 소비, `DisconnectSilenceTicks`. CONTRACTS 9·10.1절, `Rekey`·`ResumeInfo`, M14-NET-01·TE-01 |
| D07 | 큰 | 버려진 누름·뗌 뒤 키 상태가 어긋난 채 남는다 | 반영(변형) | CONTRACTS 6.4절 "버려진 내 입력과 키 상태 맞추기", `IInputSource.CurrentKeys`, `PlayerView.Keys`, M2-NET-05의 "2틱 안에 같다". 변형: 보정 이벤트의 시각(위 표) |
| D08 | 큰 | 되감기 한도 초과의 처리가 online 4.11-6과 다르고 문서끼리도 다르다 | 반영 | online 4.11-6과 spec 10 TV-N22를 확인했다("멈추고 기다린다"). 네 곳(netcode-engineer, M2-NET-05, M2-NET-06, PIPELINE 7.4)을 `Stalled`로 통일했다. 입력 시계와 예측 틱을 갈랐고, 멈춤이 풀리면 여러 틱을 따라잡는다. 전체 상태 요청은 해시 불일치와 긴 확정 공백뿐 |
| D09 | 큰 | 정수 나눗셈의 예외 동작이 런타임과 아키텍처마다 다르다 | 반영 | CONTRACTS 1.1·1.2절에 나눗셈을 전 함수로 정의했다(0 제수는 직접 검사해 `SimInvariantException`, `MinValue ÷ −1`은 래핑, 128÷64 넘침은 아래 64비트). 도우미 `DivInt` 등, 분석기의 원시 `/`·`%`와 `checked` 금지, 산술 경계 골든(`tests/golden/arith/`). T-06, T-42 |
| D10 | 큰 | `SpellTable`이 스냅숏·직렬화·해시 밖에 있다 | 반영 | CONTRACTS 3.2절 "`SpellTable`은 세계 상태다"(원천, 추가 순서의 핸들, 같은 원천은 같은 핸들, 길이로 되감기, `ReadState`는 다시 컴파일). 새 핸들이 생기는 곳을 세계 생성과 0단계로 묶었다. M2-SPL-02, M1-TE-01 |
| D11 | 큰 | T-13의 "창이 떠 있으면 틱 정지"가 순위·가속 검사의 시간 모형을 깬다 | 반영 | spec 09 8.3절("야영도 코어 틱은 나간다"), spec 07 11.4절과 TV-158("야영에서 3,000틱 머묾")을 확인했다. spec 01 16절의 [임시]만 반대였다. T-13을 고쳤다: 틱을 멈추는 것은 일시정지뿐. 통합 표, CONTRACTS 3.1절, ARCHITECTURE 6.1절, M1-CLI-02, M4-RUN-01, M11-ANT-01. spec 01의 그 줄에 `[조율]` 표시 |
| D12 | 큰 | 야영 하위 상태의 전이를 기록할 명령이 없다 | 반영 | spec 07 11.4절을 확인했다(닫기와 연습은 화면 조작). `CloseCampScreen`, `EnterPractice`, `ExitPractice`를 더하고 CONTRACTS 3.2절에 하위 상태별 키·명령 표를 적었다. `SimWorld.CanApply`를 더해 정직한 기록에는 무효 명령이 없게 하고, 재실행의 무효 명령은 `RecordCorrupt`. M4-RUN-01, M4-INT의 골든 |
| D13 | 큰 | `wizard-api`는 코어 한 버전만 링크하는데 두 버전의 판을 정산한다 | 반영(변형) | T-18에 "판 처리 경로는 코어 버전에 기대지 않는다"를 더했다. `ISettlementRules.Settle(RunResultFacts, …)`, 1차 검사의 변덕은 spec 09 8.2의 `whims_upper`(확인했다), `IRunLimits.ScoreCap(wave, whimsUpper)`. Rules → Sim 참조는 리그 제한뿐. M11-INT에 "코어 N 출발 → 서버 N+1 → 확정" 종단 테스트. 변형은 D04와 같다 |
| D14 | 큰 | `ReplayRunner`가 신뢰할 수 없는 기록을 거르는 규칙이 없다 | 반영 | CONTRACTS 3.8절: `InputRecordCodec.TryRead`의 구조 검사 목록, `Step`의 전제 검사, `ReplayStatus`에 `SimFault`·`Timeout`·`PackMissing`, 판정 셋(`match`, `mismatch`, `unverifiable`). 구현 결함은 회수로 가지 않는다. 판당 틱 상한은 `SimLimitsDef`. M1-SIM-07, M11-VER-01, M11-RUN-04, M16-ADM-01 |
| D15 | 큰 | 금지 목록에 서버 동시 실행에서만 터지는 것이 빠졌다 | 반영(변형) | 가변 정적 필드, `[ThreadStatic]`, `unsafe`, 메모리 재해석 API, `checked`를 CLAUDE 원본 3절, CONTRACTS 0.1절, T-42, M0-BT-02에 넣었다. "세계 16개를 16스레드에서"를 M1-TE-01에. 변형: 설치는 M0-TL-02(위 표) |
| D16 | 큰 | 세 런타임 골든이 전부 arm64다 | 반영 | 이 기기를 확인했다: Rosetta 2 있음, macOS x64 Mono 변형 있음, x64 dotnet 런타임 없음. T-30을 런타임 × 아키텍처로 고치고 `dotnet test -a x64`와 Intel 64-bit 플레이어의 골든을 M1-UE-03과 `unity-golden.sh`의 필수 열로 넣었다. M18-REL-01은 "배포하는 조합마다 그 조합의 골든"(T-39). x64 런타임 설치는 사용자(Q-09) |
| D17 | 작 | 순번의 생산자가 둘이다 | 반영 | CONTRACTS 3.1절: 순번은 `InputRecorder`와 `DuelClientCore`만 매긴다. 양자화기는 `QuantizedKey`. 9번째는 번호를 매기기 전에 버린다. `Seq`는 상태와 해시에 넣지 않는다. M1-TE-01의 "순번을 바꿔도 해시가 같다", M4-RUN-01 |
| D18 | 작 | 멱등과 정산의 트랜잭션 경계가 명세보다 느슨하다 | 반영 | ARCHITECTURE 6.3절을 "request 행 INSERT 먼저"로, `IIdempotency.RunAsync`에 본문 지문을 더했다. 6.2절과 CONTRACTS 8.3절에 "토큰 소모부터 `verify_queue` 등록까지 한 트랜잭션". M10-DB-01, M11-RUN-03의 "1차 검사 직후 종료 → 재제출 정상" |
| D19 | 작 | 체력 배율의 타입과 넘침 규칙이 명세와 다르다 | 반영 | spec 07 6.1절과 TV-022·023을 확인했다(`long HMicro`, 단계별 내림, `int.MaxValue` 포화). `WaveMath.HpMicro`·`EnemyHp`·`BossHp`·`ScaledDamage`. 1.1절에 포화와 래핑의 구분, `PowTable`의 끝값 고정, `MulPermille` = `MulDiv` |
| D20 | 작 | uuid 16바이트의 순서가 없다 | 반영 | `UuidBytes`(RFC 4122 네트워크 순서), 픽스처 `tests/fixtures/uuid/`. CONTRACTS 1.5·11.1절, M1-CORE-05 |
| D21 | 작 | 고정소수점 상수와 반올림의 남은 불일치 | 반영(변형) | spec 01(…500)과 spec 11(…499)을 확인했다. `FixMath.InvSqrt2Raw = 3,037,000,500` 하나. 속도 식과 `Normalized`의 정수 식을 고정했다(3.76 → 269,151,284로 검산). 변형: 광장은 "60틱 적분 6회"가 아니라 spec 11 2절의 0.1초 고정 스텝 `MoveAndSlide`를 쓴다(명세가 그렇게 정했고 광장은 해시로 검증하지 않는다). ARCHITECTURE 6.5절, T-05 |
| D22 | 작 | 판정기의 되감기에 대한 문장이 구현을 잘못 이끈다 | 반영 | CONTRACTS 3.5절의 문장을 바꿨다(판정기 상태도 복원 대상, 비용과 대체 기술은 지갑에 따라 달라진다). M2-NET-06에 늦게 온 확정 사례 |
| D23 | 작 | 넷코드 API가 명세의 몇 가지를 표현하지 못한다 | 반영 | (1) `TryWriteSpectatorStart`. (2) `ReplayRunner.RunDuel`. (3) 확정 세계 + 예측 세계(T-14 개정. 틱별 스냅숏 고리를 없앴다). (4) 이벤트 열쇠 `(Tick, Type, Slot, Ordinal)`. (5) 결과는 `match_id`당 처음 확정된 것 |

### 명세 대응

| # | 심각도 | 문제 | 판단 | 이유와 반영한 곳 |
|---|---|---|---|---|
| S01 | 막 | DECISIONS 1.24(이메일 복구)를 실제로 해 주는 작업이 없다 | 반영(변형) | 작업 M10-ACC-05(`ApiMailSender`)를 더했다. T-36, CONTRACTS 7.1절, M18-REL-01과 M18-INT의 "출시 구성에서 `recovery_mail_enabled = true`, 실제 도착". 변형: 사업자는 계정을 만드는 사용자가 정해야 하므로 구조(HTTPS API + 어댑터)만 정하고 OPEN-QUESTIONS Q-01에 올렸다 |
| S02 | 큰 | 어느 작업에도 들어 있지 않은 벡터 구간이 있다 | 반영 | 명세에서 벡터를 뽑아 전부 배정했다. spec 04 15.3 → 새 작업 M5-ART-05. spec 14 7.2 → M8-DAT-02(M8-DAT-01을 `deps`에), 7.3의 보스 순번 → M8-BOS-01, 시간대·날 → M1-SIM-03. spec 03 B군은 M1-SIM-08, M5-ART-01~04, M5-CHR-03, M10-CLI-02(B27), M14-DUL-02(B28·29)로 갈랐다. 미배정 0을 검사기가 강제한다 |
| S03 | 큰 | 수락 조건이 모호한 부분집합으로 벡터를 가리킨다 | 반영(변형) | 작업마다 `vectors` 필드를 뒀다(128개 작업). 결투 관련 spec 13 벡터는 M14-UL-01·02로 옮겼다. 비기와 얽힌 종별 반응 벡터는 원형 작업(M3)에서 M8-ENM-03으로 옮기고 비기 작업을 `deps`에 넣었다(그 시점에 시험할 수 없었다). `check-tasks.mjs`가 "…절 중 … 벡터" 표현과 미배정·이중 배정을 거절한다. 변형: 선택자(위 표) |
| S04 | 큰 | 결투에 들어갈 때 캐릭터·지팡이를 고르는 계약이 없다 | 반영 | `DuelEntrySelection`, `duel.match_accept`, `duel.prep_select`, `duel.prep_state`, `POST /v1/duel/queue`의 본문, `duel.room_ready`. `IDuelService`에 대응 메서드와 9절의 대응표. M14-PRO-01, M14-SRV-02 |
| S05 | 큰 | 시즌 주화와 시즌 상점이 비어 있다 | 반영 | 엔드포인트 셋과 `ISeasonService`의 메서드 셋, `season_shop_purchase` 표. CONTRACTS 12.4절에 얻는 길·쓰는 곳·환전. M6-RUL-03, M11-RUN-03, M14-SRV-03(6/3, 하루 10판), M15-SRV-01(환전 한 번만) |
| S06 | 큰 | 누적 플레이 2시간을 셀 자리가 없다 | 반영(변형) | spec 12의 DDL에 `play_seconds`가 있는 것을 확인했다(목록에서 빠져 있었다). CONTRACTS 12.1·12.2·12.4절, `ProfileView.TradeOpen`·`TradeMissing`, M12-API-01, M13-RUL-01, M14-SRV-02. 변형: HTTP 어림은 두지 않는다(위 표) |
| S07 | 큰 | 비기 해금의 서버 쪽 경로에 수락 테스트가 없다 | 반영 | M11-RUN-03(정산 트랜잭션에서 반영, 한 번만), M11-RUN-04(불일치 때 되돌림). 되돌릴지는 명세에 없어 임시로 "되돌린다"로 두고 OPEN-QUESTIONS Q-03 |
| S08 | 큰 | 오프라인↔서버 프로필 분리의 수락 테스트가 없다 | 반영 | CONTRACTS 6.1절 계약, M10-CLI-02(의존·경로 검사, B27·TV-PF-06·T-OFF-04), M10-TE-01(종단) |
| S09 | 큰 | `PreviewArt`/`PreviewCost`로는 HUD 미리보기를 표현할 수 없다 | 반영 | `ArtPreview`, `RecentInputs`, `SpellNodeExecuted(480)`. M5-ART-01(B08·B13·B14·B15·B15b·B37), M4-UL-02 |
| S10 | 큰 | 맡은 작업이 없는 화면과 HUD 요소가 있다 | 반영 | 새 작업 M6-UL-04(서고·조립 바꾸기 모델), M6-UV-03(뷰), M9-COS-02(옷장 뷰). M4-UL-02에 진행 바·처치 수·최근 입력·회로 점등. V-16은 발표용 그림만 맡는다. 보스 HUD 모델을 M8-UL-01에 명시 |
| S11 | 큰 | 로컬 2인 통과 기준을 재는 작업이 없다 | 반영 | 새 작업 M14-TE-03(적중률, 시간 종료 비율, 사람 확인). M14-UL-01에 조준선, 입력 버려짐, 이름표, 관전 HUD |
| S12 | 큰 | 표시 이벤트 계약이 Sim 이벤트만 다룬다 | 반영 | CONTRACTS 13절에 `UiEventKeys`와 `RaiseUi`, 빠짐 검사. M9-PRS-01의 명세 참조와 수락 조건 |
| S13 | 큰 | 열거형·`ContentDb`·팩 섹션 표가 spec 14 3절을 덮지 못한다 | 반영 | CONTRACTS 2.1절에 열거형 10개, 2.3절에 접근자, 11.2절에 섹션 id 표. 확인하다 찾은 것도 고쳤다: `TimeOfDay`와 `LeagueId`의 번호가 spec 14의 코드(1부터)와 달랐다(T-16 위반) |
| S14 | 큰 | ARCHITECTURE 13절이 GDD 16절과 맞지 않는다 | 반영 | GDD 16절과 online 11절을 읽고 13절을 표로 다시 썼다(18단계, 10단계, 마일스톤, 대표 작업). 9단계는 "빈 FX·SFX 재생기와 식별자, 실제 재생기는 V". M0-TL-01의 수락 조건에도 넣었다 |
| S15 | 큰 | 에이전트 규칙에 게임 규칙 불변식이 없다 | 반영(변형) | CLAUDE 원본에 2.5절(12개 항목), code-reviewer에 점검표. M0-TL-02에 바이트 대조, M0-TL-01이 M0-TL-02에 의존. 변형: 설치(위 표) |
| S16 | 작 | 시설 id가 명세마다 다르고 작업용 이름이 식별자에 들어 있다 | 반영 | spec 11(`fac_*`)과 spec 14(`facility.*`)의 열 줄을 확인해 대응표에 넣었다. `names-scan.sh`에 로마자 작업 이름 금지(M1-CONT-02) |
| S17 | 작 | 닉네임 바꾸기를 표현할 수 없다 | 반영 | `POST /v1/account/nickname`, `ChangeNicknameAsync`, M10-SHP-01·PRO-02. 오프라인에는 두지 않는다(Q-04) |
| S18 | 작 | 디버그 op이 계약에 없다 | 반영 | `RunCommandOp` 200~255, 꼬리말의 `debug_used`, 서버 거절(M11-RUN-03), 출시 빌드 검사(M18-REL-01). B19와 같이 풀었다 |
| S19 | 작 | 지팡이의 캐릭터 제한과 그믐 팔레트에 수락 테스트가 없다 | 반영 | `Rules.Profile.CanUseWand`, M6-RUL-01·CLI-03, M10-INV-01, M11-RUN-02, M14-SRV-02. M9-AST-02에 팔레트 검사 |

### 빌드 가능성·병렬성

| # | 심각도 | 문제 | 판단 | 이유와 반영한 곳 |
|---|---|---|---|---|
| B01 | 막 | 이 맥에 macOS IL2CPP 모듈이 없다 | 반영 | `modules.json`과 `Variations`를 확인했다(`mac-il2cpp` 꺼짐, `*_mono`만). ARCHITECTURE 4.1절, PIPELINE 4.1절의 사용자 할 일, M1-UE-03의 사람 확인, 모듈이 없으면 건너뛰지 않고 실패 |
| B02 | 막 | 실행 파일 프로젝트에 `Program.cs`가 없어 빌드가 깨진다 | 반영 | M0-BT-01의 소유에 `server/*/Program.cs`, `tools/*/Program.cs`. T-43 |
| B03 | 막 | 분석기가 `net10.0`이고 연결 줄의 주인이 없다 | 반영 | 분석기만 `netstandard2.0`. 연결 줄은 M0-BT-01이 최상위 `Directory.Build.props`에 넣는다. T-43, ARCHITECTURE 3.3·3.4절 |
| B04 | 막 | 패키지를 더할 권한이 뒤 작업에 없다 | 반영(변형) | 두 안을 다 썼다: 계획된 의존은 M0-BT-01이 전부 미리 넣고(T-37, Unity 쪽 DLL·asmdef 포함), 계획에 없던 의존은 `PackageReference` 한 줄 추가를 공유 추가 전용으로 허용한다(PIPELINE 4.4절) |
| B05 | 막 | `SimVersion.g.cs`와 골든을 한 작업만 가져서 코어를 고치는 모든 작업이 실패한다 | 반영(변형) | "다시 만드는 파일" 부류(T-41, PIPELINE 4.4절): 도구 출력과 바이트가 같으면 허용, 병합 때 다시 만든다. 골든은 `CHANGELOG.md`에 이유, 검토 항목 9번. 변형: 절 생략은 풀 절에만(위 표) |
| B06 | 큰 | 작업 트리에서 상대 경로 명령이 main을 검사한다 | 반영 | 작업 카드에 `WT`(절대 경로), CLAUDE 원본 9절의 명령을 전부 `$WT/…`로, 도구는 뿌리를 자기 위치에서 구한다. M0-BT-05의 테스트 |
| B07 | 큰 | `.claude/` 수정 금지와 작업 트리 위치가 모순이다 | 반영(변형) | 규칙을 "루트 `CLAUDE.md`, `.claude/agents/`, `.claude/settings*.json`, `docs/DECISIONS.md`"로 좁혔다. 변형: 위치는 옮기지 않는다(위 표) |
| B08 | 큰 | Unity 테스트에 `-quit`을 쓰면 테스트가 돌지 않고 0이 나온다 | 반영 | 테스트는 `-runTests …` 또는 콜백 종료, 빌드만 `-quit`. 프레임 시간은 `-nographics` 없이. M0-UE-01에 "일부러 실패하는 테스트로 종료 코드 확인" |
| B09 | 큰 | TestKit을 Unity가 쓸 길이 없다 | 반영 | `tests/Wizard.TestKit/Shared`(netstandard2.1, C# 9, UPM, Newtonsoft). M0-UE-01이 M0-TE-01에 의존하고 manifest에 넣는다 |
| B10 | 큰 | `RingBuffer<T>` 구조체는 C# 9에서 만들 수 없다 | 반영 | `sealed class` + `CopyFrom`. `DetSort`는 구조체 제네릭 비교자. CONTRACTS 1.5절, M1-CORE-05 |
| B11 | 큰 | Windows IL2CPP는 맥에서 빌드할 수 없다 | 반영(변형) | `modules.json`에 `windows-il2cpp`가 없음을 확인했다. T-39에 두 길을 적고 기본을 "맥에서 Windows Mono 빌드 + Windows 실기의 골든"으로 정했다. 변형: 기기가 있어야 하므로 바꿀지는 사용자(Q-02) |
| B12 | 큰 | PostgreSQL이 없을 때 통합 테스트의 동작이 없다 | 반영 | Docker가 돌고 있음을 확인했다. T-38: `pg-test.sh`가 고정 이미지를 띄운다. 없으면 실패(건너뛰지 않음). M10-DB-01 |
| B13 | 큰 | bash 3.2, 시간 제한과 잠금 없음, 상태 변경이 `tasks.json` 한 파일에 몰린다 | 반영 | `/bin/bash` 3.2.57, `timeout`·`flock` 없음을 확인했다. T-40: 로직은 `tools/Wizard.Ci`, 상태는 `.claude/taskstate.json`(잠금, `reconcile`). `tasks.json`의 `status`는 처음 분류만 |
| B14 | 큰 | `ci.sh gen-check` 같은 하위 명령이 없고 순환 의존이다 | 반영 | 스크립트 파일로 바꿨다(`gen-check.sh`, `names-scan.sh`, `tasks-check.sh`). `ci.sh run <스크립트>` |
| B15 | 큰 | 유닉스 소켓 경로가 macOS의 104바이트를 넘는다 | 반영 | `/tmp/wz/<해시 8자>-<목적>.sock`, 100자 초과면 시작 때 실패. CONTRACTS 7.3절, PIPELINE 4.3절 |
| B16 | 큰 | M10-DB-01의 `deps`가 모자라고 타입 셋이 정의되지 않았다 | 반영 | `deps`에 M1-CONT-01과 M1-CORE-05. CONTRACTS 7.2절에 `LedgerRef`, `ItemOrigin`, `WandRow`. PIPELINE 3.1절의 문장 |
| B17 | 큰 | 이름 표가 M6에야 생겨 M1~M5의 데이터 게이트가 통과할 수 없다 | 반영 | 뼈대와 `names --check`를 M1-CONT-02로 당겼다. 이름 표는 이진 절(`.wznm`)로 바꿔 `Wizard.Text`가 `ByteReader`로 읽는다(JSON 파서 없음) |
| B18 | 큰 | 코어 버전 해시의 범위가 넓다 | 반영(변형) | T-18에 대상 목록. `Core/Errors/`와 `Content/Rules/`를 뺐다. `tasks.json`에서 규칙 데이터 작업의 Content 경로를 `Rules/`로 옮겼다. 변형: 새 어셈블리가 아니라 폴더(위 표) |
| B19 | 큰 | 디버그 op이 코어에 들어가고 "순위 제출 불가 표시"는 클라이언트가 단다 | 반영 | `SimConfig.AllowDebugOps`(서버 경로는 언제나 꺼짐 → 무효 명령 → `RecordCorrupt`). 코어 쪽을 새 작업 M3-CB-03(combat-engineer)으로 떼었다 |
| B20 | 큰 | Unity가 스스로 고쳐 쓰는 파일이 소유 검사와 병합을 깬다 | 반영 | metagen을 먼저, 실행 뒤 되돌리기, `check-diff`의 무시 목록, GUID 대조, 가져오기 블록의 형식. PIPELINE 4.3~4.5절, M0-BT-04·UE-01 |
| B21 | 큰 | `FixConvert`와 `Vector2`가 ViewLogic의 일을 막는다 | 반영 | `FixConvert`를 `Wizard.ViewLogic`으로, 표시 계약의 타입을 `System.Numerics.Vector2`로. T-27, CONTRACTS 13절, M1-UE-02 |
| B22 | 큰 | "네임스페이스 = 폴더"와 부분 클래스, 부분 메서드의 단일 구현이 부딪친다 | 반영 | 훅을 (단계, 모듈)마다 따로 선언하고 CONTRACTS 3.2절에 표로 고정했다. 네임스페이스의 예외 셋을 적었다. M1-SIM-02, M0-BT-02 |
| B23 | 큰 | IL2CPP의 코드 제거를 다루지 않았다 | 반영 | T-44: `link.xml`, Minimal 고정, 플레이어에서 픽스처 왕복과 AEAD 벡터. M0-UE-01, M10-PRO-01 |
| B24 | 작 | 소유 경로의 뜻이 작업 배정과 어긋난다 | 반영 | "주로 맡는 경로(참고). 권한은 작업 카드의 `owned_paths`뿐"으로 PIPELINE 3.2절과 에이전트 정의 전부를 고쳤다 |
| B25 | 작 | 서브에이전트는 다른 서브에이전트를 부를 수 없고, 모델 우선이 호출 인자에 기댄다 | 반영 | "에이전트끼리 부르지 않는다, 지휘 세션이 띄운다". 모델별 변형 에이전트 셋(`*-opus`)을 만들고 `tasks.json`의 `owner_agent`를 바꿨다(32개 작업). doc-scout의 도구를 고쳤다 |
| B26 | 작 | 가짜 결제 제공자의 DLL이 출시 배포물에 들어간다 | 반영 | `Api`는 참조하지 않는다. 개발용 호스트 `Wizard.Server.Api.DevHost`. M18-REL-01의 검사 |
| B27 | 작 | 분석기 수락 기준의 빈틈 | 반영 | 생성 코드 검사, 진단을 끄는 설정 거절, `Rules`의 실수형과 `Netcode`의 시계·소켓. M0-BT-02 |
| B28 | 작 | 한글 리터럴 검사의 범위 | 반영 | 제품 코드로 한정(`tests/` 제외). 화면에 보이지 않는 한글 데이터는 `data/rules/chat_filter.json` |
| B29 | 작 | V 작업의 마일스톤과 묶음이 집계와 어긋난다 | 반영 | V 작업을 마일스톤 `V`로 옮겼다(게이트 없음). 집계를 검사기가 본다 |
| B30 | 작 | M0 부트스트랩 순서의 빈틈 | 반영 | PIPELINE 3.3절에 부트스트랩 절차(손으로 작업 트리, 직접 대조, 소급 검사). 설치본 = 원본을 CI가 본다 |
| B31 | 작 | 최상위 빌드 설정이 Unity의 csproj에 걸리고, `.gitignore`가 고정되어 있지 않고, 솔루션 형식이 다르다 | 반영 | T-43: `unity/Directory.Build.props`, 위치를 고정한 무시 규칙, `.sln`, `rollForward: latestPatch`. M0-BT-01 |
| B32 | 작 | 쓸 라이브러리가 정해지지 않았다 | 반영 | T-37(BouncyCastle, Konscious Argon2 + 세마포어, JsonSchema.Net, Newtonsoft). 판 번호는 M0-BT-01이 고정 |
| B33 | 작 | 옮기기가 main 체크아웃에서 거부되고 병합이 직렬이다 | 반영 | 작업 트리 안에서 옮긴다. `ci.sh full`의 경로 조건(Unity 단계 생략), 묶음 검사(순서는 그대로) |
| B34 | 작 | LaunchDaemon 설치에 sudo가 든다 | 반영 | M10-OPS-01의 사람 확인과 PIPELINE 4.1절. 스크립트는 plist를 만들고 검증하는 데까지 |

## 남은 위험

- **x64는 Rosetta 2다.** 진짜 x64 CPU에서의 동작(특히 Windows)은 Windows 실기에서 골든을 돌리기 전까지 확인되지 않는다. T-39의 배포 조건이 이것을 막지만, 그 기기는 아직 없다(Q-02).
- **입력 지연 표가 "폐기율 0"을 못 넘을 수 있다.** 여유 식으로 보면 왕복 60ms·D = 2에서 키를 틱의 끝에 누르면 여유가 3ms쯤이다. M2-NET-06이 재고, 못 넘으면 수치 조정이다(Q-06). 표를 고치면 결투의 체감 지연이 달라진다.
- **facts 형식이 새 병목이다.** 정산에 쓰는 값은 전부 `RunResultFacts`에 있어야 하고, 형식을 올릴 때마다 `Wizard.Rules`가 옛 판과 새 판을 같이 읽어야 한다. 빠뜨린 필드는 검증 밖의 지급이 된다. M11-TE-01의 필드별 변조 테스트가 지금 있는 필드만 본다.
- **골든을 다시 만들기 쉬워졌다.** 소유 문제를 풀려고 연 길이라, 버그로 바뀐 해시를 다시 만들어 덮을 수 있다. 이유 기록, 검토 항목, 조합 전체의 재확인으로 막지만 마지막은 검토자의 눈이다.
- **벡터 배정은 한 줄 설명만 보고 갈랐다.** 2,855개가 빠짐없이 한 작업에 있지만, 몇 개는 그 작업 시점에 시험할 수 없을 수 있다(S03에서 찾은 것과 같은 종류). 그때는 빼지 않고 배정을 옮긴다(PIPELINE 3.7절).
- **명세를 고친 곳이 있다.** spec 10 14.8절(표, 의사코드, TV-N15·N16), spec 01과 spec 11의 한 줄씩. 전부 `[조율]` 표시를 남겼다. 사용자가 원래 뜻을 달리 정하면 CONTRACTS 10.6절과 같이 되돌린다.
- **확인하지 못한 것.** 이 기기에 무엇도 설치하지 않았다. `dotnet test -a x64`, IL2CPP 빌드, Docker의 PostgreSQL은 실제로 돌려 보지 않았다(필요한 것이 없거나 아직 코드가 없다). Unity Hub의 머리 없는 모듈 설치 명령도 돌려 보지 않았다.
- **에이전트 설정은 아직 원본뿐이다.** 루트 `CLAUDE.md`와 `.claude/agents/`는 사용자가 승인해야 생긴다(Q-08). 그 전에 띄우는 에이전트는 이 규칙을 저절로 읽지 못한다.
