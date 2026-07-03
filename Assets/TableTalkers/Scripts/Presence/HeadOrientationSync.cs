using Unity.Netcode;
using UnityEngine;
using TableTalkers.Core;

namespace TableTalkers.Presence
{
    /// <summary>
    /// The presence core: syncs each participant's head yaw/pitch at a low rate and smoothly
    /// interpolates it on remote clients, applying it to the avatar's head bone — so everyone can
    /// see who is looking at whom. Owner reads its local <see cref="IHeadPoseSource"/>; remotes slerp.
    /// Attach next to NetworkParticipant; assign the avatar head bone.
    /// </summary>
    public sealed class HeadOrientationSync : NetworkBehaviour
    {
        [Tooltip("Avatar head bone the synced orientation is applied to. Falls back to this transform.")]
        [SerializeField] private Transform _headBone;

        [Tooltip("Sync rate in Hz (design target 10–20). Overridden by RoomConfig if assigned.")]
        [SerializeField, Range(1f, 60f)] private float _syncHz = 15f;

        [SerializeField] private RoomConfig _config;

        [Tooltip("Remote smoothing speed — higher snaps faster.")]
        [SerializeField, Range(1f, 30f)] private float _smoothing = 12f;

        private readonly NetworkVariable<float> _yaw =
            new(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        private readonly NetworkVariable<float> _pitch =
            new(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        private IHeadPoseSource _source;
        private float _nextWriteAt;
        private Quaternion _smoothed = Quaternion.identity;

        /// <summary>Current (smoothed) head orientation, local space relative to the seat.</summary>
        public Quaternion CurrentOrientation => _smoothed;

        public override void OnNetworkSpawn()
        {
            if (_headBone == null)
            {
                _headBone = transform;
            }

            if (IsOwner)
            {
                _source = GetComponentInChildren<IHeadPoseSource>();
                if (_source == null)
                {
                    Debug.LogWarning("[HeadOrientationSync] No IHeadPoseSource on owner — head will stay still.");
                }
            }
        }

        private void Update()
        {
            if (!IsSpawned)
            {
                return;
            }

            if (IsOwner)
            {
                OwnerWrite();
                // Owner sees its own head immediately (no smoothing lag for self).
                _smoothed = _source?.HeadOrientation ?? Quaternion.identity;
            }
            else
            {
                Quaternion target = Quaternion.Euler(-_pitch.Value, _yaw.Value, 0f);
                _smoothed = Quaternion.Slerp(_smoothed, target, 1f - Mathf.Exp(-_smoothing * Time.deltaTime));
            }

            ApplyToHeadBone();
        }

        private void OwnerWrite()
        {
            if (_source == null)
            {
                return;
            }

            float hz = _config != null ? _config.HeadSyncHz : _syncHz;
            if (Time.unscaledTime < _nextWriteAt)
            {
                return;
            }

            _nextWriteAt = Time.unscaledTime + 1f / hz;
            _yaw.Value = _source.Yaw;
            _pitch.Value = _source.Pitch;
        }

        private void ApplyToHeadBone()
        {
            // Head bone rotates relative to the body (which faces the table via the seat anchor).
            _headBone.localRotation = _smoothed;
        }
    }
}
