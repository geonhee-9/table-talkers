using System.Collections.Generic;

namespace TableTalkers.UI
{
    /// <summary>
    /// Minimal localization stub: every UI string goes through a key (no hardcoded UI text),
    /// with English defaults until a real localization table replaces this in v1.
    /// </summary>
    public static class Loc
    {
        private static readonly Dictionary<string, string> _en = new()
        {
            { "mic.title", "Mic Settings (M)" },
            { "mic.mode", "Input mode" },
            { "mic.mode.vad", "Voice activated" },
            { "mic.mode.ptt", "Push to talk (V)" },
            { "mic.muted", "Mute my mic" },
            { "mic.peers", "Participants" },
            { "mic.peer.mute", "Mute" },
            { "mic.device.note", "Device selection arrives with spatial voice (v1)." },
            { "room.create", "Create Room" },
            { "room.invite", "Invite Friends" },
            { "room.leave", "Leave Room" },
            { "chat.title", "Chat (Tab)" },
            { "chat.send", "Send" },
            { "onboard.title", "Welcome — quick mic check" },
            { "onboard.mic", "Microphone" },
            { "onboard.test", "Test mic" },
            { "onboard.testing", "Speak now — the bar should move" },
            { "onboard.hear", "Can everyone hear you? All set!" },
            { "onboard.done", "Start talking" },
            { "settings.title", "Settings (O)" },
            { "settings.nameplates", "Always show nameplates" },
            { "settings.privacy", "Privacy Policy" },
            { "settings.terms", "Terms of Service" },
            { "settings.eula", "EULA" },
            { "toast.joined", "{0} joined the table" },
            { "toast.left", "{0} left the table" },
            { "hint.bar", "V hold=talk · 1-5 emotes · Tab chat · M mic · K safety · O settings · F1 debug · H hide" }
        };

        public static string Get(string key)
        {
            return _en.TryGetValue(key, out string value) ? value : key;
        }
    }
}
