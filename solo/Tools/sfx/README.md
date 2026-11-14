# Tools/sfx — 효과음 절차 합성

공용 소리(`Assets/Resources/Audio/common/`)와 제자 1 소리(`Assets/Resources/Audio/char.1/`)를 numpy만으로 합성한다.
녹음이나 외부 음원은 쓰지 않는다. 사용자가 "합성해서 채우라"고 지시해 만들었다(solo/CLAUDE.md의 "소리를 새로 만들지 않는다"를 이 작업에 한해 풀었다).

## 돌리기

```
python -I Tools/sfx/synth.py            # 전부 다시 만든다(+ char.1/audio.json 목록)
python -I Tools/sfx/synth.py --png      # + Logs/sfx/<폴더>_<이름>.png (파형 + 로그 주파수 스펙트로그램)
python -I Tools/sfx/synth.py --only kill_1,ui_press --png
python -I Tools/sfx/synth.py --report   # 파일을 쓰지 않고 길이·피크·음량 표만
```

- 44.1kHz 모노 16bit PCM WAV. `.meta`는 만들지 않는다(Unity가 가져올 때 만든다).
- 같은 `SEED`(synth.py 맨 위)면 같은 바이트가 나온다. 소리마다 씨앗 = `crc32(SEED:폴더/이름)`이라 소리 하나를 고쳐도 다른 소리는 바뀌지 않는다.
- 필요한 것: Python 3 + numpy. 그림(`--png`)만 PIL. scipy는 쓰지 않는다.

## 파일

| 파일 | 내용 |
|---|---|
| `dsp.py` | 도구: 발진기(사인·대역 제한 톱니·사각·FM), 감쇠 사인의 합(종·쇠·유리), 영위상 필터, 움직이는 필터(STFT), 합성 잔향(지수 감쇠 노이즈 IR), 불티·알갱이·김·거품 조각 |
| `synth.py` | 소리 목록(`@snd(폴더, 이름, 분류)`), 마무리(음량·피크·페이드), WAV 쓰기, PNG |

## 합성 방식

- 타격·처치: 음높이가 뚝 떨어지는 저음 펀치(배음을 tanh로 세움) + 띠 노이즈 버스트 + 딸깍 + 고음 반짝(짧은 높은 사인) + 짧은 잔향. 처치는 종 음색 6묶음(물컹·짐승·돌·풀나무·불꽃·작은 것).
- 쇠·종·유리: 비조화 부분음의 감쇠 사인 합(모루 `metal`, 교회 종 근사 `bell`, 유리 `chime`, 깨짐 `glass_shatter` = 무작위 높은 핑 + 노이즈).
- 바람·휙·불: 띠 노이즈의 가운데 주파수를 STFT로 쓸어 올리거나 내린다(`whoosh`, `flame`). 김은 고역 노이즈를 거칠게 일렁인다.
- 울음: 톱니 원음 + 포먼트(띠 공명) + 거칠기 변조(`growl`).
- 선율(+13 속성 주제): 속성별 악기(1 모루 종, 2 유리 종, 3 실로폰, 4 유리 하프, 5 낮은 종 + 합창) × 4마디, 낮은 깔림음, 잔향.
- 대기 반복음(char.1 `idle_*`): 2초 + 0.25초를 만들고 끝을 처음에 겹쳐(등전력 교차) 이음매를 없앤다. 마무리의 저역 필터도 원형(주기)으로 건다.
- 경고(예고): 단2도로 어긋난 두 음과 오르는 음 — 거슬리게.

## 음량 기준

기존 제자 소리(char.2~5, 지피티 합성)를 numpy로 잰 값: 피크 −3.7 ~ −15 dBFS, 50ms 최대 RMS 대체로 −12 ~ −26 dB, 대기 반복음 −30 dB 안팎.
여기 소리는 분류마다 **50ms 창 최대 RMS**(120Hz 아래를 깎고 잰다 — 작은 스피커에서 안 들리는 초저역이 음량을 정하지 않게)를 맞춘다.

| 분류 | 목표(dB) | 예 |
|---|---|---|
| ui / ui_main | −25 / −22 | 누름·커서 / 화면 열기·확인·오류 |
| fire / char_fire | −17 / −16 | 발사 블록(C-01) / 제자 1 발사 덧소리(C-02) |
| hit · tele · chime · enh | −16 | 적중, 예고, 결정·스침 종, 강화 보통 |
| kill | −14 | 처치 |
| player · boss · theme | −13 / −13 / −14 | 내가 맞음·쓰러짐, 보스·허상, +13 선율 |
| big · enh_big | −12 | 폭발·한꺼번에 처치·엘리트, 강화 성공·대성공·팡파르·+13 종 |
| art / heat | −15 / −18 | 비기 / 열·스침·결정 보조음 |
| enemy · cry · drop · wave · enh_small | −18 · −19 · −19 · −17 · −21 | 적 탄·등장, 적 울음, 줍기, 웨이브, 제물·확률 숫자 |
| ambient / loop | −22 / −30 | 시간대 전환 / 대기 반복음 |

- 피크는 −1 dBFS 이하: −7 dBFS 위만 tanh로 부드럽게 누르고, 그래도 넘으면 줄인다(실제 최대 −1.3).
- 가장 큰 분류(−12)와 보통(−16)의 차이는 4dB — "평소보다 6dB 넘게 크지 않다"(10 요약서 5.2절) 안.
- 꼬리: 피크에서 −60dB 아래로 내려간 뒤는 자르고, 끝 20ms(짧으면 길이의 1/8) 코사인 페이드, 첫 1.5ms 페이드인. 직류·28Hz 아래 제거.

## 연결(이 소리가 어디서 나나)

- 배치표: `Assets/Resources/DataSolo/Audio.json` — `common.events`(사건 id → 클립), `common.sfx`(sfx 번호 → 클립), `characters.char.1`.
  캐릭터 표의 클립 이름에 `/`가 있으면 clip_root 아래 경로(`common/enh_plus13_theme_2`). `{cycle:[...]}`은 부를 때마다 다음 것(E-18 금 번짐 세 번).
- 원문 bindings에 sfx가 없던 사건(웨이브 시작·클리어, 줍기, 발사 블록별, 적 종별 울음, 처치 음색 묶음, 불가마 꺼짐)은
  `Assets/Resources/DataSolo/G16.json` `sound.solo_events · shot_block_prefix · enemy_windup · kill_groups`가 정하고 `SoundEventMap`이 낸다.
- 배치와 음량은 원문에 없는 솔로 임시다. 실제 듣기와 조정은 사용자 QA에서 한다(소리를 고치려면 synth.py의 그 함수를 고치고 다시 돌린다).
