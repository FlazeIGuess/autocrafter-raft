namespace pp.RaftMods.AutoCrafter
{
    /// <summary>
    /// Tracks the currently active blocking overlay so only one picker/dialog overlay
    /// can be visible at any time.
    /// </summary>
    public sealed class UIOverlayStateMachine
    {
        public enum OverlayState
        {
            None,
            RecipePicker,
            ContainerPicker,
            ConfirmAction,
            Info,
            Loading,
        }

        public OverlayState ActiveState { get; private set; } = OverlayState.None;

        public bool IsBlockingOverlayActive => ActiveState != OverlayState.None;

        public bool TryActivate(OverlayState nextState)
        {
            if (nextState == OverlayState.None)
                return false;

            if (ActiveState == nextState)
                return false;

            ActiveState = nextState;
            return true;
        }

        public void ClearIf(OverlayState state)
        {
            if (ActiveState == state)
                ActiveState = OverlayState.None;
        }

        public void Reset()
        {
            ActiveState = OverlayState.None;
        }
    }
}
