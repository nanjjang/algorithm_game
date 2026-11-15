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
        }

        public static ServerProfile ReadProfile(NetReader r)
        {
            return new ServerProfile
            {
                AccountId = r.Long(), Name = r.Str(), Gold = r.Long(), Rating = r.Int(),
                DuelWins = r.Int(), DuelLosses = r.Int(), DuelDraws = r.Int(), CoopRuns = r.Int(), CoopBestWave = r.Int(),
            };
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
