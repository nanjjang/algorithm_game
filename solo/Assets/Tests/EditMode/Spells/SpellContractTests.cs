using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Wizard.Solo.Sim;
using Wizard.Solo.Sim.Spells;

namespace Wizard.Solo.Tests.Spells
{
    /// <summary>다른 모듈과 닿는 자리(장애물 = Threats, 얼음 병풍 = Arts)와 조립 글자 입출력(10 요약서 9절).</summary>
    public class SpellContractTests
    {
        sealed class OneRockThreats : ThreatModule
        {
            public ObstacleEntity Rock;
            public readonly List<float> Damages = new List<float>();
            public override bool SegmentHitsObstacle(SimWorld w, Vector2 a, Vector2 b, bool forPlayerShot, out ObstacleEntity hit)
            {
                hit = null;
                if (Rock == null || !forPlayerShot) return false;
                // 선분과 원의 가장 가까운 거리로 판정(가짜).
                Vector2 ab = b - a;
                float len2 = ab.sqrMagnitude;
                float t = len2 > 0f ? Mathf.Clamp01(Vector2.Dot(Rock.Pos - a, ab) / len2) : 0f;
                if ((a + ab * t - Rock.Pos).magnitude > Rock.Radius) return false;
                hit = Rock;
                return true;
            }
            public override void DamageObstacle(SimWorld w, ObstacleEntity o, float amount) { Damages.Add(amount); }
        }

        sealed class CountingArts : ArtModule
        {
            public int ScreenQueries;
            public override bool SegmentHitsScreen(SimWorld w, Vector2 a, Vector2 b, out int zoneId)
            {
                ScreenQueries++;
                zoneId = 1;
                return true;
            }
        }

        SpellWorld _k;

        [SetUp]
        public void SetUp() { _k = new SpellWorld(); }

        [Test]
        public void S05_Obstacle__ShotStopsAtRockAndDamagesIt()
        {
            var threats = new OneRockThreats { Rock = new ObstacleEntity { Id = 900, Pos = new Vector2(5f, 7f), Radius = 0.5f, Hp = 30f, BlocksPlayerShots = true } };
            _k.W.Mods.Threats = threats;
            var behind = _k.AddEnemy(7f, 7f);
            _k.Fire(_k.Compile(B.Mb, B.None, B.None, B.None, B.None));
            _k.Step(30);
            Assert.AreEqual(1, threats.Damages.Count);
            Assert.AreEqual(10f, threats.Damages[0], 1e-3f);
            Assert.AreEqual(behind.MaxHp, behind.Hp);
            var ev = _k.EventsOf(SimEventType.ProjectileExpired);
            Assert.AreEqual(1, ev.Count);
            Assert.AreEqual(1, ev[0].A);
            Assert.AreEqual(4.5f, ev[0].Pos.x, 1e-3f);
        }

        [Test]
        public void S05_Obstacle__BounceReflectsOffRock()
        {
            var threats = new OneRockThreats { Rock = new ObstacleEntity { Id = 900, Pos = new Vector2(5f, 7f), Radius = 0.5f } };
            _k.W.Mods.Threats = threats;
            _k.Fire(_k.Compile(B.Mb, B.Bounce, B.None, B.None, B.None));
            var pr = _k.W.Projectiles[0];
            _k.Step(14);
            Assert.AreEqual(1, _k.CountEvents(SimEventType.ProjectileBounced));
            Assert.AreEqual(1, pr.BouncesLeft);
            Assert.AreEqual(-1f, pr.Dir.x, 1e-3f);
            Assert.IsFalse(pr.Dead);
        }

        // TV-G13: 얼음 병풍 앞의 내 탄(벽 튕김 포함)은 튕기지 않고 지나간다 — 주문 모듈은 병풍을 묻지 않는다
        [Test]
        public void S02_G13__PlayerShotsPassThroughIceScreen()
        {
            var arts = new CountingArts();
            _k.W.Mods.Arts = arts;
            _k.W.PlayerZones.Add(new PlayerZoneEntity { Id = 1, Kind = PlayerZoneKind.IceScreen, OwnerSlot = 0, Pos = new Vector2(4f, 7f), Width = 3f, Thickness = 0.3f, UntilTick = 9999 });
            var e = _k.AddEnemy(6f, 7f);
            _k.Fire(_k.Compile(B.Mb, B.Bounce, B.None, B.None, B.None));
            _k.Step(30);
            Assert.AreEqual(0, arts.ScreenQueries);
            Assert.AreEqual(0, _k.CountEvents(SimEventType.ProjectileBounced));
            Assert.AreEqual(10f, e.MaxHp - e.Hp, 1e-3f);
        }

        // 10 요약서 9절 입력 문법
        [Test]
        public void S02_14_5__ParseLoadoutText()
        {
            Assert.IsTrue(SpellLoadoutText.TryParse("core=magic_bolt; links=split_three,on_hit_trigger,block.fireball,_; char=3; ext=1; mut=mirror_image,mutation.glass_wand", out var p));
            Assert.AreEqual(Ids.Block.MagicBolt, p.Core);
            Assert.AreEqual(4, p.Links.Count);
            Assert.AreEqual(Ids.Block.SplitThree, p.Links[0]);
            Assert.AreEqual(Ids.Block.OnHitTrigger, p.Links[1]);
            Assert.AreEqual(Ids.Block.Fireball, p.Links[2]);
            Assert.IsNull(p.Links[3]);
            Assert.AreEqual(3, p.Character);
            Assert.AreEqual(1, p.ExtraSlots);
            Assert.AreEqual(2, p.Mutations.Count);
            Assert.AreEqual(Ids.Mutation.MirrorImage, p.Mutations[0]);
            Assert.AreEqual(Ids.Mutation.GlassWand, p.Mutations[1]);

            Assert.IsFalse(SpellLoadoutText.TryParse("links=_,_", out _));
            Assert.IsFalse(SpellLoadoutText.TryParse("core=magic_bolt; links=_; char=9", out _));
            Assert.IsFalse(SpellLoadoutText.TryParse("core=magic_bolt; links=_; what=1", out _));
        }

        // TV-J06: 글자 출력 → 다시 파싱 → 컴파일 → 같은 결과
        [Test]
        public void S02_J06__FormatParseCompileRoundTrip()
        {
            var s = _k.Compile(B.Mb, B.S3, B.Trig, B.Fb, B.None);
            string text = SpellLoadoutText.Format(s);
            Assert.AreEqual("core=block.magic_bolt; links=block.split_three,block.on_hit_trigger,block.fireball,_", text);
            Assert.IsTrue(SpellLoadoutText.TryParse(text, out var p));
            var again = _k.Compile(p.Core, p.Links.ToArray());
            Assert.AreEqual(_k.Sys.DebugDump(s), _k.Sys.DebugDump(again));
        }
    }
}
