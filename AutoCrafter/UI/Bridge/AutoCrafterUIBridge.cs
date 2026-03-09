using System.Collections.Generic;

namespace pp.RaftMods.AutoCrafter
{
    /// <summary>
    /// Thin adapter between UI lifecycle events and the storage/behaviour registry.
    /// Keeps UI code from directly traversing manager internals.
    /// </summary>
    public sealed class AutoCrafterUIBridge
    {
        public enum ContainerReferenceState
        {
            None,
            Live,
            Missing
        }

        public bool TryGetBehaviour(uint objectIndex, out CrafterBehaviour behaviour)
        {
            behaviour = AutoCrafter.StorageManager?.GetBehaviour(objectIndex);
            return behaviour != null;
        }

        public bool TryGetStorage(uint objectIndex, out Storage_Small storage)
        {
            storage = null;
            foreach (Storage_Small candidate in StorageManager.allStorages)
            {
                if (candidate == null)
                    continue;

                if (candidate.ObjectIndex == objectIndex)
                {
                    storage = candidate;
                    return true;
                }
            }

            return false;
        }

        public List<int> GetReferencedContainerIndices(CrafterBehaviour behaviour, bool inputMode)
        {
            List<int> result = new List<int>();
            if (behaviour == null || behaviour.Data == null || behaviour.Data.Slots == null)
                return result;

            for (int i = 0; i < behaviour.Data.Slots.Count; i++)
            {
                CCrafterSlot slot = behaviour.Data.Slots[i];
                if (slot == null)
                    continue;

                int index = inputMode ? slot.InputContainerIndex : slot.OutputContainerIndex;
                if (index > 0 && !result.Contains(index))
                    result.Add(index);
            }

            return result;
        }

        public string BuildContainerIdentityLabel(int objectIndex)
        {
            string chestName = AutoCrafter.DataManager?.GetChestName((uint)objectIndex) ?? string.Empty;
            if (string.IsNullOrEmpty(chestName))
                return "Chest #" + objectIndex;

            return "\"" + chestName + "\" (#" + objectIndex + ")";
        }

        public Storage_Small ResolveStorageByIndex(int objectIndex)
        {
            if (objectIndex <= 0)
                return null;

            foreach (Storage_Small candidate in StorageManager.allStorages)
            {
                if (candidate == null)
                    continue;

                if (candidate.ObjectIndex == (uint)objectIndex)
                    return candidate;
            }

            return null;
        }

        public ContainerReferenceState GetContainerReferenceState(int objectIndex, Storage_Small cachedStorage)
        {
            if (objectIndex < 0)
                return ContainerReferenceState.None;

            if (cachedStorage != null)
                return ContainerReferenceState.Live;

            return ResolveStorageByIndex(objectIndex) != null
                ? ContainerReferenceState.Live
                : ContainerReferenceState.Missing;
        }
    }
}
