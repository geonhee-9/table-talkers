using UnityEngine;

namespace TableTalkers.Voice
{
    public enum VoiceInputMode
    {
        VoiceActivated = 0,
        PushToTalk = 1
    }

    /// <summary>
    /// Local voice preferences persisted via PlayerPrefs. Pure data — the input driver and UI
    /// read/write this; the voice backend only ever sees SetInputEnabled.
    /// (Input device selection arrives with ODIN in v1 — Steam voice uses the system default.)
    /// </summary>
    public static class VoiceSettings
    {
        private const string ModeKey = "tt.voice.mode";
        private const string MutedKey = "tt.voice.muted";

        public static VoiceInputMode Mode
        {
            get => (VoiceInputMode)PlayerPrefs.GetInt(ModeKey, (int)VoiceInputMode.VoiceActivated);
            set
            {
                PlayerPrefs.SetInt(ModeKey, (int)value);
                PlayerPrefs.Save();
            }
        }

        /// <summary>Local mic muted by the user (independent of PTT).</summary>
        public static bool Muted
        {
            get => PlayerPrefs.GetInt(MutedKey, 0) == 1;
            set
            {
                PlayerPrefs.SetInt(MutedKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }
    }
}
