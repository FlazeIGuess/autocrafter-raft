using System;

namespace pp.RaftMods.AutoCrafter
{
    /// <summary>
    /// Serializable network payload for AutoCrafter host-authoritative sync.
    /// </summary>
    [Serializable]
    public class CAutoCrafterNetMessage
    {
        public EAutoCrafterRequestType Type;
        public uint ObjectIndex;

        public int SlotIndex = -1;
        public int IntValue = 0;
        public bool BoolValue = false;
        public string StringValue = string.Empty;

        public CCrafterData Data;
        public string ChestName = string.Empty;

        public CAutoCrafterNetMessage() { }

        public CAutoCrafterNetMessage(EAutoCrafterRequestType type, uint objectIndex)
        {
            Type = type;
            ObjectIndex = objectIndex;
        }
    }
}
