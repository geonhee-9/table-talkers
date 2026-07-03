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
            { "room.leave", "Leave Room" }
        };

        public static string Get(string key)
        {
            return _en.TryGetValue(key, out string value) ? value : key;
        }
    }
}
