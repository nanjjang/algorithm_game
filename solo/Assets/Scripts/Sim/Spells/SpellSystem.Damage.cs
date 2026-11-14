using System;
using UnityEngine;

namespace Wizard.Solo.Sim.Spells
{
    // 적이 받는 피해: 내 탄 적중과 직접 피해(DealDirect)가 모이는 한 함수. 00 문서 5.4절 (나), 03 요약서 7.3·9절.
    public sealed partial class SpellSystem
    {
        struct DamageInput
        {
            public int AttackerSlot;
            /// <summary>속성·대상 상태·적 쪽 조정을 곱하기 전 값.</summary>
            public float Amount;
            public string Element;
            public DamageOrigin Origin;
            public ShotSource Source;
            public int ShotId;
            public bool IsCrit;
            public bool IgnoresArmor;
            public Vector2 FromPos;
            public Vector2 HitPos;
            /// <summary>맞힌 탄(직접 피해면 null). 서리·잠식·얼음 송곳·반동 넉백을 건다.</summary>
            public ProjectileEntity Projectile;
        }

        /// <summary>탄 하나가 적 하나를 맞혔다. extraMul = 전이 배율(연쇄 번개 0.7^k), 그 밖은 1.</summary>
        DamageResult HitEnemy(SimWorld w, SpellTuning t, ProjectileEntity pr, EnemyState e, float extraMul, Vector2 hitPos, Vector2 fromPos)
        {
            var d = new DamageInput
            {
                AttackerSlot = pr.OwnerSlot,
                Amount = pr.Block.BaseDamage * pr.DamageMul * extraMul,
                // EL-1: 피해 속성 = 맞힌 투사체 블록의 속성(변형의 속성은 바꾸지 않는다).
                Element = pr.Block.Element,
                Origin = DamageOrigin.Projectile,
                Source = pr.Source,
                ShotId = pr.ShotId,
                IsCrit = pr.IsCrit,
                IgnoresArmor = false,
                // 온 쪽(정면 철갑 판정용): 탄이 날아온 구간의 시작점, 폭발이면 중심, 전이면 앞 대상.
                FromPos = fromPos,
                HitPos = hitPos,
                Projectile = pr
            };
            return ApplyDamage(w, t, e, d);
        }

        public override DamageResult DealDirect(SimWorld w, in DirectDamage dmg)
        {
            var e = w.FindEnemy(dmg.TargetEnemyId);
            if (e == null) return new DamageResult { Immune = true };
            Vector2 from = e.Pos;
            if (dmg.AttackerSlot >= 0 && dmg.AttackerSlot < w.Players.Length) from = w.Players[dmg.AttackerSlot].Pos;
            var d = new DamageInput
            {
                AttackerSlot = dmg.AttackerSlot,
                Amount = dmg.Amount,
                Element = dmg.Element,
                Origin = dmg.Origin,
                // 직접 피해는 J 발사가 아니다(콤보·임계 셈에서 빠지게 한다).
                Source = ShotSource.ArtOther,
                ShotId = 0,
                IsCrit = false,
                IgnoresArmor = dmg.IgnoresArmor,
                FromPos = from,
                HitPos = e.Pos,
                Projectile = null
            };
            return ApplyDamage(w, Tune(w.Content), e, d);
        }

        /// <summary>
        /// 피해 한 함수. 순서(00 문서 5.4절 (나)):
        /// 1 대상이 Targetable이 아니면 끝 → 2 속성 배율 × 잠식(이번 피해 전의 상태) → 3 적 쪽 조정 → 4 체력·바닥·PendingDeath·KillingHit
        /// → 5 상태 적용 → 6 훅(콤보 → 살얼음) → 8 EnemyDamaged → 9 약점·저항 알아냄. (7 트리거는 부른 쪽이 이 함수 바로 뒤에 한다.)
        /// </summary>
        DamageResult ApplyDamage(SimWorld w, SpellTuning t, EnemyState e, in DamageInput d)
        {
            var res = default(DamageResult);
            // 1. 죽어 가는 적(PendingDeath)·땅속 적은 없는 것이다(공통 규칙 2).
            if (e == null || !e.Targetable) { res.Immune = true; return res; }

            // 2. 속성 배율, 잠식(잠식을 거는 탄 자신에게는 아직 안 걸려 있다 → 다음 피해부터).
            Affinity affinity = AffinityOf(e, d.Element);
            float amount = d.Amount;
            if (affinity == Affinity.Weak) amount *= t.WeakMul;
            else if (affinity == Affinity.Resist) amount *= t.ResistMul;
            bool corroded = e.Corroded(w.Tick);
            if (corroded) amount *= t.CorrosionMul;

            // 3. 적 쪽 조정(철갑, 기절, 보스).
            float before = amount;
            amount = w.Mods.Enemies.ModifyIncomingDamage(w, e, amount, d.IsCrit, d.IgnoresArmor, d.FromPos, d.HitPos);
            if (!(amount > 0f)) { res.Immune = true; res.Affinity = affinity; return res; }

            // 4. 체력. MaxHp가 0 이하인 표적(허수아비)은 죽지 않고 받은 피해만 쌓인다(Hp가 음수로 내려간다).
            bool killable = e.MaxHp > 0f;
            e.Hp -= amount;
            if (e.HpFloor > 0f && e.Hp < e.HpFloor) e.Hp = e.HpFloor;
            bool killed = killable && e.Hp <= 0f;
            if (killed) e.Hp = 0f;

            var hit = new HitInfo
            {
                AttackerSlot = d.AttackerSlot,
                EnemyId = e.Id,
                Origin = d.Origin,
                ShotSource = d.Source,
                ShotId = d.ShotId,
                IsCrit = d.IsCrit,
                Element = d.Element,
                Amount = amount,
                Killed = killed,
                Pos = d.HitPos,
                Affinity = affinity
            };
            if (killed)
            {
                // Targetable을 먼저 걸렀으므로 여기 오는 것은 언제나 첫 치명타다(KillingHit는 처음 것 유지).
                e.PendingDeath = true;
                e.KillingHit = hit;
            }

            // 5. 상태 적용(죽어 가는 적에게는 걸지 않는다).
            if (!killed && d.Projectile != null) ApplyStatuses(w, t, e, d.Projectile);

            // 6. 훅: 콤보 → 살얼음.
            w.Mods.Player.OnPlayerHitEnemy(w, hit);
            w.Mods.Traits.OnEnemyHit(w, hit);

            // 8. 표시 이벤트. 피해 숫자는 가장 가까운 정수(.5 올림).
            int flags = 0;
            if (d.IsCrit) flags |= DamageFlags.Crit;
            if (affinity == Affinity.Weak) flags |= DamageFlags.Weak;
            else if (affinity == Affinity.Resist) flags |= DamageFlags.Resist;
            if (corroded) flags |= DamageFlags.Corroded;
            if (d.Origin == DamageOrigin.Art || (d.Origin == DamageOrigin.Projectile && d.Source != ShotSource.Fire)) flags |= DamageFlags.Art;
            if (d.Origin == DamageOrigin.Passive) flags |= DamageFlags.Passive;
            if (amount < before - Eps) flags |= DamageFlags.Armored;
            w.Emit(SimEventType.EnemyDamaged, d.AttackerSlot, e.Id, a: RoundHalfUp(amount), b: flags, pos: d.HitPos, id: d.Element);

            // 9. 약점·저항을 처음 알아냄(D-54). 이 집합에 더하는 곳은 여기뿐이다.
            if (affinity != Affinity.Normal && !string.IsNullOrEmpty(e.DefId)
                && w.Run.AffinityKnown.Add(AffinityKey.Of(e.DefId, affinity)))
            {
                w.Emit(SimEventType.AffinityDiscovered, d.AttackerSlot, e.Id, a: (int)affinity, pos: e.Pos, id: d.Element);
            }

            res.Applied = amount;
            res.Killed = killed;
            res.Affinity = affinity;
            return res;
        }

        /// <summary>블록이 거는 상태(03 요약서 10절, U28: 폭발·전이 대상에도). 보스 절반·땅속 무시는 Enemies 쪽이 한다.</summary>
        void ApplyStatuses(SimWorld w, SpellTuning t, EnemyState e, ProjectileEntity pr)
        {
            var enemies = w.Mods.Enemies;
            var prm = pr.Block.Params;
            if (prm != null && prm.SlowPercent > 0f)
                enemies.ApplySlow(w, e, SlowSlot.IceSpike, SpellTuning.PercentRatio(prm.SlowPercent), SimConst.Ticks(prm.SlowSeconds), CcSource.BlockSlow);
            if (pr.Mods.Frost && t.FrostStrength > 0f)
                enemies.ApplySlow(w, e, SlowSlot.Frost, t.FrostStrength, t.FrostTicks, CcSource.BlockSlow);
            if (pr.Mods.Corrosion && t.CorrosionTicks > 0)
                enemies.ApplyCorrosion(w, e, t.CorrosionTicks);
            if (pr.HitEffect == ShotHitEffect.RecoilKnockback)
            {
                // 반동 사격: 탄이 날던 방향으로 밀고, 돌진 예고 중이면 끊는다.
                enemies.ApplyCc(w, e, CcKind.Knockback, t.RecoilKnockback, CcSource.Recoil, pr.Dir);
                if (enemies.TryCancelDash(w, e, t.RecoilStaggerTicks))
                    w.Emit(SimEventType.EnemyDashCancelled, pr.OwnerSlot, e.Id, pos: e.Pos);
            }
        }

        /// <summary>약점 ×1.25 / 저항 ×0.8 / 그 밖 ×1. 무속성은 상성이 없다(졸개의 neutral/neutral 포함).</summary>
        static Affinity AffinityOf(EnemyState e, string element)
        {
            if (string.IsNullOrEmpty(element) || element == Ids.Element.Neutral) return Affinity.Normal;
            if (e.Weak == element) return Affinity.Weak;
            if (e.Resist == element) return Affinity.Resist;
            return Affinity.Normal;
        }

        static int RoundHalfUp(float value) => (int)Math.Floor(value + 0.5d);
    }
}
