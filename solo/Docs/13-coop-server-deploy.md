# 13. 협동 서버 올리기 — 맥미니 + 서브도메인

구조는 `Docs/11-online.md`. 이 문서는 맥미니를 서버로 두고 `coop.도메인`으로 접속하게 하는 순서만 적는다.
(아래 `coop.example.com`은 자리 표시다. 실제 도메인으로 바꿔 읽는다.)

```
[게임(각자 기기)] --TCP 27870--> coop.example.com --(DNS A 레코드)--> [집 공인 IP] --(공유기 포트포워딩)--> [맥미니 :27870]
```

## 0. 알아 둘 것

- 서버는 **TCP 27870** 하나만 쓴다. HTTP가 아니다 → 웹 프록시·HTTPS 인증서가 필요 없다.
- 그래서 DNS는 **A 레코드(공인 IP)** 여야 한다. Cloudflare를 쓰면 이 레코드는 **프록시 끔(회색 구름, DNS only)** — 주황 구름이면 TCP 27870이 통과하지 않는다.
- 서버와 게임은 **같은 빌드**여야 한다(프로토콜 판이 다르면 "방에 들어가지 못했다"). 게임을 고치면 서버도 같이 바꾼다(4절).
- 아직 계정·암호화가 없다(4단계). 주소를 아는 사람은 누구나 방에 들어올 수 있다. 방은 서버당 하나, 4명까지.

## 1. 빌드 (개발 맥)

Unity 에디터를 닫고:

```bash
solo/Tools/coop-server.sh build
```

결과: `solo/Builds/CoopServer/Wizard.app` (git에는 안 올라간다 — `solo/.gitignore`의 `/Builds/`). 이 앱 하나가 서버이자 게임이다.
- 서버로 띄우기: `-batchmode -nographics -coopServer -port 27870`
- 게임으로 띄우기: 더블클릭(서버가 같은 앱으로 돌고 있으면 `open -n Wizard.app`)

## 2. 맥미니에 서비스로 설치

1. `Wizard.app`과 `solo/Tools/macmini/coop-server-service.sh`를 맥미니로 옮긴다(AirDrop 등, 예: `~/Downloads`).
2. 맥미니 터미널:

```bash
chmod +x ~/Downloads/coop-server-service.sh
```
```bash
~/Downloads/coop-server-service.sh install ~/Downloads/Wizard.app 27870
```

- 앱이 `~/WizardCoopServer/Wizard.app`으로 복사되고, launchd 서비스(`com.wizard.coopserver`)로 등록된다.
- 로그인하면 자동으로 켜지고, 죽으면 다시 뜬다. 로그: `~/Library/Logs/WizardCoopServer.log`.
- 확인: `~/Downloads/coop-server-service.sh status` → `포트 27870: 열림`, 로그에 `[coop-server] listening on port 27870`.
- 처음 켤 때 "들어오는 네트워크 연결을 허용할까요?" 창이 뜨면 **허용**.

맥미니 설정(시스템 설정):
- **잠자기 끄기**: 에너지 → "디스플레이가 꺼져 있을 때 자동으로 잠자기 방지" 켬.
- **자동 로그인**: 사용자 및 그룹 → 자동 로그인(서비스가 로그인 세션에서 돈다. 정전 뒤 재부팅에도 다시 뜨게).
- **정전 후 자동 시동**: 에너지 → "정전 후 자동으로 시동" 켬.
- **내부 IP 고정**: 공유기 DHCP 예약으로 맥미니 MAC에 고정 IP를 준다(포트포워딩 대상이 바뀌지 않게). `status`가 내부 IP를 보여 준다.

## 3. 밖에서 들어오게: 포트포워딩 + 서브도메인

### 3.1 공유기 포트포워딩
공유기 관리 화면(보통 `192.168.0.1` / `192.168.1.1`)에서:
- 외부 포트 **27870/TCP** → 내부 IP(맥미니) **27870/TCP**.

### 3.2 공인 IP 확인
맥미니에서:
```bash
curl -s https://api.ipify.org; echo
```

### 3.3 DNS에 서브도메인
도메인 DNS 관리(가비아·Cloudflare·Route 53 …)에서 레코드 하나:

| 종류 | 이름 | 값 | TTL | 비고 |
|---|---|---|---|---|
| A | `coop` | (3.2의 공인 IP) | 300 | Cloudflare면 **DNS only(회색 구름)** |

확인(아무 기기):
```bash
dig +short coop.example.com
```
```bash
nc -z -w 3 coop.example.com 27870 && echo OPEN
```

> 같은 집 와이파이 안에서는 공유기가 "자기 공인 IP로 되돌아오는 연결(NAT 루프백·헤어핀)"을 막는 경우가 있다. 집 안에서 `coop.도메인`이 안 되면 집 안 기기는 맥미니 내부 IP로 들어가고, 바깥 확인은 휴대폰 핫스팟으로 한다.

### 3.4 공인 IP가 바뀌는 집이면 (DDNS)
가정용 인터넷은 공인 IP가 가끔 바뀐다. 둘 중 하나:
- 공유기의 DDNS(예: ipTIME `xxx.iptime.org`)를 켜고 DNS에 `coop` **CNAME → xxx.iptime.org**.
- Cloudflare면 맥미니에서 주기적으로 A 레코드를 갱신(Cloudflare API 토큰, `cron`/launchd 5분마다). 토큰은 맥미니에만 두고 저장소에 올리지 않는다.

## 4. 새 빌드로 바꾸기

개발 맥에서 다시 빌드 → 새 `Wizard.app`을 맥미니로 옮기고:

```bash
~/Downloads/coop-server-service.sh update ~/Downloads/Wizard.app
```

게임을 하는 기기들도 같은 새 `Wizard.app`으로 바꾼다.

## 5. 게임에서 접속

메인 메뉴 **협동** → 주소 칸에 `coop.example.com` (포트 27870이면 생략 가능, 다르면 `coop.example.com:포트`) → **참가**.
먼저 들어온 사람이 방장, 방장이 **시작**. 판이 끝나고 모두 나가면 서버가 새 방을 연다.

## 6. 문제가 생기면

| 증상 | 볼 것 |
|---|---|
| "방에 연결하지 못했다" | `status`의 포트 열림 → 맥미니 방화벽 허용 → 포트포워딩 → `nc -z coop.도메인 27870` → DNS가 회색 구름인지 |
| "방에 들어가지 못했다" | 이미 시작한 방(끝날 때까지 기다림) / 4명 가득 / 서버·게임 빌드가 다름(4절) |
| 서버가 계속 다시 뜬다 | `coop-server-service.sh logs`의 마지막 오류 |
| 끊김 | 판 상태를 1초 20번 통째로 보낸다(손님당 약 100KB/s). 보간·차이만 보내기는 다음 작업 |

서비스 해제: `coop-server-service.sh uninstall`.
