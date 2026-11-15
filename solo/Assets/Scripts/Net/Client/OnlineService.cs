using System;
using System.Collections.Generic;
using System.Threading;
using Wizard.Solo.Data;
using Wizard.Solo.Meta;

namespace Wizard.Solo.Net
{
    /// <summary>
    /// 방 서버에 "요청 → 답" 한 번씩 하는 연결(내 기록 화면용): 계정으로 들어가 서버 프로필을 받고, 서버 강화·순위표를 묻는다.
    /// 답을 기다리며 잠깐 막힌다(timeoutMs). 화면은 버튼을 누를 때만 부른다.
    /// </summary>
    public sealed class OnlineService : IDisposable
    {
        TcpPipe _pipe;
        public ServerProfile Profile { get; private set; }
        public bool IsGuest { get; private set; }
        public bool Connected => _pipe != null && _pipe.Connected;

        public struct EnhanceReply
        {
            public AccountStore.EnhanceResult Result;
            public EnhanceOutcome Outcome;
            public int StageBefore;
            public ServerProfile Profile;
        }

        /// <summary>붙고 계정이 있으면 프로필을 받는다. 서버에 못 붙거나 계정이 틀리면 null.</summary>
        public static OnlineService Open(IContentDb db, int timeoutMs = 4000)
        {
            var pipe = OnlineServer.Connect(db);
            if (pipe == null) return null;
            var s = new OnlineService { _pipe = pipe };
            var acct = OnlineAccount.Load();
            if (acct == null) { s.IsGuest = true; return s; }
            acct.SendLogin(pipe);
            var data = s.Wait(timeoutMs, NetMsg.AuthOk, NetMsg.AuthFail);
            if (data == null || (NetMsg)data[0] != NetMsg.AuthOk) { s.Dispose(); return null; }
            var r = new NetReader(data);
            r.Byte();
            r.Long();
            s.Profile = NetCodec.ReadProfile(r);
            return s;
        }

        byte[] Wait(int timeoutMs, params NetMsg[] kinds)
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (DateTime.UtcNow < deadline && _pipe.Connected)
            {
                if (!_pipe.TryReceive(out var data)) { Thread.Sleep(3); continue; }
                if (data.Length == 0) continue;
                foreach (var k in kinds) if ((NetMsg)data[0] == k) return data;
            }
            return null;
        }

        public EnhanceReply? Enhance(int character, bool useCharm = false, IList<string> sacrifices = null, int timeoutMs = 4000)
        {
            if (!Connected) return null;
            var m = new NetWriter(NetMsg.EnhanceRequest);
            m.Int(character);
            m.Bool(useCharm);
            m.StrList(sacrifices ?? new List<string>());
            _pipe.Send(m.ToArray());
            return ReadReply(timeoutMs);
        }

        /// <summary>대기 중인 변이 제시에 답한다(받기 / 거절).</summary>
        public EnhanceReply? ResolveMutation(int character, bool accept, int timeoutMs = 4000)
        {
            if (!Connected) return null;
            var m = new NetWriter(NetMsg.MutationResolve);
            m.Int(character);
            m.Bool(accept);
            _pipe.Send(m.ToArray());
            return ReadReply(timeoutMs);
        }

        EnhanceReply? ReadReply(int timeoutMs)
        {
            var data = Wait(timeoutMs, NetMsg.EnhanceReply);
            if (data == null) return null;
            var r = new NetReader(data);
            r.Byte();
            var rep = new EnhanceReply
            {
                Result = (AccountStore.EnhanceResult)r.Byte(),
                Outcome = (EnhanceOutcome)r.Byte(),
                StageBefore = r.Int(),
                Profile = NetCodec.ReadProfile(r),
            };
            if (rep.Result != AccountStore.EnhanceResult.NoAccount && rep.Result != AccountStore.EnhanceResult.BadCharacter) Profile = rep.Profile;
            return rep;
        }

        public List<ServerProfile> Leaderboard(int timeoutMs = 4000)
        {
            if (!Connected) return null;
            _pipe.Send(new NetWriter(NetMsg.LeaderboardRequest).ToArray());
            var data = Wait(timeoutMs, NetMsg.LeaderboardReply);
            if (data == null) return null;
            var r = new NetReader(data);
            r.Byte();
            int n = r.Int();
            var l = new List<ServerProfile>(n);
            for (int i = 0; i < n; i++) l.Add(NetCodec.ReadProfile(r));
            return l;
        }

        public void Dispose()
        {
            _pipe?.Close();
            _pipe = null;
        }
    }
}
