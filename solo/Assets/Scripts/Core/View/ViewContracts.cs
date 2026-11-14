using System.Collections.Generic;
using UnityEngine;
using Wizard.Solo.Art;
using Wizard.Solo.Sim;

namespace Wizard.Solo.View
{
    /// <summary>
    /// 그리는 층 순서(10 요약서 3.1절). SpriteRenderer.sortingOrder의 띠로 쓴다(띠 안에서 0~99 자유).
    /// 몸(Bodies) 띠 안에서는 아래쪽(시뮬레이션 y가 큰 쪽)이 위에 그려지게 YOrder를 더한다.
    /// </summary>
    public static class SortBands
    {
        public const int Floor = 0, FloorMarks = 100, PlayerZones = 200, TelegraphFill = 300, Shadows = 400,
            Bodies = 500, PlayerShots = 700, Bursts = 800, TelegraphEdge = 900, EnemyShots = 1000,
            PlayerMarks = 1100, Weather = 1200, DamageNumbers = 1300;
        /// <summary>몸 띠 안의 y 순서(0~139). 시뮬레이션 y(0~14)를 10배 한다.</summary>
        public static int YOrder(float simY) => Mathf.Clamp(Mathf.RoundToInt(simY * 10f), 0, 139);
    }

    /// <summary>
    /// 월드 그리기 풀. 구현: App/SpritePool(F5). 빌린 것은 반드시 돌려준다.
    /// 머티리얼은 전부 ctx.Art.Material(kind)에서 온다 — 표시 층은 Shader.Find·new Material을 쓰지 않는다(빌드에서 셰이더가 벗겨진다).
    /// 개체별 값(단색 덮기, 외곽선)은 MaterialPropertyBlock + SpriteShaderProps로 넣는다. 돌려받을 때 풀이 PropertyBlock·색·flip·scale을 비운다.
    /// 전장 위 글자는 여기서 빌리지 않는다: 숫자는 스프라이트 글리프(status/damage_glyphs, combo_digits), 그 밖의 글자는
    /// ViewContext.WorldTextRoot 아래 uGUI Text(+ Outline)를 WorldToHud 좌표에 둔다.
    /// </summary>
    public interface ISpritePool
    {
        SpriteRenderer Rent(Sprite sprite, int sortingOrder, SpriteMaterialKind material = SpriteMaterialKind.Sprite);
        void Return(SpriteRenderer renderer);
        /// <summary>코드로 만든 메시(예고 구역의 원·부채·띠·고리)용 MeshRenderer(+ MeshFilter). 메시는 빌린 쪽이 만들어 넣고,
        /// 돌려주기 전에 자기가 만든 Mesh를 Destroy 한다. 정점 색이 곧 색이다(SpriteMaterialKind.Shape).</summary>
        MeshRenderer RentMesh(int sortingOrder, SpriteMaterialKind material = SpriteMaterialKind.Shape);
        void Return(MeshRenderer renderer);
    }

    /// <summary>표시 층들이 함께 쓰는 것. WorldViewHost(F5)가 판(또는 보조 판)마다 만들어 넘긴다.</summary>
    public sealed class ViewContext
    {
        public GameContext Game;
        /// <summary>이 표시 층들이 그리는 세계(보조 판이면 보조 세계).</summary>
        public SimWorld World;
        /// <summary>전장 루트. 자식은 ToView로 바꾼 좌표에 둔다(전장 왼쪽 아래 = (0,0), 가운데 = (12,7)).</summary>
        public Transform WorldRoot;
        /// <summary>정사영 카메라. 평범한 UnityEngine.Camera만 쓴다(URP 전용 형을 쓰지 않는다).</summary>
        public Camera Camera;
        /// <summary>HUD 캔버스의 1920×1080 루트(UiCanvasFactory가 만든 것). 화면 흔들림을 받지 않는다.</summary>
        public RectTransform HudRoot;
        /// <summary>전장 위 글자의 부모(HudRoot의 첫 자식, HUD 위젯보다 아래에 그려진다). 자식의 localPosition = WorldToHud(simPos).</summary>
        public RectTransform WorldTextRoot;
        public ISpritePool Pool;
        /// <summary>설정에서 읽은 표시 옵션. 설정이 바뀌면(ISettingsStore.Changed) 호스트가 같은 객체의 값을 갱신한다 — 층은 매 프레임 읽는다.</summary>
        public ViewOptions Options = new ViewOptions();
        /// <summary>연출 감도 0 전체 / 1 줄임 / 2 최소(= Options.EffectsLevel). 적 예고·적 탄·열 링·피격 표시·시야 화살표는 어떤 값에도 줄이지 않는다.</summary>
        public int EffectsLevel => Options.EffectsLevel;
        /// <summary>화면 흔들림 요청(세기 px, 초). 광과민 한도와 Options.ShakeScale은 호스트가 적용한다.</summary>
        public System.Action<float, float> Shake;
        /// <summary>그림 정지 요청(초). 게임 틱은 멈추지 않고 그림만 멈춘다. Options.HitStopEnabled가 꺼져 있으면 호스트가 무시한다.</summary>
        public System.Action<float> HitStop;

        public Vector3 ToView(Vector2 simPos) => SimConst.ToView(simPos, World != null ? World.ArenaHeight : 14f);

        /// <summary>시뮬레이션 좌표 → WorldTextRoot(= HudRoot) 안의 localPosition(1920×1080 기준 px, 가운데 원점).
        /// 카메라 흔들림을 따라간다. 카메라가 없으면 (0,0).</summary>
        public Vector2 WorldToHud(Vector2 simPos)
        {
            if (Camera == null || HudRoot == null) return Vector2.zero;
            Vector3 screen = Camera.WorldToScreenPoint(ToView(simPos));
            Vector2 local;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(HudRoot, screen, null, out local);
            return local;
        }
    }

    /// <summary>
    /// 월드 표시 층 하나(타일, 캐릭터, 적, 예고, 탄, 이펙트, HUD …). 시뮬레이션 상태를 읽기만 하고 고치지 않는다.
    /// 수명: Build(판 시작) → [OnEvent × n → Render] 매 프레임 → Dispose(판 끝).
    /// Build는 진행 중인 세계에 대해서도 불린다(보조 판을 걷고 본판으로 돌아올 때): 이벤트 없이 지금 상태만 보고 다시 그릴 수 있어야 한다.
    /// </summary>
    public interface IWorldViewLayer
    {
        void Build(ViewContext vc, SimWorld w);
        /// <summary>틱에서 나온 이벤트(나온 순서대로). Render보다 먼저 불린다.</summary>
        void OnEvent(SimWorld w, in SimEvent e);
        /// <summary>프레임마다. alpha = 지난 틱과 지금 틱 사이 보간(0~1), dt = 프레임 초(일시정지 중 0).</summary>
        void Render(SimWorld w, float alpha, float dt);
        void Dispose();
    }

    public static class ViewMath
    {
        /// <summary>위치 보간. 한 틱에 1칸 넘게 움직였으면 보간 없이 지금 위치(10 요약서 8절 Q-17).</summary>
        public static Vector2 Lerp(Vector2 prev, Vector2 cur, float alpha)
        {
            if ((cur - prev).sqrMagnitude > 1f) return cur;
            return Vector2.LerpUnclamped(prev, cur, alpha);
        }
    }

    /// <summary>
    /// 표시 층 등록 자리(SimModuleFactory와 같은 방식). 표시 작업은 자기 폴더의 `ViewRegistry.&lt;이름&gt;.cs`에서
    /// 자기 partial 메서드 하나만 구현해 layers.Add(new XLayer())를 한다. 더한 순서대로 Build·Render된다.
    /// </summary>
    public static partial class ViewRegistry
    {
        static partial void AddArena(List<IWorldViewLayer> layers);
        static partial void AddCharacters(List<IWorldViewLayer> layers);
        static partial void AddEnemies(List<IWorldViewLayer> layers);
        static partial void AddThreats(List<IWorldViewLayer> layers);
        static partial void AddShots(List<IWorldViewLayer> layers);
        static partial void AddEffects(List<IWorldViewLayer> layers);
        static partial void AddHud(List<IWorldViewLayer> layers);
        static partial void AddAudio(List<IWorldViewLayer> layers);
        static partial void AddStory(List<IWorldViewLayer> layers);

        public static List<IWorldViewLayer> CreateAll()
        {
            var layers = new List<IWorldViewLayer>();
            AddArena(layers);
            AddCharacters(layers);
            AddEnemies(layers);
            AddThreats(layers);
            AddShots(layers);
            AddEffects(layers);
            AddHud(layers);
            AddAudio(layers);
            AddStory(layers);
            return layers;
        }
    }
}
