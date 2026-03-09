namespace pp.RaftMods.AutoCrafter
{
    /// <summary>
    /// AutoCrafter custom multiplayer message ids.
    /// Negative values avoid collisions with vanilla message ids.
    /// </summary>
    public enum EAutoCrafterRequestType : short
    {
        REQUEST_STATE = -120,
        RESPOND_STATE = -121,
        REQUEST_UPGRADE = -122,
        REQUEST_DOWNGRADE = -123,
        SET_SLOT_RECIPE = -124,
        SET_SLOT_ACTIVE = -125,
        SET_SLOT_INFINITE = -126,
        SET_SLOT_COUNT = -127,
        SET_SLOT_OUTPUT = -128,
        SET_SLOT_INPUT = -129,
        SET_CHEST_NAME = -130,
        STATE_UPDATE = -131
    }
}
