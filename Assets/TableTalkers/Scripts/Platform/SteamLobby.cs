using System;
using System.Threading.Tasks;
using Steamworks;
using Steamworks.Data;
using UnityEngine;

namespace TableTalkers.Platform
{
    /// <summary>
    /// Thin wrapper over Facepunch.Steamworks: initializes Steam, pumps callbacks, and covers the
    /// lobby lifecycle (create / join / leave / invite). Higher layers (the session) subscribe to the
    /// events below and never touch the Steam API directly. Everything is guarded so a missing or
    /// not-running Steam client degrades to <see cref="IsAvailable"/> == false instead of crashing.
    /// </summary>
    public sealed class SteamLobby : MonoBehaviour
    {
        [SerializeField] private SteamConfig _config;

        /// <summary>True when the Steam client initialized successfully.</summary>
        public bool IsAvailable { get; private set; }

        /// <summary>The lobby this client is currently in, if any.</summary>
        public Lobby? CurrentLobby { get; private set; }

        // Events surfaced to the session layer. Kept as plain C# events so Platform depends on nothing above it.
        public event Action<Lobby> LobbyEntered;
        public event Action<Lobby> LobbyCreated;
        public event Action LobbyCreateFailed;
        public event Action<Lobby, Friend> MemberJoined;
        public event Action<Lobby, Friend> MemberLeft;
        /// <summary>Raised when the local user accepts an invite (from overlay/friends list).</summary>
        public event Action<Lobby> InviteAccepted;

        private void Awake()
        {
            TryInitSteam();
        }

        private void TryInitSteam()
        {
            if (_config == null)
            {
                Debug.LogError("[SteamLobby] SteamConfig not assigned; cannot initialize Steam.");
                IsAvailable = false;
                return;
            }

            try
            {
                if (!SteamClient.IsValid)
                {
                    SteamClient.Init(_config.AppId, asyncCallbacks: false);
                }

                IsAvailable = SteamClient.IsValid;
            }
            catch (Exception e)
            {
                // Steam client not running / not installed — stay available == false, don't crash.
                Debug.LogWarning($"[SteamLobby] Steam init failed ({e.Message}). Running without Steam.");
                IsAvailable = false;
                return;
            }

            SubscribeSteamEvents();
        }

        private void SubscribeSteamEvents()
        {
            SteamMatchmaking.OnLobbyEntered += HandleLobbyEntered;
            SteamMatchmaking.OnLobbyMemberJoined += HandleMemberJoined;
            SteamMatchmaking.OnLobbyMemberLeave += HandleMemberLeft;
            SteamFriends.OnGameLobbyJoinRequested += HandleJoinRequested;
        }

        private void UnsubscribeSteamEvents()
        {
            SteamMatchmaking.OnLobbyEntered -= HandleLobbyEntered;
            SteamMatchmaking.OnLobbyMemberJoined -= HandleMemberJoined;
            SteamMatchmaking.OnLobbyMemberLeave -= HandleMemberLeft;
            SteamFriends.OnGameLobbyJoinRequested -= HandleJoinRequested;
        }

        private void Update()
        {
            if (IsAvailable)
            {
                SteamClient.RunCallbacks();
            }
        }

        private void OnDestroy()
        {
            if (!IsAvailable)
            {
                return;
            }

            UnsubscribeSteamEvents();
            LeaveLobby();
            SteamClient.Shutdown();
            IsAvailable = false;
        }

        /// <summary>Creates a lobby with the given capacity. Owner becomes the host. Invite-only by default.</summary>
        public async Task CreateLobbyAsync(int maxMembers)
        {
            if (!IsAvailable)
            {
                LobbyCreateFailed?.Invoke();
                return;
            }

            try
            {
                Lobby? created = await SteamMatchmaking.CreateLobbyAsync(maxMembers);
                if (created == null)
                {
                    LobbyCreateFailed?.Invoke();
                    return;
                }

                Lobby lobby = created.Value;
                // Invite-only per product model: friends can be invited, not publicly discoverable.
                lobby.SetFriendsOnly();
                lobby.SetJoinable(true);

                CurrentLobby = lobby;
                LobbyCreated?.Invoke(lobby);
                // OnLobbyEntered also fires for the creator, surfacing LobbyEntered.
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SteamLobby] CreateLobby failed: {e.Message}");
                LobbyCreateFailed?.Invoke();
            }
        }

        /// <summary>Joins a lobby by its Steam id (e.g. from an invite or a shared code).</summary>
        public async Task JoinLobbyAsync(SteamId lobbyId)
        {
            if (!IsAvailable)
            {
                return;
            }

            try
            {
                await SteamMatchmaking.JoinLobbyAsync(lobbyId);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SteamLobby] JoinLobby failed: {e.Message}");
            }
        }

        /// <summary>Opens the Steam overlay invite dialog for the current lobby.</summary>
        public void OpenInviteOverlay()
        {
            if (IsAvailable && CurrentLobby.HasValue)
            {
                SteamFriends.OpenGameInviteOverlay(CurrentLobby.Value.Id);
            }
        }

        /// <summary>Host: allow/deny new joins (room lock).</summary>
        public void SetRoomJoinable(bool joinable)
        {
            if (IsAvailable && CurrentLobby.HasValue)
            {
                CurrentLobby.Value.SetJoinable(joinable);
            }
        }

        /// <summary>Leaves the current lobby, if in one.</summary>
        public void LeaveLobby()
        {
            if (CurrentLobby.HasValue)
            {
                CurrentLobby.Value.Leave();
                CurrentLobby = null;
            }
        }

        private void HandleLobbyEntered(Lobby lobby)
        {
            CurrentLobby = lobby;
            LobbyEntered?.Invoke(lobby);
        }

        private void HandleMemberJoined(Lobby lobby, Friend member) => MemberJoined?.Invoke(lobby, member);

        private void HandleMemberLeft(Lobby lobby, Friend member) => MemberLeft?.Invoke(lobby, member);

        private async void HandleJoinRequested(Lobby lobby, SteamId invitedBy)
        {
            // User accepted an invite from the overlay/friends list — join the lobby.
            await SteamMatchmaking.JoinLobbyAsync(lobby.Id);
            InviteAccepted?.Invoke(lobby);
        }
    }
}
