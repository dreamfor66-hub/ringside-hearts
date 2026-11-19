using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using RingsideHearts.Battle.Core;

namespace RingsideHearts.Battle.Tests
{
    /// <summary>
    /// Balance report over many seeded matches per bot profile, with the real asset values.
    /// Run: dotnet test Tools/CoreTests --filter "FullyQualifiedName~BalanceReport.Report" --logger "console;verbosity=detailed"
    /// </summary>
    [Category("Balance")]
    public class BalanceReport
    {
        public static string Table(Tuning t, int seeds, string matchId, IEnumerable<BotProfile> bots, Dictionary<string, TeamSetup> teams = null)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"BAL [{matchId}] 실력 | 승률 | 턴제한패 | 턴 | 분 | 내다운 | 적다운 | 내피날레(성공) | 적피날레(성공) | 태그 | 어시 | 어필 | 필살 | 합동");
            foreach (BotProfile bot in bots)
            {
                var all = new List<MatchStats>();
                for (int s = 1; s <= seeds; s++) all.Add(BalanceHarness.Play(t, bot, s, matchId, teams));
                sb.AppendLine($"BAL {bot.Name} | {all.Count(x => x.Won) * 100 / seeds}% | {all.Count(x => x.TurnLimit) * 100 / seeds}% | " +
                              $"{all.Average(x => x.Turns):0.0} | {all.Average(x => x.Minutes):0.0} | {all.Average(x => x.MyDowns):0.0} | {all.Average(x => x.EnemyDowns):0.0} | " +
                              $"{all.Average(x => x.MyFinales):0.0}({all.Average(x => x.MyFinaleWins):0.00}) | {all.Average(x => x.EnemyFinales):0.0}({all.Average(x => x.EnemyFinaleWins):0.00}) | " +
                              $"{all.Average(x => x.Tags):0.0} | {all.Average(x => x.Assists):0.0} | {all.Average(x => x.Appeals):0.0} | {all.Average(x => x.Specials):0.0} | {all.Average(x => x.Joints):0.0}");
            }
            return sb.ToString();
        }

        [Test, Explicit]
        public void Report()
        {
            Tuning t = BalanceHarness.LoadAssetTuning();
            TestContext.Progress.WriteLine(Table(t, 100, "tag", BotProfile.Main));
            foreach (string id in new[] { "cage", "hardcore", "norope", "submission", "boss", "tornado" })
                TestContext.Progress.WriteLine(Table(t, 40, id, new[] { BotProfile.Novice, BotProfile.Average, BotProfile.Expert }));
        }

        [Test]
        public void AssetHasEveryKey() => Assert.DoesNotThrow(() => BalanceHarness.LoadAssetTuning());
    }
}
