using RingsideHearts.Battle.Core;

namespace RingsideHearts.Prototype
{
    /// <summary>What the menus chose; survives the scene reload that starts each match.</summary>
    public static class PrototypeSession
    {
        /// <summary>Match to start when the scene loads; null shows the match select screen.</summary>
        public static string PendingMatch;
        /// <summary>My team as edited on the formation screen (null = teams.csv player_default).</summary>
        public static TeamSetup Formation;
        /// <summary>0 = new seed each match.</summary>
        public static int FixedSeed;
        public static int LastSeed;
    }
}
