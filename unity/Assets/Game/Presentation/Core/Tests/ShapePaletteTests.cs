#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Wizard.Unity.Presentation.Core.Tests
{
    // 위험색(색상각 330~15도, 채도·명도 70% 이상)은 적의 공격과 예고 전용이다(CLAUDE.md 2.5). 플레이어 쪽·바닥·대체 도형에는
    // 위험색·금색·연두·청록을 쓰지 않는다(unity-engineer 규칙). 금색·연두·청록의 범위는 명세가 수치로 정하지 않아
    // 이 시험이 [임시]로 잡는다: 채도 35% 이상이고 색상각이 금색 35~65도, 연두 70~110도, 청록 165~200도.
    public sealed class ShapePaletteTests
    {
        private static readonly (string Name, Color Color)[] PlayerSide =
        {
            ("Background", ShapePalette.Background), ("Floor", ShapePalette.Floor), ("Pillar", ShapePalette.Pillar),
            ("PlayerBody", ShapePalette.PlayerBody), ("PlayerBodyInvulnerable", ShapePalette.PlayerBodyInvulnerable),
            ("PlayerBullet", ShapePalette.PlayerBullet), ("PlayerBulletAlt", ShapePalette.PlayerBulletAlt), ("Outline", ShapePalette.Outline),
            ("HeatRising", ShapePalette.HeatRising), ("HeatImminent", ShapePalette.HeatImminent), ("HeatCritical", ShapePalette.HeatCritical),
            ("HeatLocked", ShapePalette.HeatLocked), ("HeatExempt", ShapePalette.HeatExempt), ("HeatMark", ShapePalette.HeatMark),
            ("HeatMarkFaint", ShapePalette.HeatMarkFaint), ("LockCross", ShapePalette.LockCross),
            ("FacingNormal", ShapePalette.FacingNormal), ("FacingStance", ShapePalette.FacingStance), ("FacingReverse", ShapePalette.FacingReverse),
            ("Slime", ShapePalette.Slime), ("Turret", ShapePalette.Turret), ("Enemy", ShapePalette.Enemy), ("EnemyHpBack", ShapePalette.EnemyHpBack),
            ("EnemyHpFill", ShapePalette.EnemyHpFill), ("DebugText", ShapePalette.DebugText),
        };

        [Test]
        public void PlayerSideAndPlaceholderColors_AvoidDangerGoldLimeAndCyan()
        {
            var bad = new List<string>();
            foreach ((string name, Color c) in PlayerSide)
            {
                if (IsDanger(c) || IsSaturatedIn(c, 35f, 65f) || IsSaturatedIn(c, 70f, 110f) || IsSaturatedIn(c, 165f, 200f))
                {
                    bad.Add(name);
                }
            }
            Assert.That(bad, Is.Empty);
        }

        [Test]
        public void EnemyAttackAndTelegraph_UseTheDangerColor()
        {
            Assert.That(IsDanger(ShapePalette.EnemyShot), Is.True);
            Assert.That(IsDanger(ShapePalette.EnemyTelegraph), Is.True);
        }

        [Test]
        public void HeatRingColors_AreTheEffectsSpecValues()
        {
            // effects 10: 회청 #AEB8C8, 재색 #5A5F6B, 오르는 중 불투명도 60%
            Assert.That((Color32)ShapePalette.HeatImminent, Is.EqualTo(new Color32(0xAE, 0xB8, 0xC8, 0xFF)));
            Assert.That((Color32)ShapePalette.HeatLocked, Is.EqualTo(new Color32(0x5A, 0x5F, 0x6B, 0xFF)));
            Assert.That(ShapePalette.HeatRising.a, Is.EqualTo(0.6f).Within(1e-6f));
        }

        private static bool IsDanger(Color c)
        {
            Color.RGBToHSV(c, out float h, out float s, out float v);
            float deg = h * 360f;
            return (deg >= 330f || deg <= 15f) && s >= 0.7f && v >= 0.7f;
        }

        private static bool IsSaturatedIn(Color c, float fromDeg, float toDeg)
        {
            Color.RGBToHSV(c, out float h, out float s, out float _);
            float deg = h * 360f;
            return s >= 0.35f && deg >= fromDeg && deg <= toDeg;
        }
    }
}
