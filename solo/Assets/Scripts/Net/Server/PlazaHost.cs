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
        }

        readonly List<Peer> _peers = new List<Peer>();
        int _tick;
        int _nextId = 1;
        readonly System.Random _rng = new System.Random();

        public int Count => _peers.Count;
        public IEnumerable<PlazaAvatar> Avatars { get { foreach (var p in _peers) yield return p.Avatar; } }

        /// <summary>EnterPlaza 메시지(이름, 캐릭터 id)를 읽고 들인다. 가득이면 RoomError(Full).</summary>
        public void Accept(INetPipe pipe, NetReader enter, string accountName = null)
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
            _peers.Add(new Peer { Pipe = pipe, Avatar = a });
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
                if (!p.Pipe.Connected) { _peers.RemoveAt(i); continue; }
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
                        p.LastChatTick = _tick;
                        p.Avatar.Bubble = text;
                        p.Avatar.BubbleUntilTick = _tick + BubbleTicks;
                    }
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

        void SendState()
        {
            var m = new NetWriter(NetMsg.PlazaState);
            m.Int(_tick);
            m.Int(_peers.Count);
            foreach (var p in _peers) PlazaCodec.Write(m, p.Avatar);
            var bytes = m.ToArray();
            foreach (var p in _peers) p.Pipe.Send(bytes);
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
