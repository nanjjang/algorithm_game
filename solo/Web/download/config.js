// 사이트 설정. 받을 파일 위치와 고객센터 연락처는 여기 한 곳만 고친다.
// 기본: GitHub Releases의 "가장 최근 릴리스" 고정 주소. 새 릴리스를 올리면 링크는 그대로 두고 파일만 바뀐다.
window.DOWNLOAD_CONFIG = {
  repo: "nanjjang/algorithm_game",
  files: {
    windows: "Wizard-Windows.zip",
    mac: "Wizard-macOS.zip",
  },
  // 다른 곳(맥미니, R2 등)에 올렸으면 여기 전체 주소를 넣는다. 비워 두면 GitHub Releases를 쓴다.
  overrideUrls: {
    windows: "",
    mac: "",
  },
  // 고객센터 1:1 문의를 받을 메일 주소. 비워 두면 문의 양식이 "준비 중"으로 보인다.
  supportEmail: "",
};
