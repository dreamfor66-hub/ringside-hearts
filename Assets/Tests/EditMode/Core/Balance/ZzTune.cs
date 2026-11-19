using NUnit.Framework;
using RingsideHearts.Battle.Core;

namespace RingsideHearts.Battle.Tests
{
    /// <summary>
    /// Balance tools on the default values (before they are in the asset): a report for every match
    /// type, and a per-bout log for diagnosis. BalanceReport.Report does the same on the real asset.
    /// </summary>
    [Category("Balance")]
    public class ZzTune
    {
        [Test, Explicit]
        public void Defaults()
        {
            Tuning t = GameVariableKeys.Defaults();
            TestContext.Progress.WriteLine(BalanceReport.Table(t, 60, "tag", BotProfile.Main));
            foreach (string id in new[] { "cage", "hardcore", "norope", "submission", "boss", "tornado" })
                TestContext.Progress.WriteLine(BalanceReport.Table(t, 24, id, new[] { BotProfile.Novice, BotProfile.Average, BotProfile.Expert }));
        }

        [Test, Explicit]
        public void Bouts()
        {
            Tuning t = GameVariableKeys.Defaults();
            BalanceHarness.BoutLog = line => TestContext.Progress.WriteLine(line);
            for (int seed = 1; seed <= 14; seed++) BalanceHarness.Play(t, BotProfile.Average, seed);
            BalanceHarness.BoutLog = null;
        }
    }
}
