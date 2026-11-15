using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Wizard.Solo.App;
using Wizard.Solo.Net;
using Wizard.Solo.Sim;
using Wizard.Solo.UI;

namespace Wizard.Solo.Tests.PlayMode.Integration
{
    /// <summary>
    /// 온라인 화면 QA(11-online.md): 진짜 앱(Bootstrap)을 띄우고, 같은 프로세스에 방 서버(127.0.0.1 TCP)를 돌리며 온라인 화면을 차례로 열고
    /// 단추를 눌러(화면의 private 처리기를 그대로 부른다) Logs/shots/qa-*.png를 남긴다. 사람이 그림을 보고 배치·글자를 확인하는 용도.
    /// 오류 로그(예외)는 러너가 실패로 잡는다.
    /// </summary>
    public class OnlineQaTests
    {
        string _dataRoot, _shotDir, _acct;
        BootResult _boot;
        TcpPipeListener _listener;
        RoomServer _server;

        // 서버는 따로 스레드에서 60Hz로 돈다(게임의 "요청 → 답" 연결은 답을 기다리며 메인 스레드를 잠깐 막기 때문 — 진짜 서버는 다른 기기에 있다).
        System.Threading.Thread _thread;
        volatile bool _stop;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _dataRoot = Path.Combine(Path.GetTempPath(), "wizard-qa-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dataRoot);
            _shotDir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "shots");
            Directory.CreateDirectory(_shotDir);
            _acct = Path.Combine(_dataRoot, "online_account.json");
            OnlineAccount.FilePath = _acct;
            _boot = Bootstrap.Boot(new BootOptions { BuildVersion = "qa", DataRoot = _dataRoot, InputOverride = new BotInputSource() });
            _listener = new TcpPipeListener(0, bindAll: false);
            _server = new RoomServer(_boot.Context.Content);
            OnlineServer.Override = new List<string> { "127.0.0.1:" + _listener.Port };
            _stop = false;
            _thread = new System.Threading.Thread(() =>
            {
                while (!_stop)
                {
                    lock (_server)
                    {
                        TcpPipe p;
                        while ((p = _listener.Poll()) != null) _server.Accept(p);
                        _server.Tick();
                    }
                    System.Threading.Thread.Sleep(16);
                }
            }) { IsBackground = true };
            _thread.Start();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            _stop = true;
            _thread?.Join(1000);
            OnlineServer.Override = null;
            OnlineAccount.FilePath = null;
            _listener?.Stop();
            Bootstrap.Shutdown();
            yield return null;
            try { if (Directory.Exists(_dataRoot)) Directory.Delete(_dataRoot, true); } catch (Exception) { }
        }

        static IEnumerator RunFor(float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            int frames = 0;
            while (Time.realtimeSinceStartup < end || frames < 5) { frames++; yield return null; }
        }

        IEnumerator Shot(string name)
        {
            string path = Path.Combine(_shotDir, "qa-" + name + ".png");
            var runner = (GameRunner)_boot.Context.Session;
            yield return SmokeCapture.Capture(runner.Host.Camera, path, _ => { });
            Debug.Log("[qa] " + path);
        }

        ScreenManager Mgr => (ScreenManager)_boot.Context.Screens;
        object Top => Mgr.Stack.TopScreen;

        void Call(string method, params object[] args)
        {
            var s = Top;
            var m = s.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.IsNotNull(m, s.GetType().Name + "." + method);
            m.Invoke(s, args);
        }

        void SetInput(string field, string text)
        {
            var s = Top;
            var f = s.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(f, field);
            ((UnityEngine.UI.InputField)f.GetValue(s)).text = text;
        }

        [UnityTest]
        public IEnumerator Qa_OnlineScreens()
        {
            var ctx = _boot.Context;
            ctx.Screens.Replace(ScreenId.MainMenu);
            yield return RunFor(1f);
            yield return Shot("01-mainmenu");

            // 온라인 방 화면(계정 없음)
            ctx.Screens.Push(ScreenId.CoopLobby, ScreenArgs.From(ScreenId.MainMenu));
            yield return RunFor(0.5f);
            yield return Shot("02-lobby-noaccount");

            // 이름 정하기
            SetInput("_nameInput", "QA마법사");
            Call("OnRegister");
            yield return RunFor(0.5f);
            Assert.IsNotNull(OnlineAccount.Load(), "account saved");
            yield return Shot("03-lobby-account");

            // 이 기기에서 협동 + 봇 둘 → 시작
            Call("OnCreateLocal");
            Call("OnAddBot");
            Call("OnAddBot");
            yield return RunFor(0.5f);
            yield return Shot("04-lobby-local-bots");
            Call("OnStart");
            yield return RunFor(2f);
            yield return Shot("05-coop-battle-start");
            Mgr.Dispatch(MenuKey.Back);
            yield return RunFor(0.4f);
            yield return Shot("05b-coop-esc-confirm");
            Mgr.Dispatch(MenuKey.Cancel);
            yield return RunFor(0.3f);
            yield return RunFor(8f);
            yield return Shot("06-coop-battle-8s");
            var w = ctx.Session.MainWorld;
            Assert.IsNotNull(w);
            Debug.Log("[qa] coop phase " + w.Run.Phase + ", players " + w.Players[0].Active + w.Players[1].Active + w.Players[2].Active + ", enemies " + w.Enemies.Count);

            // 나가기(Esc 확인 창)
            Mgr.Dispatch(MenuKey.Back);
            yield return RunFor(0.5f);
            yield return Shot("07-coop-leave-confirm");
            ctx.Session.EndRun();
            ctx.Screens.PopTo(ScreenId.MainMenu);
            ctx.Screens.Push(ScreenId.CoopLobby, ScreenArgs.From(ScreenId.MainMenu));
            yield return RunFor(0.5f);

            // 결투(이 기기, 봇 상대)
            Call("SelectMode", RoomMode.Duel);
            Call("OnCreateLocal");
            Call("OnAddBot");
            yield return RunFor(0.3f);
            yield return Shot("08-lobby-duel");
            Call("OnStart");
            yield return RunFor(1f);
            yield return Shot("09-duel-countdown");
            yield return RunFor(6f);
            yield return Shot("10-duel-fight");
            w = ctx.Session.MainWorld;
            Debug.Log("[qa] duel phase " + w.Run.Phase + " hp " + w.Players[0].Hp + "/" + w.Players[1].Hp + " round " + w.Run.Duel.Round);
            ctx.Session.EndRun();
            ctx.Screens.PopTo(ScreenId.MainMenu);
            ctx.Screens.Push(ScreenId.CoopLobby, ScreenArgs.From(ScreenId.MainMenu));
            yield return RunFor(0.5f);

            // 온라인 방(코드)
            Call("SelectMode", RoomMode.Coop);
            Call("OnCreateOnline");
            yield return RunFor(1f);
            yield return Shot("11-lobby-online-code");
            Call("OnLeave");
            yield return RunFor(0.3f);

            // 광장: 나 + 다른 사람 하나(말풍선)
            ctx.Screens.Push(ScreenId.CoopLobby, ScreenArgs.From(ScreenId.MainMenu));
            yield return RunFor(0.3f);
            Call("OnPlaza");
            var other = new PlazaClient();
            other.Connect(OnlineServer.Connect(ctx.Content), "다른사람", "char.3");
            yield return RunFor(1.5f);
            other.Say("안녕! 거래할래?");
            other.Emote(3);
            yield return RunFor(1f);
            yield return Shot("12-plaza");
            other.Leave();
            ctx.Screens.PopTo(ScreenId.MainMenu);

            // 내 기록·강화 (재료를 넣어 둔다)
            var acct = OnlineAccount.Load();
            lock (_server)
            {
                var prof = _server.Accounts.Get(acct.Id).Profile;
                prof.Gold = 50000;
                prof.Add(Ids.Item.StoneLow, 5);
                prof.Add(Ids.Item.Charm, 1);
                foreach (var b in ctx.Content.Blocks) { if (b.Id != Ids.Block.MagicBolt) prof.Add(b.Id, 1); if (prof.Items.Count > 10) break; }
                // 장터에 다른 사람이 올린 것 하나, 친구 신청 하나
                _server.Accounts.Register("장사꾼", new string('x', 40), out var seller);
                seller.Profile.Add(Ids.Item.StoneMid, 3);
                seller.Profile.Gold = 100;
                _server.Accounts.List(seller.Id, Ids.Item.StoneMid, 3, 900, DateTime.UtcNow.Ticks, out _);
                _server.Accounts.RequestFriend(seller.Id, "QA마법사");
            }
            ctx.Screens.Push(ScreenId.OnlineProfile, ScreenArgs.From(ScreenId.MainMenu));
            yield return RunFor(0.8f);
            yield return Shot("13-profile");
            Call("OnEnhance", 1);
            yield return RunFor(0.5f);
            yield return Shot("14-profile-after-enhance");
            Call("OpenAssembly", 1);
            yield return RunFor(0.8f);
            yield return Shot("15-assembly");
            ctx.Screens.Pop();
            yield return RunFor(0.3f);
            Call("OpenChild", ScreenId.OnlineMarket);
            yield return RunFor(0.8f);
            yield return Shot("16-market");
            ctx.Screens.Pop();
            yield return RunFor(0.3f);
            Call("OpenChild", ScreenId.OnlineFriends);
            yield return RunFor(0.8f);
            yield return Shot("17-friends");
        }
    }
}
