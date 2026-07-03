using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using TableTalkers.Core;
using TableTalkers.Voice;

namespace TableTalkers.Presence
{
    /// <summary>
    /// Represents an <see cref="IParticipant"/> on the network — the bridge between NGO and the
    /// participant abstraction. Owner writes identity + speaking; the HOST assigns seats
    /// (host-authoritative) with reconnect seat holds. Synced state stays within the allowed
    /// minimum: seat index, display name, speaking flag (head orientation lives in
    /// <see cref="HeadOrientationSync"/>). Attach to the player prefab with a NetworkObject.
    /// </summary>
    public sealed class NetworkParticipant : NetworkBehaviour, IParticipant
    {
        [Tooltip("Speaking level above this counts as speaking.")]
        [SerializeField, Range(0.01f, 0.5f)] private float _speakingThreshold = 0.05f;

        [Tooltip("How often the owner refreshes its speaking flag, in Hz.")]
        [SerializeField, Range(1f, 30f)] private float _speakingSyncHz = 10f;

        private readonly NetworkVariable<ulong> _platformId =
            new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        private readonly NetworkVariable<FixedString64Bytes> _displayName =
            new(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        private readonly NetworkVariable<int> _seatIndex =
            new(SeatManager.Unseated, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<bool> _isSpeaking =
            new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        private HeadOrientationSync _head;
        private float _nextSpeakingWriteAt;

        // IParticipant — the only surface gameplay/UI code sees.
        public string Id => _platformId.Value.ToString();
        public int SeatIndex => _seatIndex.Value;
        public string DisplayName => _displayName.Value.ToString();
        public ParticipantKind Kind => ParticipantKind.Human;
        public bool IsLocal => IsOwner;
        public bool IsSpeaking => _isSpeaking.Value;
        public Quaternion HeadOrientation => _head != null ? _head.CurrentOrientation : Quaternion.identity;

        public override void OnNetworkSpawn()
        {
            _head = GetComponent<HeadOrientationSync>();
            ParticipantRegistry.Add(this);
            _seatIndex.OnValueChanged += HandleSeatChanged;

            if (IsOwner)
            {
                _platformId.Value = LocalPlayerInfo.PlatformId;
                _displayName.Value = LocalPlayerInfo.DisplayName;
            }

            if (IsServer)
            {
                // Seat assignment needs the participant id (for reconnect holds), which the owner
                // writes after spawn — assign now if we already have it, otherwise when it arrives.
                if (_platformId.Value != 0)
                {
                    AssignSeatOnServer();
                }
                else
                {
                    _platformId.OnValueChanged += HandleIdArrivedOnServer;
                }
            }

            SnapToSeat(_seatIndex.Value);
        }

        public override void OnNetworkDespawn()
        {
            ParticipantRegistry.Remove(this);
            _seatIndex.OnValueChanged -= HandleSeatChanged;

            if (IsServer && RoomSession.IsInitialized && _seatIndex.Value != SeatManager.Unseated)
            {
                // Hold the seat briefly so a dropped participant comes back to the same chair.
                RoomSession.Seats.Release(_seatIndex.Value);
                RoomSession.Reservations?.Reserve(Id, _seatIndex.Value, Time.unscaledTimeAsDouble);
            }
        }

        private void Update()
        {
            if (!IsSpawned || !IsOwner || VoiceServices.Current == null)
            {
                return;
            }

            if (Time.unscaledTime >= _nextSpeakingWriteAt)
            {
                _nextSpeakingWriteAt = Time.unscaledTime + 1f / _speakingSyncHz;
                bool speaking = VoiceServices.Current.GetPeerSpeakingLevel(Id) >= _speakingThreshold;
                if (speaking != _isSpeaking.Value)
                {
                    _isSpeaking.Value = speaking;
                }
            }
        }

        private void HandleIdArrivedOnServer(ulong previous, ulong current)
        {
            _platformId.OnValueChanged -= HandleIdArrivedOnServer;
            AssignSeatOnServer();
        }

        private void AssignSeatOnServer()
        {
            if (!RoomSession.IsInitialized)
            {
                Debug.LogWarning("[NetworkParticipant] RoomSession not initialized on host; no seat assigned.");
                return;
            }

            double now = Time.unscaledTimeAsDouble;
            if (RoomSession.Reservations != null
                && RoomSession.Reservations.TryClaim(Id, now, out int heldSeat)
                && RoomSession.Seats.AssignSeat(this, heldSeat))
            {
                _seatIndex.Value = heldSeat;
                return;
            }

            if (RoomSession.Seats.TryAssignSeat(this, out int seat))
            {
                _seatIndex.Value = seat;
            }
            else
            {
                Debug.LogWarning($"[NetworkParticipant] Room full — no seat for {DisplayName}.");
            }
        }

        private void HandleSeatChanged(int previous, int current) => SnapToSeat(current);

        private void SnapToSeat(int seatIndex)
        {
            Transform anchor = SeatAnchorRegistry.Instance != null
                ? SeatAnchorRegistry.Instance.GetAnchor(seatIndex)
                : null;

            if (anchor != null)
            {
                transform.SetPositionAndRotation(anchor.position, anchor.rotation);
            }
        }
    }
}
