using UnityEngine;
using UnityEngine.InputSystem;
using TableTalkers.Core;

namespace TableTalkers.UI
{
    /// <summary>
    /// Chat window (Tab toggles, Enter sends). Finds the local player's ChatChannel to send.
    /// IMGUI for MVP; strings via Loc keys.
    /// </summary>
    public sealed class ChatPanel : MonoBehaviour
    {
        private bool _visible;
        private string _draft = "";
        private Vector2 _scroll;

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame)
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
            const float h = 240f;
            GUILayout.BeginArea(new Rect(10, Screen.height - h - 10, w, h), GUI.skin.box);
            GUILayout.Label(Loc.Get("chat.title"));

            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(h - 80f));
            var history = ChatChannel.History;
            for (int i = 0; i < history.Count; i++)
            {
                GUILayout.Label($"{history[i].SenderName}: {history[i].Text}");
            }

            GUILayout.EndScrollView();

            GUILayout.BeginHorizontal();
            _draft = GUILayout.TextField(_draft, ChatChannel.MaxMessageLength);
            bool send = GUILayout.Button(Loc.Get("chat.send"), GUILayout.Width(60f));
            if ((send || (Keyboard.current != null && Keyboard.current.enterKey.wasPressedThisFrame))
                && !string.IsNullOrWhiteSpace(_draft))
            {
                LocalChannel()?.Send(_draft);
                _draft = "";
            }

            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private static ChatChannel LocalChannel()
        {
            return ParticipantRegistry.Local is Component c ? c.GetComponent<ChatChannel>() : null;
        }
    }
}
