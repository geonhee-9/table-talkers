using UnityEngine;

namespace TableTalkers.Voice
{
    /// <summary>
    /// Voice tunables kept out of code. Create via Assets > Create > TableTalkers > Voice Config.
    /// </summary>
    [CreateAssetMenu(fileName = "VoiceConfig", menuName = "TableTalkers/Voice Config", order = 2)]
    public sealed class VoiceConfig : ScriptableObject
    {
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

        public int VoiceChannel => _voiceChannel;
        public int FallbackSampleRate => _fallbackSampleRate;
        public float JitterBufferSeconds => _jitterBufferSeconds;
        public float RingBufferSeconds => _ringBufferSeconds;
        public float LevelDecayPerSecond => _levelDecayPerSecond;
    }
}
