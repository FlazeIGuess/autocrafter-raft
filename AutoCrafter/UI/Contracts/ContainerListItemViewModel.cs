namespace pp.RaftMods.AutoCrafter
{
    /// <summary>
    /// View-model row for the container picker.
    /// Keeps chest identity and selection state explicit for stale-reference handling.
    /// </summary>
    public sealed class ContainerListItemViewModel
    {
        public int ObjectIndex;
        public string DisplayName;
        public string IdentityMarker;
        public string Subtitle;
        public bool IsSelectable;
        public bool IsStaleReference;
        public bool IsAutoCrafterChest;
        public float DistanceMeters;
        public Storage_Small Storage;
    }
}