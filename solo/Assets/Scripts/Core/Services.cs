using System.Collections.Generic;
using UnityEngine;

namespace Wizard.Solo.Data
{
    /// <summary>
    /// 읽기 전용 콘텐츠 창고. 구현: Wizard.Solo.Data.ContentDb(F1). Resources/Data의 TextAsset을 Newtonsoft.Json으로 읽는다.
    /// 조회 메서드는 없는 id에 null을 돌려준다(예외를 던지지 않는다). 목록은 JSON에 적힌 순서다.
    /// </summary>
    public interface IContentDb
    {
        /// <summary>데이터 판 식별 글자(저장 파일의 data_version에 적는다).</summary>
        string DataVersion { get; }

        IReadOnlyList<BlockDef> Blocks { get; }
        BlockDef Block(string blockId);
        /// <summary>등급 안 추첨 순서(06 요약서 0.2절)로 정렬된 블록 id. rarity = Ids.Rarity.*.</summary>
        IReadOnlyList<string> BlocksOfRarity(string rarity);
        ElementsDef Elements { get; }

        IReadOnlyList<CharacterDef> Characters { get; }
        /// <summary>"char.N" → 정의.</summary>
        CharacterDef Character(string charId);
        CharacterDef BaselineCharacter { get; }

        IReadOnlyList<ArtDef> Arts { get; }
        ArtDef Art(string artId);
        TriesDef Tries { get; }
        /// <summary>그 캐릭터 트라이에 넣는 비기 id 11개(tries.json characters[].arts).</summary>
        IReadOnlyList<string> TrieArts(string charId);

        IReadOnlyList<EnemyDef> Enemies { get; }
        /// <summary>일반 적·졸개·허수아비(Ids.Enemy.Dummy)까지.</summary>
        EnemyDef Enemy(string enemyId);
        /// <summary>"elite.xxx" 또는 바탕 "enemy.xxx" 어느 쪽으로도 찾는다.</summary>
        EliteDef Elite(string eliteOrEnemyId);
        /// <summary>엘리트 정의 전부(elites.json에 적힌 순서). 웨이브 구성·기록 화면이 목록으로 돌 때 쓴다.</summary>
        IReadOnlyList<EliteDef> Elites { get; }
        BossCommonDef BossCommon { get; }
        /// <summary>rotation_index 오름차순.</summary>
        IReadOnlyList<BossDef> Bosses { get; }
        BossDef Boss(string bossId);

        WavesDef Waves { get; }
        IReadOnlyList<FrameDef> Frames { get; }
        FrameDef Frame(string frameId);
        IReadOnlyList<ModifierDef> Modifiers { get; }
        ModifierDef Modifier(string whimId);
        IReadOnlyList<TodDef> Tods { get; }
        TodDef Tod(string todId);
        ScoringDef Scoring { get; }
        StarfallDef Starfall { get; }
        IReadOnlyList<WeatherEventDef> WeatherEvents { get; }
        /// <summary>무한 웨이브·연습 전장(place.field).</summary>
        ArenaDef Field { get; }

        EnhanceConfig Enhance { get; }
        EconomyConfig Economy { get; }
        MutationsConfig Mutations { get; }
        DropsConfig Drops { get; }
        CampConfig Camp { get; }
        SoloConstDef Const { get; }
        PracticeConfig Practice { get; }

        /// <summary>
        /// 작업별 보충 수치 조각: Resources/DataSolo/&lt;name&gt;.json 을 읽어 돌려준다(없으면 null, 한 번 읽고 캐시).
        /// 기반 단계 뒤에 구현 작업이 Def에 없는 수치를 둘 곳이다. name = 작업 id(예: "G05"). 값은 요약서의 명세 값만.
        /// </summary>
        Newtonsoft.Json.Linq.JObject SoloExtra(string name);
    }

    /// <summary>
    /// 이름 표. names.ko.json 사본 + Resources/NamesSolo/*.ko.json(원본에 없는 열쇠, 작업마다 조각 하나). 구현: Wizard.Solo.Data.NameTable(F1).
    /// 화면에 보이는 글자는 전부 여기서 나온다. 코드에 한국어 문자열 리터럴을 두지 않는다.
    /// </summary>
    public interface INameTable
    {
        /// <summary>열쇠의 글자. 없으면 열쇠 그대로 돌려준다(빠진 열쇠가 화면에서 보이게).</summary>
        string Get(string key);
        bool Has(string key);
        /// <summary>"{name}" 자리 채우기. args는 (이름, 값) 쌍을 번갈아: Format("str.x", "n", 3, "max", 5).</summary>
        string Format(string key, params object[] nameValuePairs);
        /// <summary>세 자리 쉼표 숫자(08 요약서 1.4절).</summary>
        string Number(long value);
    }
}

namespace Wizard.Solo.Art
{
    /// <summary>스프라이트 애니메이션 한 벌. 프레임은 캐시된 Sprite(pivot 반영).</summary>
    public sealed class SpriteAnim
    {
        public string Key;
        public Sprite[] Frames;
        public float Fps;
        public bool Loop;
        public float Duration => Frames == null || Fps <= 0f ? 0f : Frames.Length / Fps;
        /// <summary>경과 초 → 프레임. 반복이 아니면 마지막 프레임에 머문다.</summary>
        public Sprite At(float seconds)
        {
            if (Frames == null || Frames.Length == 0) return null;
            int i = Fps <= 0f ? 0 : (int)(seconds * Fps);
            if (Loop) i %= Frames.Length; else if (i >= Frames.Length) i = Frames.Length - 1;
            if (i < 0) i = 0;
            return Frames[i];
        }
    }

    /// <summary>캐릭터·지팡이 그림의 세 방향(왼쪽은 오른쪽을 뒤집는다 → flipX).</summary>
    public enum Facing3 { Down, Right, Up }

    public enum UiFontId { F12 = 12, F16 = 16, F24 = 24, F32 = 32, F48 = 48, F64 = 64 }

    /// <summary>
    /// 월드 그리기에 쓰는 머티리얼 종류. 셰이더·머티리얼의 주인은 그림 창고(F2) 하나다: Assets/Resources/Shaders 아래 솔로 셰이더를
    /// Resources.Load로 읽어 머티리얼을 만들고 캐시한다(Shader.Find 금지 — 빈 장면 + 코드 생성이라 빌드에서 벗겨진다).
    /// 모두 조명을 받지 않는다(장면에 Light2D가 없다. Sprite-Lit-Default를 쓰지 않는다).
    /// </summary>
    public enum SpriteMaterialKind
    {
        /// <summary>텍스처 × 색(알파 혼합). SpriteShaderProps의 단색 덮기·외곽선을 MaterialPropertyBlock으로 받는다.</summary>
        Sprite = 0,
        /// <summary>가산 혼합. 32px 이하 그림에만(10 요약서 3.2절).</summary>
        SpriteAdditive = 1,
        /// <summary>텍스처 없이 정점 색 × _Color(알파 혼합). 코드로 만든 메시(예고 구역)용.</summary>
        Shape = 2
    }

    /// <summary>솔로 스프라이트 셰이더의 속성 이름(F2가 셰이더를 이 이름으로 만든다). MaterialPropertyBlock에 넣는다.</summary>
    public static class SpriteShaderProps
    {
        /// <summary>단색 덮기 색과 양(0 = 원래 그림, 1 = 전부 그 색). 피격 흰 번쩍.</summary>
        public const string FlashColor = "_FlashColor", FlashAmount = "_FlashAmount";
        /// <summary>외곽선 색과 두께(텍셀, 0 = 없음). 엘리트 금색 2px, 림.</summary>
        public const string OutlineColor = "_OutlineColor", OutlineSize = "_OutlineSize";
    }

    /// <summary>9조각 정보가 붙은 UI 스프라이트.</summary>
    public struct UiSprite
    {
        public Sprite Sprite;
        public bool NineSlice;
        public int Width, Height;
        public bool IsValid => Sprite != null;
    }

    /// <summary>
    /// 그림 창고. 구현: Wizard.Solo.Art.ArtLibrary(F2). Resources/Art 아래 텍스처와 manifest JSON을 읽어
    /// 런타임에 Sprite.Create로 자르고 캐시한다. 그림이 없으면 예외 대신 대체 그림(회청·나무 어두움 바둑판)을 돌려준다.
    /// 늦게 읽는다: 부팅 때는 manifest만 읽고, 텍스처는 처음 요청될 때 Resources.Load, 스프라이트는 요청된 칸만 만든다(00 문서 10절).
    /// 폴더 이름(작업용 로마자) ↔ char.N 대응은 이 구현 안의 표 한 곳에만 둔다.
    /// </summary>
    public interface IArtLibrary
    {
        // ---- 캐릭터 ----
        /// <summary>캐릭터 몸 클립. clip은 players/manifest.json의 클립 이름(방향 접미사 없이). 왼쪽은 Right + flipX.</summary>
        SpriteAnim CharacterClip(string charId, string clip, Facing3 facing);
        /// <summary>그 방향에 실제로 쓰는 클립 정보(프레임별 feet·hand·head·shoulder 점). 지팡이 붙이기·머리 위 표시용. 없으면 null.</summary>
        PlayerClip CharacterClipInfo(string charId, string clip, Facing3 facing);
        /// <summary>초상(0 기본, 1~3 표정), 선택 화면 큰 그림.</summary>
        Sprite CharacterPortrait(string charId, int expression);
        Sprite CharacterSelection(string charId);
        /// <summary>실루엣 32×32(선택 화면의 잠긴 칸 등).</summary>
        Sprite CharacterSilhouette(string charId);

        // ---- 지팡이 ----
        /// <summary>손에 든 지팡이. stage 0~13, aim = 내밀기(자세·발사) / 들기.</summary>
        Sprite WandHand(string charId, int stage, Facing3 facing, bool aim);
        /// <summary>강화·선택 화면 큰 그림(192×192), 목록 아이콘(64×64).</summary>
        Sprite WandInspection(string charId, int stage);
        Sprite WandIcon(string charId, int stage);
        /// <summary>단계별 점(머리, 변이 2~3점, 확장 칸). 32×64 칸 안 px, 모든 방향 공통 [임시 U-04]. 없으면 null.</summary>
        WandStage WandStageInfo(string charId, int stage);
        /// <summary>손잡이 기준점(지팡이 칸 안 px). 지팡이 칸 왼쪽 위 = 캐릭터 hand − 이 점(WandSheet.AttachTopLeft). manifest에 없으면 (16, 48).</summary>
        PixelPoint WandGrip { get; }
        /// <summary>그 캐릭터 지팡이의 분리 조각(wands/parts). pivot = manifest anchor, 솔로 임시 12fps 반복. 조각이 없는 캐릭터면 null(대체 그림 없음).</summary>
        SpriteAnim WandDetachedPart(string charId);

        // ---- 몬스터 ----
        /// <summary>몬스터·엘리트 바탕·보스 클립. id = enemy.* / boss.*. 없는 클립이면 첫 클립.</summary>
        SpriteAnim MonsterClip(string monsterId, string clip);
        /// <summary>그 몬스터가 가진 클립 이름(manifest 순서). 모르는 id면 빈 목록.</summary>
        IReadOnlyList<string> MonsterClipNames(string monsterId);
        Sprite BossPortrait(string bossId);
        Sprite BossHudIcon(string bossId);

        // ---- 효과 ----
        /// <summary>
        /// 효과. group = player/special/ritual/enemy/loot/wand/status, 보강분 "enhance"(native-64 강화 묶음의 new 항목)와
        /// "layers"(캐릭터 겹 그림 — key에 char.N과 "key:prefix" 거르개를 쓸 수 있다). key = manifest 열쇠.
        /// </summary>
        SpriteAnim Effect(string group, string key);
        /// <summary>효과 항목 정보(크기, 프레임 rect, 원본 JSON). 없으면 null.</summary>
        EffectEntry EffectInfo(string group, string key);
        /// <summary>EffectInfo("layers", key).</summary>
        EffectEntry LayerInfo(string key);

        // ---- UI ----
        /// <summary>UI 키트. id = "K-20" 같은 ID, variant·state는 파일 이름 규칙(09 요약서 6.1절). state null = normal.</summary>
        UiSprite Ui(string id, string variant = null, string state = null);
        /// <summary>크기 갈래가 있는 UI(K-40 16/24/32, K-41, K-43, K-53 …)에서 높이가 sizePx에 가까운 것. 0 = Ui와 같다.</summary>
        UiSprite UiSized(string id, string variant, string state, int sizePx);
        /// <summary>UI 항목 정보(frame_ms, hotspot, frame_offsets 같은 값은 원본에서). 없으면 null.</summary>
        UiEntry UiInfo(string id, string variant = null, string state = null, int sizePx = 0);
        /// <summary>여러 프레임 UI(K-44 불러오는 모닥불, K-40 선택 괄호, K-42 새 소식 점 …)의 애니. fps = 1000 / frame_ms(없으면 0), 반복.</summary>
        SpriteAnim UiAnim(string id, string variant = null, string state = null, int sizePx = 0);
        /// <summary>블록·비기·변이·속성·재화 등 아이콘. 콘텐츠 id로 찾는다(대응표는 구현 안). "ui:&lt;ID&gt;/&lt;variant&gt;" 꼴이면 UI 아이콘을 직접 가리킨다.</summary>
        Sprite Icon(string contentId, int sizePx = 32);

        // ---- 전장 ----
        /// <summary>전장 타일·소품. assetId = world/manifest.json의 열쇠("id:pose" 꼴로 자세를 고를 수 있다), tod = Ids.Tod.*(타일형만 구분).</summary>
        SpriteAnim World(string assetId, string todId = null);
        /// <summary>월드 항목 정보(origin, mirrorFor, poseFrames …). 없으면 null.</summary>
        WorldEntry WorldInfo(string assetId);
        /// <summary>맵 배치 JSON 글(world/maps/&lt;mapId&gt;.json, 예 "field.01"). 없으면 null.</summary>
        string WorldMapJson(string mapId);
        /// <summary>배경. key = "title", "enhance.workbench", UI id, world 열쇠.</summary>
        Sprite Background(string key);

        // ---- 공통 에셋(Resources/Art/common, 사용자 제작) ----
        /// <summary>
        /// 공통 시트의 프레임들. relPath = "Combat/hit_spark_ice_32x32_f4" 꼴(common 기준, 확장자 없음). 파일 이름의 "_WxH"(한 칸)와
        /// "_fN"(칸 수)로 잘라 위→아래, 왼쪽→오른쪽 순으로 준다. 캐시한다. 없으면 빈 배열.
        /// </summary>
        Sprite[] CommonFrames(string relPath);
        /// <summary>공통 시트의 첫 프레임. 없으면 null(부르는 쪽이 대신 그린다).</summary>
        Sprite CommonSprite(string relPath);

        // ---- 글꼴·도형·머티리얼 ----
        /// <summary>한글 TTF 동적 글꼴. 크기는 여섯 가지만. 돌려주는 것은 TTF이고 부르는 쪽이 fontSize = (int)size 로 쓴다.</summary>
        Font Font(UiFontId size);
        /// <summary>대체 그림(w×h px 바둑판). 위험색·금색·연두·청록을 쓰지 않는다.</summary>
        Sprite Placeholder(int widthPx, int heightPx);
        /// <summary>1×1 흰 점(색 입혀 도형 그리기용). PPU 1이라 localScale = 칸 수.</summary>
        Sprite WhitePixel { get; }
        /// <summary>반지름 px의 흰 원·고리(색 입혀 쓰는 도형).</summary>
        Sprite Circle(int radiusPx, bool ringOnly = false, int ringWidthPx = 2);
        /// <summary>월드 그리기용 공유 머티리얼(캐시). 셰이더가 없으면 Sprites/Default로 만든 대체 머티리얼(null을 주지 않는다).</summary>
        Material Material(SpriteMaterialKind kind);
        /// <summary>효과 묶음(Effect의 group) 하나의 텍스처·스프라이트 캐시를 내린다(강화 화면이 닫힐 때 "ritual"). 다시 요청하면 다시 읽는다.</summary>
        void Release(string effectGroup);
    }
}

namespace Wizard.Solo
{
    /// <summary>
    /// 소리 사건 재생기. 소리 파일이 없으므로 구현은 무음(F4의 SilentSoundPlayer). 사건 id는 10 요약서 5.1절과
    /// data/presentation/event_bindings.json의 열쇠를 쓴다(예: "player.hit", "heat.crit_hit").
    /// </summary>
    /// <remarks>
    /// 부르는 곳: 전투 사건 = 표시 층 AudioLayer(G16), 화면 사건(ui.*, 강화 E-묶음) = 그 화면이 요약서의 이벤트 줄 시점에 직접,
    /// 배경음 전환(SetMusic) = AudioLayer(판 중)와 화면(메뉴). 섞는 규칙(동시 32개, 사건별 초당 횟수, 우선순위, 누르기 — 10 요약서 5.2절)은
    /// 재생기 안의 정책 클래스가 맡는다(무음 구현이어도 정책은 시험할 수 있게 F4가 만든다). 음량은 설정의 vol_*를 묶음별로 받는다.
    /// </remarks>
    public interface ISoundPlayer
    {
        void Play(string eventId, SoundBus bus = SoundBus.Sfx, float volume = 1f, float pitch = 1f);
        /// <summary>배경음을 바꾼다(null = 끔). 곡 목록은 미정이라 id만 흘린다.</summary>
        void SetMusic(string trackId);
        /// <summary>묶음 음량(0~1). Master는 전체에 곱한다.</summary>
        void SetVolume(SoundBus bus, float volume01);
    }

    /// <summary>
    /// 실제 클립 재생기(Audio.ClipSoundPlayer)가 더 하는 일. ISoundPlayer에 넣지 않고 따로 둔 것은 기존 가짜 구현(테스트)을
    /// 깨지 않기 위해서다. 부르는 쪽은 SoundControl의 확장 메서드를 쓴다(재생기가 이것을 구현하지 않으면 아무 일도 없다).
    /// </summary>
    public interface ISoundControl
    {
        /// <summary>캐릭터 전용 소리를 고를 문맥(캐릭터 id "char.N", 지팡이 단계 0~13). 판은 표시 층이, 강화 화면은 그 화면이 넣는다.</summary>
        void SetContext(string characterId, int wandStage);
        /// <summary>반복음 시작. 같은 열쇠가 이미 같은 클립으로 돌고 있으면 음량만 바꾼다. 그 사건에 반복 클립이 없으면 그 열쇠의 반복음을 멈춘다.</summary>
        void StartLoop(string loopKey, string eventId, SoundBus bus, float volume = 1f);
        void StopLoop(string loopKey);
        void StopAllLoops();
    }

    /// <summary>ISoundPlayer가 ISoundControl도 구현할 때만 넘기는 도우미(null·무음 재생기·테스트 가짜면 아무 일도 없다).</summary>
    public static class SoundControl
    {
        public static void SetSoundContext(this ISoundPlayer sound, string characterId, int wandStage)
        {
            var c = sound as ISoundControl;
            if (c != null) c.SetContext(characterId, wandStage);
        }

        public static void StartLoop(this ISoundPlayer sound, string loopKey, string eventId, SoundBus bus, float volume = 1f)
        {
            var c = sound as ISoundControl;
            if (c != null) c.StartLoop(loopKey, eventId, bus, volume);
        }

        public static void StopLoop(this ISoundPlayer sound, string loopKey)
        {
            var c = sound as ISoundControl;
            if (c != null) c.StopLoop(loopKey);
        }
    }

    /// <summary>한 번의 키 변화. TimeU는 시뮬레이션 시각(u) — 틱 t에 속하는 이벤트는 t×256 ≤ TimeU &lt; (t+1)×256.</summary>
    public struct InputEvent
    {
        public LogicalKey Key;
        public bool Down;
        public long TimeU;
        public InputEvent(LogicalKey key, bool down, long timeU) { Key = key; Down = down; TimeU = timeU; }
    }

    /// <summary>
    /// 전투 입력원. 구현: KeyboardInputSource(Input System의 Keyboard.current[Key]를 직접 읽는다), 테스트·튜토리얼용 BotInputSource(F5).
    /// 논리 상태가 바뀔 때만 이벤트를 낸다(한 논리 키에 물리 키 둘이면 하나라도 눌려 있으면 눌림).
    /// Esc는 메뉴 길(ScreenManager)의 것이라 이 입력원은 Esc를 LogicalKey.Pause로 내지 않는다(Pause는 재설정한 다른 키만).
    /// Keyboard.current가 null이어도(배치 모드) 죽지 않고 "아무 키도 안 눌림"으로 본다.
    /// "동작 키" = Fire, Skill, Reverse, Pause(누르는 순간이 뜻인 키). "상태 키" = 방향 넷과 Stance.
    /// </summary>
    public interface IInputSource
    {
        /// <summary>지난 호출 뒤 생긴 논리 키 변화를 시간순으로 into에 더한다. simTimeU = 지금 시뮬레이션 시각(u).</summary>
        void Poll(long simTimeU, List<InputEvent> into);
        bool IsDown(LogicalKey key);
        /// <summary>상태를 다시 맞춘다(일시정지 풀림, 메뉴에서 돌아옴, 창 초점 복귀): 지금 물리 상태와 내부 상태의 차이를 이벤트로 낸다.
        /// 뗀 키는 뗌으로, 새로 눌린 상태 키는 누름으로 낸다. swallowHeldActions가 참이면 새로 눌려 있는 동작 키는 누름을 내지 않고
        /// 물리적으로 뗐다가 다시 누를 때까지 삼킨다(전부터 눌려 있던 동작 키는 그대로 눌림).</summary>
        void Resync(long simTimeU, List<InputEvent> into, bool swallowHeldActions = false);
        /// <summary>내부에서 눌림으로 알고 있는 논리 키를 전부 뗌 이벤트로 내고 내부 상태를 "전부 뗌"으로 만든다
        /// (다른 화면이 전투 화면 위에 뜰 때, 창이 초점을 잃을 때). 그 뒤 돌아올 때는 Resync(swallowHeldActions: true).</summary>
        void ReleaseAll(long simTimeU, List<InputEvent> into);
    }

    /// <summary>벽시계(상점 주간 한도, 저장 시각). 테스트가 갈아 끼운다.</summary>
    public interface IClock
    {
        System.DateTime Now { get; }
        System.DateTime UtcNow { get; }
    }
}
