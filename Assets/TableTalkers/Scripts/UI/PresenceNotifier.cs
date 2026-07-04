using System.Collections.Generic;
using UnityEngine;
using TableTalkers.Core;

namespace TableTalkers.UI
{
    /// <summary>
    /// Join/leave awareness: shows a brief toast ("○○ joined the table") and drops a system line
    /// into chat history, so arrivals and departures never go unnoticed. Attach to the App object.
    /// </summary>
    public sealed class PresenceNotifier : MonoBehaviour
    {
        private struct Toast
        {
            public string Text;
            public float ShownAt;
        }

        private const float ToastSeconds = 4f;
        private readonly List<Toast> _toasts = new();

        private void OnEnable()
        {
            ParticipantRegistry.Added += HandleAdded;
            ParticipantRegistry.Removed += HandleRemoved;
        }

        private void OnDisable()
        {
            ParticipantRegistry.Added -= HandleAdded;
            ParticipantRegistry.Removed -= HandleRemoved;
        }

        private void HandleAdded(IParticipant p)
        {
            if (!p.IsLocal)
            {
                Push(string.Format(Loc.Get("toast.joined"), SafeName(p)));
            }
        }

        private void HandleRemoved(IParticipant p)
        {
            if (!p.IsLocal)
            {
                Push(string.Format(Loc.Get("toast.left"), SafeName(p)));
            }
        }

        private static string SafeName(IParticipant p)
        {
            return string.IsNullOrEmpty(p.DisplayName) ? "?" : p.DisplayName;
        }

        private void Push(string text)
        {
            _toasts.Add(new Toast { Text = text, ShownAt = Time.unscaledTime });
            ChatChannel.History.Add(new ChatChannel.ChatMessage("•", text));
        }

        private void OnGUI()
        {
            float now = Time.unscaledTime;
            _toasts.RemoveAll(t => now - t.ShownAt > ToastSeconds);
            if (_toasts.Count == 0)
            {
                return;
            }

            const float w = 260f;
            GUILayout.BeginArea(new Rect(Screen.width / 2f - w / 2f, 14f, w, 120f));
            foreach (Toast toast in _toasts)
            {
                float age = now - toast.ShownAt;
                float alpha = Mathf.Clamp01((ToastSeconds - age) / 1f);
                GUI.color = new Color(1f, 1f, 1f, alpha);
                GUILayout.Label(toast.Text, GUI.skin.box, GUILayout.ExpandWidth(true));
            }

            GUI.color = Color.white;
            GUILayout.EndArea();
        }
    }
}
