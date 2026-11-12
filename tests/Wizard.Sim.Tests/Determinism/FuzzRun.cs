using System;
using System.Collections.Generic;
using Wizard.Core;
using Wizard.ReplayCli;

namespace Wizard.Sim.Tests.Determinism
{
    // 되감기·직렬화 퍼저의 판 하나: 세계의 종류, 봇 꼴과 씨앗, 판 길이, 끊는 틱. 사례 씨앗 하나에서 만든다(Pcg32)
    internal sealed class FuzzRun
    {
        public static readonly string[] Profiles = { "play", "dense", "stance", "loadout" };

        public WorldKind Kind;
        public string Profile = "play";
        public ulong BotSeed;
        public ulong WorldSeed;
        public uint Ticks;
        public uint At;          // 끊는 틱(world.Tick == At 에서)
        public int K;            // 되감기: 저장 뒤 나아가는 틱 수(1~16)
        public bool Noisy;       // 되감기: 나아가는 동안 다른 입력을 넣는다(넷코드의 예측처럼). 복원 뒤에는 원래 입력
        public bool DirtyTarget; // 직렬화: 받는 세계를 먼저 다른 틱까지 돌려 둔다(ReadState 가 전부 덮어써야 한다)

        public FuzzRun Clone() => (FuzzRun)MemberwiseClone();

        // 종류: 연습 70%(판정기·탄·적·조립이 다 도는 세계), 결투 20%(두 사람), 무한 웨이브 10%
        public static FuzzRun Generate(ref Pcg32 rng, uint ticks, int maxK)
        {
            int roll = rng.NextInt(10);
            var r = new FuzzRun
            {
                Kind = roll < 7 ? WorldKind.Practice : roll < 9 ? WorldKind.Duel : WorldKind.Endless,
                Profile = Profiles[rng.NextInt(Profiles.Length)],
                BotSeed = ((ulong)rng.NextU32() << 32) | rng.NextU32(),
                WorldSeed = ((ulong)rng.NextU32() << 32) | rng.NextU32(),
                Ticks = ticks,
                K = 1 + rng.NextInt(maxK),
                Noisy = rng.NextInt(2) == 0,
                DirtyTarget = rng.NextInt(2) == 0,
            };
            r.At = (uint)rng.NextInt((int)(ticks - (uint)r.K));
            return r;
        }

        public override string ToString() =>
            "kind=" + ReplayWorlds.KindName(Kind) + " profile=" + Profile + " botSeed=0x" + BotSeed.ToString("x16") + " worldSeed=0x" +
            WorldSeed.ToString("x16") + " ticks=" + Ticks + " at=" + At + " k=" + K + " noisy=" + Noisy + " dirtyTarget=" + DirtyTarget;

        // 줄이기 후보(앞일수록 크게 줄인다). 모두 원래보다 엄격히 작다: 판 길이, 끊는 틱, k, 깃발, 꼴
        public IEnumerable<FuzzRun> Smaller()
        {
            uint minTicks = At + (uint)K + 1;
            if (Ticks > minTicks)
            {
                FuzzRun c = Clone();
                c.Ticks = minTicks;
                yield return c;
            }
            if (At > 0)
            {
                FuzzRun c = Clone();
                c.At = 0;
                c.Ticks = Math.Min(Ticks, (uint)K + 1);
                yield return c;
                FuzzRun h = Clone();
                h.At = At / 2;
                yield return h;
            }
            if (K > 1)
            {
                FuzzRun c = Clone();
                c.K = 1;
                yield return c;
            }
            if (Noisy)
            {
                FuzzRun c = Clone();
                c.Noisy = false;
                yield return c;
            }
            if (DirtyTarget)
            {
                FuzzRun c = Clone();
                c.DirtyTarget = false;
                yield return c;
            }
            if (Profile != "play")
            {
                FuzzRun c = Clone();
                c.Profile = "play";
                yield return c;
            }
        }
    }
}
