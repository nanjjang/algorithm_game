using System.Collections.Generic;
using UnityEngine;

namespace Wizard.Solo.Net
{
    /// <summary>
    /// 광장 손님(11-online.md 6단계). 받은 상태(10Hz)를 사람마다 보간해 보이고, 내 이동 방향·말·감정 표현을 보낸다.
    /// 그리기는 PlazaScreen이 한다(이 클래스는 엔진 화면을 모른다).
    /// </summary>
    public sealed class PlazaClient
    {
        public sealed class Shown
        {
            public PlazaAvatar Now;
            public Vector2 From, To;
            public float T; // 0~1, From → To
            public Vector2 Pos => Vector2.Lerp(From, To, Mathf.Clamp01(T));
        }

        INetPipe _pipe;
        int _sentDir = -2;
        readonly Dictionary<int, Shown> _shown = new Dictionary<int, Shown>();
        readonly List<int> _gone = new List<int>();

        public int MyId { get; private set; } = -1;
        public bool Connected => _pipe != null && _pipe.Connected;
        public bool Rejected { get; private set; }
        public int StatesReceived { get; private set; }

        /// <summary>거래 상태(서버가 보낸 마지막 것).</summary>
        public sealed class TradeView
        {
            public bool Open;
            public string Partner;
            public AccountStore.TradeOffer Mine = new AccountStore.TradeOffer(), Theirs = new AccountStore.TradeOffer(), Inventory = new AccountStore.TradeOffer();
            public bool MyConfirm, TheirConfirm;
            public PlazaHost.TradeResult Result;
        }

        public TradeView Trade { get; private set; } = new TradeView();
        /// <summary>나에게 온 거래 청(없으면 −1).</summary>
        public int InviteFrom { get; private set; } = -1;
        public string InviteName { get; private set; }
        /// <summary>새 거래 결과(Done·Cancelled·Failed …)가 왔으면 한 번 true.</summary>
        public bool TradeResultFresh;

        /// <summary>귓속말 기록(새 것이 뒤). (보낸 이름, 글). 보낸 이름 null = "받을 사람이 없다"(글 = 그 이름).</summary>
        public readonly List<KeyValuePair<string, string>> Whispers = new List<KeyValuePair<string, string>>();

        public void Whisper(string toName, string text)
        {
            if (_pipe == null || string.IsNullOrWhiteSpace(toName) || string.IsNullOrWhiteSpace(text)) return;
            var m = new NetWriter(NetMsg.PlazaWhisper);
            m.Str(toName);
            m.Str(text);
            _pipe.Send(m.ToArray());
        }

        /// <summary>채팅 글이 "/귓 이름 내용" 또는 "@이름 내용"이면 귓속말로 보내고 true.</summary>
        public bool TryWhisperCommand(string line)
        {
            if (string.IsNullOrEmpty(line)) return false;
            string rest = null;
            if (line.StartsWith("@")) rest = line.Substring(1);
            else if (line.StartsWith("/귓 ")) rest = line.Substring(3);
            else if (line.StartsWith("/w ")) rest = line.Substring(3);
            if (rest == null) return false;
            int sp = rest.IndexOf(' ');
            if (sp <= 0) return true;
            Whisper(rest.Substring(0, sp), rest.Substring(sp + 1));
            return true;
        }

        void SendSimple(NetMsg k) { if (_pipe != null) _pipe.Send(new NetWriter(k).ToArray()); }

        public void RequestTrade(int avatarId)
        {
            if (_pipe == null) return;
            var m = new NetWriter(NetMsg.TradeRequest);
            m.Int(avatarId);
            _pipe.Send(m.ToArray());
        }

        public void AnswerTrade(bool accept)
        {
            if (_pipe == null || InviteFrom < 0) return;
            var m = new NetWriter(NetMsg.TradeAnswer);
            m.Int(InviteFrom);
            m.Bool(accept);
            _pipe.Send(m.ToArray());
            InviteFrom = -1;
        }

        public void OfferTrade(AccountStore.TradeOffer o)
        {
            if (_pipe == null) return;
            var m = new NetWriter(NetMsg.TradeOffer);
            m.Long(o.Gold);
            m.Int(o.Items.Count);
            foreach (var kv in o.Items) { m.Str(kv.Key); m.Int(kv.Value); }
            _pipe.Send(m.ToArray());
        }

        public void ConfirmTrade() => SendSimple(NetMsg.TradeConfirm);
        public void CancelTrade() => SendSimple(NetMsg.TradeCancel);

        static AccountStore.TradeOffer ReadOffer(NetReader r)
        {
            var o = new AccountStore.TradeOffer { Gold = r.Long() };
            int n = r.Int();
            for (int i = 0; i < n; i++) { string k = r.Str(); o.Items[k] = r.Int(); }
            return o;
        }
        public IReadOnlyDictionary<int, Shown> People => _shown;

        /// <summary>방 서버에 붙어 광장에 들어간다.</summary>
        public void Connect(INetPipe pipe, string name, string charId)
        {
            _pipe = pipe;
            var m = new NetWriter(NetMsg.EnterPlaza);
            m.Str(name);
            m.Str(charId);
            _pipe.Send(m.ToArray());
        }

        public void Leave() { _pipe?.Close(); _pipe = null; _shown.Clear(); MyId = -1; }

        /// <summary>내 이동 방향(−1 = 멈춤, 0~7 = 위부터 시계 방향). 바뀔 때만 보낸다.</summary>
        public void Move(int dir)
        {
            if (_pipe == null || dir == _sentDir) return;
            _sentDir = dir;
            var m = new NetWriter(NetMsg.PlazaMove);
            m.Int(dir);
            _pipe.Send(m.ToArray());
        }

        public void Say(string text)
        {
            if (_pipe == null || string.IsNullOrWhiteSpace(text)) return;
            var m = new NetWriter(NetMsg.PlazaChat);
            m.Str(text);
            _pipe.Send(m.ToArray());
        }

        public void Emote(byte n)
        {
            if (_pipe == null) return;
            var m = new NetWriter(NetMsg.PlazaEmote);
            m.Byte(n);
            _pipe.Send(m.ToArray());
        }

        /// <summary>프레임마다: 받은 것 처리 + 보간 진행(dt초).</summary>
        public void Update(float dt)
        {
            if (_pipe != null)
            {
                while (_pipe.TryReceive(out var data)) Handle(data);
            }
            float step = dt / (PlazaHost.StateEveryTicks / 60f);
            foreach (var s in _shown.Values) s.T += step;
        }

        void Handle(byte[] data)
        {
            var r = new NetReader(data);
            switch ((NetMsg)r.Byte())
            {
                case NetMsg.PlazaWelcome:
                    MyId = r.Int();
                    break;
                case NetMsg.RoomError:
                    Rejected = true;
                    break;
                case NetMsg.WhisperIn:
                    {
                        string from = r.Str(), text = r.Str();
                        Whispers.Add(new KeyValuePair<string, string>(from, text));
                        if (Whispers.Count > 20) Whispers.RemoveAt(0);
                    }
                    break;
                case NetMsg.TradeInvite:
                    InviteFrom = r.Int();
                    InviteName = r.Str();
                    break;
                case NetMsg.TradeState:
                    {
                        var t = new TradeView { Open = r.Bool(), Partner = r.Str() };
                        t.Mine = ReadOffer(r);
                        t.Theirs = ReadOffer(r);
                        t.MyConfirm = r.Bool();
                        t.TheirConfirm = r.Bool();
                        t.Inventory = ReadOffer(r);
                        t.Result = (PlazaHost.TradeResult)r.Byte();
                        Trade = t;
                        if (t.Result != PlazaHost.TradeResult.None) TradeResultFresh = true;
                    }
                    break;
                case NetMsg.PlazaState:
                    {
                        r.Int();
                        int n = r.Int();
                        _gone.Clear();
                        _gone.AddRange(_shown.Keys);
                        for (int i = 0; i < n; i++)
                        {
                            var a = PlazaCodec.Read(r);
                            _gone.Remove(a.Id);
                            if (_shown.TryGetValue(a.Id, out var s))
                            {
                                s.From = s.Pos;
                                s.To = a.Pos;
                                s.T = 0f;
                                s.Now = a;
                            }
                            else _shown[a.Id] = new Shown { Now = a, From = a.Pos, To = a.Pos, T = 1f };
                        }
                        foreach (var id in _gone) _shown.Remove(id);
                        StatesReceived++;
                    }
                    break;
            }
        }
    }
}
