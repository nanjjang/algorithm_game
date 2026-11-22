using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using Wizard.Solo.Data;
using Wizard.Solo.Meta;
using Wizard.Solo.Meta.Enhance;
using Wizard.Solo.Meta.Inventory;
using Wizard.Solo.Meta.Settlement;
using Wizard.Solo.Meta.Shop;
using Wizard.Solo.Save;
using Wizard.Solo.Sim;
using Wizard.Solo.Tests.Enhance;

namespace Wizard.Solo.Tests.Integration
{
    /// <summary>
    /// 통합 테스트 한 벌: 진짜 콘텐츠(Resources/Data) + 임시 폴더의 진짜 ProfileStore + 진짜 메타 서비스 + 진짜 시뮬레이션 모듈 전부
    /// (SimModuleSet.CreateDefault). 가짜는 강화 난수(정해 둔 값)와 시계뿐이다. 00 문서 12절.
    /// </summary>
    internal sealed class IntegrationKit : IDisposable
    {
        sealed class Clock : IClock
        {
            public DateTime Now => new DateTime(2026, 11, 18, 15, 30, 0, DateTimeKind.Local);
            public DateTime UtcNow => Now.ToUniversalTime();
        }

        public readonly string Root;
        public readonly GameContext Ctx;
        public readonly ProfileStore Store;
        public readonly ScriptedRandom EnhanceRng = new ScriptedRandom();
        public SimWorld W { get; private set; }
        public RunConfig Config { get; private set; }
        public readonly List<SimEvent> AllEvents = new List<SimEvent>();

        readonly Clock _clock = new Clock();
        readonly bool[] _dirDown = new bool[4];
        bool _fireDown;

        /// <summary>이 벌이 쓰는 콘텐츠. 기본은 명세 값(TestContent.Db). 재미 조정 손잡이를 재는 테스트는 FunPassKit.TunedDb를 준다.</summary>
        public readonly IContentDb Db;
        public ProfileData Profile => Store.Current;
        public PlayerState P => W.P0;

        public IntegrationKit(IContentDb db = null)
        {
            Db = db ?? TestContent.Db;
            Root = Path.Combine(Path.GetTempPath(), "wizard-solo-integ-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
            Store = NewStore();
            Store.LoadOrCreate(out _, out _);
            Ctx = new GameContext { Content = Db, Profiles = Store, Clock = _clock, Rules = SimModuleSet.CreateDefault() };
            Ctx.Settlement = new SettlementService(Ctx, new Pcg32(20261118UL));
            Ctx.Wands = new WandService(Ctx);
            Ctx.Enhance = new EnhanceService(Ctx, EnhanceRng);
            Ctx.Shop = new ShopService(Ctx);
        }

        ProfileStore NewStore() =>
            new ProfileStore(SavePaths.ProfileDirectory(Root), () => NewProfile.Create(Db, _clock), Db.DataVersion);

        /// <summary>디스크에서 프로필을 다시 읽은 새 저장소(다음 실행을 흉내 낸다).</summary>
        public ProfileStore Relaunch()
        {
            var s = NewStore();
            s.LoadOrCreate(out bool created, out _);
            Assert.IsFalse(created, "profile should have been saved");
            return s;
        }

        public void Dispose()
        {
            try { if (Directory.Exists(Root)) Directory.Delete(Root, true); } catch (IOException) { }
        }

        // ------------------------------------------------------------ 판 시작

        /// <summary>진짜 WandService가 만든 RunConfig로 진짜 세계를 연다(시작 웨이브 1).</summary>
        public SimWorld StartRun(int character, ulong seed = 1, Action<RunConfig> tweak = null)
        {
            var wand = Profile.Characters[character - 1].EquippedWandId;
            var r = Ctx.Wands.BuildRunConfig(character, wand, 1, seed);
            Assert.IsTrue(r.Ok, "BuildRunConfig " + character + ": " + r.Error);
            return Begin(r.Value, tweak);
        }

        /// <summary>RunConfig를 손으로 만들어 연다(웨이브 10 같은 곳에서 바로 시작).</summary>
        public SimWorld Begin(RunConfig cfg, Action<RunConfig> tweak = null)
        {
            tweak?.Invoke(cfg);
            Config = cfg;
            W = new SimWorld(Db, SimModuleSet.CreateDefault(), cfg.Seed);
            AllEvents.Clear();
            for (int i = 0; i < _dirDown.Length; i++) _dirDown[i] = false;
            _fireDown = false;
            W.StartRun(cfg);
            Drain();
            return W;
        }

        // ------------------------------------------------------------ 틱

        void Drain()
        {
            AllEvents.AddRange(W.Events);
            W.Events.Clear();
        }

        public int Count(SimEventType type)
        {
            int n = 0;
            for (int i = 0; i < AllEvents.Count; i++) if (AllEvents[i].Type == type) n++;
            return n;
        }

        public List<SimEvent> Events(SimEventType type)
        {
            var l = new List<SimEvent>();
            for (int i = 0; i < AllEvents.Count; i++) if (AllEvents[i].Type == type) l.Add(AllEvents[i]);
            return l;
        }

        /// <summary>그 종류 이벤트가 처음 난 틱(없으면 −1).</summary>
        public int FirstTick(SimEventType type)
        {
            for (int i = 0; i < AllEvents.Count; i++) if (AllEvents[i].Type == type) return AllEvents[i].Tick;
            return -1;
        }

        public void SetKey(LogicalKey key, bool down)
        {
            int idx = (int)key;
            if (key == LogicalKey.Fire)
            {
                if (_fireDown == down) return;
                _fireDown = down;
            }
            else if (idx >= 0 && idx < 4)
            {
                if (_dirDown[idx] == down) return;
                _dirDown[idx] = down;
            }
            W.EnqueueInput(0, new InputEvent(key, down, SimConst.TickStartU(W.Tick)));
        }

        public void ReleaseAll()
        {
            SetKey(LogicalKey.Fire, false);
            for (int i = 0; i < 4; i++) SetKey((LogicalKey)i, false);
        }

        /// <summary>한 틱: 이 틱의 입력을 넣고 Step, 이벤트를 모은다. 예외는 그대로 올린다.</summary>
        public void Step(bool god = false)
        {
            if (god) P.InvulnUntilTick = W.Tick + 2;
            W.Step();
            Drain();
        }

        public void Step(int ticks, bool god = false)
        {
            for (int i = 0; i < ticks; i++) Step(god);
        }

        // ------------------------------------------------------------ 봇

        /// <summary>가장 가까운 살아 있는 적(없으면 null).</summary>
        public EnemyState NearestEnemy()
        {
            EnemyState best = null;
            float bd = float.MaxValue;
            for (int i = 0; i < W.Enemies.Count; i++)
            {
                var e = W.Enemies[i];
                if (!e.Targetable) continue;
                float d = (e.Pos - P.Pos).sqrMagnitude;
                if (d < bd) { bd = d; best = e; }
            }
            return best;
        }

        /// <summary>가장 가까운 적 쪽으로 걸으며 J를 누르고 있는다(전투 중). 적이 없으면 멈춘다.</summary>
        public void CombatBot(bool fire = true)
        {
            var e = NearestEnemy();
            SetKey(LogicalKey.Fire, fire);
            if (e == null)
            {
                for (int i = 0; i < 4; i++) SetKey((LogicalKey)i, false);
                return;
            }
            Vector2 d = e.Pos - P.Pos;
            bool horizontal = Mathf.Abs(d.x) > Mathf.Abs(d.y);
            // 너무 가까우면 멈춰서 쏜다(몸이 닿지 않게). 좌표는 y 아래가 +.
            bool close = d.sqrMagnitude < 2.5f * 2.5f;
            SetKey(LogicalKey.Left, !close && horizontal && d.x < 0);
            SetKey(LogicalKey.Right, !close && horizontal && d.x > 0);
            SetKey(LogicalKey.Up, !close && !horizontal && d.y < 0);
            SetKey(LogicalKey.Down, !close && !horizontal && d.y > 0);
            if (close)
            {
                // 가까운 적을 보려면 그쪽 방향키를 한 틱 눌러야 시야가 바뀐다 → 짧게 누른다.
                if (horizontal) SetKey(d.x < 0 ? LogicalKey.Left : LogicalKey.Right, (W.Tick & 1) == 0);
                else SetKey(d.y < 0 ? LogicalKey.Up : LogicalKey.Down, (W.Tick & 1) == 0);
            }
        }

        /// <summary>
        /// 판 전체를 알아서 진행하는 봇: 전투는 CombatBot, 전리품은 카드를 고르고 확정, 야영은 해금 차례를 고르고 출발.
        /// until이 참이 되거나 maxTicks가 지나면 멈춘다. 돌려주는 값 = until이 참이 됐나.
        /// </summary>
        public bool AutoPlay(int maxTicks, Func<SimWorld, bool> until, bool god = true)
        {
            for (int t = 0; t < maxTicks; t++)
            {
                if (until(W)) return true;
                var run = W.Run;
                switch (run.Phase)
                {
                    case RunPhase.LootPick:
                        DriveLoot();
                        ReleaseAll();
                        break;
                    case RunPhase.StartCamp:
                    case RunPhase.Camp:
                        DriveCamp();
                        ReleaseAll();
                        break;
                    case RunPhase.RunEnded:
                        return until(W);
                    default:
                        CombatBot();
                        break;
                }
                Step(god);
            }
            return until(W);
        }

        void DriveLoot()
        {
            var loot = W.Run.Loot;
            if (loot == null) return;
            if (loot.OverflowToDiscard > 0)
            {
                W.Enqueue(new RunCommand(RunCommandKind.LootDiscard, 100));
                return;
            }
            for (int i = 0; i < loot.Cards.Count && i < loot.PickCount; i++)
                W.Enqueue(new RunCommand(RunCommandKind.LootPick, i));
            W.Enqueue(new RunCommand(RunCommandKind.LootConfirm));
        }

        void DriveCamp()
        {
            var run = W.Run;
            if (run.CurrentArtOffer != null && run.CurrentArtOffer.Candidates.Count > 0)
                W.Enqueue(new RunCommand(RunCommandKind.ArtPick, 0, 0, run.CurrentArtOffer.Candidates[0], 0));
            else
                W.Enqueue(new RunCommand(RunCommandKind.CampDepart));
        }
    }
}
