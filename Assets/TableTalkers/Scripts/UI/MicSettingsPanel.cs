using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using TableTalkers.Core;
using TableTalkers.Voice;

namespace TableTalkers.UI
{
    /// <summary>
    /// Mic settings panel (M toggles): PTT vs voice-activated, local mute, and per-participant
    /// volume/mute. Depends on IVoiceService + IParticipant only; preferences persist locally.
    /// IMGUI for MVP — replaced by a real canvas UI later without touching the logic underneath.
    /// </summary>
    public sealed class MicSettingsPanel : MonoBehaviour
    {
        [SerializeField] private bool _visible;

        private readonly Dictionary<string, float> _volumes = new();
        private readonly Dictionary<string, bool> _mutes = new();

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.mKey.wasPressedThisFrame)
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

            IVoiceService voice = VoiceServices.Current;
            const float w = 300f;
            GUILayout.BeginArea(new Rect(10, 220, w, 400), GUI.skin.box);
            GUILayout.Label(Loc.Get("mic.title"));

            // Input mode
            GUILayout.Label(Loc.Get("mic.mode"));
            bool vad = VoiceSettings.Mode == VoiceInputMode.VoiceActivated;
            if (GUILayout.Toggle(vad, Loc.Get("mic.mode.vad")) != vad)
            {
                VoiceSettings.Mode = vad ? VoiceInputMode.PushToTalk : VoiceInputMode.VoiceActivated;
            }

            GUILayout.Label(Loc.Get("mic.mode.ptt"));

            // Local mute
            bool muted = GUILayout.Toggle(VoiceSettings.Muted, Loc.Get("mic.muted"));
            if (muted != VoiceSettings.Muted)
            {
                VoiceSettings.Muted = muted;
            }

            GUILayout.Label(Loc.Get("mic.device.note"));
            GUILayout.Space(8f);

            // Per-participant volume / mute
            GUILayout.Label(Loc.Get("mic.peers"));
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

                float current = _volumes.TryGetValue(p.Id, out float v) ? v : 1f;
                float updated = GUILayout.HorizontalSlider(current, 0f, 1f, GUILayout.Width(90f));
                if (!Mathf.Approximately(updated, current))
                {
                    _volumes[p.Id] = updated;
                    voice?.SetPeerVolume(p.Id, updated);
                }

                bool peerMuted = _mutes.TryGetValue(p.Id, out bool m) && m;
                bool newMuted = GUILayout.Toggle(peerMuted, Loc.Get("mic.peer.mute"));
                if (newMuted != peerMuted)
                {
                    _mutes[p.Id] = newMuted;
                    voice?.SetPeerMuted(p.Id, newMuted);
                }

                GUILayout.EndHorizontal();
            }

            GUILayout.EndArea();
        }
    }
}
