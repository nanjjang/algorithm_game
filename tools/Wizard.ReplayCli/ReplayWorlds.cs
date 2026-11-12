using System;
using Wizard.Content;
using Wizard.Sim;

namespace Wizard.ReplayCli
{
    // 기록에서 세계를 만든다. 설정은 그 모드의 세계를 실제로 만드는 쪽과 같다:
    //   무한 웨이브: SimConfig.ForEndless(db)              — ReplayRunner.Run, RunSession 과 같다
    //   연습:        SimConfig.ForPractice(db, ForEndless) — PracticeSession 의 기본과 같다. 연습 세계는 머리말을 읽지 않는다
    //   결투:        SimConfig.ForDuel(db, setup.Options)  — ReplayRunner.RunDuel 과 같다
    // 코어 버전은 보지 않는다(골든은 해시가 바뀌지 않는 코어 변경을 넘어 살아야 한다). 팩 버전은 다르면 거절한다(다른 팩의 해시는 비교할 수 없다).
    public static class ReplayWorlds
    {
        public static WorldKind DefaultKind(RecordFeed feed) => feed.Format == RecordFormat.Wzdr ? WorldKind.Duel : WorldKind.Endless;

        public static SimWorld Create(ContentDb db, WorldKind kind, RecordFeed feed)
        {
            if (db == null)
            {
                throw new ArgumentNullException(nameof(db));
            }
            if (feed == null)
            {
                throw new ArgumentNullException(nameof(feed));
            }
            switch (kind)
            {
                case WorldKind.Endless:
                    RequireRun(db, feed, kind);
                    return SimWorld.CreateEndless(db, SimConfig.ForEndless(db), feed.Endless!);
                case WorldKind.Practice:
                    RequireRun(db, feed, kind);
                    return SimWorld.CreatePractice(db, SimConfig.ForPractice(db, SimConfig.ForEndless(db)), new PracticeSetup());
                case WorldKind.Duel:
                    if (feed.Duel == null)
                    {
                        throw new ArgumentException("a duel world needs a .wzdr record", nameof(feed));
                    }
                    if (feed.Duel.DataVersion != db.DataVersion)
                    {
                        throw new ArgumentException("record data_version " + feed.Duel.DataVersion + " is not the loaded pack " + db.DataVersion);
                    }
                    return SimWorld.CreateDuel(db, SimConfig.ForDuel(db, feed.Duel.Options), feed.Duel);
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }

        // 연습 세계의 씨앗은 0이다(PracticeSetup 에 씨앗 칸이 아직 없다). 그 밖에는 머리말의 씨앗
        public static ulong WorldSeed(WorldKind kind, RecordFeed feed) => kind == WorldKind.Practice ? 0UL : feed.SetupSeed;

        public static WorldKind ParseKind(string text)
        {
            switch (text)
            {
                case "endless":
                    return WorldKind.Endless;
                case "practice":
                    return WorldKind.Practice;
                case "duel":
                    return WorldKind.Duel;
                default:
                    throw new FormatException("kind '" + text + "' is not endless, practice or duel");
            }
        }

        public static string KindName(WorldKind kind) => kind switch
        {
            WorldKind.Endless => "endless",
            WorldKind.Practice => "practice",
            WorldKind.Duel => "duel",
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };

        private static void RequireRun(ContentDb db, RecordFeed feed, WorldKind kind)
        {
            if (feed.Endless == null)
            {
                throw new ArgumentException("a " + KindName(kind) + " world needs a .wzrc record", nameof(feed));
            }
            if (feed.Endless.DataVersion != db.DataVersion)
            {
                throw new ArgumentException("record data_version " + feed.Endless.DataVersion + " is not the loaded pack " + db.DataVersion);
            }
        }
    }
}
