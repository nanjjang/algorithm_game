# 결정론 골든

같은 씨앗과 같은 입력 기록이면 dotnet(CoreCLR), Unity Mono, IL2CPP에서, arm64와 x64에서 같은 상태 해시가 나와야 한다(DECISIONS-TECH T-30, T-39, ARCHITECTURE 11절, PIPELINE 7.3절). 이 폴더는 그 비교의 기준이다.

읽는 코드는 `tests/Wizard.TestKit/Shared`(netstandard2.1, C# 9, UPM 패키지 `com.wizard.testkit.shared`)에 한 벌만 있다. dotnet 테스트와 Unity EditMode·플레이어가 같은 소스를 컴파일해 쓴다. 이 문서의 형식과 로더가 다르면 로더의 자체 테스트(`tests/Wizard.TestKit/SelfTests/`)가 실패한다.

## 폴더

```
tests/golden/
├ README.md            이 문서
├ CHANGELOG.md         골든을 다시 만든 기록(줄을 더하기만 한다)
├ arith/               산술 경계 벡터 묶음(형식은 arith/README.md)
└ m<N>/                마일스톤별 골든. m1/, m2/ …
   └ <이름>/            항목 하나 = 폴더 하나. 이름은 소문자, 숫자, '.', '_', '-'
      ├ golden.json      항목 정의(사람이 쓴다)
      ├ record.wzrc      입력 기록(CONTRACTS 11.1). 결투는 .wzdr
      └ expected.json    기대 해시 목록(다시 만드는 파일. 손으로 고치지 않는다)
```

뿌리에 이 밖의 파일이나 폴더가 있으면 로더가 형식 문제로 실패한다. 이름을 잘못 지은 골든이 조용히 빠지지 않게 하려는 것이다.

## 항목 정의 `golden.json`

```json
{
  "format": 1,
  "id": "m1/practice-solo",
  "kind": "practice",
  "seed": "0x00000000c0ffee01",
  "setup": { "players": 1, "character": "char.1", "startWave": 1 },
  "record": "record.wzrc",
  "notes": "연습 모드 1인, 30초"
}
```

| 필드 | 뜻 |
|---|---|
| `format` | 1 |
| `id` | 골든 뿌리 기준 폴더 경로. 폴더와 다르면 실패 |
| `kind` | `endless`, `practice`, `duel` 가운데 하나 [임시: 새 모드가 생기면 TestKit에 더한다] |
| `seed` | u64. `"0x"` + 16진 1~16자리 또는 십진 문자열. JSON 숫자로 쓰지 않는다(u64가 long을 넘는다) |
| `setup` | 재실행기가 세계를 만들 때 쓰는 설정. 값은 문자열, 정수, 참거짓만. 실수, 배열, 중첩 객체는 거절한다. 무엇을 넣는지는 골든을 만드는 작업(M1-TE-01부터)이 정한다. 머리말의 정규 바이트(`EndlessSetup`)는 입력 기록 안에 있으므로 여기서 되풀이하지 않는다 |
| `record` | 항목 폴더 기준 상대 경로. `..`, 절대 경로, `\` 금지. `.wzrc` 또는 `.wzdr` |
| `notes` | 선택. 사람이 읽는 설명 |

모르는 필드가 있으면 실패한다(오타가 조용히 무시되지 않게).

## 기대 해시 `expected.json`

```json
{
  "format": 1,
  "id": "m1/practice-solo",
  "hashes": [
    { "tick": 300, "hash": "9a3f0c2e5b7d1146", "label": "wave5" },
    { "tick": 1800, "hash": "04c1d2e3f4a5b6c7", "label": "final" }
  ]
}
```

- `hash`는 그 틱이 끝난 뒤의 상태 해시(FNV-1a 64, CONTRACTS 1.4)를 소문자 16진 16자리로 쓴 것이다. `tick`은 u32이고 엄격히 늘어난다. 목록은 비어 있지 않다. 마지막 칸이 판의 끝이다.
- 비교는 (틱, 해시)의 열 전체다. 기대에 없는 틱을 내거나 기대의 틱을 내지 않아도 실패다. `label`은 비교하지 않는다.
- 이 파일은 "다시 만드는 파일"이다(PIPELINE 4.4절, T-41). `replaycli golden --update`(M1-TE-01)가 `GoldenEntry.FormatExpected`로 쓰고, 같은 해시면 같은 바이트가 나온다. **해시가 바뀔 이유가 있는 변경**(상태 절을 더했다, 규칙을 고쳤다)일 때만 다시 만들고 `CHANGELOG.md`에 한 줄을 더한다. 이유 없이 바뀐 해시는 검토에서 막는다(PIPELINE 3.6절 9번).
- 같은 기록을 두 번 돌리기, 스냅숏 저장·복원 뒤 이어 돌리기는 같은 기대 목록과 비교한다. 별도의 기대값을 두지 않는다.

## 조합별 결과 파일

골든을 돌린 조합마다 결과 파일 하나를 쓴다. 조합 이름은 `<os>-<runtime>-<host>-<arch>`다.

| 칸 | 값 |
|---|---|
| os | `macos`, `windows`, `linux` |
| runtime | `dotnet`, `mono`, `il2cpp` |
| host | `test`(dotnet 테스트), `editor`(Unity EditMode), `player`(머리 없는 플레이어) |
| arch | `arm64`, `x64`(이 맥에서는 Rosetta 2) |

예: `macos-dotnet-test-arm64`, `macos-dotnet-test-x64`, `macos-mono-editor-arm64`, `macos-mono-player-arm64`, `macos-mono-player-x64`, `macos-il2cpp-player-arm64`, `macos-il2cpp-player-x64`, `windows-mono-player-x64`.

- 파일 이름: `golden-result.<조합>.json`. 기본 위치는 `artifacts/golden/`이다(커밋하지 않는다). 다른 기기(Windows 실기)에서 돌린 결과는 그 파일을 게이트에 붙인다(T-39).
- 내용(`GoldenRunResult`):

```json
{
  "format": 1,
  "combo": "macos-dotnet-test-arm64",
  "entries": [
    { "id": "m1/practice-solo", "hashes": [ { "tick": 300, "hash": "9a3f0c2e5b7d1146" } ] },
    { "id": "m1/duel-basic", "error": "RecordCorrupt: …" }
  ],
  "arith": [
    { "bundle": "fixmath-div", "case": "divint-min-neg1", "values": ["-2147483648"] },
    { "bundle": "fixmath-div", "case": "divint-by-zero", "fault": "SimInvariant" }
  ]
}
```

- 시각, 기기 이름, 경과 시간처럼 실행마다 바뀌는 값은 넣지 않는다. 같은 결과면 바이트가 같다.
- 대조는 `GoldenComparison.Compare(catalog, results, requiredCombos)`가 한다. 모든 조합의 모든 항목이 기대 해시와 같고 모든 산술 벡터가 기대를 만족해야 통과다. 결과에 빠진 항목, 모르는 항목, 같은 조합 두 번, 요구한 조합의 빠짐은 실패다. 출력의 표는 행이 골든·산술 벡터, 열이 조합이다(`unity-golden.sh`, M1-UE-03).

## 쓰는 법

```csharp
GoldenCatalog catalog = TestRepo.LoadGolden();          // dotnet. Unity 는 GoldenCatalog.Load(<뿌리>/tests/golden)
foreach (GoldenEntry e in catalog.InMilestone("m1"))
{
    IReadOnlyList<GoldenHash> actual = /* e.ReadRecord()와 e.Setup으로 재실행 */;
    GoldenAssert.Matches(e, actual);                       // 처음 갈라진 틱을 알려 준다
}
```

저장소 뿌리는 `RepoRoot.Find(시작 위치)`가 찾는다. dotnet은 테스트 어셈블리 위치, Unity는 `Application.dataPath`에서 위로 올라가 `Wizard.sln`과 `docs/spec`이 있는 첫 폴더다. 작업 트리에서 돌면 그 작업 트리를 읽는다.
