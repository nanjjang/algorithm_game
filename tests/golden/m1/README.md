# m1 골든

M1(손맛 시제품)의 결정론 골든이다. 형식은 `tests/golden/README.md`, 재실행 규칙은 아래와 같고 dotnet 쪽 구현은 `tools/Wizard.ReplayCli/GoldenRunner.cs`다. Unity 실행기(M1-UE-03)도 같은 규칙으로 돌려야 같은 해시가 나온다.

## 세계 만들기

| kind | 기록 | 세계 |
|---|---|---|
| `practice` | `.wzrc` | `SimWorld.CreatePractice(db, SimConfig.ForPractice(db, SimConfig.ForEndless(db)), new PracticeSetup())`. 머리말은 읽지 않는다(자리 값). 씨앗은 0이라 `seed`는 `"0x0"` |
| `endless` | `.wzrc` | `SimWorld.CreateEndless(db, SimConfig.ForEndless(db), 머리말의 EndlessSetup)`. `seed` = 머리말의 씨앗 |
| `duel` | `.wzdr` | `SimWorld.CreateDuel(db, SimConfig.ForDuel(db, setup.Options), 머리말의 DuelSetup)`. `seed` = 머리말의 씨앗 |

- 팩은 저장소 `data/`로 만든 팩(기본 `data_version`)이다. 기록 머리말의 `data_version`과 다르면 돌리지 않는다. 코어 버전은 보지 않는다(해시가 바뀌지 않는 코어 변경을 넘어 살아야 한다).
- 틱 t 의 `Step`에는 기록의 틱 t 열만 넣는다. `.wzrc`의 키·명령 순번은 레코드 순서대로 1부터(명령과 키가 같은 공간), `.wzdr`은 실린 순번 그대로다. 명령과 키의 슬롯은 `.wzrc`에서 0이다(`ReplayRunner`와 같다).
- 기록의 꼬리말 `end_tick`까지 돌린다.

## setup

| 열쇠 | 뜻 |
|---|---|
| `players` | 세계의 플레이어 수(연습·무한 웨이브 1, 결투 2). 다르면 오류 |
| `hashEveryTicks` | 해시를 남기는 간격 |

모르는 열쇠는 오류다.

## 해시 목록

`world.Tick`(그만큼 `Step`한 뒤)이 `hashEveryTicks`의 배수이고 `end_tick`보다 작을 때마다 한 칸, 그리고 `end_tick`에 한 칸(`label` = `final`). 해시는 그때의 `ComputeStateHash()`다. `.wzrc`의 `0x04`·꼬리말과 같은 "world.Tick == 틱" 규약이다.

## 기록 만들기와 다시 만들기

- 입력 기록은 `replaycli bot`이 판을 하는 쪽과 같은 길(`InputRecorder`, `DuelRecordWriter`)로 만든 것이다. 명령 줄은 각 항목의 `notes`에 있다. 기록 파일은 다시 만들지 않는다(입력이다).
- `expected.json`은 `replaycli golden --update`가 쓴다. 해시가 바뀔 이유가 있는 변경일 때만 다시 만들고 `tests/golden/CHANGELOG.md`에 한 줄을 더한다.
- 해시가 갈라지면 `replaycli diff <기록> <기록>`(같은 기기) 또는 조합마다 `replaycli trace` → `replaycli diff <trace> <trace>`로 처음 갈라진 틱과 상태 절을 본다.
