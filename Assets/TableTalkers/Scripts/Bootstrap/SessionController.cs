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
        [SerializeField] private TableTalkers.Moderation.HostModeration _hostModeration;
        [SerializeField] private SteamDlc _dlc;

        private bool _voiceJoined;

        private IVoiceService Voice => _voiceSelector.Service;
        private IVoicePeerRoster VoiceRoster => _voiceSelector.Roster;

        private void Start()
        {
            // Self-heal: same-GameObject components are found automatically if refs are broken.
            if (_appEntry == null) _appEntry = GetComponent<AppEntry>();
            if (_lobby == null) _lobby = GetComponent<SteamLobby>();
            if (_network == null) _network = GetComponent<NetworkSessionService>();
            if (_voiceSelector == null) _voiceSelector = GetComponent<VoiceServiceSelector>();
            if (_hostModeration == null) _hostModeration = GetComponent<TableTalkers.Moderation.HostModeration>();
            if (_dlc == null) _dlc = GetComponent<SteamDlc>();
            if (_roomConfig == null) _roomConfig = Resources.Load<RoomConfig>("RoomConfig");

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

            Debug.Log($"[Session] Running. Steam available: {_lobby.IsAvailable}. " +
                      "If you see this in Play mode, the Create Room panel is drawing top-left.");

            if (_hostModeration != null)
            {
                // Room lock = the lobby stops accepting joins (moderation stays Platform-free).
                _hostModeration.RoomLockChanged += locked => _lobby.SetRoomJoinable(!locked);
            }
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

        /// <summary>
        /// Host path: create an invite-only lobby. Host-pays model: if the host owns Pro,
        /// the WHOLE room upgrades (capacity up to MaxCapacity); guests always enter free.
        /// </summary>
        public void HostRoom()
        {
            bool pro = _dlc != null && _dlc.OwnsPro();
            int capacity = pro ? _roomConfig.MaxCapacity : _roomConfig.SeatCount;
            RoomEntitlements.Set(pro, capacity);
            _ = _lobby.CreateLobbyAsync(capacity);
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
            // Advertise the room tier so guests know without owning anything (host-pays).
            lobby.SetData("pro", RoomEntitlements.ProActive ? "1" : "0");
            lobby.SetData("capacity", RoomEntitlements.RoomCapacity.ToString());

            // Host is the source of truth: init seats, then start hosting over the relay.
            RoomSession.Initialize(
                new SeatManager(RoomEntitlements.RoomCapacity),
                new SeatReservations(_roomConfig.SeatHoldSeconds));
            _network.StartHost();

            // Pro conversion funnel: creating rooms is where the upgrade shows its value.
            AnalyticsClient.Instance?.Track("room_created",
                ("capacity", RoomEntitlements.RoomCapacity.ToString()),
                ("pro", RoomEntitlements.ProActive ? "1" : "0"));
        }

        private void HandleLobbyEntered(Lobby lobby)
        {
            _currentHostId = lobby.Owner.Id.Value;

            // Guests read the room tier from lobby data (free entry to upgraded rooms).
            if (lobby.Owner.Id.Value != SteamClient.SteamId.Value)
            {
                bool pro = lobby.GetData("pro") == "1";
                int capacity = int.TryParse(lobby.GetData("capacity"), out int c) ? c : _roomConfig.SeatCount;
                RoomEntitlements.Set(pro, capacity);
            }

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

        private void HandleMemberLeft(Lobby lobby, Friend member)
        {
            VoiceRoster.RemovePeer(member.Id.Value);

            // Host migration: when the host leaves, Steam auto-transfers lobby ownership.
            // The new owner rehosts; everyone else reconnects to them. Seats reassign on respawn.
            if (_currentHostId != 0 && member.Id.Value == _currentHostId)
            {
                MigrateHost(lobby);
            }
        }

        private ulong _currentHostId;

        private void MigrateHost(Lobby lobby)
        {
            _network.Shutdown();
            _voiceJoined = false;

            ulong newOwner = lobby.Owner.Id.Value;
            _currentHostId = newOwner;

            if (newOwner == SteamClient.SteamId.Value)
            {
                Debug.Log("[Session] Host left — this client is the new host.");
                RoomSession.Initialize(
                    new SeatManager(_roomConfig.SeatCount),
                    new SeatReservations(_roomConfig.SeatHoldSeconds));
                _network.StartHost();
            }
            else
            {
                Debug.Log("[Session] Host left — reconnecting to the new host.");
                _network.StartClient(newOwner);
            }
        }

        private void HandleConnected()
        {
            if (!_voiceJoined && _lobby.CurrentLobby.HasValue)
            {
                _voiceJoined = true;
                _ = Voice.JoinAsync(_lobby.CurrentLobby.Value.Id.ToString());
            }

            _appEntry.Flow.TransitionTo(AppState.InRoom);

            AnalyticsClient.Instance?.Track("room_joined",
                ("is_host", _network.IsHost ? "1" : "0"),
                ("members", _lobby.CurrentLobby?.MemberCount.ToString() ?? "?"));
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
