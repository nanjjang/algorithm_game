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
        /// <summary>클라이언트 → 방 서버(계정 뒤): 서버 지팡이 강화. 몸 = 캐릭터(int 1~5). 응답 EnhanceReply.</summary>
        EnhanceRequest = 44,
        /// <summary>방 서버 → 클라이언트: 강화 결과. 몸 = AccountStore.EnhanceResult(byte), EnhanceOutcome(byte), 단계 전(int), 서버 프로필.</summary>
        EnhanceReply = 45,
        /// <summary>클라이언트 → 방 서버: 순위표(손님도 된다). 응답 LeaderboardReply.</summary>
        LeaderboardRequest = 46,
        /// <summary>방 서버 → 클라이언트: 순위표. 몸 = n, 서버 프로필 n개.</summary>
        LeaderboardReply = 47,
    }

    public enum AuthError : byte { BadName = 1, NameTaken = 2, BadSecret = 3, WrongLogin = 4 }

    /// <summary>방 종류.</summary>
    public enum RoomMode : byte { Coop = 0, Duel = 1 }

    public enum RoomError : byte { NotFound = 1, Full = 2, Started = 3, BadRequest = 4, ServerFull = 5 }

    public static class NetProtocol
    {
        /// <summary>메시지 꼴이 바뀌면 올린다. 다르면 Reject(Version).</summary>
        public const int Version = 6;
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
