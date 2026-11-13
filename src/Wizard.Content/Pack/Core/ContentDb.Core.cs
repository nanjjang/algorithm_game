using System;
using System.Collections.Generic;

namespace Wizard.Content
{
    // CONTRACTS 2.3, 11.2, DECISIONS-TECH T-33. ContentDb 의 절 훅.
    //
    // 절 하나에 훅 둘:
    //   partial void Load<절>(ReadOnlySpan<byte> section)  팩에 그 절이 있으면 Build 가 id 오름차순으로 한 번 부른다.
    //                                                       절 파일이 바이트를 ByteReader 로 읽어 자기 필드에 정의를 둔다.
    //                                                       깨진 바이트는 ContentException.
    //   partial void Find<접근자>(…, ref T? def)            접근자가 부른다. 절 파일이 찾은 정의를 def 에 넣는다.
    //                                                       def 가 null 로 남으면(절을 읽는 코드가 아직 없거나, 절이 팩에 없거나,
    //                                                       그 id 가 없으면) 접근자는 ContentException 을 던진다.
    // 구현이 없는 부분 메서드는 호출째 컴파일에서 사라진다. 그래서 절 파일이 아직 없어도 컴파일되고 비용이 없다.
    // 절 파일은 ContentDb.<절>.cs(<절> 은 ContentSection 의 이름) 하나이고, 전투 쪽이 src/Wizard.Content/Pack/Sections/,
    // 규칙 쪽이 src/Wizard.Content/Rules/Sections/ 에 있다(T-47). 네임스페이스는 Wizard.Content 이고 partial class ContentDb 만 담는다.
    // 규칙 쪽 Find 훅은 규칙 타입을 쓰므로 해시 밖(src/Wizard.Content/Rules/ContentDb.Rules.cs)에 선언한다(WZ0017). Load 훅은 바이트만 받으므로 여기 둔다.
    // 접근자가 없는 절(il, fees, chat_filter)의 접근자와 조회는 그 절의 파일이 더한다(CONTRACTS 2.3 의 목록에 올리는 계약 변경과 같이).
    // 훅을 더하거나 부르는 순서를 바꾸는 것은 계약 변경이다(CONTRACTS 0.1).
    public sealed partial class ContentDb
    {
        // 팩의 절을 id 오름차순으로 훅에 넘긴다. 모르는 절은 건너뛴다(CONTRACTS 11.2).
        private void LoadSections()
        {
            ReadOnlyMemory<byte> s;
            if (_pack.TryGetSection((ushort)ContentSection.Characters, out s)) LoadCharacters(s.Span);
            if (_pack.TryGetSection((ushort)ContentSection.Blocks, out s)) LoadBlocks(s.Span);
            if (_pack.TryGetSection((ushort)ContentSection.Arts, out s)) LoadArts(s.Span);
            if (_pack.TryGetSection((ushort)ContentSection.Mutations, out s)) LoadMutations(s.Span);
            if (_pack.TryGetSection((ushort)ContentSection.Enemies, out s)) LoadEnemies(s.Span);
            if (_pack.TryGetSection((ushort)ContentSection.Bosses, out s)) LoadBosses(s.Span);
            if (_pack.TryGetSection((ushort)ContentSection.Waves, out s)) LoadWaves(s.Span);
            if (_pack.TryGetSection((ushort)ContentSection.Frames, out s)) LoadFrames(s.Span);
            if (_pack.TryGetSection((ushort)ContentSection.Modifiers, out s)) LoadModifiers(s.Span);
            if (_pack.TryGetSection((ushort)ContentSection.Tod, out s)) LoadTod(s.Span);
            if (_pack.TryGetSection((ushort)ContentSection.Scoring, out s)) LoadScoring(s.Span);
            if (_pack.TryGetSection((ushort)ContentSection.Arena, out s)) LoadArena(s.Span);
            if (_pack.TryGetSection((ushort)ContentSection.DuelRules, out s)) LoadDuelRules(s.Span);
            if (_pack.TryGetSection((ushort)ContentSection.Tries, out s)) LoadTries(s.Span);
            if (_pack.TryGetSection((ushort)ContentSection.Starfall, out s)) LoadStarfall(s.Span);
            if (_pack.TryGetSection((ushort)ContentSection.WeatherEvents, out s)) LoadWeatherEvents(s.Span);
            if (_pack.TryGetSection((ushort)ContentSection.BossRotation, out s)) LoadBossRotation(s.Span);
            if (_pack.TryGetSection((ushort)ContentSection.Elites, out s)) LoadElites(s.Span);
            if (_pack.TryGetSection((ushort)ContentSection.Elements, out s)) LoadElements(s.Span);
            if (_pack.TryGetSection((ushort)ContentSection.Enhance, out s)) LoadEnhance(s.Span);
            if (_pack.TryGetSection((ushort)ContentSection.Economy, out s)) LoadEconomy(s.Span);
            if (_pack.TryGetSection((ushort)ContentSection.Drops, out s)) LoadDrops(s.Span);
            if (_pack.TryGetSection((ushort)ContentSection.Leagues, out s)) LoadLeagues(s.Span);
            if (_pack.TryGetSection((ushort)ContentSection.Season, out s)) LoadSeason(s.Span);
            if (_pack.TryGetSection((ushort)ContentSection.Titles, out s)) LoadTitles(s.Span);
            if (_pack.TryGetSection((ushort)ContentSection.Cosmetics, out s)) LoadCosmetics(s.Span);
            if (_pack.TryGetSection((ushort)ContentSection.Plaza, out s)) LoadPlaza(s.Span);
            if (_pack.TryGetSection((ushort)ContentSection.Shop, out s)) LoadShop(s.Span);
            if (_pack.TryGetSection((ushort)ContentSection.Items, out s)) LoadItems(s.Span);
            if (_pack.TryGetSection((ushort)ContentSection.Bag, out s)) LoadBag(s.Span);
            if (_pack.TryGetSection((ushort)ContentSection.RunLimits, out s)) LoadRunLimits(s.Span);
            if (_pack.TryGetSection((ushort)ContentSection.Il, out s)) LoadIl(s.Span);
            if (_pack.TryGetSection((ushort)ContentSection.Fees, out s)) LoadFees(s.Span);
            if (_pack.TryGetSection((ushort)ContentSection.Net, out s)) LoadNet(s.Span);
            if (_pack.TryGetSection((ushort)ContentSection.ChatFilter, out s)) LoadChatFilter(s.Span);
            if (_pack.TryGetSection((ushort)ContentSection.SimLimits, out s)) LoadSimLimits(s.Span);
        }

        // 접근자의 몸통: 훅이 채운 정의, 없으면 ContentException
        private static T Require<T>(T? def, ContentSection section) where T : class =>
            def ?? throw new ContentException("content section '" + (ContentSections.Name(section) ?? "?") + "' is not loaded or has no such entry");

        // ── 절 읽기 훅(CONTRACTS 11.2 의 절마다 하나) ──
        partial void LoadCharacters(ReadOnlySpan<byte> section);
        partial void LoadBlocks(ReadOnlySpan<byte> section);
        partial void LoadArts(ReadOnlySpan<byte> section);
        partial void LoadMutations(ReadOnlySpan<byte> section);
        partial void LoadEnemies(ReadOnlySpan<byte> section);
        partial void LoadBosses(ReadOnlySpan<byte> section);
        partial void LoadWaves(ReadOnlySpan<byte> section);
        partial void LoadFrames(ReadOnlySpan<byte> section);
        partial void LoadModifiers(ReadOnlySpan<byte> section);
        partial void LoadTod(ReadOnlySpan<byte> section);
        partial void LoadScoring(ReadOnlySpan<byte> section);
        partial void LoadArena(ReadOnlySpan<byte> section);
        partial void LoadDuelRules(ReadOnlySpan<byte> section);
        partial void LoadTries(ReadOnlySpan<byte> section);
        partial void LoadStarfall(ReadOnlySpan<byte> section);
        partial void LoadWeatherEvents(ReadOnlySpan<byte> section);
        partial void LoadBossRotation(ReadOnlySpan<byte> section);
        partial void LoadElites(ReadOnlySpan<byte> section);
        partial void LoadElements(ReadOnlySpan<byte> section);
        partial void LoadEnhance(ReadOnlySpan<byte> section);
        partial void LoadEconomy(ReadOnlySpan<byte> section);
        partial void LoadDrops(ReadOnlySpan<byte> section);
        partial void LoadLeagues(ReadOnlySpan<byte> section);
        partial void LoadSeason(ReadOnlySpan<byte> section);
        partial void LoadTitles(ReadOnlySpan<byte> section);
        partial void LoadCosmetics(ReadOnlySpan<byte> section);
        partial void LoadPlaza(ReadOnlySpan<byte> section);
        partial void LoadShop(ReadOnlySpan<byte> section);
        partial void LoadItems(ReadOnlySpan<byte> section);
        partial void LoadBag(ReadOnlySpan<byte> section);
        partial void LoadRunLimits(ReadOnlySpan<byte> section);
        partial void LoadIl(ReadOnlySpan<byte> section);
        partial void LoadFees(ReadOnlySpan<byte> section);
        partial void LoadNet(ReadOnlySpan<byte> section);
        partial void LoadChatFilter(ReadOnlySpan<byte> section);
        partial void LoadSimLimits(ReadOnlySpan<byte> section);

        // ── 전투 쪽 접근자의 조회 훅(CONTRACTS 2.3) ──
        partial void FindCharacter(CharacterId id, ref CharacterDef? def);
        partial void FindBlock(BlockId id, ref BlockDef? def);
        partial void FindArt(ArtId id, ref ArtDef? def);
        partial void FindMutation(MutationId id, ref MutationDef? def);
        partial void FindEnemy(EnemyTypeId id, ref EnemyDef? def);
        partial void FindBoss(BossId id, ref BossDef? def);
        partial void FindWaveCurve(ref WaveCurveDef? def);
        partial void FindFrames(ref IReadOnlyList<FrameDef>? defs);
        partial void FindModifiers(ref IReadOnlyList<ModifierDef>? defs);
        partial void FindScoring(ref ScoringDef? def);
        partial void FindArena(SimMode mode, ref ArenaDef? def);
        partial void FindDuelRules(ref DuelRuleSet? def);
        partial void FindTries(CharacterId id, ref TrieDef? def);
        partial void FindElite(EnemyTypeId baseType, ref EliteDef? def);
        partial void FindStarfall(ref StarfallDef? def);
        partial void FindWeatherEvents(ref IReadOnlyList<WeatherEventDef>? defs);
        partial void FindBossRotation(ref BossRotationDef? def);
        partial void FindTimeOfDay(TimeOfDay tod, ref TimeOfDayDef? def);
        partial void FindElementRules(ref ElementDef? def);
        partial void FindSimLimits(ref SimLimitsDef? def);
        partial void FindSpellRules(ref SpellRulesDef? def);       // blocks 절의 꼬리(T-57)
    }
}
