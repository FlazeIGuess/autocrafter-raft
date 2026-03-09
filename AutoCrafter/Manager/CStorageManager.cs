using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace pp.RaftMods.AutoCrafter
{
    /// <summary>
    /// Tracks all Storage_Small instances that have a CrafterBehaviour attached.
    /// Handles registering new storages and providing access to all behaviours for the craft loop.
    /// </summary>
    public class CStorageManager
    {
        private readonly Dictionary<uint, CrafterBehaviour> mi_behaviours =
            new Dictionary<uint, CrafterBehaviour>();

        private readonly CDataManager mi_dataManager;

        public CStorageManager(CDataManager dataManager)
        {
            mi_dataManager = dataManager;
        }

        /// <summary>All currently registered crafter behaviours.</summary>
        public IEnumerable<CrafterBehaviour> AllBehaviours => mi_behaviours.Values;

        /// <summary>
        /// Registers a storage and attaches a CrafterBehaviour if not already present.
        /// Loads existing data from the DataManager for this chest's ObjectIndex.
        /// </summary>
        public CrafterBehaviour RegisterStorage(Storage_Small storage)
        {
            if (storage == null) return null;

            uint id = storage.ObjectIndex;
            if (mi_behaviours.TryGetValue(id, out CrafterBehaviour existing) && existing != null)
                return existing;

            // Add component if not already there
            CrafterBehaviour behaviour = storage.GetComponent<CrafterBehaviour>();
            if (behaviour == null)
                behaviour = storage.gameObject.AddComponent<CrafterBehaviour>();

            // Load saved data (may be null if chest was never upgraded)
            CCrafterData data = mi_dataManager.GetData(id);
            Debug.Log("[AutoCrafter] RegisterStorage: ObjectIndex=" + id
                + " upgraded=" + (data != null && data.IsUpgraded));
            behaviour.Initialize(storage, data, mi_dataManager);

            mi_behaviours[id] = behaviour;
            AutoCrafter.NetworkManager?.RegisterBehaviour(behaviour);
            return behaviour;
        }

        /// <summary>
        /// Unregisters a storage (called when the chest is destroyed or the mod unloads).
        /// Does NOT delete save data - that is only done on downgrade.
        /// </summary>
        public void UnregisterStorage(uint objectIndex)
        {
            mi_behaviours.Remove(objectIndex);
            AutoCrafter.NetworkManager?.UnregisterBehaviour(objectIndex);
        }

        /// <summary>Returns the behaviour for a given objectIndex, or null.</summary>
        public CrafterBehaviour GetBehaviour(uint objectIndex)
        {
            mi_behaviours.TryGetValue(objectIndex, out CrafterBehaviour b);
            return b;
        }

        /// <summary>Removes null entries from the dictionary (safety cleanup).</summary>
        public void CleanupNullBehaviours()
        {
            var toRemove = mi_behaviours
                .Where(kv => kv.Value == null)
                .Select(kv => kv.Key)
                .ToList();
            foreach (var key in toRemove)
                mi_behaviours.Remove(key);
        }

        /// <summary>Clears all registered behaviours without destroying the components.</summary>
        public void Clear()
        {
            mi_behaviours.Clear();
            AutoCrafter.NetworkManager?.Clear();
        }
    }
}
