# 산술 경계 벡터 묶음

0 제수, `MinValue ÷ −1`, 128÷64의 몫 넘침, 포화 경계처럼 런타임·아키텍처에 따라 갈릴 수 있는 곳의 벡터다(CONTRACTS 1.1, DECISIONS-TECH T-30, PIPELINE 7.3절의 "산술 경계"). 모든 조합에서 같은 값, 같은 예외 분류가 나와야 한다. 묶음 파일은 M1-CORE-01이 채운다. 읽는 코드는 `Wizard.TestKit.Shared.Arith`(`ArithBundle`, `ArithRunner`)에 있다.

## 파일

`tests/golden/arith/<묶음 이름>.json`. 이 폴더에는 이 README와 묶음 파일만 둔다(하위 폴더, 다른 확장자는 형식 문제로 실패한다).

```json
{
  "format": 1,
  "bundle": "fixmath-div",
  "cases": [
    { "id": "divint-min-neg1", "op": "FixMath.DivInt", "args": ["-2147483648", "-1"], "expect": ["-2147483648"] },
    { "id": "modint-min-neg1", "op": "FixMath.ModInt", "args": ["-2147483648", "-1"], "expect": ["0"] },
    { "id": "divint-by-zero", "op": "FixMath.DivInt", "args": ["7", "0"], "fault": "SimInvariant" }
  ]
}
```

| 필드 | 뜻 |
|---|---|
| `bundle` | 파일 이름에서 `.json`을 뺀 것과 같아야 한다 |
| `cases[].id` | 묶음 안에서 겹치지 않는다 |
| `cases[].op` | 연산 이름. 이름을 실제 함수에 잇는 표(`IArithOps`)는 묶음의 주인이 만든다 |
| `cases[].args` | 문자열 배열. 십진(음수 가능) 또는 `"0x"` + 16진 1~16자리(비트 그대로). JSON 숫자는 64비트 값을 잃으므로 쓰지 않는다 |
| `cases[].expect` | 기대 결과(출력이 여럿이면 순서대로). 십진으로 쓴다. 비교는 64비트 패턴이라 int의 −1을 `0xffffffff`로 쓰면 다르다 |
| `cases[].fault` | 기대 예외 분류. 지금은 `SimInvariant`만 된다(제수 0은 코드가 검사해 `SimInvariantException`을 던진다) |
| `cases[].notes` | 선택 |

`expect`와 `fault`는 정확히 하나만 둔다.

- 기대값은 계약(CONTRACTS 1.1의 나눗셈·넘침 규칙, spec 01)에서 손으로 적는다. 구현을 돌려 나온 값을 베끼지 않는다. 이 파일들은 "다시 만드는 파일"이 아니다.
- 런타임의 예외(`DivideByZeroException`, `OverflowException` …)가 나오면 결과에 `Exception:<형식 이름>`으로 기록되고, 기대와 다르므로 실패한다. 연산 표에 없는 이름은 `UnknownOp`으로 실패한다.
- 결과는 조합별 결과 파일(`../README.md`)의 `arith` 배열에 실린다.
