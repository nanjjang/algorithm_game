using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using Wizard.Solo.Art;
using Wizard.Solo.Net;
using Wizard.Solo.UI;
using UImage = UnityEngine.UI.Image;
using UInputField = UnityEngine.UI.InputField;

namespace Wizard.Solo.Screens.Online
{
    /// <summary>
    /// 온라인 로그인·가입(ScreenId.OnlineLogin, docs/spec/13-ui-flows.md 3절, docs/ui-assets.md 4.3절).
    /// 야영지 밤 장면 위, 왼쪽에 "서버 명부" 판(S-0301 서버 카드 그림을 머리에), 오른쪽은 장면(모닥불과 지금 캐릭터)이 그대로 보인다.
    /// 탭 두 개(로그인 / 새 계정) · 이메일 · 비밀번호(가림 점 + 눈) · [들어가기] · 또는 · [Google 계정으로 계속] · [비밀번호를 잊었다](복구 메일 꺼짐 꼬리표).
    /// 서버 일은 전부 Task — 기다리는 동안 버튼 자리에 작은 모닥불(K-44)이 돈다. Google 첫 가입이면 이름 정하기 판으로 바뀐다.
    /// 로그인하면 Close(true)로 부른 화면에 돌아간다(OnResult). 뒤로 가면 Close(null).
    /// </summary>
    public sealed class OnlineLoginScreen : ScreenBase
    {
        const float BoardX = 120f, BoardY = 72f, BoardW = 640f, BoardH = 936f, Pad = 56f;
        const float FieldW = BoardW - 2f * Pad;

        enum Mode { Login, Signup, NeedName }

        Mode _mode = Mode.Login;
        RectTransform _board, _loginGroup, _nameGroup, _hero, _tabs;
        UiLabel _title;
        UiButton _tabLogin, _tabSignup, _submit, _google, _forgot, _back, _nameSubmit;
        UInputField _email, _password, _password2, _name, _googleName;
        RectTransform _emailRow, _passwordRow, _password2Row, _nameRow, _emailNote, _lowerBlock;
        UiLabel _working, _needNameTitle;
        UImage _spinner;
        UiLabel _mailOffTag;
        OnlineUi.ErrorLine _error, _nameError;

        Task<ServerInfo> _infoTask;
        ServerInfo _info;
        Task<AuthResult> _authTask;
        GoogleSignIn _googleFlow;
        AuthResult _pendingGoogle;

        public override ScreenId Id => ScreenId.OnlineLogin;
        protected override bool HasFrame => false;

        protected override void BuildContent(RectTransform content)
        {
            BuildScene(content);
            var ledger = OnlineUi.OnlineImg(content, "frame_ledger", BoardX, BoardY, BoardW, BoardH);
            var boardImg = ledger ?? UiKit.Panel(Ctx, content, UiPanelKind.Board, BoardX, BoardY, BoardW, BoardH);
            _board = boardImg.rectTransform;
            _board.name = "LoginBoard";
            UiKit.Bevel(_board);
            // 머리 그림: 여럿이 앉은 모닥불(서버 프로필).
            _hero = OnlineUi.Img(Ctx, _board, "S-0301", "server_card", (BoardW - 496f) * 0.5f, 28f, 496f, 240f).rectTransform;
            OnlineUi.Img(Ctx, _board, "K-12", "crow_feather", BoardW - 40f, -8f, 32f, 32f);
            _title = UiKit.Label(Ctx, _board, T(OnlineKeys.LoginTitle), UiFontId.F32, Pad, 276f, FieldW, 44f, UiTheme.Chalk, TextAnchor.MiddleCenter);
            UiKit.DropShadow(_title);

            _loginGroup = UiKit.Node(_board, "LoginGroup", 0f, 0f, BoardW, BoardH);
            float tabW = FieldW / 2f;
            _tabs = UiKit.Node(_loginGroup, "Tabs", 0f, 332f, BoardW, 48f);
            _tabLogin = UiKit.Button(Ctx, _tabs, T(OnlineKeys.LoginTabLogin), Pad, 0f, tabW, 48f, () => SetMode(Mode.Login), UiButtonStyle.Tab, Focus);
            _tabSignup = UiKit.Button(Ctx, _tabs, T(OnlineKeys.LoginTabSignup), Pad + tabW, 0f, tabW, 48f, () => SetMode(Mode.Signup), UiButtonStyle.Tab, Focus);

            _emailRow = UiKit.Node(_loginGroup, "EmailRow", 0f, 0f, BoardW, 80f);
            _email = OnlineUi.Field(Ctx, Focus, _emailRow, T(OnlineKeys.LoginEmail), T(OnlineKeys.LoginEmailHint), Pad, 0f, FieldW, AccountStore.EmailMax, false, UiFontId.F24);
            _email.contentType = UInputField.ContentType.EmailAddress;
            OnlineUi.OnlineImg((RectTransform)_email.transform, "icon_email_account", FieldW - 44f, 8f, 32f, 32f);
            _emailNote = UiKit.Node(_loginGroup, "EmailNote", Pad, 0f, FieldW, 22f);
            UiKit.Label(Ctx, _emailNote, T(OnlineKeys.LoginEmailNote), UiFontId.F12, 2f, 0f, FieldW, 22f, UiTheme.SlateBlue);
            _nameRow = UiKit.Node(_loginGroup, "NameRow", 0f, 0f, BoardW, 80f);
            _name = OnlineUi.Field(Ctx, Focus, _nameRow, T(OnlineKeys.LoginName), T(OnlineKeys.LoginNameHint), Pad, 0f, FieldW, AccountStore.NameMax);
            _passwordRow = UiKit.Node(_loginGroup, "PasswordRow", 0f, 0f, BoardW, 80f);
            _password = OnlineUi.Field(Ctx, Focus, _passwordRow, T(OnlineKeys.LoginPassword), T(OnlineKeys.LoginPasswordHint), Pad, 0f, FieldW, AccountStore.PasswordMax, true, UiFontId.F24);
            _password2Row = UiKit.Node(_loginGroup, "Password2Row", 0f, 0f, BoardW, 80f);
            _password2 = OnlineUi.Field(Ctx, Focus, _password2Row, T(OnlineKeys.LoginPassword2), T(OnlineKeys.LoginPassword2Hint), Pad, 0f, FieldW, AccountStore.PasswordMax, true, UiFontId.F24);

            // 아래 묶음(버튼·또는·Google·잊음·오류)은 칸 수에 따라 위아래로 옮긴다.
            _lowerBlock = UiKit.Node(_loginGroup, "Lower", 0f, 0f, BoardW, 360f);
            _submit = UiKit.Button(Ctx, _lowerBlock, T(OnlineKeys.LoginSubmit), Pad, 0f, FieldW, 64f, Submit, UiButtonStyle.Primary, Focus);
            _spinner = OnlineUi.AnimImg(Ctx, _lowerBlock, "K-44", null, Pad + 24f, 16f, 32f, 32f);
            _working = UiKit.Label(Ctx, _lowerBlock, T(OnlineKeys.LoginWorking), UiFontId.F16, Pad + 72f, 0f, FieldW - 72f, 64f, UiTheme.Chalk, TextAnchor.MiddleLeft);
            UiKit.Divider(Ctx, _lowerBlock, Pad, 92f, FieldW / 2f - 40f);
            UiKit.Label(Ctx, _lowerBlock, T(OnlineKeys.LoginOr), UiFontId.F16, Pad + FieldW / 2f - 40f, 80f, 80f, 24f, UiTheme.SlateBlue, TextAnchor.MiddleCenter);
            UiKit.Divider(Ctx, _lowerBlock, Pad + FieldW / 2f + 40f, 92f, FieldW / 2f - 40f);
            // Google 버튼은 공식 자산(google_button.png, 브랜드 규정상 다시 그리지 않는다)이 있으면 그것만 보인다.
            var googleArt = OnlineUi.Online("google_button");
            if (googleArt != null)
            {
                const float GW = 252f, GH = 56f;
                _google = UiKit.Button(Ctx, _lowerBlock, null, Pad + (FieldW - GW) * 0.5f, 116f, GW, GH, StartGoogle, UiButtonStyle.Plain, Focus, null, UiFontId.F24, null, true);
                var gr = _google.ContentRoot != null ? _google.ContentRoot : _google.Rect;
                UiKit.Image(gr, googleArt, 0f, 0f, GW, GH, Color.white, true).name = "google_button";
            }
            else _google = UiKit.Button(Ctx, _lowerBlock, T(OnlineKeys.LoginGoogle), Pad, 116f, FieldW, 56f, StartGoogle, UiButtonStyle.Medium, Focus);
            _forgot = UiKit.Button(Ctx, _lowerBlock, T(OnlineKeys.LoginForgot), Pad, 188f, 220f, 32f, Forgot, UiButtonStyle.Plain, Focus, null, UiFontId.F16);
            _mailOffTag = OnlineUi.Tag(Ctx, _lowerBlock, T(OnlineKeys.LoginMailOffTag), Pad + 228f, 192f, UiTheme.Amber);
            _error = OnlineUi.Error(Ctx, _lowerBlock, Pad, 236f, FieldW);

            // Google 첫 가입: 이름 정하기.
            _nameGroup = UiKit.Node(_board, "NeedName", 0f, 0f, BoardW, BoardH);
            _needNameTitle = UiKit.Label(Ctx, _nameGroup, T(OnlineKeys.LoginNeedName), UiFontId.F24, Pad, 348f, FieldW, 40f, UiTheme.SelectYellow, TextAnchor.MiddleLeft);
            _googleName = OnlineUi.Field(Ctx, Focus, _nameGroup, T(OnlineKeys.LoginName), T(OnlineKeys.LoginNameHint), Pad, 404f, FieldW, AccountStore.NameMax);
            _nameSubmit = UiKit.Button(Ctx, _nameGroup, T(OnlineKeys.LoginNeedNameSubmit), Pad, 500f, FieldW, 64f, SubmitGoogleName, UiButtonStyle.Primary, Focus);
            _nameError = OnlineUi.Error(Ctx, _nameGroup, Pad, 580f, FieldW);

            _back = UiKit.Button(Ctx, _board, T(OnlineKeys.LoginBack), Pad, BoardH - 72f, 180f, 40f, () => Close(), UiButtonStyle.Small, Focus, null, UiFontId.F16);
            UiKit.Label(Ctx, _board, T(OnlineKeys.LoginSecureNote), UiFontId.F12, Pad + 196f, BoardH - 72f, FieldW - 196f, 40f, UiTheme.SlateBlue, TextAnchor.MiddleRight, wrap: true);
        }

        /// <summary>야영지 밤 장면 + 모닥불 + 오른쪽에 서버에서 할 수 있는 것 세 줄(그림 + 짧은 글).</summary>
        void BuildScene(RectTransform content)
        {
            OnlineUi.Scene(Ctx, content, "S-0501", "camp_background", 0.35f, "bg_login");
            var fire = OnlineUi.AnimImg(Ctx, content, "S-0501", "campfire", 1236f - 64f, 780f - 100f, 128f, 128f);
            fire.name = "Campfire";
            float x = 880f, y = 168f;
            var head = UiKit.Label(Ctx, content, T(OnlineKeys.LoginSideTitle), UiFontId.F48, x, y, 900f, 64f, UiTheme.Chalk, TextAnchor.MiddleLeft);
            UiKit.DropShadow(head, 3f);
            y += 88f;
            string[] icons = { "duel", "square", "market" };
            string[] keys = { OnlineKeys.LoginSide1, OnlineKeys.LoginSide2, OnlineKeys.LoginSide3 };
            for (int i = 0; i < 3; i++)
            {
                OnlineUi.Img(Ctx, content, "I-F1", icons[i], x, y + 4f, 32f, 32f);
                var l = UiKit.Label(Ctx, content, T(keys[i]), UiFontId.F24, x + 48f, y, 860f, 40f, UiTheme.Chalk, TextAnchor.MiddleLeft);
                UiKit.DropShadow(l);
                y += 52f;
            }
        }

        public override void OnEnter(ScreenArgs args)
        {
            base.OnEnter(args);
            _info = null;
            _infoTask = OnlineAuth.Info(Ctx != null ? Ctx.Content : null);
            _authTask = null;
            SetMode(Mode.Login);
            Focus.SetCursor(0, false);
        }

        public override void OnExit()
        {
            _googleFlow?.Dispose();
            _googleFlow = null;
            _pendingGoogle?.Dispose();
            _pendingGoogle = null;
            if (Screens != null) Screens.CaptureKeys = false;
            base.OnExit();
        }

        bool Busy => _authTask != null || _googleFlow != null;

        void SetMode(Mode m)
        {
            _mode = m;
            bool signup = m == Mode.Signup, need = m == Mode.NeedName;
            _loginGroup.gameObject.SetActive(!need);
            _nameGroup.gameObject.SetActive(need);
            _tabLogin.On = m == Mode.Login;
            _tabSignup.On = signup;
            _nameRow.gameObject.SetActive(signup);
            _password2Row.gameObject.SetActive(signup);
            _emailNote.gameObject.SetActive(signup);
            // 새 계정은 칸이 넷이라 머리 그림을 접고 제목·탭을 위로 올린다.
            _hero.gameObject.SetActive(!signup);
            UiKit.Place(_title.Rect, Pad, signup ? 28f : 276f, FieldW, 44f);
            float top = signup ? 84f : 332f;
            UiKit.Place(_tabs, 0f, top, BoardW, 48f);
            // 칸 배치: 이메일 → [메모] → [이름] → 비밀번호 → [비밀번호 확인] → 아래 묶음.
            float y = top + 64f;
            UiKit.Place(_emailRow, 0f, y, BoardW, 80f);
            y += 80f;
            if (signup) { UiKit.Place(_emailNote, Pad, y - 6f, FieldW, 22f); y += 20f; }
            if (signup) { UiKit.Place(_nameRow, 0f, y, BoardW, 80f); y += 80f; }
            UiKit.Place(_passwordRow, 0f, y, BoardW, 80f);
            y += 80f;
            if (signup) { UiKit.Place(_password2Row, 0f, y, BoardW, 80f); y += 80f; }
            UiKit.Place(_lowerBlock, 0f, y + 12f, BoardW, 360f);
            _submit.SetText(T(signup ? OnlineKeys.LoginSubmitSignup : OnlineKeys.LoginSubmit));
            _error.Set(null);
            Refresh();
        }

        void Refresh()
        {
            bool busy = Busy;
            _submit.gameObject.SetActive(!busy);
            _spinner.gameObject.SetActive(busy);
            _working.gameObject.SetActive(busy);
            _working.SetText(T(_googleFlow != null ? OnlineKeys.LoginGoogleWait : OnlineKeys.LoginWorking));
            bool googleOn = _info != null && _info.GoogleOn;
            _google.Enabled = !busy && googleOn;
            _google.DisabledReasonKey = googleOn ? null : OnlineKeys.LoginGoogleOff;
            _tabLogin.Enabled = !busy;
            _tabSignup.Enabled = !busy;
            _mailOffTag.transform.parent.gameObject.SetActive(_info == null || !_info.RecoveryMail);
            _nameSubmit.Enabled = !busy;
        }

        public override void Tick(float dt)
        {
            if (Screens != null)
                Screens.CaptureKeys = _email.isFocused || _password.isFocused || _password2.isFocused || _name.isFocused || _googleName.isFocused;
            HandleKeys();
            if (_infoTask != null && _infoTask.IsCompleted)
            {
                _info = _infoTask.Status == TaskStatus.RanToCompletion ? _infoTask.Result : null;
                _infoTask = null;
                if (_info == null) _error.Set(T(OnlineKeys.LoginNoServer));
                Refresh();
            }
            if (_googleFlow != null && _googleFlow.Result.IsCompleted)
            {
                var code = _googleFlow.Result.Result;
                _googleFlow.Dispose();
                _googleFlow = null;
                if (code.Error != null) { _error.Set(T(OnlineKeys.LoginErrorPrefix + "GoogleFailed")); Refresh(); }
                else { _authTask = OnlineAuth.GoogleExchange(Ctx != null ? Ctx.Content : null, code); Refresh(); }
            }
            if (_authTask != null && _authTask.IsCompleted)
            {
                var r = _authTask.Status == TaskStatus.RanToCompletion ? _authTask.Result : new AuthResult { NoServer = true };
                _authTask = null;
                Finish(r);
            }
        }

        /// <summary>Tab = 다음 칸, Enter = 보내기.</summary>
        void HandleKeys()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.tabKey.wasPressedThisFrame)
            {
                var order = _mode == Mode.Signup ? new[] { _email, _name, _password, _password2 } : new[] { _email, _password };
                for (int i = 0; i < order.Length; i++)
                    if (order[i].isFocused) { order[(i + 1) % order.Length].Select(); order[(i + 1) % order.Length].ActivateInputField(); break; }
            }
            if ((kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) && !Busy)
            {
                if (_mode == Mode.NeedName) { if (_googleName.isFocused) SubmitGoogleName(); }
                else if (_email.isFocused || _password.isFocused || _password2.isFocused || _name.isFocused) Submit();
            }
        }

        string ErrorText(AuthResult r)
        {
            if (r.NoServer) return T(OnlineKeys.LoginNoServer);
            if (r.Error == null) return null;
            if (r.Error == AuthError.Throttled) return F(OnlineKeys.LoginErrorPrefix + "Throttled", "sec", r.WaitSeconds);
            return T(OnlineKeys.LoginErrorPrefix + r.Error.Value);
        }

        void Finish(AuthResult r)
        {
            if (r.Ok)
            {
                PlayUi(UiSounds.ConfirmCheck);
                _pendingGoogle?.Dispose();
                _pendingGoogle = null;
                Close(true);
                return;
            }
            if (r.NeedName)
            {
                _pendingGoogle = r;
                SetMode(Mode.NeedName);
                if (string.IsNullOrEmpty(_googleName.text)) _googleName.text = r.SuggestedName ?? string.Empty;
                _nameError.Set(r.Error != null ? ErrorText(r) : null);
                Refresh();
                return;
            }
            if (_mode == Mode.NeedName) _nameError.Set(ErrorText(r));
            else _error.Set(ErrorText(r));
            Refresh();
        }

        void Submit()
        {
            if (Busy) return;
            var db = Ctx != null ? Ctx.Content : null;
            string email = _email.text != null ? _email.text.Trim() : string.Empty;
            if (!AccountStore.IsValidEmail(email)) { _error.Set(T(OnlineKeys.LoginErrorPrefix + AuthError.BadEmail)); return; }
            if (_mode == Mode.Signup)
            {
                if (_password.text != _password2.text) { _error.Set(T(OnlineKeys.LoginMismatch)); return; }
                if (!AccountStore.IsValidPassword(_password.text, email)) { _error.Set(T(OnlineKeys.LoginErrorPrefix + AuthError.WeakPassword)); return; }
                _authTask = OnlineAuth.EmailSignup(db, email, _password.text, _name.text);
            }
            else _authTask = OnlineAuth.EmailLogin(db, email, _password.text);
            _error.Set(null);
            Refresh();
        }

        void StartGoogle()
        {
            if (Busy || _info == null || !_info.GoogleOn) return;
            string html = "<!doctype html><html><head><meta charset=\"utf-8\"><title>Wizard</title></head>"
                + "<body style=\"background:#1F2130;color:#F2EDE0;font-family:sans-serif;text-align:center;padding-top:20vh\">"
                + "<h2>" + System.Net.WebUtility.HtmlEncode(T(OnlineKeys.LoginBrowserDone)) + "</h2>"
                + "<p>" + System.Net.WebUtility.HtmlEncode(T(OnlineKeys.LoginBrowserClose)) + "</p></body></html>";
            _googleFlow = GoogleSignIn.Begin(_info.GoogleClientId, html);
            if (_googleFlow.Url != null) Application.OpenURL(_googleFlow.Url);
            _error.Set(null);
            Refresh();
        }

        void SubmitGoogleName()
        {
            if (Busy || _pendingGoogle == null) return;
            _authTask = OnlineAuth.GoogleFinish(_pendingGoogle, _googleName.text);
            _nameError.Set(null);
            Refresh();
        }

        void Forget()
        {
            // 복구 메일은 아직 꺼져 있다(13-ui-flows 3.5 "복구 메일 꺼짐 안내").
            _error.Set(T(OnlineKeys.LoginMailOff));
        }

        void Forgot() => Forget();

        public override bool OnBack()
        {
            if (_mode == Mode.NeedName) { _pendingGoogle?.Dispose(); _pendingGoogle = null; SetMode(Mode.Login); return true; }
            Close();
            return true;
        }
    }
}
