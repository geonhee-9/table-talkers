using System;
using Unity.Netcode;
using UnityEngine;
using TableTalkers.Core;

namespace TableTalkers.Moderation
{
    /// <summary>
    /// Host-only powers: kick a participant and lock the room. Kicking disconnects via NGO;
    /// locking raises an event the composition root routes to the lobby (Moderation stays
    /// Platform-free). Attach to a persistent object.
    /// </summary>
    public sealed class HostModeration : MonoBehaviour
    {
        /// <summary>Composition root subscribes and applies to the Steam lobby (joinable on/off).</summary>
        public event Action<bool> RoomLockChanged;

        public bool RoomLocked { get; private set; }

        public bool IsHost => NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost;

        /// <summary>Kick a participant (host only). They cannot rejoin while the room is locked.</summary>
        public void Kick(IParticipant participant)
        {
            if (!IsHost || participant == null || participant.IsLocal)
            {
                return;
            }

            if (participant is Component component
                && component.TryGetComponent(out NetworkObject networkObject))
            {
                NetworkManager.Singleton.DisconnectClient(networkObject.OwnerClientId);
            }
        }

        public void SetRoomLocked(bool locked)
        {
            if (!IsHost || RoomLocked == locked)
            {
                return;
            }

            RoomLocked = locked;
            RoomLockChanged?.Invoke(locked);
        }
    }
}
