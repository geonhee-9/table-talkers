using UnityEngine;

namespace TableTalkers.Voice
{
    /// <summary>
    /// Picks the active voice backend (Steam voice now, ODIN in v1) from VoiceConfig at startup and
    /// publishes it via <see cref="VoiceServices"/>. Swapping backends = changing one config value;
    /// nothing above IVoiceService changes. Attach next to both backend components.
    /// </summary>
    public sealed class VoiceServiceSelector : MonoBehaviour
    {
        [SerializeField] private VoiceConfig _config;
        [SerializeField] private SteamVoiceService _steam;
        [SerializeField] private OdinVoiceService _odin;

        public IVoiceService Service { get; private set; }
        public IVoicePeerRoster Roster { get; private set; }

        private void Awake()
        {
            bool useOdin = _config != null && _config.Backend == VoiceBackend.Odin && _odin != null;
            if (useOdin)
            {
                Service = _odin;
                Roster = _odin;
            }
            else
            {
                Service = _steam;
                Roster = _steam;
            }

            if (Service == null)
            {
                Debug.LogError("[VoiceSelector] No voice backend assigned.");
                return;
            }

            VoiceServices.Set(Service);
        }
    }
}
