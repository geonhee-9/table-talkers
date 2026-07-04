using UnityEngine;

namespace TableTalkers.Voice
{
    /// <summary>
    /// Voice tunables kept out of code. Create via Assets > Create > TableTalkers > Voice Config.
    /// </summary>
    public enum VoiceBackend
    {
        SteamVoice = 0,
        Odin = 1
    }

    [CreateAssetMenu(fileName = "VoiceConfig", menuName = "TableTalkers/Voice Config", order = 2)]
    public sealed class VoiceConfig : ScriptableObject
    {
        [Header("Backend")]
        [Tooltip("Which IVoiceService implementation to use. Odin requires the ODIN SDK (v1).")]
        [SerializeField] private VoiceBackend _backend = VoiceBackend.SteamVoice;

        [Header("Audio quality (applied by backends that support them)")]
        [Tooltip("Echo cancellation. Steam voice applies its own pipeline; ODIN uses these flags.")]
        [SerializeField] private bool _echoCancellation = true;
        [SerializeField] private bool _noiseSuppression = true;
        [SerializeField] private bool _autoGainControl = true;

        [Tooltip("Steam P2P channel reserved for voice — keeps voice separate from state sync.")]
        [SerializeField, Min(0)] private int _voiceChannel = 1;

        [Tooltip("Fallback capture sample rate when Steam does not report one.")]
        [SerializeField] private int _fallbackSampleRate = 48000;

        [Tooltip("Seconds of audio buffered per peer before playback (jitter buffer). Latency vs stability.")]
        [SerializeField, Range(0.02f, 0.5f)] private float _jitterBufferSeconds = 0.08f;

        [Tooltip("Playback ring buffer capacity per peer, in seconds.")]
        [SerializeField, Range(0.5f, 5f)] private float _ringBufferSeconds = 2f;

        [Tooltip("How fast the measured speaking level decays per second.")]
        [SerializeField, Range(0.5f, 20f)] private float _levelDecayPerSecond = 6f;

        [Tooltip("Light stereo panning by seat direction (0 = mono). Not true 3D — that's ODIN in v1.")]
        [SerializeField, Range(0f, 1f)] private float _stereoPanStrength = 0.45f;

        public VoiceBackend Backend => _backend;
        public bool EchoCancellation => _echoCancellation;
        public bool NoiseSuppression => _noiseSuppression;
        public bool AutoGainControl => _autoGainControl;
        public int VoiceChannel => _voiceChannel;
        public int FallbackSampleRate => _fallbackSampleRate;
        public float JitterBufferSeconds => _jitterBufferSeconds;
        public float RingBufferSeconds => _ringBufferSeconds;
        public float LevelDecayPerSecond => _levelDecayPerSecond;
        public float StereoPanStrength => _stereoPanStrength;
    }
}
