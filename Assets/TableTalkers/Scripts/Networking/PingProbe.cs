using Unity.Netcode;
using UnityEngine;

namespace TableTalkers.Networking
{
    /// <summary>
    /// Measures round-trip latency for the local player via a tiny RPC echo, so the debug overlay
    /// can always show it (latency is the product's primary metric). Attach to the player prefab.
    /// </summary>
    public sealed class PingProbe : NetworkBehaviour
    {
        [Tooltip("Seconds between pings.")]
        [SerializeField, Min(0.1f)] private float _interval = 1f;

        /// <summary>Latest RTT of the local player in milliseconds. -1 until measured.</summary>
        public static float LastRttMs { get; private set; } = -1f;

        private float _nextPingAt;

        private void Update()
        {
            if (!IsSpawned || !IsOwner || IsServer)
            {
                // Host has ~0 network RTT to itself; show 0.
                if (IsSpawned && IsOwner && IsServer)
                {
                    LastRttMs = 0f;
                }

                return;
            }

            if (Time.unscaledTime >= _nextPingAt)
            {
                _nextPingAt = Time.unscaledTime + _interval;
                PingServerRpc(Time.unscaledTimeAsDouble);
            }
        }

        [ServerRpc]
        private void PingServerRpc(double clientSendTime, ServerRpcParams rpcParams = default)
        {
            var target = new ClientRpcParams
            {
                Send = new ClientRpcSendParams { TargetClientIds = new[] { rpcParams.Receive.SenderClientId } }
            };
            PongClientRpc(clientSendTime, target);
        }

        [ClientRpc]
        private void PongClientRpc(double clientSendTime, ClientRpcParams rpcParams = default)
        {
            LastRttMs = (float)((Time.unscaledTimeAsDouble - clientSendTime) * 1000.0);
        }
    }
}
