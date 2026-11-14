using System;
using System.Collections.Generic;
using NUnit.Framework;
using Wizard.Solo.Data;
using Wizard.Solo.Meta;
using Wizard.Solo.Meta.Enhance;
using Wizard.Solo.Save;

namespace Wizard.Solo.Tests.Enhance
{
    /// <summary>정해 둔 값을 차례로 내는 난수. 판정 한 번(Next(10000)), 변이 후보 한 번(Next(후보 수)).</summary>
    sealed class ScriptedRandom : IEnhanceRandom
    {
        readonly Queue<int> _values = new Queue<int>();
        public int Calls;

        public ScriptedRandom(params int[] values) { Push(values); }

        public void Push(params int[] values)
        {
            foreach (int v in values) _values.Enqueue(v);
        }

        public int Next(int maxExclusive)
        {
            Calls++;
            if (_values.Count == 0) throw new InvalidOperationException("ScriptedRandom is empty");
            int v = _values.Dequeue();
            if (v < 0 || v >= maxExclusive) throw new InvalidOperationException("scripted value " + v + " is outside [0, " + maxExclusive + ")");
            return v;
        }
    }

    /// <summary>파일을 쓰지 않는 프로필 저장소(저장 횟수만 센다).</summary>
    sealed class MemoryProfileStore : IProfileStore
    {
        public int SaveCount;
        public ProfileData Current { get; set; }
        public string Directory => null;
        public event Action Saved;
        public void LoadOrCreate(out bool created, out bool restored) { created = false; restored = false; }
        public void Save() { SaveCount++; Saved?.Invoke(); }
        public bool RestorePrevious() => false;
    }

    sealed class EnhanceTestClock : IClock
    {
        public DateTime Now => new DateTime(2026, 11, 18, 15, 30, 0, DateTimeKind.Local);
        public DateTime UtcNow => new DateTime(2026, 11, 18, 6, 30, 0, DateTimeKind.Utc);
    }

    /// <summary>강화 테스트 한 벌: 진짜 콘텐츠(Resources/Data) + 새 프로필 + 정해 둔 난수.</summary>
    sealed class EnhanceKit
    {
        public readonly GameContext Ctx;
        public readonly MemoryProfileStore Store;
        public readonly ScriptedRandom Rng;
        public readonly EnhanceService Service;

        public EnhanceKit() : this(null) { }

        public EnhanceKit(IEnhanceRandom rng)
        {
            IContentDb db = TestContent.Db;
            var clock = new EnhanceTestClock();
            Store = new MemoryProfileStore { Current = NewProfile.Create(db, clock) };
            Ctx = new GameContext { Content = db, Clock = clock, Profiles = Store };
            Rng = new ScriptedRandom();
            Service = new EnhanceService(Ctx, rng ?? Rng);
            Ctx.Enhance = Service;
        }

        public EnhanceConfig Cfg => Ctx.Content.Enhance;
        public ProfileData Profile => Store.Current;
        public long Gold { get => Profile.Currencies.Gold; set => Profile.Currencies.Gold = value; }

        /// <summary>그 캐릭터의 시작 지팡이.</summary>
        public WandData Wand(int character = 1) => Profile.Wand(Profile.Character(character).EquippedWandId);

        /// <summary>지팡이를 (단계, floor_peak, 기운) 상태로 놓는다. record_peak = floor_peak(솔로에선 같이 움직인다).</summary>
        public WandData Set(int stage, int floorPeak, int aura, int character = 1)
        {
            WandData w = Wand(character);
            w.Stage = stage; w.FloorPeak = floorPeak; w.RecordPeak = floorPeak; w.Aura = aura;
            return w;
        }

        public void SetSlots(WandData w, int slots)
        {
            w.SlotsExtra = slots;
            w.Loadout.Extra.Clear();
            for (int i = 0; i < slots; i++) w.Loadout.Extra.Add(null);
        }

        public void SetMutations(WandData w, params string[] ids)
        {
            w.Mutations.Clear();
            for (int i = 0; i < ids.Length; i++) w.Mutations.Add(new MutationEntry { Id = ids[i], Seq = i + 1 });
        }

        public int Qty(string itemId) => Profile.Items.TryGetValue(itemId, out ItemStack s) ? s.Qty : 0;

        public void Give(string itemId, int qty) => Profile.Item(itemId).Qty += qty;

        /// <summary>골드와 강화석을 넉넉히 준다(비용이 아닌 것을 보는 테스트용).</summary>
        public void Rich()
        {
            Gold = 10_000_000;
            Give(Ids.Item.StoneLow, 100); Give(Ids.Item.StoneMid, 100); Give(Ids.Item.StoneHigh, 100);
        }

        // 구간마다 그 구간에 떨어지는 값 하나(구간의 첫 값).
        public int RollGreat(ProbBreakdown b) { Assert.Greater(b.GreatBp, 0, "no great band"); return 0; }
        public int RollSuccess(ProbBreakdown b) { Assert.Less(b.GreatBp, b.SuccessBp, "no success band"); return b.GreatBp; }
        public int RollMutation(ProbBreakdown b) { Assert.Less(b.SuccessBp, b.MutationEndBp, "no mutation band"); return b.SuccessBp; }
        public int RollFail(ProbBreakdown b) { Assert.Less(b.MutationEndBp, EnhanceRules.BpFull, "no fail band"); return b.MutationEndBp; }

        public ProbBreakdown Preview(WandData w, bool useCharm = false, IReadOnlyList<string> sacrifices = null) =>
            Service.Preview(w.Id, sacrifices, useCharm);

        EnhanceResult Run(WandData w, int roll, bool useCharm, IReadOnlyList<string> sacrifices, params int[] more)
        {
            Rng.Push(roll);
            Rng.Push(more);
            Result<EnhanceResult> r = Service.Enhance(w.Id, sacrifices, useCharm);
            Assert.AreEqual(ErrorCode.None, r.Error, "enhance was refused");
            AssertInvariants();
            return r.Value;
        }

        public EnhanceResult Great(WandData w, bool useCharm = false, IReadOnlyList<string> sacrifices = null) =>
            Run(w, RollGreat(Preview(w, useCharm, sacrifices)), useCharm, sacrifices);

        public EnhanceResult Succeed(WandData w, bool useCharm = false, IReadOnlyList<string> sacrifices = null) =>
            Run(w, RollSuccess(Preview(w, useCharm, sacrifices)), useCharm, sacrifices);

        public EnhanceResult Fail(WandData w, bool useCharm = false, IReadOnlyList<string> sacrifices = null) =>
            Run(w, RollFail(Preview(w, useCharm, sacrifices)), useCharm, sacrifices);

        /// <summary>변이 구간에 떨어뜨린다. candidateIndex = 후보 목록(번호 순, 가진 것 제외)에서 고를 자리.</summary>
        public EnhanceResult Mutate(WandData w, bool useCharm = false, int candidateIndex = 0, IReadOnlyList<string> sacrifices = null) =>
            Run(w, RollMutation(Preview(w, useCharm, sacrifices)), useCharm, sacrifices, candidateIndex);

        public EnhanceResult Resolve(WandData w, bool accept)
        {
            Assert.IsNotNull(w.PendingOffer, "no pending offer");
            Result<EnhanceResult> r = Service.ResolveMutation(w.Id, w.PendingOffer.AttemptId, accept);
            Assert.AreEqual(ErrorCode.None, r.Error, "resolve was refused");
            AssertInvariants();
            return r.Value;
        }

        /// <summary>07 요약서 3.1절 불변식을 모든 지팡이와 수량에 대해 본다(매 시도 뒤).</summary>
        public void AssertInvariants()
        {
            ProfileData p = Profile;
            foreach (WandData w in p.Wands)
            {
                string broken = EnhanceRules.CheckInvariants(Cfg, w, EnhanceRules.MutationCap(Cfg, Ctx.Content, w.Character));
                Assert.IsNull(broken, "wand " + w.Id + " invariant: " + broken);
                Assert.LessOrEqual(w.CarveBonus, 3, "carve_bonus <= 3");
                Assert.LessOrEqual(w.SlotsExtra, 3, "slots_extra <= 3");
                Assert.LessOrEqual(w.FloorPeak, Math.Min(13, w.Stage + 2), "floor_peak <= min(13, stage + 2)");
            }
            Assert.GreaterOrEqual(p.Currencies.Gold, 0, "gold >= 0");
            foreach (KeyValuePair<string, ItemStack> kv in p.Items)
            {
                Assert.GreaterOrEqual(kv.Value.Qty, 0, kv.Key + " qty >= 0");
                Assert.GreaterOrEqual(kv.Value.QtyBound, 0, kv.Key + " qty_bound >= 0");
                Assert.LessOrEqual(kv.Value.QtyBound, kv.Value.Qty, kv.Key + " qty_bound <= qty");
                Assert.LessOrEqual(kv.Value.QtySocketed, kv.Value.Qty, kv.Key + " qty_socketed <= qty");
            }
        }
    }
}
