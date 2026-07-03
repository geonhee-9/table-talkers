using UnityEngine;
using UnityEngine.InputSystem;
using TableTalkers.Core;
using TableTalkers.Networking;

namespace TableTalkers.UI
{
    /// <summary>
    /// Always-available latency/presence overlay (F1 toggles). Latency is the product's primary
    /// metric, so this exists from M0 onward: RTT, participant count, per-participant speaking.
    /// IMGUI on purpose — zero scene wiring needed. Attach to any persistent object.
    /// </summary>
    public sealed class DebugOverlay : MonoBehaviour
    {
        [SerializeField] private bool _visible = true;

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame)
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

            const float w = 260f;
            GUILayout.BeginArea(new Rect(Screen.width - w - 10, 10, w, 320), GUI.skin.box);
            GUILayout.Label("Debug (F1)");

            float rtt = PingProbe.LastRttMs;
            GUILayout.Label(rtt < 0 ? "RTT: --" : $"RTT: {rtt:F0} ms");

            var participants = ParticipantRegistry.All;
            GUILayout.Label($"Participants: {participants.Count}");

            for (int i = 0; i < participants.Count; i++)
            {
                IParticipant p = participants[i];
                string speaking = p.IsSpeaking ? " 🔊" : "";
                string local = p.IsLocal ? " (you)" : "";
                GUILayout.Label($"  seat {p.SeatIndex}: {p.DisplayName}{local}{speaking}");
            }

            GUILayout.EndArea();
        }
    }
}
