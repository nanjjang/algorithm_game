using System;
using System.Collections.Generic;
using Wizard.Solo.Sim;

namespace Wizard.Solo.Net
{
    // 우편함·차단·신고·시즌·순위표·리그의 "요청 → 답"과 자동 매치 대기열(docs/online.md 4.6·4.10절, 11-online.md 6.14절).
    public sealed partial class RoomServer
    {
        sealed class QueueEntry
        {
            public INetPipe Pipe;
            public long AccountId;
            public int League;
            public double Rating;
            public int WaitTicks;
        }

        readonly List<QueueEntry> _queue = new List<QueueEntry>();
        int _queueTicks, _seasonTicks;

        /// <summary>자동 매치 대기 인원.</summary>
        public int QueueCount => _queue.Count;
        /// <summary>시즌이 정산됐을 때(전용 서버가 기록한다).</summary>
        public Action<SeasonArchive> SeasonSettled;

        long Now => Accounts.Clock();

        void SendQueueState(INetPipe pipe, AccountStore.QueueCheck check, int wait, int league, bool waiting)
        {
            var m = new NetWriter(NetMsg.QueueState);
            m.Byte((byte)check); m.Int(wait); m.Int(league); m.Bool(waiting);
            pipe.Send(m.ToArray());
        }

        bool InRoom(long accountId)
        {
            foreach (var h in _rooms.Values) if (h.HasAccount(accountId) || (h.Reserved != null && h.Reserved.Contains(accountId))) return true;
            return false;
        }

        /// <summary>대기열 한 바퀴(0.5초마다): 끊긴 사람을 빼고, 같은 리그에서 점수 차가 두 사람의 범위 안인 가장 가까운 짝을 잡는다.</summary>
        void QueueTick()
        {
            if (++_seasonTicks >= 60 * 30)
            {
                _seasonTicks = 0;
                var arch = Accounts.TickSeason(Now);
                if (arch != null)
                {
                    Log?.Invoke("[room-server] season " + arch.SeasonId + " settled, heir " + (arch.HeirName ?? "-"));
                    SeasonSettled?.Invoke(arch);
                }
            }
            if (_queue.Count == 0 || ++_queueTicks < 30) return;
            int step = _queueTicks;
            _queueTicks = 0;
            var rules = Accounts.Rules;
            for (int i = _queue.Count - 1; i >= 0; i--)
            {
                if (!_queue[i].Pipe.Connected) { _queue.RemoveAt(i); continue; }
                _queue[i].WaitTicks += step;
                // 기다리는 동안 리그가 오르면(지팡이를 샀다 등) 대기열에서 빠진다(4.7절).
                if (Accounts.LeagueOf(_queue[i].AccountId) != _queue[i].League)
                {
                    SendQueueState(_queue[i].Pipe, AccountStore.QueueCheck.Ok, 0, Accounts.LeagueOf(_queue[i].AccountId), false);
                    _queue.RemoveAt(i);
                }
            }
            double Range(QueueEntry q) => rules.MatchRangeStart + rules.MatchRangeStep * (q.WaitTicks / 60 / Math.Max(1, rules.MatchRangeStepSeconds));
            for (int i = 0; i < _queue.Count; i++)
            {
                var a = _queue[i];
                int best = -1;
                double bestDiff = double.MaxValue;
                var xa = Accounts.Peek(a.AccountId);
                for (int j = i + 1; j < _queue.Count; j++)
                {
                    var b = _queue[j];
                    if (b.League != a.League || b.AccountId == a.AccountId) continue;
                    double diff = Math.Abs(a.Rating - b.Rating);
                    if (diff > Range(a) || diff > Range(b)) continue;
                    // 직전 상대와 연속으로 잡지 않는다(4.10절).
                    var xb = Accounts.Peek(b.AccountId);
                    if (xa != null && xa.Profile.X != null && xa.Profile.X.LastRankedOpponent == b.AccountId) continue;
                    if (xb != null && xb.Profile.X != null && xb.Profile.X.LastRankedOpponent == a.AccountId) continue;
                    if (Accounts.BlockedEither(a.AccountId, b.AccountId)) continue;
                    if (diff < bestDiff) { bestDiff = diff; best = j; }
                }
                if (best < 0) continue;
                if (_rooms.Count >= MaxRooms) return;
                var o = _queue[best];
                _queue.RemoveAt(best);
                _queue.RemoveAt(i);
                i--;
                StartMatch(a, o);
            }
        }

        void StartMatch(QueueEntry a, QueueEntry b)
        {
            int league = a.League;
            var rules = Accounts.Rules;
            ulong seed = ((ulong)(uint)_rng.Next() << 32) | (uint)_rng.Next();
            var h = new CoopHost(_content, _modules, 2, RoomMode.Duel)
            {
                Code = NewCode(), StageLookup = Accounts.StageOf, MutationLookup = Accounts.MutationIds,
                LoadoutLookup = (aid, ch) => Accounts.LoadoutSpecOf(aid, ch, _content),
                Ranked = true, League = league, Reserved = new HashSet<long> { a.AccountId, b.AccountId },
                MemberFilter = m => LeagueRules.ApplyLimits(rules, _content, m, league),
                AutoStartSeed = seed == 0 ? 1UL : seed, SpectatorsMax = rules.SpectatorsMax,
            };
            _rooms[h.Code] = h;
            Log?.Invoke("[room-server] match " + h.Code + " league " + league + ": " + a.AccountId + " vs " + b.AccountId);
            foreach (var q in new[] { a, b })
            {
                var m = new NetWriter(NetMsg.MatchFound);
                m.Str(h.Code); m.Int(league);
                q.Pipe.Send(m.ToArray());
            }
        }

        SeasonInfo BuildSeasonInfo(AccountRecord acct)
        {
            long now = Now;
            var s = Accounts.EnsureSeason(now);
            var info = new SeasonInfo { SeasonId = s.Id, Phase = (byte)Accounts.PhaseAt(now), EndTicks = s.EndTicks, SettleUntilTicks = s.SettleUntilTicks, NowTicks = now };
            if (acct == null) { info.Queue = (byte)AccountStore.QueueCheck.NoAccount; return info; }
            Accounts.Touch(acct.Id);
            Accounts.Save(); // 보유 IL·리그를 지금 값으로
            var x = AccountStore.X(acct.Profile);
            info.League = x.League; info.OptedUp = x.LeagueOptedUp; info.HoldingIl = x.HoldingIl;
            for (int c = 1; c <= 5; c++) info.EquippedIl[c - 1] = Accounts.EquippedIl(acct.Id, c);
            for (int l = 0; l < OnlineRules.LeagueCount; l++)
            {
                var r = Accounts.RatingOf(acct.Id, l);
                info.Rating[l] = (int)Math.Round(r.Rating); info.RatedGames[l] = r.Games; info.RatedWins[l] = r.Wins; info.RatedLosses[l] = r.Losses;
            }
            var row = Accounts.HeirRowOf(acct.Id, now, out int pop);
            if (row != null)
            {
                info.E = (int)Math.Round(row.E); info.P = (int)Math.Round(row.P); info.Total = (int)Math.Round(row.Total);
                info.Rank = row.Rank; info.Population = pop;
            }
            bool current = x.SeasonId == s.Id;
            info.SeasonScore = current ? (long)Math.Round(Accounts.SeasonScore(x)) : 0;
            info.SeasonRuns = current ? x.SeasonRuns : 0;
            info.SeasonRanked = current ? x.SeasonRanked[x.League] : 0;
            info.MailCount = Accounts.MailOf(acct.Id, now).Count;
            info.PlaySeconds = x.PlaySeconds;
            info.Queue = (byte)Accounts.CanQueue(acct.Id, now, out int wait);
            info.QueueWaitSeconds = wait;
            info.ChatMutedSeconds = Accounts.ChatMutedSeconds(acct.Id, now);
            info.Titles = new List<string>(x.Titles);
            return info;
        }

        /// <summary>13판의 요청이면 답하고 true(연결은 그대로).</summary>
        bool TryOnlineService(INetPipe pipe, byte[] data)
        {
            if (data == null || data.Length == 0) return false;
            var kind = (NetMsg)data[0];
            if (kind < NetMsg.MailListRequest || kind > NetMsg.HallReply || kind == NetMsg.SpectateRoom) return false;
            _authed.TryGetValue(pipe, out var acct);
            NetReader r;
            try { r = new NetReader(data); r.Byte(); }
            catch (Exception) { return true; }
            long now = Now;
            switch (kind)
            {
                case NetMsg.MailListRequest:
                    {
                        var list = acct != null ? Accounts.MailOf(acct.Id, now) : new List<MailItem>();
                        var m = new NetWriter(NetMsg.MailList);
                        m.Int(list.Count);
                        foreach (var x in list) NetCodec.WriteMail(m, x);
                        pipe.Send(m.ToArray());
                        return true;
                    }
                case NetMsg.MailClaim:
                    {
                        long id = 0;
                        try { id = r.Long(); } catch (Exception) { }
                        var res = acct == null ? AccountStore.MailResult.NoAccount : Accounts.ClaimMail(acct.Id, id, now);
                        var m = new NetWriter(NetMsg.MailReply);
                        m.Byte((byte)res);
                        NetCodec.WriteProfile(m, acct != null ? acct.Profile : new ServerProfile());
                        pipe.Send(m.ToArray());
                        return true;
                    }
                case NetMsg.BlockListRequest:
                    {
                        var list = acct != null ? Accounts.BlockedOf(acct.Id) : new List<FriendInfo>();
                        var m = new NetWriter(NetMsg.BlockList);
                        m.Int(list.Count);
                        foreach (var b in list) { m.Long(b.Id); m.Str(b.Name); }
                        pipe.Send(m.ToArray());
                        return true;
                    }
                case NetMsg.BlockAdd:
                case NetMsg.BlockRemove:
                    {
                        var res = AccountStore.BlockResult.NoAccount;
                        try
                        {
                            if (acct != null) res = kind == NetMsg.BlockAdd ? Accounts.Block(acct.Id, r.Str()) : Accounts.Unblock(acct.Id, r.Long());
                        }
                        catch (Exception) { res = AccountStore.BlockResult.NotFound; }
                        var m = new NetWriter(NetMsg.BlockReply);
                        m.Byte((byte)res);
                        pipe.Send(m.ToArray());
                        return true;
                    }
                case NetMsg.ReportSend:
                    {
                        var res = AccountStore.ReportResult.NoAccount;
                        try
                        {
                            if (acct != null)
                            {
                                string name = r.Str();
                                int reason = r.Int();
                                var target = Accounts.FindByName(name);
                                res = Accounts.Report(acct.Id, name, reason, target != null ? Plaza.RecentChat(target.Id) : null, now);
                                if (res == AccountStore.ReportResult.Ok) Log?.Invoke("[room-server] report " + acct.Id + " → " + target.Id + " reason " + reason);
                            }
                        }
                        catch (Exception) { res = AccountStore.ReportResult.NotFound; }
                        var m = new NetWriter(NetMsg.ReportReply);
                        m.Byte((byte)res);
                        pipe.Send(m.ToArray());
                        return true;
                    }
                case NetMsg.SeasonInfoRequest:
                    {
                        var m = new NetWriter(NetMsg.SeasonInfo);
                        NetCodec.WriteSeasonInfo(m, BuildSeasonInfo(acct));
                        pipe.Send(m.ToArray());
                        return true;
                    }
                case NetMsg.BoardRequest:
                    {
                        byte bk = 0;
                        int arg = 0;
                        try { bk = r.Byte(); arg = r.Int(); } catch (Exception) { }
                        var rows = Accounts.Board((BoardKind)bk, arg, 50, acct != null ? acct.Id : 0, now);
                        var m = new NetWriter(NetMsg.BoardReply);
                        m.Byte(bk); m.Int(arg); m.Int(rows.Count);
                        foreach (var b in rows) NetCodec.WriteBoardRow(m, b);
                        pipe.Send(m.ToArray());
                        return true;
                    }
                case NetMsg.LeagueOptUp:
                    {
                        var res = acct == null ? AccountStore.LeagueResult.NoAccount : Accounts.OptUpLeague(acct.Id);
                        var m = new NetWriter(NetMsg.LeagueReply);
                        m.Byte((byte)res);
                        m.Int(acct != null ? Accounts.LeagueOf(acct.Id) : 0);
                        pipe.Send(m.ToArray());
                        return true;
                    }
                case NetMsg.QueueJoin:
                    {
                        if (acct == null) { SendQueueState(pipe, AccountStore.QueueCheck.NoAccount, 0, 0, false); return true; }
                        Accounts.Touch(acct.Id);
                        Accounts.Save();
                        var check = Accounts.CanQueue(acct.Id, now, out int wait);
                        int league = Accounts.LeagueOf(acct.Id);
                        if (check == AccountStore.QueueCheck.Ok && InRoom(acct.Id)) check = AccountStore.QueueCheck.Busy;
                        if (check != AccountStore.QueueCheck.Ok) { SendQueueState(pipe, check, wait, league, false); return true; }
                        _queue.RemoveAll(q => q.AccountId == acct.Id); // 다른 연결로 들어와 있었다면 새 연결이 대신한다
                        _queue.Add(new QueueEntry { Pipe = pipe, AccountId = acct.Id, League = league, Rating = Accounts.RatingOf(acct.Id, league).Rating });
                        SendQueueState(pipe, AccountStore.QueueCheck.Ok, 0, league, true);
                        return true;
                    }
                case NetMsg.QueueLeave:
                    {
                        _queue.RemoveAll(q => q.Pipe == pipe);
                        SendQueueState(pipe, AccountStore.QueueCheck.Ok, 0, acct != null ? Accounts.LeagueOf(acct.Id) : 0, false);
                        return true;
                    }
                case NetMsg.HallRequest:
                    {
                        var hall = Accounts.Hall;
                        var m = new NetWriter(NetMsg.HallReply);
                        m.Int(hall.Count);
                        foreach (var a in hall) { m.Int(a.SeasonId); m.Str(a.HeirName); m.Int((int)Math.Round(a.HeirTotal)); }
                        pipe.Send(m.ToArray());
                        return true;
                    }
            }
            return true; // 서버 → 클라이언트 전용 번호가 거꾸로 온 것: 버린다
        }
    }
}
