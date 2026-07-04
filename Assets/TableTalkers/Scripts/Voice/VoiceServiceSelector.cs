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
            // Self-heal: broken/missing inspector refs must never break voice.
            // Find siblings on this GameObject; create them if absent.
            if (_steam == null)
            {
                _steam = GetComponent<SteamVoiceService>();
            }

            if (_steam == null)
            {
                _steam = gameObject.AddComponent<SteamVoiceService>();
            }

            if (_odin == null)
            {
                _odin = GetComponent<OdinVoiceService>();
            }

            _steam.SetConfigIfMissing(_config);
            if (_odin != null)
            {
                _odin.SetConfigIfMissing(_config);
            }

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

            VoiceServices.Set(Service);
        }
    }
}
