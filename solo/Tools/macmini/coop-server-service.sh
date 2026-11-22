#!/usr/bin/env bash
# 맥미니에서 협동 전용 서버를 "항상 켜진 서비스"로 돌린다(Docs/13-coop-server-deploy.md).
# 로그인하면 자동으로 켜지고, 죽으면 launchd가 다시 띄운다.
#
#   ./coop-server-service.sh install <Wizard.app 경로> [포트]   # 앱을 ~/WizardCoopServer로 복사하고 서비스 등록·시작
#   ./coop-server-service.sh update  <Wizard.app 경로>          # 새 빌드로 바꾸고 다시 시작(포트는 그대로)
#   ./coop-server-service.sh status                             # 켜져 있나, 포트가 열렸나, 로그 끝 몇 줄
#   ./coop-server-service.sh logs                               # 로그 따라 보기(Ctrl+C로 나감)
#   ./coop-server-service.sh restart | stop | start
#   ./coop-server-service.sh uninstall                          # 서비스 해제(앱 폴더는 남긴다)
set -euo pipefail

LABEL="com.wizard.coopserver"
HOME_DIR="$HOME/WizardCoopServer"
APP_DST="$HOME_DIR/Wizard.app"
PORT_FILE="$HOME_DIR/port"
PLIST="$HOME/Library/LaunchAgents/$LABEL.plist"
LOG="$HOME/Library/Logs/WizardCoopServer.log"
DEFAULT_PORT=27870

die() { echo "coop-server-service: $*" >&2; exit 1; }

bin_path() {
  local b
  b="$(ls "$APP_DST/Contents/MacOS/" 2>/dev/null | head -1)"
  [ -n "$b" ] || die "앱이 없다: $APP_DST (먼저 install)"
  printf '%s/Contents/MacOS/%s' "$APP_DST" "$b"
}

copy_app() {
  local src="${1:-}"
  [ -n "$src" ] || die "Wizard.app 경로를 준다"
  [ -d "$src/Contents/MacOS" ] || die "Wizard.app이 아니다: $src"
  mkdir -p "$HOME_DIR"
  rm -rf "$APP_DST"
  cp -R "$src" "$APP_DST"
  # 다른 기기에서 옮겨 온 앱의 격리 표시(Gatekeeper)를 지운다.
  xattr -dr com.apple.quarantine "$APP_DST" 2>/dev/null || true
}

write_plist() {
  local port="$1" bin
  bin="$(bin_path)"
  mkdir -p "$(dirname "$PLIST")" "$(dirname "$LOG")"
  cat > "$PLIST" <<EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>Label</key><string>$LABEL</string>
  <key>ProgramArguments</key>
  <array>
    <string>$bin</string>
    <string>-batchmode</string>
    <string>-nographics</string>
    <string>-coopServer</string>
    <string>-port</string>
    <string>$port</string>
    <string>-logFile</string>
    <string>$LOG</string>
  </array>
  <key>RunAtLoad</key><true/>
  <key>KeepAlive</key><true/>
  <key>ThrottleInterval</key><integer>5</integer>
  <key>ProcessType</key><string>Interactive</string>
</dict>
</plist>
EOF
}

svc_stop() {
  launchctl bootout "gui/$(id -u)/$LABEL" 2>/dev/null || true
  # 내리기는 비동기다: 다 내려갈 때까지 기다린다(최대 10초).
  for _ in 1 2 3 4 5 6 7 8 9 10; do
    launchctl print "gui/$(id -u)/$LABEL" >/dev/null 2>&1 || return 0
    sleep 1
  done
}
svc_start() {
  for _ in 1 2 3; do
    launchctl bootstrap "gui/$(id -u)" "$PLIST" 2>/dev/null && return 0
    sleep 2
  done
  launchctl bootstrap "gui/$(id -u)" "$PLIST"
}

port_now() { [ -f "$PORT_FILE" ] && cat "$PORT_FILE" || echo "$DEFAULT_PORT"; }

case "${1:-}" in
  install)
    port="${3:-$DEFAULT_PORT}"
    copy_app "${2:-}"
    echo "$port" > "$PORT_FILE"
    write_plist "$port"
    svc_stop
    svc_start
    sleep 3
    "$0" status
    echo
    echo "다음: 공유기에서 $port/TCP를 이 맥미니로 포트포워딩하고, 서브도메인 A 레코드를 공인 IP로 둔다(Docs/13-coop-server-deploy.md)."
    ;;
  update)
    svc_stop
    copy_app "${2:-}"
    write_plist "$(port_now)"
    svc_start
    sleep 3
    "$0" status
    ;;
  start)   svc_start ;;
  stop)    svc_stop ;;
  restart) svc_stop; svc_start ;;
  status)
    port="$(port_now)"
    if launchctl print "gui/$(id -u)/$LABEL" >/dev/null 2>&1; then echo "서비스: 등록됨"; else echo "서비스: 없음"; fi
    if nc -z -w 2 127.0.0.1 "$port" 2>/dev/null; then echo "포트 $port: 열림"; else echo "포트 $port: 닫힘"; fi
    echo "내부 IP: $(ipconfig getifaddr en0 2>/dev/null || ipconfig getifaddr en1 2>/dev/null || echo '?')"
    echo "--- 로그 ($LOG)"
    grep -E "coop-server" "$LOG" 2>/dev/null | tail -5 || true
    ;;
  logs)    tail -f "$LOG" ;;
  uninstall)
    svc_stop
    rm -f "$PLIST"
    echo "서비스를 지웠다. 앱은 $HOME_DIR 에 남아 있다."
    ;;
  *)
    sed -n '2,11p' "$0"
    exit 2
    ;;
esac
