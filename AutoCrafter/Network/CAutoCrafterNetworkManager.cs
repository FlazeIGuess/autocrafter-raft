using Steamworks;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace pp.RaftMods.AutoCrafter
{
    /// <summary>
    /// Routes AutoCrafter custom multiplayer messages to the matching storage behaviour.
    /// </summary>
    public class CAutoCrafterNetworkManager
    {
        private readonly Dictionary<uint, CrafterBehaviour> mi_registeredBehaviours =
            new Dictionary<uint, CrafterBehaviour>();

        private short mi_messageFloor = short.MaxValue;
        private short mi_messageCeil = short.MinValue;

        public CAutoCrafterNetworkManager()
        {
            foreach (EAutoCrafterRequestType type in Enum.GetValues(typeof(EAutoCrafterRequestType)))
            {
                mi_messageFloor = (short)Mathf.Min(mi_messageFloor, (short)type);
                mi_messageCeil = (short)Mathf.Max(mi_messageCeil, (short)type);
            }
        }

        public void Clear()
        {
            mi_registeredBehaviours.Clear();
        }

        public void RegisterBehaviour(CrafterBehaviour behaviour)
        {
            if (behaviour == null)
                return;

            mi_registeredBehaviours[behaviour.ObjectIndex] = behaviour;
        }

        public void UnregisterBehaviour(uint objectIndex)
        {
            mi_registeredBehaviours.Remove(objectIndex);
        }

        public void SendToHost(CAutoCrafterNetMessage message)
        {
            var network = ComponentManager<Raft_Network>.Value;
            if (network == null || message == null)
                return;

            SendTo(message, network.HostID);
        }

        public void SendTo(CAutoCrafterNetMessage message, CSteamID target)
        {
            var network = ComponentManager<Raft_Network>.Value;
            if (network == null || message == null)
                return;

            network.SendP2P(target, CreateCarrier(message), EP2PSend.k_EP2PSendReliable, NetworkChannel.Channel_Game);
        }

        public void Broadcast(CAutoCrafterNetMessage message)
        {
            var network = ComponentManager<Raft_Network>.Value;
            if (network == null || message == null)
                return;

            network.RPC(CreateCarrier(message), Target.Other, EP2PSend.k_EP2PSendReliable, NetworkChannel.Channel_Game);
        }

        public bool HandlePacket(Packet_Multiple packet, CSteamID remoteID)
        {
            if (packet == null || packet.messages == null || packet.messages.Length == 0)
                return true;

            List<Message> remaining = packet.messages.ToList();
            foreach (Message package in packet.messages)
            {
                if (package == null)
                    continue;

                if (package.t > mi_messageCeil || package.t < mi_messageFloor)
                    continue;

                remaining.Remove(package);

                var carrier = package as Message_InitiateConnection;
                if (carrier == null || string.IsNullOrEmpty(carrier.password))
                    continue;

                try
                {
                    CAutoCrafterNetMessage message = JsonUtility.FromJson<CAutoCrafterNetMessage>(carrier.password);
                    if (message == null)
                        continue;

                    CrafterBehaviour behaviour;
                    if (!mi_registeredBehaviours.TryGetValue(message.ObjectIndex, out behaviour) || behaviour == null)
                    {
                        Debug.LogWarning("[AutoCrafter] Network message dropped - no behaviour for ObjectIndex=" + message.ObjectIndex);
                        continue;
                    }

                    behaviour.OnNetworkMessageReceived(message, remoteID);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[AutoCrafter] Failed to parse network message: " + ex.Message);
                }
            }

            packet.messages = remaining.ToArray();
            return remaining.Count > 0;
        }

        private Message CreateCarrier(CAutoCrafterNetMessage message)
        {
            string payload = JsonUtility.ToJson(message);
            return new Message_InitiateConnection((Messages)message.Type, 0, payload);
        }
    }
}
