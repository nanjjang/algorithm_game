# 골든 변경 기록

골든의 기대 해시(`tests/golden/**/expected*.json`)를 새로 만들거나 다시 만든 작업은 아래에 한 줄을 더한다(PIPELINE 4.4절, DECISIONS-TECH T-41). 이 파일은 줄을 더하기만 한다. 남의 줄을 고치거나 지우지 않는다. 병합에서 충돌하면 양쪽 줄을 다 살린다.

형식: `- <작업 id> | <골든 경로(쉼표로 여럿)> | <왜 해시가 생겼거나 바뀌었는가>`

검토자는 "해시가 바뀔 이유가 있는 변경인가"를 본다(PIPELINE 3.6절 9번). 이유가 "다시 돌렸더니 달라졌다"뿐이면 반려다.

- M0-TE-01 | (없음) | 기록 파일을 만든다. 아직 골든이 없다
- M1-TE-01 | m1/practice-solo, m1/practice-dense, m1/practice-stance, m1/practice-loadout, m1/endless-solo, m1/duel-basic, m1/duel-dense | 첫 골든을 만든다(M1 코어: 이동·자세·과열·스침·판정기와 시제품 내용, 저장소 data/ 의 팩 data_version 1)
- M2-SPL-02 | m1/practice-solo, m1/practice-dense, m1/practice-stance, m1/practice-loadout, m1/endless-solo, m1/duel-basic, m1/duel-dense | 세계 상태가 늘었다(DECISIONS-TECH T-57): 블록이 있는 팩의 모든 세계에서 절 6(SpellTable)에 표의 길이와 플레이어마다 현재 주문 핸들이 생기고, 시제품의 준비 표시와 조립이 절 6에서 절 7(Arts)의 앞으로 옮겨 갔다. main과 이 브랜치의 replaycli trace를 일곱 골든의 모든 틱에서 비교해 절 6과 절 7의 요약만 다르고 나머지 절(이동·열·스침·적·결투 등)은 모든 틱에서 같다는 것을 확인했다
