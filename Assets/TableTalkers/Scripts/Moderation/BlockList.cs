using System;
using System.Collections.Generic;
using UnityEngine;

namespace TableTalkers.Moderation
{
    /// <summary>
    /// Locally persisted set of blocked participant ids. Blocking hides and silences the other
    /// party for this user (mutual non-exposure is completed by the peer's own copy of the block
    /// arriving via reports/backend later — locally we can only control our side).
    /// </summary>
    public static class BlockList
    {
        private const string PrefsKey = "tt.moderation.blocked";
        private static HashSet<string> _blocked;

        public static event Action<string, bool> Changed;

        public static bool IsBlocked(string participantId)
        {
            EnsureLoaded();
            return _blocked.Contains(participantId);
        }

        public static void SetBlocked(string participantId, bool blocked)
        {
            EnsureLoaded();
            bool changed = blocked ? _blocked.Add(participantId) : _blocked.Remove(participantId);
            if (changed)
            {
                Save();
                Changed?.Invoke(participantId, blocked);
            }
        }

        private static void EnsureLoaded()
        {
            if (_blocked != null)
            {
                return;
            }

            _blocked = new HashSet<string>();
            string raw = PlayerPrefs.GetString(PrefsKey, "");
            foreach (string id in raw.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                _blocked.Add(id);
            }
        }

        private static void Save()
        {
            PlayerPrefs.SetString(PrefsKey, string.Join(";", _blocked));
            PlayerPrefs.Save();
        }
    }
}
