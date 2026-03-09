namespace pp.RaftMods.AutoCrafter
{
    public enum SlotCraftabilityState
    {
        MissingIngredients,
        OutputFull,
        NoOutputContainer,
        Inactive,
        Ready
    }

    /// <summary>
    /// Canonical slot craftability status produced from runtime slot state.
    /// </summary>
    public sealed class SlotStatusViewModel
    {
        public SlotCraftabilityState State;
        public string Reason;

        public string DisplayText => State + ": " + Reason;

        public static SlotStatusViewModel Create(SlotCraftabilityState state, string reason)
        {
            return new SlotStatusViewModel
            {
                State = state,
                Reason = string.IsNullOrEmpty(reason) ? "No details" : reason
            };
        }
    }
}