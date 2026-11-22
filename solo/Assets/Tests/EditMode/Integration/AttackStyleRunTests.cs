using System;
using System.Text;
using NUnit.Framework;
using Wizard.Solo.Sim;
using Wizard.Solo.Tests.Spells;

namespace Wizard.Solo.Tests.Integration
{
    /// <summary>
    /// 기본 공격 유형 15종으로 진짜 판(진짜 프로필·메타 서비스·모듈 전부, 손잡이 켬)을 웨이브 1~3까지 봇이 돈다(무적).
    /// 유형마다 웨이브를 넘기는지, 예외 없이 도는지, 걸린 시간이 마력탄과 같은 자릿수인지 본다(Docs/12-attack-styles.md 5절).
    /// </summary>
    public sealed class AttackStyleRunTests
    {
        const int ToWave = 3;
        const int MaxTicks = 60 * 60 * 6;

        static void Log(string s)
        {
            try { TestContext.WriteLine(s); }
            catch (Exception) { Console.WriteLine(s); }
        }

        /// <summary>그 유형으로 웨이브 toWave까지 넘긴 틱(못 넘기면 −1).</summary>
        public static int ClearTicks(int character, string style, ulong seed)
        {
            using (var k = new IntegrationKit(AttackStyleKit.Tuned))
            {
                k.StartRun(character, seed, cfg => cfg.AttackStyle = style);
                if (style != null) Assert.AreEqual(style, k.P.Spell.Core.Id, "판 시작에 코어가 유형으로 바뀐다");
                bool ok = k.AutoPlay(MaxTicks, w => w.Run.LastClearedWave >= ToWave, god: true);
                return ok ? k.W.Tick : -1;
            }
        }

        /// <summary>진단(시험 아님): 한 유형으로 판을 돌며 30초마다 웨이브·남은 적·발사·타격 수를 적는다.</summary>
        public static string Trace(int character, string style, int seconds)
        {
            var sb = new StringBuilder();
            using (var k = new IntegrationKit(AttackStyleKit.Tuned))
            {
                k.StartRun(character, 20261115UL, cfg => cfg.AttackStyle = style);
                for (int s = 0; s < seconds; s += 30)
                {
                    k.AutoPlay(60 * 30, w => false, god: true);
                    sb.AppendLine("t=" + s + " phase=" + k.W.Run.Phase + " wave=" + k.W.Run.Wave + " alive=" + k.W.AliveEnemies()
                        + " shots=" + k.Count(SimEventType.ShotFired) + " blasts=" + k.Count(SimEventType.StyleBlast)
                        + " dmg=" + k.Count(SimEventType.EnemyDamaged) + " kills=" + k.Count(SimEventType.EnemyKilled)
                        + " p=" + k.P.Pos + " facing=" + k.P.Facing);
                    var e = k.NearestEnemy();
                    if (e != null) sb.AppendLine("   nearest " + e.DefId + " at " + e.Pos + " ai=" + e.Ai + " burrowed=" + e.Burrowed);
                }
            }
            return sb.ToString();
        }

        public static string TraceStrike() => Trace(2, "block.core.sky_strike", 240);

        [Test]
        public void AS_Run__EveryStyleClearsWave1To3_WithTheSimpleBot()
        {
            var sb = new StringBuilder();
            sb.AppendLine("| 캐릭터 | 유형 | 웨이브 1~3 틱 | 마력탄 대비 |");
            for (int c = 1; c <= 5; c++)
            {
                int bolt = ClearTicks(c, null, 20261115UL);
                Assert.Greater(bolt, 0, "마력탄 char." + c);
                sb.AppendLine("| char." + c + " | magic_bolt | " + bolt + " | 1.00 |");
                foreach (var id in AttackStyleKit.Styles[c - 1])
                {
                    int t = ClearTicks(c, id, 20261115UL);
                    Assert.Greater(t, 0, id + " 가 웨이브 " + ToWave + "까지 넘긴다");
                    sb.AppendLine("| char." + c + " | " + id.Replace("block.core.", "") + " | " + t + " | " + (t / (float)bolt).ToString("0.00") + " |");
                }
            }
            Log(sb.ToString());
        }
    }
}
