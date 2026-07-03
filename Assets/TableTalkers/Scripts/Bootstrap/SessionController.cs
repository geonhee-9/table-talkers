using Steamworks;
using Steamworks.Data;
using UnityEngine;
using TableTalkers.Core;
using TableTalkers.Networking;
using TableTalkers.Platform;
using TableTalkers.Voice;

namespace TableTalkers.Bootstrap
{
    /// <summary>
    /// Composition root glue for a room session. Wires the independent pieces together:
    /// Steam lobby (Platform) → NGO relay session (Networking) → voice channel (IVoiceService).
    /// This is the only place that knows concrete types; everything below depends on abstractions.
    /// 🔧 person: place on the Boot scene object alongside AppEntry, assign references.
    /// Includes a temporary IMGUI lobby panel until the real LobbyUI exists.
    /// </summary>
    public sealed class SessionController : MonoBehaviour
    {
        [SerializeField] private AppEntry _appEntry;
        [SerializeField] private SteamLobby _lobby;
        [SerializeField] private NetworkSessionService _network;
        [SerializeField] private VoiceServiceSelector _voiceSelector;
        [SerializeField] private RoomConfig _roomConfig;

        private bool _voiceJoined;

        private IVoiceService Voice => _voiceSelector.Service;
        private IVoicePeerRoster VoiceRoster => _voiceSelector.Roster;

        private void Start()
        {
            if (_lobby == null || _network == null || _voiceSelector == null || _roomConfig == null)
            {
                Debug.LogError("[Session] Missing references. 🔧 person: assign SteamLobby, NetworkSessionService, VoiceServiceSelector, RoomConfig.");
                enabled = false;
                return;
            }

            if (_lobby.IsAvailable)
            {
                LocalPlayerInfo.Set(SteamClient.SteamId.Value, SteamClient.Name);
            }

            _lobby.LobbyCreated += HandleLobbyCreated;
            _lobby.LobbyEntered += HandleLobbyEntered;
            _lobby.MemberJoined += HandleMemberJoined;
            _lobby.MemberLeft += HandleMemberLeft;
            _network.Connected += HandleConnected;
            _network.Disconnected += HandleDisconnected;
        }

        private void OnDestroy()
        {
            if (_lobby != null)
            {
                _lobby.LobbyCreated -= HandleLobbyCreated;
                _lobby.LobbyEntered -= HandleLobbyEntered;
                _lobby.MemberJoined -= HandleMemberJoined;
                _lobby.MemberLeft -= HandleMemberLeft;
            }

            if (_network != null)
            {
                _network.Connected -= HandleConnected;
                _network.Disconnected -= HandleDisconnected;
            }
        }

        /// <summary>Host path: create an invite-only lobby sized by RoomConfig.</summary>
        public void HostRoom()
        {
            _ = _lobby.CreateLobbyAsync(_roomConfig.SeatCount);
        }

        public void LeaveRoom()
        {
            _ = Voice.LeaveAsync();
            _voiceJoined = false;
            _network.Shutdown();
            _lobby.LeaveLobby();
            RoomSession.Reset();
            _appEntry.Flow.TransitionTo(AppState.Lobby);
        }

        private void HandleLobbyCreated(Lobby lobby)
        {
            // Host is the source of truth: init seats, then start hosting over the relay.
            RoomSession.Initialize(
                new SeatManager(_roomConfig.SeatCount),
                new SeatReservations(_roomConfig.SeatHoldSeconds));
            _network.StartHost();
        }

        private void HandleLobbyEntered(Lobby lobby)
        {
            // Guests connect to the lobby owner over Steam Relay. (Owner already hosts.)
            if (_lobby.IsAvailable && lobby.Owner.Id.Value != SteamClient.SteamId.Value)
            {
                _network.StartClient(lobby.Owner.Id.Value);
            }

            // Voice peers = current lobby members (both host and guests track everyone).
            foreach (Friend member in lobby.Members)
            {
                VoiceRoster.AddPeer(member.Id.Value);
            }
        }

        private void HandleMemberJoined(Lobby lobby, Friend member) => VoiceRoster.AddPeer(member.Id.Value);

        private void HandleMemberLeft(Lobby lobby, Friend member) => VoiceRoster.RemovePeer(member.Id.Value);

        private void HandleConnected()
        {
            if (!_voiceJoined && _lobby.CurrentLobby.HasValue)
            {
                _voiceJoined = true;
                _ = Voice.JoinAsync(_lobby.CurrentLobby.Value.Id.ToString());
            }

            _appEntry.Flow.TransitionTo(AppState.InRoom);
        }

        private void HandleDisconnected()
        {
            _ = Voice.LeaveAsync();
            _voiceJoined = false;
            _appEntry.Flow.TransitionTo(AppState.Lobby);
        }

        // ---- Temporary IMGUI lobby panel (replaced by real LobbyUI later) ----

        private void OnGUI()
        {
            const float w = 220f;
            GUILayout.BeginArea(new Rect(10, 10, w, 200), GUI.skin.box);
            GUILayout.Label(_lobby.IsAvailable ? $"Steam: {SteamClient.Name}" : "Steam: not running");

            if (!_network.IsRunning)
            {
                GUI.enabled = _lobby.IsAvailable;
                if (GUILayout.Button("Create Room"))
                {
                    HostRoom();
                }

                GUILayout.Label("Join: accept a Steam invite");
                GUI.enabled = true;
            }
            else
            {
                GUILayout.Label(_network.IsHost ? "Hosting room" : "In room (guest)");
                if (_lobby.CurrentLobby.HasValue && GUILayout.Button("Invite Friends"))
                {
                    _lobby.OpenInviteOverlay();
                }

                if (GUILayout.Button("Leave Room"))
                {
                    LeaveRoom();
                }
            }

            GUILayout.EndArea();
        }
    }
}
