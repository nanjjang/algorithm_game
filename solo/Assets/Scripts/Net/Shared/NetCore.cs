using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace Wizard.Solo.Net
{
    /// <summary>
    /// 통신 통로 하나(11-online.md 2절). 메시지 단위 바이트 배열을 순서대로, 잃지 않고 나른다.
    /// 2단계는 같은 프로세스 안의 <see cref="LoopbackPipe"/>, 3단계에서 Unity Transport 구현을 더한다.
    /// 시뮬은 이 층을 모른다.
    /// </summary>
    public interface INetPipe
    {
        bool Connected { get; }
        void Send(byte[] message);
        /// <summary>받은 메시지 하나를 꺼낸다. 없으면 false.</summary>
        bool TryReceive(out byte[] message);
        void Close();
    }

    /// <summary>같은 프로세스 안의 통로 한 쌍. A가 보낸 것을 B가 받는다.</summary>
    public sealed class LoopbackPipe : INetPipe
    {
        readonly Queue<byte[]> _inbox = new Queue<byte[]>();
        LoopbackPipe _peer;
        bool _closed;

        public static void CreatePair(out LoopbackPipe a, out LoopbackPipe b)
        {
            a = new LoopbackPipe();
            b = new LoopbackPipe();
            a._peer = b;
            b._peer = a;
        }

        public bool Connected => !_closed && _peer != null && !_peer._closed;

        public void Send(byte[] message)
        {
            if (!Connected || message == null) return;
            var copy = new byte[message.Length];
            Buffer.BlockCopy(message, 0, copy, 0, message.Length);
            _peer._inbox.Enqueue(copy);
        }

        public bool TryReceive(out byte[] message)
        {
            if (_inbox.Count > 0) { message = _inbox.Dequeue(); return true; }
            message = null;
            return false;
        }

        public void Close() { _closed = true; }
    }

    /// <summary>메시지 종류(첫 바이트).</summary>
    public enum NetMsg : byte
    {
        /// <summary>클라이언트 → 서버: 프로토콜 판, 내 출발 설정(캐릭터·지팡이·조립).</summary>
        Hello = 1,
        /// <summary>서버 → 클라이언트: 받아 줌, 내 슬롯.</summary>
        Welcome = 2,
        /// <summary>서버 → 클라이언트: 받지 않음(가득·판 다름·이미 시작). 이유 코드.</summary>
        Reject = 3,
        /// <summary>서버 → 클라이언트: 판 시작. 판 설정 전체(슬롯 0 + Party).</summary>
        Start = 4,
        /// <summary>클라이언트 → 서버: 입력 묶음.</summary>
        Input = 5,
        /// <summary>클라이언트 → 서버: 화면 명령(전리품·야영 …). 슬롯은 서버가 덮어쓴다.</summary>
        Command = 6,
        /// <summary>서버 → 클라이언트: 세계 상태.</summary>
        Snapshot = 7,
        /// <summary>서버 → 클라이언트: 표시 이벤트 묶음.</summary>
        Events = 8,
        /// <summary>서버 → 클라이언트: 동료가 들어옴·나감(로비 목록).</summary>
        Lobby = 9,
        /// <summary>클라이언트 → 서버: 판 시작 요청(방장 = 가장 낮은 슬롯만). 몸 = 씨앗(ulong).</summary>
        StartRequest = 10,
        /// <summary>클라이언트 → 서버: 동료 봇 더하기(방장만, 판 전). 몸 = 봇의 출발 설정.</summary>
        AddBot = 11,
        /// <summary>클라이언트 → 서버: 마지막 봇 빼기(방장만, 판 전).</summary>
        RemoveBot = 12,
        /// <summary>클라이언트 → 방 서버(첫 메시지): 새 방. 몸 = RoomMode(byte).</summary>
        CreateRoom = 20,
        /// <summary>클라이언트 → 방 서버(첫 메시지): 코드로 참가. 몸 = 코드(string).</summary>
        JoinRoom = 21,
        /// <summary>방 서버 → 클라이언트: 들어간 방. 몸 = 코드, RoomMode(byte), 최대 인원(int).</summary>
        RoomInfo = 22,
        /// <summary>방 서버 → 클라이언트: 방에 못 들어감. 몸 = RoomError(byte). 뒤이어 끊는다.</summary>
        RoomError = 23,
        /// <summary>클라이언트 → 방 서버(첫 메시지): 광장에 들어간다. 몸 = 이름, 캐릭터 id.</summary>
        EnterPlaza = 30,
        /// <summary>광장 서버 → 클라이언트: 광장 상태(사람들 자리·말풍선). 몸 = PlazaCodec.</summary>
        PlazaState = 31,
        /// <summary>클라이언트 → 광장 서버: 내 이동 방향(8방향, −1 = 멈춤).</summary>
        PlazaMove = 32,
        /// <summary>클라이언트 → 광장 서버: 말하기. 몸 = 글.</summary>
        PlazaChat = 33,
        /// <summary>광장 서버 → 클라이언트: 들어옴 확인. 몸 = 내 id.</summary>
        PlazaWelcome = 34,
        /// <summary>클라이언트 → 광장 서버: 감정 표현. 몸 = 번호(byte).</summary>
        PlazaEmote = 35,
        /// <summary>클라이언트 → 방 서버(맨 앞): 기기 계정 만들기. 몸 = 이름, 비밀. 응답 AuthOk/AuthFail, 이어서 다음 메시지로 길을 나눈다.</summary>
        Register = 40,
        /// <summary>방 서버 → 클라이언트: 계정 확인됨. 몸 = 계정 id, 서버 프로필.</summary>
        AuthOk = 41,
        /// <summary>방 서버 → 클라이언트: 계정 실패. 몸 = AuthError(byte). 뒤이어 끊는다.</summary>
        AuthFail = 42,
        /// <summary>클라이언트 → 방 서버(맨 앞): 기기 계정으로 들어가기. 몸 = 계정 id(long), 비밀.</summary>
        Login = 43,
        /// <summary>클라이언트 → 방 서버(계정 뒤): 서버 지팡이 강화. 몸 = 캐릭터(int 1~5), 부적 쓰기(bool), 제물 블록 id 목록. 응답 EnhanceReply.</summary>
        EnhanceRequest = 44,
        /// <summary>방 서버 → 클라이언트: 강화 결과. 몸 = AccountStore.EnhanceResult(byte), EnhanceOutcome(byte), 단계 전(int), 서버 프로필.</summary>
        EnhanceReply = 45,
        /// <summary>클라이언트 → 방 서버: 순위표(손님도 된다). 응답 LeaderboardReply.</summary>
        LeaderboardRequest = 46,
        /// <summary>방 서버 → 클라이언트: 순위표. 몸 = n, 서버 프로필 n개.</summary>
        LeaderboardReply = 47,
        /// <summary>클라이언트 → 방 서버(계정 뒤): 변이 제시에 답. 몸 = 캐릭터(int), 받기(bool). 응답 EnhanceReply.</summary>
        MutationResolve = 48,
        /// <summary>클라이언트 → 방 서버(계정 뒤): 서버 지팡이 조립 바꾸기. 몸 = 캐릭터(int), 코어(string), 연결 칸(string 목록). 응답 LoadoutReply.</summary>
        SetLoadout = 49,
        /// <summary>방 서버 → 클라이언트: 조립 결과. 몸 = AccountStore.LoadoutResult(byte), 서버 프로필.</summary>
        LoadoutReply = 50,
        /// <summary>클라이언트 → 방 서버(계정 뒤): 변이 지우기. 몸 = 캐릭터(int), 변이 id. 응답 EnhanceReply.</summary>
        RemoveMutation = 51,
        /// <summary>광장: 거래 청하기(→ 서버). 몸 = 상대 아바타 id.</summary>
        TradeRequest = 60,
        /// <summary>광장: 거래 청이 왔다(서버 →). 몸 = 청한 아바타 id, 이름.</summary>
        TradeInvite = 61,
        /// <summary>광장: 청에 답(→ 서버). 몸 = 청한 아바타 id, 받기(bool).</summary>
        TradeAnswer = 62,
        /// <summary>광장: 내 내놓을 것 바꾸기(→ 서버). 몸 = 골드(long), 아이템 수 n, (id, 개수)×n. 바꾸면 양쪽 확인이 풀린다.</summary>
        TradeOffer = 63,
        /// <summary>광장: 확인(→ 서버). 둘 다 확인하면 서버가 맞바꾼다.</summary>
        TradeConfirm = 64,
        /// <summary>광장: 거래 그만(→ 서버).</summary>
        TradeCancel = 65,
        /// <summary>광장: 거래 상태(서버 →). 몸 = 열림(bool), 상대 이름, 내 것, 상대 것, 내 확인, 상대 확인, 내 창고(거래할 수 있는 것), 결과 코드(byte).</summary>
        TradeState = 66,
        /// <summary>장터 둘러보기(→ 방 서버). 몸 = 아이템 id(null = 전부). 응답 MarketList.</summary>
        MarketBrowse = 70,
        /// <summary>장터 목록(서버 →). 몸 = n, (id, 판 사람 id, 이름, 아이템, 개수, 값)×n.</summary>
        MarketList = 71,
        /// <summary>내가 올린 것(→ 방 서버, 계정 뒤). 응답 MarketList.</summary>
        MarketMine = 72,
        /// <summary>올리기(→, 계정 뒤). 몸 = 아이템, 개수(int), 값(long). 응답 MarketReply.</summary>
        MarketSell = 73,
        /// <summary>장터 결과(서버 →). 몸 = AccountStore.MarketResult(byte), 서버 프로필.</summary>
        MarketReply = 74,
        /// <summary>사기(→, 계정 뒤). 몸 = 올린 것 id(long). 응답 MarketReply.</summary>
        MarketBuy = 75,
        /// <summary>내리기(→, 계정 뒤). 몸 = 올린 것 id(long). 응답 MarketReply.</summary>
        MarketUnlist = 76,
        /// <summary>친구 목록(→, 계정 뒤). 응답 FriendList.</summary>
        FriendListRequest = 80,
        /// <summary>친구 목록(서버 →). 몸 = 친구 n, (id, 이름, 레이팅, 어디)×n, 신청 m, (id, 이름, 레이팅)×m.</summary>
        FriendList = 81,
        /// <summary>친구 신청(→, 계정 뒤). 몸 = 이름. 응답 FriendReply.</summary>
        FriendRequest = 82,
        /// <summary>친구 결과(서버 →). 몸 = AccountStore.FriendResult(byte).</summary>
        FriendReply = 83,
        /// <summary>친구 신청에 답(→). 몸 = 신청한 계정 id(long), 받기(bool). 응답 FriendReply.</summary>
        FriendAnswer = 84,
        /// <summary>친구 끊기(→). 몸 = 계정 id(long). 응답 FriendReply.</summary>
        FriendRemove = 85,
        /// <summary>광장: 귓속말 보내기(→ 광장). 몸 = 받는 이름, 글.</summary>
        PlazaWhisper = 86,
        /// <summary>광장: 귓속말이 왔다(광장 →). 몸 = 보낸 이름, 글. 보낸 이름이 null이면 "받을 사람이 광장에 없다".</summary>
        WhisperIn = 87,
        /// <summary>클라이언트 → 방 서버(맨 앞, 로그인 전): 서버가 켠 기능을 묻는다. 응답 ServerInfo, 연결은 그대로.</summary>
        ServerInfoRequest = 90,
        /// <summary>방 서버 → 클라이언트: Google 클라이언트 id(빈 문자열 = Google 로그인 꺼짐), 복구 메일 켜짐(bool), 비밀번호 최소 길이(int), 암호화된 연결인가(bool).</summary>
        ServerInfo = 91,
        /// <summary>클라이언트 → 방 서버(맨 앞, 암호화된 연결에서만): 이메일 가입. 몸 = 이메일, 비밀번호, 이름. 응답 AuthOk(로그인 표 포함)/AuthFail.</summary>
        EmailSignup = 92,
        /// <summary>클라이언트 → 방 서버(맨 앞, 암호화된 연결에서만): 이메일 로그인. 몸 = 이메일, 비밀번호. 응답 AuthOk/AuthFail.</summary>
        EmailLogin = 93,
        /// <summary>클라이언트 → 방 서버(맨 앞, 암호화된 연결에서만): Google 인가 코드. 몸 = code, PKCE verifier, redirect_uri. 응답 AuthOk / GoogleNeedName / AuthFail.</summary>
        GoogleLogin = 94,
        /// <summary>방 서버 → 클라이언트: 처음 온 Google 사용자 — 이름을 정해 달라. 몸 = 표(ticket), 추천 이름. 같은 연결로 GoogleFinish를 보낸다.</summary>
        GoogleNeedName = 95,
        /// <summary>클라이언트 → 방 서버: Google 가입 마무리. 몸 = 표, 이름. 응답 AuthOk / AuthFail(이름 문제면 연결을 그대로 두고 다시 받는다).</summary>
        GoogleFinish = 96,
        /// <summary>클라이언트 → 방 서버(로그인 뒤): 로그아웃 — 이 로그인 표를 지운다. 몸 = 토큰. 응답 LogoutDone.</summary>
        Logout = 97,
        LogoutDone = 98,
        /// <summary>클라이언트 → 방 서버(맨 앞, 암호화된 연결에서만): 비밀번호 재설정 코드를 이메일로 보내 달라. 몸 = 이메일. 응답 RecoveryRequested / AuthFail(Throttled·MailOff). 없는 이메일도 같은 답이다.</summary>
        RecoveryRequest = 99,
        // ---- 13판: 우편함·차단·신고·시즌·순위표·리그·자동 매치·관전(11-online.md 6.14절). 전부 계정 뒤의 "요청 → 답" 연결에서 쓴다.
        /// <summary>우편 목록(→). 응답 MailList.</summary>
        MailListRequest = 100,
        /// <summary>우편 목록(서버 →). 몸 = n, (id, 까닭, 골드, 아이템 m × (id, 개수), 덧붙인 값, 온 시각, 기한)×n.</summary>
        MailList = 101,
        /// <summary>우편 받기(→). 몸 = 우편 id(long, 0 = 전부). 응답 MailReply.</summary>
        MailClaim = 102,
        /// <summary>우편 결과(서버 →). 몸 = AccountStore.MailResult(byte), 서버 프로필.</summary>
        MailReply = 103,
        /// <summary>차단 목록(→). 응답 BlockList.</summary>
        BlockListRequest = 104,
        /// <summary>차단 목록(서버 →). 몸 = n, (계정 id, 이름)×n.</summary>
        BlockList = 105,
        /// <summary>차단(→). 몸 = 이름. 응답 BlockReply.</summary>
        BlockAdd = 106,
        /// <summary>차단 풀기(→). 몸 = 계정 id(long). 응답 BlockReply.</summary>
        BlockRemove = 107,
        /// <summary>차단 결과(서버 →). 몸 = AccountStore.BlockResult(byte).</summary>
        BlockReply = 108,
        /// <summary>신고(→). 몸 = 이름, 사유(int 1~5). 서버가 그 사람의 최근 광장 채팅을 붙인다. 응답 ReportReply.</summary>
        ReportSend = 109,
        /// <summary>신고 결과(서버 →). 몸 = AccountStore.ReportResult(byte).</summary>
        ReportReply = 110,
        /// <summary>시즌·리그·내 점수(→). 응답 SeasonInfo.</summary>
        SeasonInfoRequest = 111,
        /// <summary>시즌·리그·내 점수(서버 →). 몸 = NetCodec.WriteSeasonInfo.</summary>
        SeasonInfo = 112,
        /// <summary>순위표(→, 손님도 된다). 몸 = BoardKind(byte), 인자(int: 캐릭터 또는 리그). 응답 BoardReply.</summary>
        BoardRequest = 113,
        /// <summary>순위표(서버 →). 몸 = BoardKind(byte), 인자(int), n, (순위, 계정 id, 이름, 점수, 웨이브, 캐릭터, 단계, 리그)×n.</summary>
        BoardReply = 114,
        /// <summary>스스로 위 리그로(→, 시즌에 한 번). 응답 LeagueReply.</summary>
        LeagueOptUp = 115,
        /// <summary>리그 결과(서버 →). 몸 = AccountStore.LeagueResult(byte), 리그(int).</summary>
        LeagueReply = 116,
        /// <summary>자동 매치 대기열에 들어간다(→). 응답 QueueState, 상대가 잡히면 MatchFound. 연결을 끊으면 대기열에서 빠진다.</summary>
        QueueJoin = 117,
        /// <summary>대기열 상태(서버 →). 몸 = AccountStore.QueueCheck(byte), 기다릴 초(int), 리그(int), 대기 중(bool).</summary>
        QueueState = 118,
        /// <summary>대기열에서 나간다(→). 응답 QueueState(대기 중 = false).</summary>
        QueueLeave = 119,
        /// <summary>상대가 잡혔다(서버 →). 몸 = 방 코드, 리그(int). 클라이언트는 새 연결로 JoinRoom(코드) 한다. 둘 다 들어오면 서버가 시작한다.</summary>
        MatchFound = 120,
        /// <summary>클라이언트 → 방 서버(첫 메시지): 그 방을 관전한다. 몸 = 코드. 응답 RoomInfo → Welcome(슬롯 −1), 이어서 Lobby 또는 Start·Snapshot.</summary>
        SpectateRoom = 121,
        /// <summary>역대 후계자(→, 손님도 된다). 응답 HallReply.</summary>
        HallRequest = 122,
        /// <summary>역대 후계자(서버 →). 몸 = n, (시즌 번호, 후계자 이름(없으면 null), 후계 점수(int))×n.</summary>
        HallReply = 123,
        /// <summary>방 서버 → 클라이언트: 코드를 보냈다(또는 보낸 척한다). 몸 없음.</summary>
        RecoveryRequested = 124,
        /// <summary>클라이언트 → 방 서버(맨 앞, 암호화된 연결에서만): 코드로 비밀번호 바꾸기. 몸 = 이메일, 코드, 새 비밀번호. 응답 RecoveryDone / AuthFail(BadCode·WeakPassword·Throttled).</summary>
        RecoveryConfirm = 125,
        /// <summary>방 서버 → 클라이언트: 비밀번호를 바꿨다(그 계정의 로그인 표는 모두 지웠다). 몸 없음.</summary>
        RecoveryDone = 126,
    }

    /// <summary>계정 실패 이유. AuthFail 몸 = 이유(byte), 기다릴 초(int).</summary>
    public enum AuthError : byte
    {
        BadName = 1, NameTaken = 2, BadSecret = 3, WrongLogin = 4,
        BadEmail = 5, EmailTaken = 6, WeakPassword = 7, Throttled = 8, NotSecure = 9, GoogleOff = 10, GoogleFailed = 11, BadTicket = 12, ServerError = 13,
        /// <summary>복구 코드가 틀렸거나 지났다(또는 시도 횟수를 넘었다).</summary>
        BadCode = 14,
        /// <summary>서버에서 복구 메일이 꺼져 있다.</summary>
        MailOff = 15,
    }

    /// <summary>계정 종류(AuthOk 몸 끝).</summary>
    public enum AccountKind : byte { Device = 0, Email = 1, Google = 2 }

    /// <summary>방 종류.</summary>
    public enum RoomMode : byte { Coop = 0, Duel = 1 }

    public enum RoomError : byte { NotFound = 1, Full = 2, Started = 3, BadRequest = 4, ServerFull = 5 }

    public static class NetProtocol
    {
        /// <summary>메시지 꼴이 바뀌면 올린다. 다르면 Reject(Version).</summary>
        public const int Version = 13;
        /// <summary>스냅숏을 보내는 틱 간격(60틱 / 3 = 20Hz).</summary>
        public const int SnapshotEveryTicks = 3;
    }

    public enum RejectReason : byte { Version = 1, Full = 2, AlreadyStarted = 3, BadHello = 4 }

    /// <summary>작은 바이너리 쓰개. 리틀 엔디안, 문자열은 UTF-8 길이 앞붙임(null = −1).</summary>
    public sealed class NetWriter
    {
        readonly MemoryStream _ms = new MemoryStream(256);
        readonly BinaryWriter _w;

        public NetWriter() { _w = new BinaryWriter(_ms, Encoding.UTF8); }
        public NetWriter(NetMsg kind) : this() { Byte((byte)kind); }

        public void Byte(byte v) => _w.Write(v);
        public void Bool(bool v) => _w.Write(v);
        public void Int(int v) => _w.Write(v);
        public void Long(long v) => _w.Write(v);
        public void ULong(ulong v) => _w.Write(v);
        public void Float(float v) => _w.Write(v);
        public void Vec(Vector2 v) { _w.Write(v.x); _w.Write(v.y); }
        public void Str(string s)
        {
            if (s == null) { _w.Write(-1); return; }
            var b = Encoding.UTF8.GetBytes(s);
            _w.Write(b.Length);
            _w.Write(b);
        }
        public void Bytes(byte[] b)
        {
            if (b == null) { _w.Write(-1); return; }
            _w.Write(b.Length);
            _w.Write(b);
        }
        public void StrList(ICollection<string> l)
        {
            if (l == null) { _w.Write(-1); return; }
            _w.Write(l.Count);
            foreach (var s in l) Str(s);
        }
        public void IntList(IList<int> l)
        {
            if (l == null) { _w.Write(-1); return; }
            _w.Write(l.Count);
            for (int i = 0; i < l.Count; i++) _w.Write(l[i]);
        }

        public byte[] ToArray() { _w.Flush(); return _ms.ToArray(); }
    }

    /// <summary><see cref="NetWriter"/>의 짝. 모자란 메시지는 EndOfStreamException.</summary>
    public sealed class NetReader
    {
        readonly BinaryReader _r;

        public NetReader(byte[] data) { _r = new BinaryReader(new MemoryStream(data ?? Array.Empty<byte>()), Encoding.UTF8); }

        public byte Byte() => _r.ReadByte();
        public bool Bool() => _r.ReadBoolean();
        public int Int() => _r.ReadInt32();
        public long Long() => _r.ReadInt64();
        public ulong ULong() => _r.ReadUInt64();
        public float Float() => _r.ReadSingle();
        public Vector2 Vec() { float x = _r.ReadSingle(); return new Vector2(x, _r.ReadSingle()); }
        public string Str()
        {
            int n = _r.ReadInt32();
            if (n < 0) return null;
            return Encoding.UTF8.GetString(_r.ReadBytes(n));
        }
        public byte[] Bytes()
        {
            int n = _r.ReadInt32();
            return n < 0 ? null : _r.ReadBytes(n);
        }
        public List<string> StrList()
        {
            int n = _r.ReadInt32();
            if (n < 0) return null;
            var l = new List<string>(n);
            for (int i = 0; i < n; i++) l.Add(Str());
            return l;
        }
        public List<int> IntList()
        {
            int n = _r.ReadInt32();
            if (n < 0) return null;
            var l = new List<int>(n);
            for (int i = 0; i < n; i++) l.Add(_r.ReadInt32());
            return l;
        }
        public bool AtEnd => _r.BaseStream.Position >= _r.BaseStream.Length;
    }
}
