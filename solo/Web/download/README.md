# 게임 웹사이트

정적 페이지 네 개. 빌드 도구 없이 이 폴더를 그대로 올리면 된다.

| 파일 | 내용 |
|---|---|
| `index.html` | 홈: 첫 화면, 느루 이야기, 다섯 제자, 다운로드 |
| `game.html` | 게임 소개: 웨이브, 보스, 주문 조립, 강화 확률, 네 모드 (게임 화면 사진은 `solo/Logs/shots`에서) |
| `releases.html` | 버전 기록: GitHub Releases를 읽어 최신 판 카드, 숫자(판 수·받은 횟수), 흐름 타임라인, 모든 판 표(OS별 받기, 바뀐 점 펼치기)로 보여 준다. 릴리스 본문은 마크다운(`##` 제목, `-` 목록)으로 쓰면 된다. `releases.html?demo=1`은 예시 데이터로 모양만 본다 |
| `support.html` | 고객센터: 자주 묻는 질문(검색), 버그 제보(GitHub 이슈), 1:1 문의(메일) |
| `site.css`, `site.js` | 모든 페이지가 같이 쓰는 모양·메뉴·다운로드 주소 |
| `config.js` | 저장소, 파일 이름, **고객센터 메일 주소(`supportEmail`)** |

1:1 문의는 서버 없이 방문자의 메일 앱을 내용이 채워진 채로 연다. `supportEmail`이 비어 있으면 양식이 "준비 중"으로 잠긴다.
`assets/`는 기존 그림에서 뽑았다: `hero.jpg`=`art/sprite-pack/redrawn/world/backplates/title-spec.png`,
`logo.png`=`art/production/ui/screen/title/s-0202_title_korean_960x320.png`,
`d1~5.mp4/.jpg`=예전 제자 클립을 바탕 그림 rect 자리에 합성해 앞뒤 왕복 반복(원본 도구는 지웠다), `p1~5.jpg`=같은 바탕의 캐릭터 부분. 게임 파일은 여기에 두지 않고 **GitHub Releases**에 올린다
(파일 하나 2GB까지 무료, 저장소가 공개라 로그인 없이 받힘).

버튼은 `https://github.com/nanjjang/algorithm_game/releases/latest/download/Wizard-Windows.zip` 같은
"가장 최근 릴리스" 고정 주소를 가리키므로, 새 버전을 올려도 페이지는 고칠 필요가 없다.
접속한 사람의 OS(Windows/macOS)를 알아서 큰 버튼에 띄우고, 휴대폰이면 "PC에서 받아 주세요"를 보여 준다.

## 1. 게임 파일 올리기 (버전마다)

Unity에서 Windows·macOS 빌드를 `solo/Builds/Windows`, `solo/Builds/macOS`로 뽑은 뒤:

```bash
solo/Tools/release-download.sh v1.0.0 "첫 공개 버전"
```

zip을 새로 만들 때 `.DS_Store`와 `*_BurstDebugInformation_DoNotShip` 폴더는 뺀다.

## 2. 페이지 올리기 (한 번)

아무 정적 호스팅이면 된다. 도메인 DNS를 Cloudflare에서 관리하므로 **Cloudflare Pages**가 가장 간단하다.

1. Cloudflare 대시보드 → Workers & Pages → 만들기 → Pages → **Direct Upload**.
2. `solo/Web/download` 폴더(README 제외해도 됨)를 끌어다 놓는다.
3. Custom domains에서 `도메인` 또는 `play.도메인`을 붙인다.

또는 명령줄: `npx wrangler pages deploy solo/Web/download --project-name wizard-download`

## 로컬에서 보기

```bash
python3 -m http.server 8765 --directory solo/Web/download
```

## 다른 곳에 파일을 둘 때

`config.js`의 `overrideUrls`에 전체 주소를 넣으면 GitHub 대신 그 주소로 받는다.
