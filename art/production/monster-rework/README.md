# 한뎃벌 몬스터 이미지 재제작

`source/`는 ImageGen에서 생성한 고해상도 픽셀 아트 원본이다. `monsters.md`의 외형과 동작, §9의 프레임 구성에 맞춰 프롬프트를 작성했다. 기존 전투용 시트와 경로를 공유하지 않는다.

- 최종 기록 시점 원본 PNG 172개. animations 60, base 25, boss-ui 14, effects 48, elite 15, ui 10.
- `source/animations/`의 시트는 왼쪽 위에서 오른쪽 아래 순서다. 같은 동작을 따로 생성한 파일도 함께 들어 있다.
- `source-check.json`의 `grid_checked`는 요청한 그리드의 셀 수, 알파와 경계 여백을 검사한 결과다. `missing`은 저장되지 않은 원본, `requires_…`는 개별 셀을 재검토할 원본이다.
- 검사 결과 {'grid_checked': 167, 'requires_edge_review': 3}. 경계 검사에서 제외된 초기 효과 시트는 재생성한 모닥불 시트와 개별 북두꺼비 고리 프레임으로 대체했다.
- 원본은 생성 크기다. 설계 해상도로 줄이고 동작 기준점을 붙이는 과정과 Unity 전투에서의 재생 검증이 필요하다.
