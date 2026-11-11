# 게임 UI 에셋

[`manifest.json`](manifest.json)은 `docs/ui-assets.md`의 ID를 실제 PNG와 아틀라스 좌표에 연결한다. 좌표는 왼쪽 위 기준이며 9조각 인셋은 왼쪽, 위, 오른쪽, 아래 순서다. 여러 프레임이 있는 PNG는 한 줄로 이어 붙였고 `width`와 `height`는 프레임 하나의 크기다. 1920×1080 기준에서 점 필터로 표시한다.

- [UI 미리보기](ui-preview.png)와 [배경 미리보기](backgrounds-preview.png)
- 배경과 카드 삽화는 픽셀 격자에서 작성한 `source/algorithm_ui_illustrations.py`로 다시 굽는다.
- 타이틀 본체와 NPC 대화 초상은 매니페스트의 `external` 항목을 사용한다. 타이틀 제목은 별도 알파 PNG이며, 모닥불 애니메이션은 모닥불이 있는 타이틀 구성에 합성할 수 있다.
- 닉네임, 설명, 점수, 키 이름, 설정 값은 런타임에서 그린다. 꺼진 상태의 사유, 셰이더 원형 타이머, 후순위 꾸미기 낱개 I-M8도 해당 코드와 품목 데이터에서 결정한다.
- `font/`에는 12px와 16px용 비트맵, 글리프 순서, 자폭 및 베이스라인이 있다. 24, 32, 48, 64px 단계는 해당 비트맵을 정수 배율로 키운다. 숫자 스프라이트의 글자 순서는 매니페스트에 있다.
- 12px는 Galmuri11의 한글 및 기호와 Galmuri11 Condensed의 반각 ASCII를 쓴다. 16px는 Neo둥근모와 네 개의 수제 기호 `− ★ → ←`를 쓴다. 두 비트맵 모두 현대 한글 완성형 11,172자를 담았다. 각 글꼴의 SIL OFL 1.1 원문을 함께 둔다. [Galmuri v2.40.4](https://github.com/quiple/galmuri/releases/tag/v2.40.4), [Neo둥근모 v1.601](https://github.com/neodgm/neodgm/releases/tag/v1.601)

공용 부품과 아이콘의 원본과 아틀라스는 이 폴더의 `source/build.py`로 다시 만들 수 있다. Pillow와 fontTools가 필요하며 큰 화면 배경은 480×270 픽셀에서 그려 4배로 저장한다.
