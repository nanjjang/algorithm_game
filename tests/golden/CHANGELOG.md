# 골든 변경 기록

골든의 기대 해시(`tests/golden/**/expected*.json`)를 새로 만들거나 다시 만든 작업은 아래에 한 줄을 더한다(PIPELINE 4.4절, DECISIONS-TECH T-41). 이 파일은 줄을 더하기만 한다. 남의 줄을 고치거나 지우지 않는다. 병합에서 충돌하면 양쪽 줄을 다 살린다.

형식: `- <작업 id> | <골든 경로(쉼표로 여럿)> | <왜 해시가 생겼거나 바뀌었는가>`

검토자는 "해시가 바뀔 이유가 있는 변경인가"를 본다(PIPELINE 3.6절 9번). 이유가 "다시 돌렸더니 달라졌다"뿐이면 반려다.

- M0-TE-01 | (없음) | 기록 파일을 만든다. 아직 골든이 없다
- M1-TE-01 | m1/practice-solo, m1/practice-dense, m1/practice-stance, m1/practice-loadout, m1/endless-solo, m1/duel-basic, m1/duel-dense | 첫 골든을 만든다(M1 코어: 이동·자세·과열·스침·판정기와 시제품 내용, 저장소 data/ 의 팩 data_version 1)
