using System;
using System.Collections.Generic;
using UnityEngine;

namespace Wizard.Solo.Net
{
    /// <summary>광장의 한 사람(서버 권위). 좀비고식 광장: 걸어 다니고, 말풍선으로 말하고, 감정 표현을 한다.</summary>
    public sealed class PlazaAvatar
    {
        public int Id;
        public string Name;
        public string CharId;
        public Vector2 Pos;
        /// <summary>마지막으로 본 방향(−1 = 멈춤 중, 0~7 = Dir8 순서와 같은 8방향: 0 위, 1 오른위 … 시계 방향).</summary>
        public int MoveDir = -1;
        public int FaceDir = 4;
        public string Bubble;
        public int BubbleUntilTick;
        public byte Emote;
        public int EmoteUntilTick;
    }

    /// <summary>
    /// 광장 서버(11-online.md 6단계, 좀비고식). 시뮬(SimWorld) 없이 가볍게: 사람마다 자리·방향·말풍선만. 한 광장 최대 40명.
    /// 60Hz로 움직이고 10Hz로 전체 상태를 보낸다(40명 × ~40바이트 ≈ 1.6KB). 말은 1초에 1번, 60자까지, 6초 동안 말풍선.
    /// 방(협동·결투)은 광장 안에서 [방 만들기]·[코드로 참가]로 간다 — 그때 클라이언트가 방 서버에 새 연결을 연다.
    /// </summary>
    public sealed class PlazaHost
    {
        public const int MaxAvatars = 40;
        public const float Width = 40f, Height = 22f;
        public const float Speed = 4.5f;
        public const int StateEveryTicks = 6;
        public const int ChatMaxChars = 60;
        public const int ChatCooldownTicks = 60;
        public const int BubbleTicks = 60 * 6;
        public const int EmoteTicks = 60 * 2;
        public const int NameMaxChars = 12;

        sealed class Peer
        {
            public INetPipe Pipe;
            public PlazaAvatar Avatar;
            public int LastChatTick = -100000;
            public long AccountId;
            public Trade Trade;
            public int InviteFrom = -1; // 나에게 온 청(아바타 id)
        }

        /// <summary>두 사람의 거래 한 판.</summary>
        sealed class Trade
        {
            public Peer A, B;
            public AccountStore.TradeOffer OfferA = new AccountStore.TradeOffer(), OfferB = new AccountStore.TradeOffer();
            public bool ConfirmA, ConfirmB;
            public Peer Other(Peer p) => p == A ? B : A;
            public AccountStore.TradeOffer Mine(Peer p) => p == A ? OfferA : OfferB;
        }

        public enum TradeResult : byte { None = 0, Done = 1, Cancelled = 2, Failed = 3, Busy = 4, Guest = 5, TooFar = 6 }

        /// <summary>계정 저장소(방 서버가 넣는다). 없으면 거래를 하지 않는다.</summary>
        public AccountStore Accounts;
        /// <summary>거래를 청할 수 있는 거리(칸).</summary>
        public const float TradeRange = 3f;

        readonly List<Peer> _peers = new List<Peer>();
        int _tick;
        int _nextId = 1;
        readonly System.Random _rng = new System.Random();

        public int Count => _peers.Count;
        public IEnumerable<PlazaAvatar> Avatars { get { foreach (var p in _peers) yield return p.Avatar; } }

        /// <summary>EnterPlaza 메시지(이름, 캐릭터 id)를 읽고 들인다. 가득이면 RoomError(Full).</summary>
        public void Accept(INetPipe pipe, NetReader enter, string accountName = null, long accountId = 0)
        {
            string name = null, charId = null;
            try { name = enter.Str(); charId = enter.Str(); } catch (Exception) { }
            if (!string.IsNullOrEmpty(accountName)) name = accountName; // 계정이 있으면 계정 이름
            if (_peers.Count >= MaxAvatars)
            {
                var m = new NetWriter(NetMsg.RoomError);
                m.Byte((byte)RoomError.Full);
                pipe.Send(m.ToArray());
                pipe.Close();
                return;
            }
            name = Clean(name, NameMaxChars);
            if (string.IsNullOrEmpty(name)) name = "?";
            if (string.IsNullOrEmpty(charId) || !charId.StartsWith("char.", StringComparison.Ordinal)) charId = "char.1";
            var a = new PlazaAvatar
            {
                Id = _nextId++,
                Name = name,
                CharId = charId,
                // 광장 가운데 분수 둘레에 흩어서 선다.
                Pos = new Vector2(Width * 0.5f + (float)(_rng.NextDouble() * 6 - 3), Height * 0.5f + (float)(_rng.NextDouble() * 4 - 2)),
            };
            _peers.Add(new Peer { Pipe = pipe, Avatar = a, AccountId = accountId });
            var w = new NetWriter(NetMsg.PlazaWelcome);
            w.Int(a.Id);
            pipe.Send(w.ToArray());
            SendState();
        }

        public void Tick()
        {
            _tick++;
            for (int i = _peers.Count - 1; i >= 0; i--)
            {
                var p = _peers[i];
                if (!p.Pipe.Connected) { EndTrade(p, TradeResult.Cancelled); _peers.RemoveAt(i); continue; }
                while (p.Pipe.TryReceive(out var data))
                {
                    try { Handle(p, data); }
                    catch (Exception) { p.Pipe.Close(); break; }
                }
            }
            const float dt = 1f / 60f;
            foreach (var p in _peers)
            {
                var a = p.Avatar;
                if (a.MoveDir >= 0)
                {
                    a.Pos += DirVector(a.MoveDir) * (Speed * dt);
                    a.Pos = new Vector2(Mathf.Clamp(a.Pos.x, 0.5f, Width - 0.5f), Mathf.Clamp(a.Pos.y, 1.0f, Height - 0.5f));
                }
                if (a.Bubble != null && _tick >= a.BubbleUntilTick) a.Bubble = null;
                if (a.Emote != 0 && _tick >= a.EmoteUntilTick) a.Emote = 0;
            }
            if (_tick % StateEveryTicks == 0) SendState();
        }

        void Handle(Peer p, byte[] data)
        {
            var r = new NetReader(data);
            switch ((NetMsg)r.Byte())
            {
                case NetMsg.PlazaMove:
                    {
                        int d = r.Int();
                        p.Avatar.MoveDir = d >= 0 && d < 8 ? d : -1;
                        if (p.Avatar.MoveDir >= 0) p.Avatar.FaceDir = p.Avatar.MoveDir;
                    }
                    break;
                case NetMsg.PlazaChat:
                    {
                        string text = Clean(r.Str(), ChatMaxChars);
                        if (string.IsNullOrEmpty(text) || _tick - p.LastChatTick < ChatCooldownTicks) return;
                        if (Muted(p)) return; // 신고가 쌓여 채팅이 막힌 동안(5.3절)
                        p.LastChatTick = _tick;
                        Remember(p, text);
                        p.Avatar.Bubble = text;
                        p.Avatar.BubbleUntilTick = _tick + BubbleTicks;
                    }
                    break;
                case NetMsg.PlazaWhisper:
                    OnWhisper(p, r.Str(), r.Str());
                    break;
                case NetMsg.TradeRequest:
                    OnTradeRequest(p, r.Int());
                    break;
                case NetMsg.TradeAnswer:
                    OnTradeAnswer(p, r.Int(), r.Bool());
                    break;
                case NetMsg.TradeOffer:
                    {
                        var o = new AccountStore.TradeOffer { Gold = Math.Max(0, r.Long()) };
                        int n = Math.Min(r.Int(), 32);
                        for (int k = 0; k < n; k++) { string id = r.Str(); int q = r.Int(); if (id != null && q > 0) o.Items[id] = q; }
                        OnTradeOffer(p, o);
                    }
                    break;
                case NetMsg.TradeConfirm:
                    OnTradeConfirm(p);
                    break;
                case NetMsg.TradeCancel:
                    EndTrade(p, TradeResult.Cancelled);
                    break;
                case NetMsg.PlazaEmote:
                    {
                        byte e = r.Byte();
                        if (e == 0 || e > 8) return;
                        p.Avatar.Emote = e;
                        p.Avatar.EmoteUntilTick = _tick + EmoteTicks;
                    }
                    break;
            }
        }

        public bool HasAccount(long id)
        {
            if (id <= 0) return false;
            foreach (var p in _peers) if (p.AccountId == id) return true;
            return false;
        }

        /// <summary>귓속말: 광장에 있는 그 이름의 사람에게만(대소문자 무시). 없으면 보낸 사람에게 "없다".</summary>
        void OnWhisper(Peer p, string toName, string text)
        {
            text = Clean(text, ChatMaxChars);
            if (string.IsNullOrEmpty(text) || toName == null) return;
            if (_tick - p.LastChatTick < ChatCooldownTicks) return;
            if (Muted(p)) return;
            p.LastChatTick = _tick;
            Remember(p, text);
            Peer to = null;
            foreach (var q in _peers) if (q != p && string.Equals(q.Avatar.Name, toName.Trim(), StringComparison.OrdinalIgnoreCase)) { to = q; break; }
            var m = new NetWriter(NetMsg.WhisperIn);
            if (to == null)
            {
                m.Str(null);
                m.Str(toName);
                p.Pipe.Send(m.ToArray());
                return;
            }
            // 차단(5.3절): 어느 쪽이든 막았으면 귓속말은 가지 않는다(보낸 쪽에는 알리지 않는다).
            if (Accounts != null && Accounts.BlockedEither(p.AccountId, to.AccountId)) return;
            m.Str(p.Avatar.Name);
            m.Str(text);
            to.Pipe.Send(m.ToArray());
        }

        // ------------------------------------------------------------ 채팅 기록 · 정지 · 차단

        readonly Dictionary<long, List<string>> _chatLog = new Dictionary<long, List<string>>();
        /// <summary>계정마다 들고 있는 최근 채팅 줄 수(신고에 붙인다).</summary>
        public const int ChatLogLines = 30;

        bool Muted(Peer p) => Accounts != null && p.AccountId > 0 && Accounts.ChatMutedSeconds(p.AccountId, Accounts.Clock()) > 0;

        void Remember(Peer p, string text)
        {
            if (p.AccountId <= 0) return;
            if (!_chatLog.TryGetValue(p.AccountId, out var l)) _chatLog[p.AccountId] = l = new List<string>();
            l.Add(text);
            if (l.Count > ChatLogLines) l.RemoveRange(0, l.Count - ChatLogLines);
        }

        /// <summary>그 계정이 광장에서 한 최근 말(신고에 붙인다). 없으면 빈 목록.</summary>
        public List<string> RecentChat(long accountId) => _chatLog.TryGetValue(accountId, out var l) ? new List<string>(l) : new List<string>();

        // ------------------------------------------------------------ 거래

        Peer ById(int avatarId)
        {
            foreach (var q in _peers) if (q.Avatar.Id == avatarId) return q;
            return null;
        }

        void OnTradeRequest(Peer p, int targetId)
        {
            var t = ById(targetId);
            if (Accounts == null || p.AccountId <= 0 || t == null || t == p) { SendTrade(p, TradeResult.Guest); return; }
            if (t.AccountId <= 0) { SendTrade(p, TradeResult.Guest); return; }
            if (Accounts.BlockedEither(p.AccountId, t.AccountId)) { SendTrade(p, TradeResult.Busy); return; } // 차단한 사이에는 청이 가지 않는다
            if (p.Trade != null || t.Trade != null) { SendTrade(p, TradeResult.Busy); return; }
            if ((t.Avatar.Pos - p.Avatar.Pos).magnitude > TradeRange) { SendTrade(p, TradeResult.TooFar); return; }
            t.InviteFrom = p.Avatar.Id;
            var m = new NetWriter(NetMsg.TradeInvite);
            m.Int(p.Avatar.Id);
            m.Str(p.Avatar.Name);
            t.Pipe.Send(m.ToArray());
        }

        void OnTradeAnswer(Peer p, int fromId, bool accept)
        {
            if (p.InviteFrom != fromId) return;
            p.InviteFrom = -1;
            var f = ById(fromId);
            if (f == null || !accept || f.Trade != null || p.Trade != null) { if (f != null) SendTrade(f, TradeResult.Cancelled); return; }
            var tr = new Trade { A = f, B = p };
            f.Trade = tr;
            p.Trade = tr;
            SendTrade(f, TradeResult.None);
            SendTrade(p, TradeResult.None);
        }

        void OnTradeOffer(Peer p, AccountStore.TradeOffer o)
        {
            var tr = p.Trade;
            if (tr == null) return;
            if (!Accounts.Owns(p.AccountId, o)) o = new AccountStore.TradeOffer(); // 없는 것은 못 올린다
            if (p == tr.A) tr.OfferA = o; else tr.OfferB = o;
            tr.ConfirmA = tr.ConfirmB = false; // 바뀌면 확인을 다시
            SendTrade(tr.A, TradeResult.None);
            SendTrade(tr.B, TradeResult.None);
        }

        void OnTradeConfirm(Peer p)
        {
            var tr = p.Trade;
            if (tr == null) return;
            if (p == tr.A) tr.ConfirmA = true; else tr.ConfirmB = true;
            if (!(tr.ConfirmA && tr.ConfirmB)) { SendTrade(tr.A, TradeResult.None); SendTrade(tr.B, TradeResult.None); return; }
            bool ok = Accounts.Swap(tr.A.AccountId, tr.OfferA, tr.B.AccountId, tr.OfferB);
            var a = tr.A; var b = tr.B;
            a.Trade = b.Trade = null;
            SendTrade(a, ok ? TradeResult.Done : TradeResult.Failed);
            SendTrade(b, ok ? TradeResult.Done : TradeResult.Failed);
        }

        void EndTrade(Peer p, TradeResult why)
        {
            var tr = p.Trade;
            if (tr == null) return;
            var o = tr.Other(p);
            p.Trade = null;
            if (o != null) { o.Trade = null; SendTrade(o, why); }
            SendTrade(p, why);
        }

        static void WriteOffer(NetWriter m, AccountStore.TradeOffer o)
        {
            m.Long(o.Gold);
            m.Int(o.Items.Count);
            foreach (var kv in o.Items) { m.Str(kv.Key); m.Int(kv.Value); }
        }

        void SendTrade(Peer p, TradeResult result)
        {
            var tr = p.Trade;
            var m = new NetWriter(NetMsg.TradeState);
            m.Bool(tr != null);
            m.Str(tr != null ? tr.Other(p).Avatar.Name : null);
            WriteOffer(m, tr != null ? tr.Mine(p) : new AccountStore.TradeOffer());
            WriteOffer(m, tr != null ? tr.Mine(tr.Other(p)) : new AccountStore.TradeOffer());
            m.Bool(tr != null && (p == tr.A ? tr.ConfirmA : tr.ConfirmB));
            m.Bool(tr != null && (p == tr.A ? tr.ConfirmB : tr.ConfirmA));
            // 내 창고(거래할 수 있는 것): 골드 + 아이템
            var acct = Accounts != null && p.AccountId > 0 ? Accounts.Get(p.AccountId) : null;
            var inv = new AccountStore.TradeOffer();
            if (acct != null) { inv.Gold = acct.Profile.Gold; if (acct.Profile.Items != null) foreach (var kv in acct.Profile.Items) inv.Items[kv.Key] = kv.Value; }
            WriteOffer(m, inv);
            m.Byte((byte)result);
            p.Pipe.Send(m.ToArray());
        }

        void SendState()
        {
            var m = new NetWriter(NetMsg.PlazaState);
            m.Int(_tick);
            m.Int(_peers.Count);
            foreach (var p in _peers) PlazaCodec.Write(m, p.Avatar);
            var bytes = m.ToArray();
            foreach (var p in _peers)
            {
                // 차단한 사람의 말풍선은 그 사람에게만 가려서 보낸다(5.3절).
                var acct = Accounts != null && p.AccountId > 0 ? Accounts.Peek(p.AccountId) : null;
                var blocked = acct != null && acct.Profile.X != null ? acct.Profile.X.Blocked : null;
                if (blocked == null || blocked.Count == 0) { p.Pipe.Send(bytes); continue; }
                var mine = new NetWriter(NetMsg.PlazaState);
                mine.Int(_tick);
                mine.Int(_peers.Count);
                foreach (var q in _peers)
                {
                    if (q.Avatar.Bubble != null && q.AccountId > 0 && blocked.Contains(q.AccountId))
                    {
                        string keep = q.Avatar.Bubble;
                        q.Avatar.Bubble = null;
                        PlazaCodec.Write(mine, q.Avatar);
                        q.Avatar.Bubble = keep;
                    }
                    else PlazaCodec.Write(mine, q.Avatar);
                }
                p.Pipe.Send(mine.ToArray());
            }
        }

        public static Vector2 DirVector(int d)
        {
            // 0 위(y −), 시계 방향. 시뮬 좌표처럼 y는 아래가 +.
            switch (d)
            {
                case 0: return new Vector2(0, -1);
                case 1: return new Vector2(0.7071f, -0.7071f);
                case 2: return new Vector2(1, 0);
                case 3: return new Vector2(0.7071f, 0.7071f);
                case 4: return new Vector2(0, 1);
                case 5: return new Vector2(-0.7071f, 0.7071f);
                case 6: return new Vector2(-1, 0);
                case 7: return new Vector2(-0.7071f, -0.7071f);
                default: return Vector2.zero;
            }
        }

        /// <summary>줄바꿈·제어 문자를 빼고 길이를 자른다.</summary>
        public static string Clean(string s, int max)
        {
            if (s == null) return null;
            var sb = new System.Text.StringBuilder(Math.Min(s.Length, max));
            foreach (char ch in s)
            {
                if (char.IsControl(ch)) continue;
                if (sb.Length >= max) break;
                sb.Append(ch);
            }
            return sb.ToString().Trim();
        }
    }

    public static class PlazaCodec
    {
        public static void Write(NetWriter w, PlazaAvatar a)
        {
            w.Int(a.Id);
            w.Str(a.Name);
            w.Str(a.CharId);
            w.Vec(a.Pos);
            w.Byte((byte)(a.MoveDir + 1));
            w.Byte((byte)a.FaceDir);
            w.Str(a.Bubble);
            w.Byte(a.Emote);
        }

        public static PlazaAvatar Read(NetReader r)
        {
            return new PlazaAvatar
            {
                Id = r.Int(),
                Name = r.Str(),
                CharId = r.Str(),
                Pos = r.Vec(),
                MoveDir = r.Byte() - 1,
                FaceDir = r.Byte(),
                Bubble = r.Str(),
                Emote = r.Byte(),
            };
        }
    }
}
