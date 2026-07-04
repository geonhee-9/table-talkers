using UnityEngine;
using UnityEngine.InputSystem;
using TableTalkers.Core;

namespace TableTalkers.Moderation
{
    /// <summary>
    /// Per-participant moderation actions (K toggles): Block / Report for everyone,
    /// Kick / room lock for the host. IMGUI for MVP; logic lives in the controllers.
    /// </summary>
    public sealed class ModerationPanel : MonoBehaviour
    {
        [SerializeField] private ReportClient _reportClient;
        [SerializeField] private HostModeration _hostModeration;

        private bool _visible;

        private void Start()
        {
            // Self-heal: find siblings if inspector refs are broken.
            if (_reportClient == null) _reportClient = GetComponent<ReportClient>();
            if (_hostModeration == null) _hostModeration = GetComponent<HostModeration>();
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.kKey.wasPressedThisFrame)
            {
                _visible = !_visible;
            }
        }

        private void OnGUI()
        {
            if (!_visible)
            {
                return;
            }

            const float w = 320f;
            GUILayout.BeginArea(new Rect(Screen.width / 2f - w / 2f, 40, w, 400), GUI.skin.box);
            GUILayout.Label("Safety (K)");

            bool isHost = _hostModeration != null && _hostModeration.IsHost;
            if (isHost)
            {
                bool locked = GUILayout.Toggle(_hostModeration.RoomLocked, "Lock room (no new joins)");
                if (locked != _hostModeration.RoomLocked)
                {
                    _hostModeration.SetRoomLocked(locked);
                }
            }

            var participants = ParticipantRegistry.All;
            for (int i = 0; i < participants.Count; i++)
            {
                IParticipant p = participants[i];
                if (p.IsLocal)
                {
                    continue;
                }

                GUILayout.BeginHorizontal();
                GUILayout.Label(p.DisplayName, GUILayout.Width(100f));

                bool blocked = BlockList.IsBlocked(p.Id);
                if (GUILayout.Button(blocked ? "Unblock" : "Block"))
                {
                    BlockList.SetBlocked(p.Id, !blocked);
                }

                if (_reportClient != null && GUILayout.Button("Report"))
                {
                    _reportClient.Report(p, "user_report");
                }

                if (isHost && GUILayout.Button("Kick"))
                {
                    _hostModeration.Kick(p);
                }

                GUILayout.EndHorizontal();
            }

            GUILayout.EndArea();
        }
    }
}
