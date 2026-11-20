using System.Collections.Generic;
using UnityEngine;
using Wizard.Solo.Data;

namespace Wizard.Solo.Sim.Arts
{
    /// <summary>결정 지갑을 Player 모듈로 잇는다(결제는 Player.CanAfford / SpendCrystals / SpendAllCrystals. 00 문서 5.4절 (사)).</summary>
    public sealed class PlayerArtWallet : IArtWallet
    {
        SimWorld _w;
        PlayerState _p;
        public void Bind(SimWorld w, PlayerState p) { _w = w; _p = p; }
        public int Crystals => _p.Crystals;
        public int Debt => _p.Debt;
        public bool HasCredit => _p.Derived.HasCredit;
        public bool CanAfford(int cost, bool allowCredit) => _w.Mods.Player.CanAfford(_w, _p, cost, allowCredit);
        public int Spend(int cost, bool allowCredit) => _w.Mods.Player.SpendCrystals(_w, _p, cost, allowCredit);
        public int SpendAll() => _w.Mods.Player.SpendAllCrystals(_w, _p);
    }

    /// <summary>발동이 정해진 비기 하나(준비 시간·만개 반복을 기다린다). 판 안의 상태.</summary>
    public sealed class ArtCast
    {
        public ArtDef Def;
        /// <summary>판정이 적용된 틱.</summary>
        public int JudgeTick;
        /// <summary>효과가 시작되는 틱(판정 틱 + 준비 시간).</summary>
        public int StartTick;
        /// <summary>만개: 다음에 낼 파동 번호(0부터)와 전체 반복 수.</summary>
        public int NextIndex;
        public int Repeats = 1;
        public int CrystalsAfter;
        /// <summary>시전 순간의 캐릭터 배율(쓴 뒤 결정 수 기준).</summary>
        public float CharMul = 1f;
        public bool HasPhysical;
        public Dir8 Physical;
        /// <summary>비기 한 번의 탄 예산(남은 양). Capped가 거짓이면 제한 없음.</summary>
        public bool Capped;
        public int BudgetLeft;
        public bool Done;
    }

    /// <summary>플레이어별 비기 상태(p.ArtScratch). 주인: Arts.</summary>
    public sealed class ArtPlayerState
    {
        public CommandTrie Trie;
        public CommandParser Parser;
        public readonly PlayerArtWallet Wallet = new PlayerArtWallet();
        public bool HasPhysical;
        public Dir8 LastPhysical;
        public bool BufferedHasPhysical;
        public Dir8 BufferedPhysical;
        public readonly List<ArtCast> Casts = new List<ArtCast>();
        /// <summary>비기·사본의 발사 번호(음수로 센다 — J 발사 번호와 겹치지 않는다).</summary>
        public int NextShotId = -1;
        public bool HasLastDecision;
        public ArtDecision LastDecision;
        /// <summary>고유 비기 강화의 진행 중인 단계들(불기둥 고리, 낙뢰 궤적, 책장 소용돌이, 얼음 가시 줄, 그림자 손 짓누름). Docs/12 4절.</summary>
        public readonly List<ArtSequence> Sequences = new List<ArtSequence>();
        /// <summary>진행 중인 파이널 어빌리티(게이지 가득 + K). ArtSystem.Finals.cs.</summary>
        public readonly List<FinalRun> Finals = new List<FinalRun>();
        /// <summary>두 번째 K를 기다리는 첫 K(파이널 어빌리티 두 번 누르기, ArtSystem.Finals.cs).</summary>
        public bool PendingTap;
        public long PendingTapU;
        /// <summary>이 틱이 지나면 첫 K를 마력 방출로 낸다.</summary>
        public int PendingTapUntilTick;
    }

    /// <summary>
    /// 비기 모듈: 트라이·판정기·결제, 비기 15종, 플레이어 지대, 해금 차례. 04 요약서.
    /// 판정·결제는 누른 시각으로(2단계), 효과는 5단계에서. 공격 허용은 SimWorld.AttackAllowed 하나로 본다(공통 규칙 7).
    /// </summary>
    public sealed partial class ArtSystem : ArtModule
    {
        // ------------------------------------------------------------ 상태 접근

        public static ArtPlayerState StateOf(PlayerState p) => p != null ? p.ArtScratch as ArtPlayerState : null;

        /// <summary>마지막으로 적용된 K 판정(발동·불발). 연습 화면의 실패 원인 줄(늦은 양 u)용.</summary>
        public static bool TryGetLastDecision(PlayerState p, out ArtDecision decision)
        {
            var s = StateOf(p);
            if (s == null || !s.HasLastDecision) { decision = default; return false; }
            decision = s.LastDecision;
            return true;
        }

        ArtPlayerState Ensure(SimWorld w, PlayerState p)
        {
            if (p == null) return null;
            var s = StateOf(p);
            if (s != null) return s;
            if (w == null || w.Content == null || p.CharId == null) return null;
            SetupPlayer(w, p);
            return StateOf(p);
        }

        // ------------------------------------------------------------ 판 시작·초기화

        public override void SetupPlayer(SimWorld w, PlayerState p)
        {
            if (w == null || p == null || w.Content == null) return;
            var s = new ArtPlayerState();
            s.Trie = CommandTrie.Build(w.Content, p.CharId);
            s.Parser = new CommandParser(s.Trie);
            s.Parser.SetStance(p.InStance);
            p.ArtScratch = s;

            // 시작 해금(마력 방출, 반동 사격, 고유)과 프로필에 남은 해금. 이 캐릭터 트라이에 있는 것만.
            var nodes = s.Trie.Nodes;
            var cfg = w.Run != null ? w.Run.Config : null;
            for (int i = 0; i < nodes.Count; i++)
            {
                AddStartUnlock(p, nodes[i].KArt, cfg);
                AddStartUnlock(p, nodes[i].JustArt, cfg);
            }
            p.KilnAbsorbLeftHu = 0;
            p.InKilnZone = false;
        }

        static void AddStartUnlock(PlayerState p, ArtDef art, RunConfig cfg)
        {
            if (art == null) return;
            bool atStart = art.Unlock != null && art.Unlock.AtStart;
            bool fromProfile = cfg != null && cfg.UnlockedArts != null && cfg.UnlockedArts.Contains(art.Id);
            if (atStart || fromProfile) p.UnlockedArts.Add(art.Id);
        }

        public override void OnCommandReset(SimWorld w, PlayerState p, CommandResetReason reason)
        {
            var s = Ensure(w, p);
            if (s == null) return;
            // 자세 여부: 진입·해제는 까닭이 곧 값이고, 그 밖(반전·일시정지·웨이브 시작)은 플레이어 상태를 따른다.
            if (reason == CommandResetReason.StanceEnter) s.Parser.SetStance(true);
            else if (reason == CommandResetReason.StanceExit) s.Parser.SetStance(false);
            else s.Parser.SetStance(p.InStance);
            s.Parser.Reset(reason);
            s.HasPhysical = false;
        }

        // ------------------------------------------------------------ 입력 (2단계)

        public override void OnDirectionPress(SimWorld w, PlayerState p, CmdDir dir, Dir8 physical, long timeU)
        {
            var s = Ensure(w, p);
            if (s == null) return;
            // 방향은 공격이 막힌 단계에서도 늘 판정기에 넣는다(판정기는 계속 듣는다).
            s.Parser.SetStance(p.InStance);
            if (!s.Parser.OnDirection(dir, timeU)) return;
            s.HasPhysical = true;
            s.LastPhysical = physical;
            w.Emit(SimEventType.CommandDirInput, p.Slot, a: (int)dir, b: s.Parser.LastDirChained ? 1 : 0);
        }

        public override void OnSkillPress(SimWorld w, PlayerState p, long timeU)
        {
            var s = Ensure(w, p);
            if (s == null) return;
            if (!w.AttackAllowed(p))
            {
                // 불발 이벤트 없이 버린다. 결정을 쓰지 않고 보관된 K도 버린다.
                s.Parser.DropBuffered();
                return;
            }
            s.Parser.SetStance(p.InStance);
            s.Wallet.Bind(w, p);
            if (s.PendingTap)
            {
                // 두 번째 K: 창 안이면 파이널, 아니면 첫 K를 마력 방출로 내고 이번 K는 보통으로.
                s.PendingTap = false;
                if (timeU - s.PendingTapU <= DoubleTapU(w) && TryStartFinal(w, p, s)) return;
                PressNormal(w, p, s, s.PendingTapU);
            }
            else if (FinalReady(w, p) && PlainKIsBurst(s, p, timeU))
            {
                // 첫 K: 두 번째 K를 기다린다(5단계가 창이 지나면 마력 방출로 낸다).
                s.PendingTap = true;
                s.PendingTapU = timeU;
                s.PendingTapUntilTick = w.Tick + Mathf.Max(1, SimConst.Ticks(DoubleTapU(w) / (float)SimConst.UnitsPerSecond));
                return;
            }
            PressNormal(w, p, s, timeU);
        }

        void PressNormal(SimWorld w, PlayerState p, ArtPlayerState s, long timeU)
        {
            s.Parser.SetStance(p.InStance);
            s.Wallet.Bind(w, p);
            bool hasPhysical = s.HasPhysical;
            Dir8 physical = s.LastPhysical;
            ArtDecision d;
            var r = s.Parser.OnSkillPress(timeU, s.Wallet, p.UnlockedArts, out d);
            if (r == SkillPressResult.Ignored) return;
            s.HasPhysical = false;
            if (r == SkillPressResult.Buffered)
            {
                s.BufferedHasPhysical = hasPhysical;
                s.BufferedPhysical = physical;
                w.Emit(SimEventType.ArtBuffered, p.Slot);
                return;
            }
            w.Emit(SimEventType.CommandReset, p.Slot, a: (int)(d.IsFire ? CommandResetReason.Fired : CommandResetReason.Fizzled));
            Deliver(w, p, s, in d, hasPhysical, physical);
        }

        /// <summary>판정 결과를 적용한다: 불발이면 이벤트만, 발동이면 이벤트 + 효과 예약(판정 틱 + 준비 시간).</summary>
        void Deliver(SimWorld w, PlayerState p, ArtPlayerState s, in ArtDecision d, bool hasPhysical, Dir8 physical)
        {
            s.LastDecision = d;
            s.HasLastDecision = true;
            if (!d.IsFire)
            {
                w.Emit(SimEventType.ArtFizzled, p.Slot, a: (int)d.Reason, b: d.LateIndex, f: LateText.Seconds(d.LateByU), id: d.SelectedArt);
                return;
            }
            var def = w.Content.Art(d.Art);
            if (def == null) return;
            w.Emit(SimEventType.ArtFired, p.Slot, a: d.ViaFallback ? 1 : 0, b: d.IsJust ? 1 : 0, f: d.Repeats, id: d.Art, pos: p.Pos);

            var cast = new ArtCast
            {
                Def = def,
                JudgeTick = w.Tick,
                StartTick = w.Tick + SimConst.Ticks(def.Effect.PrepSeconds),
                Repeats = def.CostIsAll ? Mathf.Max(1, d.Repeats) : 1,
                CrystalsAfter = d.CrystalsAfter,
                CharMul = w.Mods.Traits.CharacterMul(w, p, d.CrystalsAfter),
                HasPhysical = hasPhysical,
                Physical = physical
            };
            int cap = w.Content.Const != null ? w.Content.Const.ArtShotCap : 0;
            cast.Capped = cap > 0;
            cast.BudgetLeft = cap;
            s.Casts.Add(cast);
            // 만개 발동 수(손맛 칭호)·튜토리얼 ④는 Run이 센다(RunState의 주인).
            w.Mods.Run.OnArtFired(w, p, def.Id);
        }

        // ------------------------------------------------------------ 5단계

        public override void Stage5_Arts(SimWorld w)
        {
            for (int i = 0; i < w.Players.Length; i++)
            {
                var p = w.Players[i];
                if (p == null || !p.Active) continue;
                var s = StateOf(p);
                if (s == null) continue;
                if (!w.AttackAllowed(p))
                {
                    // 공격이 막힌 단계: 보관된 K와 아직 안 나간 효과를 버린다(야영·보스 등장·전리품 중에 비기가 나가지 않는다).
                    s.Parser.DropBuffered();
                    s.Casts.Clear();
                    s.Sequences.Clear();
                    s.Finals.Clear();
                    s.PendingTap = false;
                    continue;
                }
                if (s.PendingTap && w.Tick > s.PendingTapUntilTick)
                {
                    s.PendingTap = false;
                    PressNormal(w, p, s, s.PendingTapU);
                }
                ArtDecision d;
                if (s.Parser.TryTakeBuffered(w.NowU, out d))
                    Deliver(w, p, s, in d, s.BufferedHasPhysical, s.BufferedPhysical);
                RunCasts(w, p, s);
                RunSequences(w, p, s);
                RunFinals(w, p, s);
            }
            UpdateZones(w);
        }

        void RunCasts(SimWorld w, PlayerState p, ArtPlayerState s)
        {
            for (int i = 0; i < s.Casts.Count; i++)
            {
                var c = s.Casts[i];
                if (!c.Done) StepCast(w, p, s, c);
            }
            s.Casts.RemoveAll(c => c.Done);
        }

        // ------------------------------------------------------------ 미리보기·최근 입력

        public override ArtPreview Preview(SimWorld w, PlayerState p, long nowU)
        {
            var s = Ensure(w, p);
            if (s == null) return default;
            s.Parser.SetStance(p.InStance);
            s.Wallet.Bind(w, p);
            var pv = s.Parser.Preview(nowU, s.Wallet, p.UnlockedArts);
            var m = pv.Main;
            var r = new ArtPreview
            {
                ArtId = m.Art,
                Cost = CostOf(w, m.Art, p.Crystals),
                DebtUsed = m.IsFire ? m.DebtAdded : 0,
                Fizzle = !m.IsFire,
                Reason = m.Reason,
                LateIndex = m.LateIndex,
                LateSeconds = LateText.Seconds(m.LateByU),
                Buffered = s.Parser.HasBuffered,
                CooldownRatio = s.Parser.CooldownRatio(nowU),
                LastDirTimeU = s.Parser.LastDirTimeU,
                JustWindowU = s.Trie.Timings.JustWindowU
            };
            // 결정이 충분하면 K 두 번으로 나갈 파이널 어빌리티를 함께 보여 준다.
            if (FinalReady(w, p)) { r.FinalArtId = FinalIdFor(w, p); r.FinalCost = p.Crystals; }
            if (pv.HasJustLine)
            {
                r.HasJustLine = true;
                r.JustArtId = pv.Just.Art;
                r.JustCost = CostOf(w, pv.Just.Art, p.Crystals);
            }
            return r;
        }

        /// <summary>그 비기의 결정 값(만개는 지금 가진 결정 전부, 최소 min_cost).</summary>
        static int CostOf(SimWorld w, string artId, int crystals)
        {
            var def = artId != null ? w.Content.Art(artId) : null;
            if (def == null) return 0;
            if (def.CostIsAll) return Mathf.Max(def.MinCost, crystals);
            return def.CostInt;
        }

        /// <summary>최근 방향 입력(오래된 것부터, 최대 5개). into를 비우고 채운다.</summary>
        public override void GetRecentInputs(PlayerState p, List<RecentInput> into)
        {
            if (into == null) return;
            into.Clear();
            var s = StateOf(p);
            if (s != null) s.Parser.GetRecentInputs(into);
        }

        // ------------------------------------------------------------ 해금 (11절)

        public override ArtOffer BuildOffer(SimWorld w, PlayerState p, int turnWave)
        {
            var offer = new ArtOffer { TurnWave = turnWave };
            if (w == null || p == null || w.Content == null) return offer;
            var arts = CommandTrie.ArtsOf(w.Content, p.CharId);
            var eligible = new List<string>();
            for (int i = 0; i < arts.Count; i++)
            {
                var a = arts[i];
                if (a.IsUnique || CommandParser.IsUnlocked(p.UnlockedArts, a)) continue;
                var u = a.Unlock;
                if (u != null && !string.IsNullOrEmpty(u.Prereq)
                    && !CommandParser.IsUnlocked(p.UnlockedArts, w.Content.Art(u.Prereq))) continue;
                if (u != null && turnWave < u.MinWave) continue;
                eligible.Add(a.Id);
            }
            int want = w.Content.Camp != null ? w.Content.Camp.ArtCandidates : eligible.Count;
            if (eligible.Count <= want)
            {
                offer.Candidates.AddRange(eligible);
                return offer;
            }
            // 균등 무작위(판 씨앗에서 갈라 낸 흐름): 남은 것 중 하나씩 뽑는다.
            var rng = w.Rng(RngStream.ArtUnlock);
            for (int k = 0; k < want; k++)
            {
                int j = rng.Range(0, eligible.Count);
                offer.Candidates.Add(eligible[j]);
                eligible.RemoveAt(j);
            }
            return offer;
        }

        public override bool Unlock(SimWorld w, PlayerState p, string artId)
        {
            if (w == null || p == null || artId == null || w.Content == null) return false;
            var def = w.Content.Art(artId);
            if (def == null) return false;
            // 이 캐릭터의 트라이에 있는 비기만.
            var arts = CommandTrie.ArtsOf(w.Content, p.CharId);
            if (!arts.Contains(def)) return false;
            if (p.UnlockedArts.Contains(artId)) return false;
            p.UnlockedArts.Add(artId);
            w.Emit(SimEventType.ArtUnlocked, p.Slot, id: artId);
            return true;
        }

        public override List<ArtTrieNodeView> DescribeTrie(IContentDb db, string charId, ICollection<string> unlocked, ICollection<string> candidates)
        {
            var list = new List<ArtTrieNodeView>();
            if (db == null) return list;
            var trie = CommandTrie.Build(db, charId);
            var nodes = trie.Nodes;
            for (int i = 0; i < nodes.Count; i++)
            {
                var n = nodes[i];
                var v = new ArtTrieNodeView
                {
                    NodeId = n.Id,
                    ParentId = n.Parent,
                    Depth = n.Depth,
                    Path = (CmdDir[])n.Path.Clone(),
                    ArtId = n.KArtId,
                    JustArtId = n.JustArtId,
                    GapSeconds = n.Depth >= 2 ? n.InEdgeGapSeconds : trie.Timings.GapDefaultSeconds,
                    GapIsDefault = n.Depth < 2 || n.InEdgeGapU == trie.Timings.GapDefaultU
                };
                // 기술 없는 통과 노드와 고유 비기는 늘 Unlocked. Locked·Candidate는 공용 비기에만.
                if (n.KArt == null || CommandParser.IsUnlocked(unlocked, n.KArt)) v.State = ArtNodeState.Unlocked;
                else if (candidates != null && candidates.Contains(n.KArt.Id)) v.State = ArtNodeState.Candidate;
                else v.State = ArtNodeState.Locked;
                if (n.JustArt == null || CommandParser.IsUnlocked(unlocked, n.JustArt)) v.JustState = ArtNodeState.Unlocked;
                else if (candidates != null && candidates.Contains(n.JustArt.Id)) v.JustState = ArtNodeState.Candidate;
                else v.JustState = ArtNodeState.Locked;
                list.Add(v);
            }
            return list;
        }
    }
}
