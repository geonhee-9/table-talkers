using System;
using System.Collections;
using Netcode.Transports.Facepunch;
using Unity.Netcode;
using UnityEngine;
using TableTalkers.Core;

namespace TableTalkers.Networking
{
    /// <summary>
    /// Drives NGO over the Facepunch (Steam Relay) transport. Host-authoritative: the lobby owner
    /// calls <see cref="StartHost"/>, guests call <see cref="StartClient"/> with the host's SteamId.
    /// Deliberately knows nothing about Steam lobbies — the composition root (Bootstrap) wires
    /// lobby events to these calls, keeping Networking free of Platform dependencies.
    /// Includes basic reconnect: a dropped (non-intentional) client retries a few times.
    /// </summary>
    public sealed class NetworkSessionService : MonoBehaviour
    {
        [SerializeField] private RoomConfig _config;

        /// <summary>Raised on the host when hosting starts.</summary>
        public event Action HostStarted;
        /// <summary>Raised locally when this client connects to the host.</summary>
        public event Action Connected;
        /// <summary>Raised locally when this client disconnects (after reconnect attempts, if any).</summary>
        public event Action Disconnected;
        /// <summary>Raised on the host when a peer connects/disconnects (NGO clientId).</summary>
        public event Action<ulong> PeerConnected;
        public event Action<ulong> PeerDisconnected;

        public bool IsRunning => NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
        public bool IsHost => NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost;

        private ulong _hostSteamId;
        private bool _leaveRequested;
        private Coroutine _reconnectRoutine;

        private NetworkManager Nm => NetworkManager.Singleton;

        private void Start()
        {
            if (Nm == null)
            {
                Debug.LogError("[NetworkSession] No NetworkManager in scene. 🔧 person: add one with FacepunchTransport.");
                return;
            }

            if (_config != null)
            {
                Nm.NetworkConfig.TickRate = (uint)_config.NetworkTickRate;
            }

            // Version gate: P2P means everyone must run the same build. Guests send their app
            // version on connect; the host rejects mismatches (they get an update prompt).
            Nm.NetworkConfig.ConnectionApproval = true;
            Nm.ConnectionApprovalCallback = ApproveConnection;

            Nm.OnClientConnectedCallback += HandleClientConnected;
            Nm.OnClientDisconnectCallback += HandleClientDisconnected;
        }

        private static void ApproveConnection(
            NetworkManager.ConnectionApprovalRequest request,
            NetworkManager.ConnectionApprovalResponse response)
        {
            string clientVersion = request.Payload != null && request.Payload.Length > 0
                ? System.Text.Encoding.UTF8.GetString(request.Payload)
                : "";

            bool sameVersion = clientVersion == Application.version
                               || request.ClientNetworkId == NetworkManager.Singleton.LocalClientId;

            response.Approved = sameVersion;
            response.CreatePlayerObject = sameVersion;
            if (!sameVersion)
            {
                response.Reason = $"version_mismatch:{Application.version}";
            }
        }

        private void OnDestroy()
        {
            if (Nm != null)
            {
                Nm.OnClientConnectedCallback -= HandleClientConnected;
                Nm.OnClientDisconnectCallback -= HandleClientDisconnected;
            }
        }

        /// <summary>Start hosting (lobby owner). The host is the source of truth for room state.</summary>
        public bool StartHost()
        {
            if (Nm == null || IsRunning)
            {
                return false;
            }

            _leaveRequested = false;
            Nm.NetworkConfig.ConnectionData = System.Text.Encoding.UTF8.GetBytes(Application.version);
            bool ok = Nm.StartHost();
            if (ok)
            {
                HostStarted?.Invoke();
            }

            return ok;
        }

        /// <summary>Connect to a host over Steam Relay by SteamId (guests).</summary>
        public bool StartClient(ulong hostSteamId)
        {
            if (Nm == null || IsRunning)
            {
                return false;
            }

            var transport = Nm.NetworkConfig.NetworkTransport as FacepunchTransport;
            if (transport == null)
            {
                Debug.LogError("[NetworkSession] NetworkTransport is not FacepunchTransport. 🔧 person: assign it on the NetworkManager.");
                return false;
            }

            _hostSteamId = hostSteamId;
            _leaveRequested = false;
            transport.targetSteamId = hostSteamId;
            Nm.NetworkConfig.ConnectionData = System.Text.Encoding.UTF8.GetBytes(Application.version);
            return Nm.StartClient();
        }

        /// <summary>Intentional leave — suppresses reconnect.</summary>
        public void Shutdown()
        {
            _leaveRequested = true;
            if (_reconnectRoutine != null)
            {
                StopCoroutine(_reconnectRoutine);
                _reconnectRoutine = null;
            }

            if (Nm != null && IsRunning)
            {
                Nm.Shutdown();
            }
        }

        private void HandleClientConnected(ulong clientId)
        {
            if (clientId == Nm.LocalClientId)
            {
                Connected?.Invoke();
            }
            else if (Nm.IsServer)
            {
                PeerConnected?.Invoke(clientId);
            }
        }

        private void HandleClientDisconnected(ulong clientId)
        {
            if (Nm.IsServer && clientId != Nm.LocalClientId)
            {
                PeerDisconnected?.Invoke(clientId);
                return;
            }

            if (clientId != Nm.LocalClientId)
            {
                return;
            }

            // Local client dropped. Try to rejoin the same host unless the user chose to leave.
            if (!_leaveRequested && _hostSteamId != 0 && _config != null && _config.ReconnectAttempts > 0)
            {
                _reconnectRoutine ??= StartCoroutine(ReconnectLoop());
            }
            else
            {
                Disconnected?.Invoke();
            }
        }

        private IEnumerator ReconnectLoop()
        {
            for (int attempt = 1; attempt <= _config.ReconnectAttempts; attempt++)
            {
                yield return new WaitForSeconds(_config.ReconnectDelaySeconds);
                if (_leaveRequested)
                {
                    break;
                }

                Debug.Log($"[NetworkSession] Reconnect attempt {attempt}/{_config.ReconnectAttempts}…");
                if (StartClient(_hostSteamId))
                {
                    // Wait to see whether the connection sticks.
                    yield return new WaitForSeconds(_config.ReconnectDelaySeconds);
                    if (IsRunning && Nm.IsConnectedClient)
                    {
                        _reconnectRoutine = null;
                        yield break;
                    }

                    Nm.Shutdown();
                }
            }

            _reconnectRoutine = null;
            Disconnected?.Invoke();
        }
    }
}
