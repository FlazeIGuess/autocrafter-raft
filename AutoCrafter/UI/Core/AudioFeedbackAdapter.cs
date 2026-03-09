namespace pp.RaftMods.AutoCrafter
{
    /// <summary>
    /// Centralized UI audio routing to align click/open/fail feedback with Raft conventions.
    /// </summary>
    internal static class AudioFeedbackAdapter
    {
        public static void PlayClick()
        {
            SoundManager soundMgr = ComponentManager<SoundManager>.Value;
            soundMgr?.PlayUI_Click();
        }

        public static void PlayFail()
        {
            SoundManager soundMgr = ComponentManager<SoundManager>.Value;
            soundMgr?.PlayUI_Click_Fail();
        }

        public static void PlayOpen()
        {
            SoundManager soundMgr = ComponentManager<SoundManager>.Value;
            soundMgr?.PlayUI_OpenMenu();
        }
    }
}
