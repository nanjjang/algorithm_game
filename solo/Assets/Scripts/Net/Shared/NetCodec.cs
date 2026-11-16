using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using Wizard.Solo.Sim;

namespace Wizard.Solo.Net
{
    /// <summary>판 설정·입력·명령·이벤트를 메시지 몸으로 쓰고 읽는다(11-online.md 3절).</summary>
    public static class NetCodec
    {
        // ------------------------------------------------------------ 판 설정

        public static void WriteConfig(NetWriter w, RunConfig c, bool withParty = true)
        {
            w.Byte((byte)c.Mode);
            w.ULong(c.Seed);
            w.Str(c.RunId);
            w.Str(c.CharId);
            w.Long(c.WandId);
            w.Int(c.WandStage);
            w.Int(c.LinkSlotCount);
            w.StrList(c.Mutations);
            w.Bool(c.Loadout != null);
            if (c.Loadout != null)
            {
                w.Str(c.Loadout.Core);
                w.StrList(c.Loadout.Links);
                w.IntList(c.Loadout.Stars);
            }
            w.StrList(c.UnlockedArts);
            w.Int(c.StartWave);
            w.Bool(c.PhantomFirst);
            w.Bool(c.RevengePending);
            w.Bool(c.CrowBundlePending);
            w.StrList(c.KnownAffinities);
            w.Bool(c.PracticeInfiniteCrystal);
            w.Bool(c.PracticeEnemyFire);
            w.Bool(c.PracticeHeatFreeze);
            w.Int(c.TutorialStep);
            w.Str(c.AttackStyle);
            int n = withParty && c.Party != null ? c.Party.Count : 0;
            w.Int(n);
            for (int i = 0; i < n; i++) WriteConfig(w, c.Party[i], false);
        }

        public static RunConfig ReadConfig(NetReader r)
        {
            var c = new RunConfig();
            c.Mode = (RunMode)r.Byte();
            c.Seed = r.ULong();
            c.RunId = r.Str();
            c.CharId = r.Str();
            c.WandId = r.Long();
            c.WandStage = r.Int();
            c.LinkSlotCount = r.Int();
            c.Mutations = r.StrList() ?? new List<string>();
            if (r.Bool())
            {
                c.Loadout = new LoadoutSpec { Core = r.Str(), Links = r.StrList() ?? new List<string>() };
                c.Loadout.Stars = r.IntList();
            }
            c.UnlockedArts = r.StrList() ?? new List<string>();
            c.StartWave = r.Int();
            c.PhantomFirst = r.Bool();
            c.RevengePending = r.Bool();
            c.CrowBundlePending = r.Bool();
            c.KnownAffinities = r.StrList() ?? new List<string>();
            c.PracticeInfiniteCrystal = r.Bool();
            c.PracticeEnemyFire = r.Bool();
            c.PracticeHeatFreeze = r.Bool();
            c.TutorialStep = r.Int();
            c.AttackStyle = r.Str();
            int n = r.Int();
            if (n > 0)
            {
                c.Party = new List<RunConfig>(n);
                for (int i = 0; i < n; i++) c.Party.Add(ReadConfig(r));
            }
            return c;
        }

        /// <summary>판 설정의 깊은 사본(서버와 클라이언트가 같은 설정 객체를 나눠 쓰지 않게).</summary>
        public static RunConfig Clone(RunConfig c)
        {
            var w = new NetWriter();
            WriteConfig(w, c);
            return ReadConfig(new NetReader(w.ToArray()));
        }

        // ------------------------------------------------------------ 입력·명령

        public static void WriteInput(NetWriter w, in InputEvent e)
        {
            w.Byte((byte)e.Key);
            w.Bool(e.Down);
            w.Long(e.TimeU);
        }

        public static InputEvent ReadInput(NetReader r)
        {
            var key = (LogicalKey)r.Byte();
            bool down = r.Bool();
            return new InputEvent(key, down, r.Long());
        }

        public static void WriteCommand(NetWriter w, in RunCommand c)
        {
            w.Byte((byte)c.Kind);
            w.Int(c.Slot);
            w.Int(c.A);
            w.Int(c.B);
            w.Str(c.Id);
        }

        public static RunCommand ReadCommand(NetReader r)
        {
            var kind = (RunCommandKind)r.Byte();
            int slot = r.Int();
            int a = r.Int();
            int b = r.Int();
            return new RunCommand(kind, a, b, r.Str(), slot);
        }

        // ------------------------------------------------------------ 서버 프로필

        public static void WriteProfile(NetWriter w, ServerProfile p)
        {
            w.Long(p.AccountId);
            w.Str(p.Name);
            w.Long(p.Gold);
            w.Int(p.Rating);
            w.Int(p.DuelWins);
            w.Int(p.DuelLosses);
            w.Int(p.DuelDraws);
            w.Int(p.CoopRuns);
            w.Int(p.CoopBestWave);
            for (int i = 0; i < 5; i++) w.Int(p.WandStages != null && i < p.WandStages.Length ? p.WandStages[i] : 0);
            for (int i = 0; i < 5; i++) w.Int(p.Auras != null && i < p.Auras.Length ? p.Auras[i] : 0);
            for (int c = 1; c <= 5; c++)
            {
                var muts = p.MutationsOf(c);
                w.Int(muts.Count);
                foreach (var mu in muts) w.Str(mu.Id);
                var off = p.OfferOf(c);
                w.Str(off != null ? off.Candidate : null);
                w.Str(off != null ? off.WouldPushOut : null);
                var lo = p.LoadoutOf(c);
                w.Bool(lo != null);
                if (lo != null) { w.Str(lo.Core); w.StrList(lo.Links); }
                w.Int(p.SlotsExtra != null && c - 1 < p.SlotsExtra.Length ? p.SlotsExtra[c - 1] : 0);
            }
            int n = p.Items != null ? p.Items.Count : 0;
            w.Int(n);
            if (p.Items != null)
            {
                var keys = new List<string>(p.Items.Keys);
                keys.Sort(System.StringComparer.Ordinal);
                foreach (var k in keys) { w.Str(k); w.Int(p.Items[k]); }
            }
        }

        // ------------------------------------------------------------ 우편 · 순위표 · 시즌 정보(13판)

        public static void WriteMail(NetWriter w, MailItem m)
        {
            w.Long(m.Id); w.Str(m.Reason); w.Long(m.Gold);
            w.Int(m.Items != null ? m.Items.Count : 0);
            if (m.Items != null) foreach (var kv in m.Items) { w.Str(kv.Key); w.Int(kv.Value); }
            w.Str(m.Note); w.Long(m.CreatedTicks); w.Long(m.ExpiresTicks);
        }

        public static MailItem ReadMail(NetReader r)
        {
            var m = new MailItem { Id = r.Long(), Reason = r.Str(), Gold = r.Long() };
            int n = r.Int();
            for (int i = 0; i < n; i++) { string k = r.Str(); m.Items[k] = r.Int(); }
            m.Note = r.Str(); m.CreatedTicks = r.Long(); m.ExpiresTicks = r.Long();
            return m;
        }

        public static void WriteBoardRow(NetWriter w, BoardRow b)
        {
            w.Int(b.Rank); w.Long(b.AccountId); w.Str(b.Name); w.Long(b.Score); w.Int(b.Wave); w.Int(b.Character); w.Int(b.Stage); w.Int(b.League);
        }

        public static BoardRow ReadBoardRow(NetReader r) =>
            new BoardRow { Rank = r.Int(), AccountId = r.Long(), Name = r.Str(), Score = r.Long(), Wave = r.Int(), Character = r.Int(), Stage = r.Int(), League = r.Int() };

        public static void WriteSeasonInfo(NetWriter w, SeasonInfo s)
        {
            w.Int(s.SeasonId); w.Byte(s.Phase); w.Long(s.EndTicks); w.Long(s.SettleUntilTicks); w.Long(s.NowTicks);
            w.Int(s.League); w.Bool(s.OptedUp); w.Int(s.HoldingIl);
            for (int i = 0; i < 5; i++) w.Int(s.EquippedIl[i]);
            for (int i = 0; i < OnlineRules.LeagueCount; i++) { w.Int(s.Rating[i]); w.Int(s.RatedGames[i]); w.Int(s.RatedWins[i]); w.Int(s.RatedLosses[i]); }
            w.Long(s.SeasonScore); w.Int(s.SeasonRuns); w.Int(s.SeasonRanked);
            w.Int(s.E); w.Int(s.P); w.Int(s.Total); w.Int(s.Rank); w.Int(s.Population);
            w.Int(s.MailCount); w.Long(s.PlaySeconds); w.Byte(s.Queue); w.Int(s.QueueWaitSeconds); w.Int(s.ChatMutedSeconds);
            w.StrList(s.Titles);
        }

        public static SeasonInfo ReadSeasonInfo(NetReader r)
        {
            var s = new SeasonInfo { SeasonId = r.Int(), Phase = r.Byte(), EndTicks = r.Long(), SettleUntilTicks = r.Long(), NowTicks = r.Long(), League = r.Int(), OptedUp = r.Bool(), HoldingIl = r.Int() };
            for (int i = 0; i < 5; i++) s.EquippedIl[i] = r.Int();
            for (int i = 0; i < OnlineRules.LeagueCount; i++) { s.Rating[i] = r.Int(); s.RatedGames[i] = r.Int(); s.RatedWins[i] = r.Int(); s.RatedLosses[i] = r.Int(); }
            s.SeasonScore = r.Long(); s.SeasonRuns = r.Int(); s.SeasonRanked = r.Int();
            s.E = r.Int(); s.P = r.Int(); s.Total = r.Int(); s.Rank = r.Int(); s.Population = r.Int();
            s.MailCount = r.Int(); s.PlaySeconds = r.Long(); s.Queue = r.Byte(); s.QueueWaitSeconds = r.Int(); s.ChatMutedSeconds = r.Int();
            s.Titles = r.StrList() ?? new List<string>();
            return s;
        }

        public static ServerProfile ReadProfile(NetReader r)
        {
            var p = new ServerProfile
            {
                AccountId = r.Long(), Name = r.Str(), Gold = r.Long(), Rating = r.Int(),
                DuelWins = r.Int(), DuelLosses = r.Int(), DuelDraws = r.Int(), CoopRuns = r.Int(), CoopBestWave = r.Int(),
                WandStages = new[] { r.Int(), r.Int(), r.Int(), r.Int(), r.Int() },
                Auras = new[] { r.Int(), r.Int(), r.Int(), r.Int(), r.Int() },
            };
            for (int c = 1; c <= 5; c++)
            {
                int mc = r.Int();
                var muts = p.MutationsOf(c);
                for (int k = 0; k < mc; k++) muts.Add(new Wizard.Solo.Meta.MutationEntry { Id = r.Str(), Seq = k });
                string cand = r.Str(), push = r.Str();
                if (cand != null) p.SetOffer(c, new ServerMutationOffer { Candidate = cand, WouldPushOut = push });
                p.LoadoutOf(c);
                if (r.Bool()) p.Loadouts[c - 1] = new ServerLoadout { Core = r.Str(), Links = r.StrList() ?? new List<string>() };
                p.SlotsExtra[c - 1] = r.Int();
            }
            int n = r.Int();
            for (int i = 0; i < n; i++) { string k = r.Str(); p.Items[k] = r.Int(); }
            return p;
        }

        // ------------------------------------------------------------ 압축(스냅숏)

        public static byte[] Deflate(byte[] raw)
        {
            using (var ms = new MemoryStream())
            {
                using (var z = new DeflateStream(ms, CompressionLevel.Fastest, true)) z.Write(raw, 0, raw.Length);
                return ms.ToArray();
            }
        }

        public static byte[] Inflate(byte[] packed)
        {
            using (var src = new MemoryStream(packed))
            using (var z = new DeflateStream(src, CompressionMode.Decompress))
            using (var dst = new MemoryStream())
            {
                z.CopyTo(dst);
                return dst.ToArray();
            }
        }

        // ------------------------------------------------------------ 이벤트

        public static void WriteEvent(NetWriter w, in SimEvent e)
        {
            w.Int((int)e.Type);
            w.Int(e.Tick);
            w.Int(e.Slot);
            w.Int(e.EntityId);
            w.Int(e.A);
            w.Int(e.B);
            w.Float(e.F);
            w.Vec(e.Pos);
            w.Vec(e.Pos2);
            w.Str(e.Id);
        }

        public static SimEvent ReadEvent(NetReader r)
        {
            return new SimEvent
            {
                Type = (SimEventType)r.Int(),
                Tick = r.Int(),
                Slot = r.Int(),
                EntityId = r.Int(),
                A = r.Int(),
                B = r.Int(),
                F = r.Float(),
                Pos = r.Vec(),
                Pos2 = r.Vec(),
                Id = r.Str(),
            };
        }
    }
}
