using UnityEngine;

namespace TableTalkers.Core
{
    /// <summary>
    /// Room-level tunables kept out of code (no magic numbers). Seat count, capacity, and the
    /// presence/transform sync rates live here so networking and gameplay read the same source.
    /// Create assets via Assets > Create > TableTalkers > Room Config.
    /// </summary>
    [CreateAssetMenu(fileName = "RoomConfig", menuName = "TableTalkers/Room Config", order = 0)]
    public sealed class RoomConfig : ScriptableObject
    {
        [Header("Seating")]
        [SerializeField, Min(1)] private int _seatCount = 4;

        [Tooltip("Design upper bound for a room (conversation stops working past this).")]
        [SerializeField, Min(1)] private int _maxCapacity = 12;

        [Header("Sync rates")]
        [Tooltip("Head orientation sync frequency in Hz (design target 10-20).")]
        [SerializeField, Range(1f, 60f)] private float _headSyncHz = 15f;

        [Tooltip("Optional upper-body / hand transform sync frequency in Hz.")]
        [SerializeField, Range(1f, 60f)] private float _transformSyncHz = 15f;

        [Header("Networking")]
        [Tooltip("NGO network tick rate in Hz.")]
        [SerializeField, Range(10, 60)] private int _networkTickRate = 30;

        [Tooltip("How many times a dropped client retries reconnecting.")]
        [SerializeField, Min(0)] private int _reconnectAttempts = 5;

        [Tooltip("Delay between reconnect attempts, in seconds.")]
        [SerializeField, Min(0.5f)] private float _reconnectDelaySeconds = 3f;

        [Tooltip("How long the host holds a disconnected participant's seat, in seconds.")]
        [SerializeField, Min(0f)] private float _seatHoldSeconds = 60f;

        public int SeatCount => _seatCount;
        public int MaxCapacity => _maxCapacity;
        public float HeadSyncHz => _headSyncHz;
        public float TransformSyncHz => _transformSyncHz;
        public int NetworkTickRate => _networkTickRate;
        public int ReconnectAttempts => _reconnectAttempts;
        public float ReconnectDelaySeconds => _reconnectDelaySeconds;
        public float SeatHoldSeconds => _seatHoldSeconds;
    }
}
