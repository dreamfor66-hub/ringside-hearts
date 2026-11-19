using RingsideHearts.Battle.Core;

namespace RingsideHearts.Battle.Tests
{
    /// <summary>The default values of every game variable (same table the game's asset was made from).</summary>
    public static class TestTuning
    {
        public static Tuning Default() => GameVariableKeys.Defaults();
    }
}
