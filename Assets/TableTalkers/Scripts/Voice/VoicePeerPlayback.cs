using UnityEngine;
using TableTalkers.Core;

namespace TableTalkers.Voice
{
    /// <summary>
    /// Per-peer voice playback: a ring buffer of decoded PCM fed by SteamVoiceService and drained
    /// by OnAudioFilterRead. Non-spatial (2D) for M0 — the spatial implementation arrives with ODIN.
    /// Created at runtime by SteamVoiceService; not meant to be placed by hand.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public sealed class VoicePeerPlayback : MonoBehaviour
    {
        private float[] _ring;
        private int _writePos;
        private int _readPos;
        private int _buffered;
        private int _sourceRate;
        private int _outputRate;
        private int _warmupSamples;
        private bool _started;
        private readonly object _lock = new();

        /// <summary>0..1 playback volume for this peer.</summary>
        public float Volume { get; set; } = 1f;

        /// <summary>Locally muted — audio is dropped, not played.</summary>
        public bool Muted { get; set; }

        /// <summary>Smoothed output level 0..1, for speaking indicators/lipsync.</summary>
        public float Level { get; private set; }

        /// <summary>This peer's participant id — used to pan audio toward their seat.</summary>
        public string ParticipantId { get; set; }

        /// <summary>0 = mono; higher = stronger left/right placement by seat direction.</summary>
        public float PanStrength { get; set; }

        private float _levelDecayPerSecond = 6f;
        private volatile float _pan; // -1 (left) .. +1 (right), updated on main thread

        public void Configure(int sourceSampleRate, float ringSeconds, float jitterSeconds, float levelDecayPerSecond)
        {
            _outputRate = AudioSettings.outputSampleRate;
            _sourceRate = sourceSampleRate > 0 ? sourceSampleRate : _outputRate;
            _ring = new float[Mathf.CeilToInt(_outputRate * ringSeconds)];
            _warmupSamples = Mathf.CeilToInt(_outputRate * jitterSeconds);
            _levelDecayPerSecond = levelDecayPerSecond;

            var source = GetComponent<AudioSource>();
            source.spatialBlend = 0f; // non-spatial for M0 (IsSpatial=false)
            source.loop = true;

            // A looping dummy clip keeps the source "playing" so OnAudioFilterRead is invoked.
            var dummy = AudioClip.Create("voice-driver", 1, 1, _outputRate, false);
            dummy.SetData(new[] { 0f }, 0);
            source.clip = dummy;
            source.Play();
        }

        /// <summary>Enqueue 16-bit PCM samples (already converted to float -1..1) at the source rate.</summary>
        public void Enqueue(float[] samples, int count)
        {
            if (_ring == null || Muted)
            {
                return;
            }

            lock (_lock)
            {
                // Naive linear resample from source rate to output rate while writing.
                float step = (float)_sourceRate / _outputRate;
                float pos = 0f;
                while (pos < count - 1)
                {
                    int i = (int)pos;
                    float frac = pos - i;
                    float sample = Mathf.Lerp(samples[i], samples[i + 1], frac);

                    if (_buffered < _ring.Length)
                    {
                        _ring[_writePos] = sample;
                        _writePos = (_writePos + 1) % _ring.Length;
                        _buffered++;
                    }

                    pos += step;
                }

                if (!_started && _buffered >= _warmupSamples)
                {
                    _started = true;
                }
            }
        }

        private void Update()
        {
            Level = Mathf.Max(0f, Level - _levelDecayPerSecond * Time.deltaTime * Level);
            UpdatePan();
        }

        /// <summary>
        /// Seat-direction stereo pan: a peer sitting to my head-relative left sounds from the left.
        /// Uses Core abstractions only (participants + seat anchors); real 3D audio arrives with
        /// ODIN in v1 — this is a light presence aid, IsSpatial stays false.
        /// </summary>
        private void UpdatePan()
        {
            if (PanStrength <= 0f || string.IsNullOrEmpty(ParticipantId)
                || SeatAnchorRegistry.Instance == null)
            {
                _pan = 0f;
                return;
            }

            IParticipant local = ParticipantRegistry.Local;
            IParticipant peer = null;
            var all = ParticipantRegistry.All;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].Id == ParticipantId)
                {
                    peer = all[i];
                    break;
                }
            }

            if (local == null || peer == null)
            {
                _pan = 0f;
                return;
            }

            Transform localAnchor = SeatAnchorRegistry.Instance.GetAnchor(local.SeatIndex);
            Transform peerAnchor = SeatAnchorRegistry.Instance.GetAnchor(peer.SeatIndex);
            if (localAnchor == null || peerAnchor == null)
            {
                _pan = 0f;
                return;
            }

            Vector3 toPeer = peerAnchor.position - localAnchor.position;
            if (toPeer.sqrMagnitude < 0.0001f)
            {
                _pan = 0f;
                return;
            }

            Quaternion headRotation = localAnchor.rotation * local.HeadOrientation;
            Vector3 right = headRotation * Vector3.right;
            _pan = Mathf.Clamp(Vector3.Dot(toPeer.normalized, right), -1f, 1f) * PanStrength;
        }

        private void OnAudioFilterRead(float[] data, int channels)
        {
            if (_ring == null)
            {
                return;
            }

            float peak = 0f;
            // Equal-power pan gains (constant loudness while placing the voice left/right).
            float pan = _pan;
            float panAngle = (pan + 1f) * 0.25f * Mathf.PI;
            float leftGain = Mathf.Cos(panAngle);
            float rightGain = Mathf.Sin(panAngle);

            lock (_lock)
            {
                for (int frame = 0; frame < data.Length; frame += channels)
                {
                    float sample = 0f;
                    if (_started && _buffered > 0)
                    {
                        sample = _ring[_readPos] * Volume;
                        _readPos = (_readPos + 1) % _ring.Length;
                        _buffered--;
                    }
                    else if (_started && _buffered == 0)
                    {
                        _started = false; // underrun — rebuffer before resuming
                    }

                    float abs = sample < 0 ? -sample : sample;
                    if (abs > peak)
                    {
                        peak = abs;
                    }

                    if (channels >= 2)
                    {
                        data[frame] = sample * leftGain * 1.41f;
                        data[frame + 1] = sample * rightGain * 1.41f;
                        for (int c = 2; c < channels; c++)
                        {
                            data[frame + c] = sample;
                        }
                    }
                    else
                    {
                        data[frame] = sample;
                    }
                }
            }

            if (peak > Level)
            {
                Level = Mathf.Clamp01(peak);
            }
        }
    }
}
