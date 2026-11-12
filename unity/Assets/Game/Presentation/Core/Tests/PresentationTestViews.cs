#nullable enable
using System;
using Wizard.Content;
using Wizard.Core;
using Wizard.Sim.Events;
using Wizard.Sim.Input;
using Wizard.Sim.Spell;

namespace Wizard.Unity.Presentation.Core.Tests
{
    // 표시 시험의 가짜 뷰. 표시 계층이 무엇을 부르는지(읽기만 하는지) 센다. 적과 투사체도 뷰의 목록으로 준다(CONTRACTS 3.3, T-54).
    internal sealed class FakeSimView : ISimView
    {
        public PlayerView[] Players = Array.Empty<PlayerView>();
        public EnemyView[] EnemyList = Array.Empty<EnemyView>();
        public ProjectileView[] ProjectileList = Array.Empty<ProjectileView>();

        public uint Tick { get; set; }

        public int PlayerReads { get; private set; }

        public int EnemyReads { get; private set; }

        public int ProjectileReads { get; private set; }

        public int PlayerCount => Players.Length;

        public PlayerView Player(PlayerSlot slot)
        {
            PlayerReads++;
            return Players[slot.Value];
        }

        public ReadOnlySpan<EnemyView> Enemies
        {
            get
            {
                EnemyReads++;
                return EnemyList;
            }
        }

        public ReadOnlySpan<ProjectileView> Projectiles
        {
            get
            {
                ProjectileReads++;
                return ProjectileList;
            }
        }

        public ReadOnlySpan<TelegraphView> Telegraphs => ReadOnlySpan<TelegraphView>.Empty;

        public ReadOnlySpan<HazardView> Hazards => ReadOnlySpan<HazardView>.Empty;

        public WaveView Wave => default;

        public CampView Camp => default;

        public DuelView Duel => default;

        public static PlayerView MakePlayer(float x, float y, Dir8 facing = Dir8.E, StanceMode stance = StanceMode.Normal,
                                        int heatHu = 0, int critStartHu = 10200, HeatRingState ring = HeatRingState.Cold,
                                        bool exempt = false, int lockRemain = 0, int lockTotal = 0, bool invulnerable = false) =>
            new PlayerView(new EntityId(1), CharacterId.Baseline, Cell(x, y), facing, stance, 300, 300, invulnerable,
                           heatHu, critStartHu, ring, exempt, lockRemain, lockTotal, 0, 0, false, 0, 0, 0, 0, false, default,
                           RecentInputs.Empty, KeyMask.None);

        // 시작 상태가 서기 전의 플레이어(CONTRACTS 3.3: MaxHpPoints 가 0)
        public static PlayerView NotSpawned() => default;

        // 전장 좌표(y 아래로 +)의 적. 체력은 20 칸 기준의 hp, 공격 준비는 시작 틱과 길이(0이면 없음)
        public static EnemyView MakeEnemy(int id, EnemyTypeId type, float x, float y, float radius, int hp = 20, int maxHp = 20,
                                          uint windupStart = 0U, int windupTicks = 0) =>
            new EnemyView(new EntityId(id), type, Cell(x, y), Milli(radius), Fix64.FromInt(hp), Fix64.FromInt(maxHp), windupStart, windupTicks);

        public static ProjectileView MakePlayerBullet(int id, float x, float y, float radius, BlockId core = BlockId.MagicBolt) =>
            new ProjectileView(new EntityId(id), Faction.Player, new PlayerSlot(0), core, Element.Arcane, Cell(x, y),
                               new FixVec2(Fix64.Zero, Fix64.One), Milli(radius), 1);

        public static ProjectileView MakeEnemyShot(int id, float x, float y, float radius) =>
            new ProjectileView(new EntityId(id), Faction.Enemy, PlayerSlot.None, BlockId.None, Element.None, Cell(x, y),
                               new FixVec2(Fix64.Zero, Fix64.One), Milli(radius), 1);

        private static Fix64 Milli(float cells) => Fix64.FromRatio((long)Math.Round(cells * 1000f), 1000);

        // 칸 좌표(전장, y 아래로 +)를 고정소수점으로. 1000분의 1칸 단위로만 쓴다(시험 값)
        public static FixVec2 Cell(float x, float y) =>
            new FixVec2(Fix64.FromRatio((long)Math.Round(x * 1000f), 1000), Fix64.FromRatio((long)Math.Round(y * 1000f), 1000));
    }
}
