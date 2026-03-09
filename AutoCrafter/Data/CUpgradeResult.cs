namespace pp.RaftMods.AutoCrafter
{
    /// <summary>
    /// Result returned by CrafterBehaviour.Upgrade().
    /// On failure, MissingItems contains one entry per missing resource formatted as
    /// "Item Name (have X, need Y)".
    /// </summary>
    public struct CUpgradeResult
    {
        public bool     Success;
        public string   ErrorMessage;
        public string[] MissingItems;

        public static CUpgradeResult Ok()
        {
            return new CUpgradeResult { Success = true };
        }

        public static CUpgradeResult Fail(string message)
        {
            return new CUpgradeResult
            {
                Success      = false,
                ErrorMessage = message,
                MissingItems = new string[0]
            };
        }

        public static CUpgradeResult Fail(string[] missingItems)
        {
            return new CUpgradeResult
            {
                Success      = false,
                ErrorMessage = "Not enough resources!",
                MissingItems = missingItems
            };
        }
    }
}
