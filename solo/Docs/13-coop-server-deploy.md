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

## 7. 계정 DB(PostgreSQL)와 암호화(TLS) — 2026-11-15

서버는 데이터 폴더(`~/WizardCoopServer`)의 `server.json`을 읽는다. 비밀(DB 비밀번호, 인증서 비밀번호, Google 클라이언트 비밀)은 **이 파일과 이 폴더에만** 둔다(권한 600, 저장소에 올리지 않는다).

```json
{
  "db": "Host=127.0.0.1;Port=5433;Database=wizard;Username=wizard;Password=…",
  "tls_pfx": "server.pfx",
  "tls_password": "…",
  "google_client_id": "",
  "google_client_secret": "",
  "allow_insecure_auth": false
}
```

- `db`가 비어 있으면 예전처럼 `accounts.json` 하나에 쓴다(개발용). 처음 Postgres로 바꿀 때 DB가 비어 있고 `accounts.json`이 있으면 통째로 옮기고 파일 이름을 `accounts.json.imported-날짜`로 바꾼다.
- `server.pfx`가 있으면 TLS로만 받는다. 로그에 `tls on, fingerprint sha256 …`이 찍힌다. 이 지문을 게임의 `Assets/Resources/DataSolo/Online.json` → `tls_pins`에 넣어 빌드한다. 인증서를 바꾸면 지문도 바꿔야 한다(옛 게임은 붙지 못한다).
- 비밀번호·Google 로그인은 TLS 연결에서만 받는다(`allow_insecure_auth`는 같은 기기 개발에서만 true).

### 7.1 맥미니에 처음 한 번(이미 해 둠)

```bash
brew install postgresql@17
# 맥미니의 Docker가 127.0.0.1:5432를 쓰고 있어서 게임 DB는 5433으로 띄운다.
printf "\nport = 5433\nlisten_addresses = 'localhost'\n" >> /opt/homebrew/var/postgresql@17/postgresql.conf
# TCP 접속은 비밀번호(scram) 필요, 유닉스 소켓은 관리용(trust) 그대로
sed -i "" -E "s/^(host[[:space:]]+all[[:space:]]+all[[:space:]]+(127.0.0.1\/32|::1\/128)[[:space:]]+)trust/\1scram-sha-256/" /opt/homebrew/var/postgresql@17/pg_hba.conf
brew services restart postgresql@17
psql -p 5433 -d postgres -c "create role wizard login password '<무작위>'"
createdb -p 5433 -O wizard wizard
cd ~/WizardCoopServer
openssl req -x509 -newkey rsa:2048 -nodes -keyout tls-key.pem -out tls-cert.pem -days 3650 -subj "/CN=coop.yoonjunseo.com"
openssl pkcs12 -export -legacy -inkey tls-key.pem -in tls-cert.pem -out server.pfx -passout pass:<무작위>
chmod 600 tls-key.pem server.pfx server.json
openssl x509 -in tls-cert.pem -outform der | shasum -a 256   # → Online.json tls_pins
```

표(서버가 처음 뜰 때 만든다, `PostgresAccountDb.cs`): `accounts`(계정·인증·서버 프로필 숫자, 지팡이 상태 jsonb), `items`(창고, 수량 > 0), `friends`, `friend_requests`, `sessions`(로그인 표의 SHA-256), `listings`(장터), `enhance_log`(강화 기록), `meta`, `schema_version`. 골드 ≥ 0, 수량 > 0, 이름·이메일·Google sub 유일은 DB 제약으로도 막는다.

### 7.2 DB 다루기

- 서버가 시작할 때 전부 읽어 메모리에 두고, 처리마다 바뀐 줄을 트랜잭션 하나로 쓴다. **DB를 손으로 고칠 때는 서버를 멈추고**(`coop-server-service.sh stop`) 고친 뒤 `start`. 켠 채로 고치면 서버의 메모리 사본이 덮어쓴다.
- 백업: `pg_dump -p 5433 -Fc wizard > ~/WizardCoopServer/backup-$(date +%Y%m%d).dump` / 되돌리기: `pg_restore -p 5433 -c -d wizard 파일`.
- 들여다보기: `psql -p 5433 -d wizard -c "select id, name, email, gold, rating from accounts order by id"`.
- 시험 계정 지우기(서버 멈춘 뒤): `psql -p 5433 -d wizard -c "delete from accounts where email like 'qa-live-%@example.com'"`.
- 진짜 서버 확인 테스트: `WIZARD_LIVE=1 Tools/unity-test-edit.sh -f LiveServerSmokeTests` (가입·로그인·로그아웃. 만든 계정 이메일이 로그에 찍힌다).

### 7.3 Google 로그인 켜기(사용자가 할 일)

1. https://console.cloud.google.com → 프로젝트 만들기(또는 고르기).
2. **API 및 서비스 → OAuth 동의 화면**: 사용자 유형 "외부", 앱 이름, 지원 이메일. 범위는 `openid`, `email`, `profile`만. 게시 상태가 "테스트"면 테스트 사용자에 넣은 계정만 로그인된다(모두에게 열려면 "앱 게시").
3. **사용자 인증 정보 → 사용자 인증 정보 만들기 → OAuth 클라이언트 ID → 애플리케이션 유형 "데스크톱 앱"**. 리디렉션 URI는 넣지 않는다(데스크톱 앱은 `http://127.0.0.1:아무포트`를 허용한다).
4. 받은 **클라이언트 ID**와 **클라이언트 보안 비밀**을 맥미니 `~/WizardCoopServer/server.json`의 `google_client_id`, `google_client_secret`에 넣고 `coop-server-service.sh restart`. 로그에 `google login on`.
5. 게임 빌드는 바꿀 것이 없다(클라이언트 ID는 로그인 화면이 서버에 물어본다). 로그인 화면의 [Google 계정으로 계속]이 켜진다.

흐름: 게임이 이 기기의 빈 포트에서 기다리며 브라우저로 Google 로그인 쪽을 연다(PKCE S256 + state) → 받은 인가 코드를 TLS로 게임 서버에 보냄 → 서버가 Google 토큰 끝점에서 바꿔 id_token의 iss·aud·exp를 확인 → 처음 온 Google 사용자면 이름을 정하게 한다.

### 7.4 메일(비밀번호 찾기)

비밀번호를 잃었을 때 6자리 코드를 이메일로 보내 새 비밀번호를 정하게 한다. 메일은 [Resend](https://resend.com) HTTPS API로 보낸다(무료 플랜으로 충분하다).
가입 확인 메일은 아직 없다(가입·로그인은 메일 없이 된다).

1. Resend에서 도메인을 추가해 인증한다(Cloudflare DNS에 SPF·DKIM 레코드). **발신 권한만** 있는 API 키를 도메인 하나에 묶어 만든다.
2. 맥미니 `~/WizardCoopServer/server.json`에 두 줄을 넣는다(권한 600, 저장소에 올리지 않는다).
   ```json
   "resend_api_key": "re_…", "mail_from": "Wizard Game <no-reply@도메인>"
   ```
3. `coop-server-service.sh restart` → 로그에 `recovery mail on`. 로그인 화면의 "지금은 보낼 수 없음" 꼬리표가 사라지고 [비밀번호를 잊었다]가 열린다.

규칙: 코드는 15분, 맞히기 5번, 같은 이메일은 60초 간격·시간당 5번. 없는 이메일도 같은 답을 받는다(계정이 있는지 알려 주지 않는다). 비밀번호를 바꾸면 그 계정의 로그인 표가 모두 지워져 다른 기기에서도 다시 로그인해야 한다.
키가 없거나 비어 있으면 복구 메일은 꺼진 채로 둔다(서버는 그대로 돈다).

