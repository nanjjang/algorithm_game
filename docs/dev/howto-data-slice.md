# 새 데이터 절 하나를 더하는 절차

M1-CONT-03이 만든 본보기 세 절(`characters`, `arena`, `blocks`)을 예로, 기획 수치 한 묶음을 `data/`의 JSON에서 `ContentDb`의 정의 타입까지 잇는 절차를 적는다. **이후의 데이터 작업은 이 절차를 따른다.**

근거: ARCHITECTURE 8절, CONTRACTS 2.2·2.3·11.2, DECISIONS-TECH T-17·T-33·T-47. 이 문서와 그 문서들이 다르면 그 문서들이 맞다.

## 0. 한눈에

```
data/sim/<절>.json ──(1 스키마)── data/schema/<절>.schema.json
        │
        │ tools/Wizard.DataBuild/Convert/<절>Converter.cs     (2 변환기: 십진 → 정수, 정규 순서로 절 바이트)
        ▼
base.wzpack 의 절 <번호>
        │
        │ src/Wizard.Content/Pack/Sections/ContentDb.<절>.cs  (3 절 읽기: Load<절> 과 Find<접근자> 훅)
        ▼
ContentDb.<접근자>(…) → <정의>Def                           (4 정의 타입: src/Wizard.Content/Defs/<정의>Def.cs)
        │
        └─ tests/Wizard.Content.Tests/<절>PackTests.cs        (5 테스트: JSON → 팩 → ContentDb 왕복, 명세의 정수)
           data/names/names.ko.json                           (6 자기 id의 이름 열쇠)
```

| 단계 | 본보기 |
|---|---|
| 원본 | `data/sim/characters.json`, `data/sim/arena.json`, `data/sim/blocks.json` |
| 1 스키마 | `data/schema/characters.schema.json`, `arena.schema.json`, `blocks.schema.json` |
| 2 변환기 | `tools/Wizard.DataBuild/Convert/CharactersConverter.cs`, `ArenaConverter.cs`, `BlocksConverter.cs` |
| 3 절 읽기 | `src/Wizard.Content/Pack/Sections/ContentDb.Characters.cs`, `ContentDb.Arena.cs`, `ContentDb.Blocks.cs` |
| 4 정의 타입 | `src/Wizard.Content/Defs/CharacterDef.cs`, `ArenaDef.cs`, `BlockDef.cs` |
| 5 테스트 | `tests/Wizard.Content.Tests/CharactersPackTests.cs`, `ArenaPackTests.cs`, `BlocksPackTests.cs` |
| 6 이름 열쇠 | `data/names/names.ko.json`의 `block.fireball`, `block.magic_bolt`, `place.arena`, `place.field` |

## 1. 어디에 두는가: 전투 절과 규칙 절

**전투 절은 `Content/{Defs, Pack/Sections}`, 규칙 절은 `Content/Rules/{Defs, Sections}`에 둔다.**

| | 전투 절 | 규칙 절 |
|---|---|---|
| 절 번호 | 1~19, 40~ (CONTRACTS 11.2) | 20~39 |
| 원본 | `data/sim/<절>.json` | `data/rules/<절>.json` (31 `run_limits`만 `data/rules/limits.json`) |
| 정의 타입 | `src/Wizard.Content/Defs/` (네임스페이스 `Wizard.Content`) | `src/Wizard.Content/Rules/Defs/` (네임스페이스 `Wizard.Content.Rules`) |
| 절 읽기 | `src/Wizard.Content/Pack/Sections/ContentDb.<절>.cs` | `src/Wizard.Content/Rules/Sections/ContentDb.<절>.cs` |
| 코어 버전 해시 | 안 (바꾸면 `SimVersion.CoreVersion`이 바뀐다) | 밖 |
| 누가 읽나 | `Wizard.Sim`과 그 위 전부 | `Wizard.Rules`와 그 위. `Wizard.Sim`이 참조하면 컴파일 오류(WZ0017) |

- 재실행 결과를 바꾸는 값은 `data/sim/`에만 둔다(CONTRACTS 2.3). 어느 쪽인지 모르겠으면 "검증 워커가 판을 다시 돌릴 때 이 값이 필요한가"를 묻는다. 필요하면 전투 절이다.
- `<절>`은 `ContentSection`의 이름이다(`Characters`, `Arena`, `Blocks`, `DuelRules` …). 파일 이름은 `ContentSections.Name`의 snake_case(`characters.json`, `duel_rules.json`)다. 그 대응은 `tools/Wizard.DataBuild/Core/SectionFiles.cs`가 정한다.
- 절 파일(`ContentDb.<절>.cs`)은 네임스페이스 `Wizard.Content`의 `partial class ContentDb`만 담는다. 규칙 쪽 절 파일도 같다(WZ0024의 예외는 이 꼴 하나뿐이다).
- 뼈대 파일(`Pack/ContentDb.cs`, `Pack/Core/*`, `Rules/ContentDb.Rules.cs`, `tools/Wizard.DataBuild/Core/*`, `Program.cs`)은 고치지 않는다. 접근자와 훅은 이미 전부 선언되어 있다.

## 2. 원본 JSON과 스키마 (1단계)

### 원본의 꼴

- 수는 **원문 단위의 십진 그대로** 적는다(`"walk_speed": 5.0`, `"lock_seconds": 3.0`, `"speed": 14`). 정수 단위로 미리 바꿔 적지 않는다(ARCHITECTURE 8.2, CLAUDE.md 5절). 바꾸는 것은 변환기다.
- 필드 이름에 원문 단위를 붙인다: `…_seconds`(초), 칸이 기본이므로 칸은 붙이지 않는다(`hit_radius`, `range`), 배율은 `1.2`처럼 배수로(`graze_buff_speed`, `heat_multiplier`, `damage_bonus`).
- 데이터가 **정의하는** 항목의 id는 그 객체의 `"id"`에 둔다(`block.magic_bolt`, `place.field`). `databuild check`의 이름 열쇠 검사(`NameKeyCoverage`)가 이 속성을 본다. 다른 항목을 **가리키는** 참조는 다른 이름으로 둔다(`"element": "element.arcane"`, `"passive": "passive.underline"`). 참조는 이름 열쇠를 보지 않는다.
- 문자열 id가 없는 것(검산용 `CharacterId.Baseline`, 모드)은 `"id"`를 쓰지 않는다. `characters.json`의 `"baseline"` 객체, `arena.json`의 `"modes": ["endless", "practice"]`가 그 예다.
- 출처와 [임시] 표시는 `"_note"`에 적는다(변환기가 읽지 않는다). 명세에 없는 값은 명세의 [임시] 값을 쓰고 노트에 적는다(예: `arena.json`의 연습 모드 전장).
- 아직 타입이 없는 값(예: 거대 화염구의 폭발 반경 → `BlockParams`가 비어 있다)은 원본에 넣지 않고 노트에 "어느 작업이 더한다"고 적는다. 읽지 않는 필드를 원본에 두면 팩과 원본이 어긋난다.

### 스키마

- `data/schema/<원본 파일 이름>.schema.json`. draft 2020-12, `"$id"`는 `https://wizard.invalid/data/schema/<파일 이름>`(SchemaCheck가 확인한다).
- 단위가 있는 수는 `_common.schema.json#/$defs/`의 정의(`cells`, `seconds`, `heat`, `ratio`, `cellsPerSecond` …)를 `$ref`로 가리키고, 범위(`minimum`, `exclusiveMinimum`)를 덧붙인다. 각 필드의 `description`에 "→ 어느 필드, 어느 정수 단위"를 적는다.
- 객체는 `"additionalProperties": false`로 닫는다. 정의를 `allOf`나 `$ref`로 합칠 때는 `"unevaluatedProperties": false`로 닫는다(`characters.schema.json`의 `stats`·`character`). 오타 난 필드가 조용히 무시되지 않게 한다.
- 열거 값은 `enum`으로(`"kind": ["projectile", "control", "modifier"]`), id는 `contentId`에 `pattern`(`"^block\\."`)을 덧붙인다.
- 스키마는 꼴과 범위만 본다. id가 열거형에 있는지, 겹치는지, 기둥이 전장 안인지 같은 것은 변환기가 본다.

## 3. 변환기 (2단계)

`tools/Wizard.DataBuild/Convert/<절>Converter.cs`에 `ISectionConverter` 하나. 등록하지 않는다(`ModuleRegistry`가 어셈블리에서 찾는다). 네임스페이스는 `Wizard.DataBuild`다(`Wizard.DataBuild.Convert`로 두면 같은 어셈블리의 `System.Convert` 호출이 가려진다).

```csharp
public sealed class BlocksConverter : ISectionConverter
{
    public ContentSection Section => ContentSection.Blocks;
    public void Convert(DataFile file, ByteWriter output, Diagnostics diagnostics) { … }
}
```

1. **읽기.** `file.Root`는 스키마를 통과한 JSON이다. 수는 `GetDecimal()`로만 읽는다(실수형 금지). 스키마의 `"integer"`는 `6.0`도 받으므로 정수 필드도 decimal로 읽어 정수인지 본다(본보기의 `Int` 도우미).
2. **id → 열거형.** 문자열 id는 `ContentIds.TryParse`로 바꾼다. 못 바꾸면 `diagnostics.Error(file.RepoPath, "<JSON 위치>", "<이유>")`(예: `/blocks/0/id`). 열거형 이름 같은 표가 코드에 고정된 값(`kind`, `rarity`, 모드)은 `switch`로 바꾼다.
3. **단위.** `tools/Wizard.DataBuild/Core/Units.cs`만 쓴다. 반올림은 가장 가까운 정수, 같으면 짝수(CONTRACTS 0.3).

   | 원본 | 정수 | 도우미 | 본보기 |
   |---|---|---|---|
   | 칸 | Q32.32 Raw | `Units.CellsToRaw` | `hit_radius` 0.15 → 644,245,094 |
   | 칸/초 | 틱당 Raw | `Units.CellsPerSecondToRawPerTick` | `speed` 14 → 1,002,159,036 |
   | 칸/초(이동 속도) | 센티 | `Units.ToCenti` | `walk_speed` 4.25 → 425 |
   | 초 | 틱 | `Units.SecondsToTicks` | `interval_seconds` 0.15 → 9 |
   | 열 | HU | `Units.HeatToHu` | `crit_start` 85 → 10,200 |
   | 열/초 | 틱당 HU | `Units.RoundHalfEven(v, Units.HuPerHeat, Units.TicksPerSecond)` | `cool_per_second` 45 → 90 |
   | 배율 | 천분율 | `Units.RatioToPermille` | `graze_buff_speed` 1.2 → 1,200 |
   | 칸이 아닌 Fix64 값 | Raw | `Units.RoundHalfEven(v, Fix64.OneRaw, 1)` | `base_damage` 10 → 42,949,672,960 |
   | % | 천분율·만분율 | `Units.PercentToPermille`, `PercentToBp` | |
   | 도 | 센티도 | `Units.DegreesToCenti` | |

4. **교차 검사.** 그 절 안에서 보는 규칙(겹치는 id, 즉발이면 속도 0, 기둥이 전장 안, 시작 위치가 기둥 밖)은 변환기가 진단으로 낸다. 여러 절에 걸친 검사(참조 무결성 등)는 `IDataValidator`로 따로 둔다(`tools/Wizard.DataBuild/Validate/`).
5. **정규 순서로 쓰기.** 항목을 열거형 값 오름차순으로 정렬해 쓴다(원본의 나열 순서, 사전 순회 순서에 기대지 않는다). 겹치는 키는 오류다. 같은 키끼리는 원본 순서로 두어 진단이 늘 뒤에 나온 줄을 가리키게 한다. 진단이 하나라도 생겼으면 아무것도 쓰지 않고 돌아간다.
6. **절 바이트의 꼴.** LE, `ByteWriter`만 쓴다. 본보기는 모두 `u16 개수` + 항목들이고, 항목은 `u8 id`(또는 `u8 mode`)로 시작한 뒤 정의 타입의 필드 순서대로 `i32`(int)·`i64`(Fix64 Raw)·`u8`(열거형, bool 0/1)·`u16`(플래그)을 쓴다. 꼴은 **변환기와 절 파일 두 곳의 머리 주석에 같은 표로** 적는다. 이 꼴은 그 절의 주인 작업이 정하고 바꾼다(팩 머리말·목차와 달리 CONTRACTS 11.2가 고정하지 않는다). 필드를 더할 때는 항목 끝에 더하고 변환기와 절 파일을 같은 커밋에서 고친다.

## 4. 절 읽기 (3단계)

`src/Wizard.Content/Pack/Sections/ContentDb.<절>.cs`가 뼈대(`Pack/Core/ContentDb.Core.cs`)의 훅 둘을 구현한다.

```csharp
public sealed partial class ContentDb
{
    private BlockDef?[] _blocks = Array.Empty<BlockDef?>();      // 열거형 값으로 찾는 표

    partial void LoadBlocks(ReadOnlySpan<byte> section) { … }   // ContentDb.Build 가 한 번 부른다
    partial void FindBlock(BlockId id, ref BlockDef? def) { … }  // 접근자가 부른다. 못 찾으면 def 를 null 로 둔다
}
```

- **읽기.** `ByteReader`로 읽는다. 모자람, 남는 바이트, 오름차순 위반·겹침, 정의되지 않은 열거형 값(`Enum.IsDefined`), 모르는 플래그 비트, 0/1이 아닌 bool은 전부 `ContentException`이다. 팩은 해시로 지켜지지만 읽는 쪽은 바이트를 믿지 않는다.
- **조회.** 표는 열거형 값을 첨자로 쓰는 배열이다(크기는 절에 든 가장 큰 값 + 1). `Dictionary`를 순회하지 않는다. `Find…`는 범위 밖이거나 빈 칸이면 `def`를 건드리지 않는다. 그러면 접근자가 `ContentException`을 던진다("not loaded or has no such entry").
- **불변.** 필드는 `Load…`에서 한 번 채우고 그 뒤로 바꾸지 않는다. 가변 `static`을 두지 않는다(서버는 여러 판을 동시에 돌린다).
- **이름.** 부분 클래스라 절 파일끼리 비공개 멤버 이름이 겹치면 컴파일 오류다. 도우미에는 절 이름을 붙인다(`CharacterI32`, `BlockEnumByte`, `ArenaPoints`, `CharactersBroken`).
- 문화권에 기대는 문자열 연산을 쓰지 않는다(오류 문구는 고정 문자열을 잇기만 한다).

## 5. 정의 타입 (4단계)

- 정의 타입의 이름, 필드, 타입, 단위는 **계약**이다(CONTRACTS 2.2). 필드를 더하거나 바꾸려면 결과 보고에 계약 변경 요청을 올린다. 계약이 "필드는 명세의 표가 주인이다. 그 절의 데이터 작업이 채운다"라고 비워 둔 타입(`BlockParams`, `DuelCharacterOverride`, `ArtDef` …)은 그 절의 데이터 작업이 명세의 표대로 채운다.
- 필드의 뜻 중 계약이 적지 않은 것은 주석으로 적는다(예: `ArenaDef.Pillars`는 1×1칸 기둥의 작은 쪽 모서리, `SpawnPoints`는 슬롯 순).
- 명세의 필드가 정의 타입에 없으면 먼저 그 값의 주인이 어디인지 본다. 예: spec 01 5.1 표의 `DepthDecay`는 캐릭터가 아니라 지팡이 쪽 값(spec 02 5.2, spec 04 7.1)이라 `characters.json`에 두지 않았다.

## 6. 테스트 (5단계)

`tests/Wizard.Content.Tests/<절>PackTests.cs`. 본보기 셋이 같은 꼴이다.

1. **저장소 데이터 왕복.** `Pipeline.Run(TestRepo.Root, ModuleRegistry.Discover(), …)` → `ContentPack.Load` → `ContentDb.Build` → 접근자. **정의 타입의 모든 필드**를 기대 정수와 비교한다. 기대값은 명세의 표에서 가져온다(Raw는 spec 01 2.2·spec 02 4.2 표에 적힌 값, 없으면 `0.12 × 2^32 = 515,396,075.52 → 515,396,076`처럼 계산을 주석에 적는다). 구현의 출력에서 베끼지 않는다.
2. **없는 것은 던진다.** 팩에 없는 id·모드, 절이 없는 팩 → `ContentException`.
3. **결정성.** 같은 data/에서 두 번 만든 절 바이트가 같다. 원본의 순서를 바꿔도 같다(`BlocksPackTests.ReversedSourceOrder_GivesTheSameBytes`).
4. **진단.** 임시 저장소(`Pack/TempDataRepo`, 저장소의 스키마를 복사해 쓴다)에 틀린 원본을 넣고 `"<경로>:<JSON 위치>: <이유>"` 꼴의 진단이 나오는지 본다: 스키마 위반(모르는 필드, 빠진 필드, 범위), 모르는 id, 겹침, 절 고유의 규칙.
5. **깨진 절 바이트.** 모자람, 남음, 모르는 열거형 값, 순서 위반 → `ContentException`. 원래 바이트는 읽힌다.
6. 명세에 테스트 벡터가 있으면 `S<명세 번호>_<벡터 id>__<설명>` 이름으로 옮긴다(CLAUDE.md 8절). 본보기 셋은 카드에 벡터가 없다.

## 7. 이름 열쇠 (6단계)

**자기 id의 이름 열쇠를 같이 더한다.** 데이터가 `"id"`로 정의한 id마다 `data/names/names.ko.json`에 같은 열쇠가 있어야 `databuild check`가 통과한다(없는 열쇠는 화면에 열쇠 문자열 그대로 찍힌다). 값은 spec 14의 "표시(지금)" 칸이다.

- 형식(T-35): 열쇠 오름차순, 한 줄에 열쇠 하나, 마지막 줄 `"~end": ""`. 줄을 더하기만 하고 남의 줄을 고치지 않는다. 정렬이 흐트러지면 `databuild names --fix`.
- 본보기가 더한 열쇠: `block.fireball` 거대 화염구, `block.magic_bolt` 마력탄, `place.arena` 돌마당, `place.field` 한뎃벌.
- 참조(`element.*`, `passive.*` …)의 열쇠는 그 id를 정의하는 작업이 더한다.

## 8. 메타 파일과 버전

- `src/` 아래에 파일이나 폴더를 더하면 `dotnet run --project $WT/tools/Wizard.MetaGen`으로 `.meta`를 만들고 커밋한다(Unity 로컬 패키지).
- 전투 쪽 `src/Wizard.Content/`(Rules 밖)의 `.cs`를 고치면 코어 버전 해시가 바뀐다. `dotnet run --project $WT/tools/Wizard.VersionStamp`로 `src/Wizard.Sim/Generated/SimVersion.g.cs`를 다시 만들어 같이 커밋한다(`gen-check.sh`가 본다). 데이터 JSON만 고치면 해시는 그대로다(데이터는 `data_version`이 가른다).

## 9. 검사

```
dotnet run --project $WT/tools/Wizard.DataBuild -- check      # 꼴·스키마·변환·이름 열쇠·결정성
dotnet run --project $WT/tools/Wizard.DataBuild -- names --check
dotnet test $WT/tests/Wizard.Content.Tests
$WT/tools/ci/ci.sh fast
```

`tests/Wizard.Content.Tests/Pack/PackBuildTests.RepoData_PassesCheck`가 저장소의 data/ 전체를 늘 돌린다. 원본, 스키마, 변환기, 이름 열쇠 중 하나라도 빠지면 거기서 실패한다(원본은 있는데 변환기가 없는 절도 오류다).
