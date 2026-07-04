using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Steamworks;
using UnityEngine;

namespace TableTalkers.Voice
{
    /// <summary>
    /// M0 voice: Steam built-in voice implementing <see cref="IVoiceService"/> (IsSpatial=false).
    /// Capture: SteamUser voice record → compressed packets → Steam P2P on a dedicated channel
    /// (voice never rides the NGO state-sync path — two-independent-paths invariant).
    /// Playback: decompress per peer into a <see cref="VoicePeerPlayback"/> ring buffer.
    /// No gameplay/presence code touches this class — only IVoiceService.
    /// The composition root manages the peer set via AddPeer/RemovePeer (lobby membership).
    /// </summary>
    public sealed class SteamVoiceService : MonoBehaviour, IVoiceService, IVoicePeerRoster
    {
        [SerializeField] private VoiceConfig _config;

        public bool IsSpatial => false;

        /// <summary>Self-heal path: lets the selector supply the config when the ref is missing.</summary>
        public void SetConfigIfMissing(VoiceConfig config)
        {
            if (_config == null)
            {
                _config = config;
            }
        }

        private readonly Dictionary<ulong, VoicePeerPlayback> _peers = new();
        private readonly HashSet<ulong> _mutedPeers = new();
        private readonly Dictionary<ulong, float> _pendingVolumes = new();

        private bool _joined;
        private bool _inputEnabled = true;
        private float _localLevel;

        private MemoryStream _captureStream;
        private MemoryStream _decompressStream;
        private float[] _floatScratch;
        private int _sampleRate;

        private void Awake()
        {
            _captureStream = new MemoryStream(8192);
            _decompressStream = new MemoryStream(22050);
            _floatScratch = new float[22050];
        }

        public Task JoinAsync(string roomId)
        {
            if (!SteamClient.IsValid)
            {
                Debug.LogWarning("[SteamVoice] Steam not available — voice disabled.");
                return Task.CompletedTask;
            }

            _sampleRate = ResolveSampleRate();
            SteamNetworking.AllowP2PPacketRelay(true);
            SteamNetworking.OnP2PSessionRequest = HandleSessionRequest;
            SteamUser.VoiceRecord = _inputEnabled;
            _joined = true;
            return Task.CompletedTask;
        }

        public Task LeaveAsync()
        {
            _joined = false;
            if (SteamClient.IsValid)
            {
                SteamUser.VoiceRecord = false;
            }

            foreach (VoicePeerPlayback playback in _peers.Values)
            {
                if (playback != null)
                {
                    Destroy(playback.gameObject);
                }
            }

            _peers.Clear();
            return Task.CompletedTask;
        }

        public void SetInputEnabled(bool enabled)
        {
            _inputEnabled = enabled;
            if (_joined && SteamClient.IsValid)
            {
                SteamUser.VoiceRecord = enabled;
            }
        }

        public void SetPeerVolume(string participantId, float volume01)
        {
            if (!ulong.TryParse(participantId, out ulong id))
            {
                return;
            }

            volume01 = Mathf.Clamp01(volume01);
            if (_peers.TryGetValue(id, out VoicePeerPlayback playback) && playback != null)
            {
                playback.Volume = volume01;
            }
            else
            {
                _pendingVolumes[id] = volume01; // applied when the peer joins
            }
        }

        public void SetPeerMuted(string participantId, bool muted)
        {
            if (!ulong.TryParse(participantId, out ulong id))
            {
                return;
            }

            if (muted)
            {
                _mutedPeers.Add(id);
            }
            else
            {
                _mutedPeers.Remove(id);
            }

            if (_peers.TryGetValue(id, out VoicePeerPlayback playback) && playback != null)
            {
                playback.Muted = muted;
            }
        }

        public float GetPeerSpeakingLevel(string participantId)
        {
            if (!ulong.TryParse(participantId, out ulong id))
            {
                return 0f;
            }

            if (SteamClient.IsValid && id == SteamClient.SteamId.Value)
            {
                return _localLevel;
            }

            return _peers.TryGetValue(id, out VoicePeerPlayback playback) && playback != null
                ? playback.Level
                : 0f;
        }

        /// <summary>Composition root: register a lobby member as a voice peer.</summary>
        public void AddPeer(ulong steamId)
        {
            if (!SteamClient.IsValid || steamId == SteamClient.SteamId.Value || _peers.ContainsKey(steamId))
            {
                return;
            }

            var go = new GameObject($"VoicePeer_{steamId}");
            go.transform.SetParent(transform, false);
            var playback = go.AddComponent<VoicePeerPlayback>();
            playback.Configure(
                _sampleRate > 0 ? _sampleRate : ResolveSampleRate(),
                _config != null ? _config.RingBufferSeconds : 2f,
                _config != null ? _config.JitterBufferSeconds : 0.08f,
                _config != null ? _config.LevelDecayPerSecond : 6f);
            playback.Muted = _mutedPeers.Contains(steamId);
            if (_pendingVolumes.TryGetValue(steamId, out float volume))
            {
                playback.Volume = volume;
                _pendingVolumes.Remove(steamId);
            }

            _peers[steamId] = playback;
        }

        /// <summary>Composition root: unregister a departed lobby member.</summary>
        public void RemovePeer(ulong steamId)
        {
            if (_peers.TryGetValue(steamId, out VoicePeerPlayback playback))
            {
                if (playback != null)
                {
                    Destroy(playback.gameObject);
                }

                _peers.Remove(steamId);
            }
        }

        private void Update()
        {
            if (!_joined || !SteamClient.IsValid)
            {
                return;
            }

            PumpCapture();
            PumpIncoming();
            DecayLocalLevel();
        }

        private void PumpCapture()
        {
            if (!_inputEnabled || !SteamUser.HasVoiceData)
            {
                return;
            }

            _captureStream.Position = 0;
            _captureStream.SetLength(0);
            int compressedBytes = SteamUser.ReadVoiceData(_captureStream);
            if (compressedBytes <= 0)
            {
                return;
            }

            byte[] packet = _captureStream.GetBuffer();

            // Local speaking level from our own compressed output (Steam gates silence internally,
            // so producing data ≈ speaking; decompress locally for a real amplitude).
            UpdateLocalLevel(packet, compressedBytes);

            int channel = _config != null ? _config.VoiceChannel : 1;
            foreach (ulong peerId in _peers.Keys)
            {
                SteamNetworking.SendP2PPacket(peerId, packet, compressedBytes, channel, P2PSend.UnreliableNoDelay);
            }
        }

        private void PumpIncoming()
        {
            int channel = _config != null ? _config.VoiceChannel : 1;
            while (SteamNetworking.IsP2PPacketAvailable(channel))
            {
                Steamworks.Data.P2Packet? packet = SteamNetworking.ReadP2PPacket(channel);
                if (packet == null)
                {
                    break;
                }

                ulong senderId = packet.Value.SteamId.Value;
                if (!_peers.TryGetValue(senderId, out VoicePeerPlayback playback) || playback == null)
                {
                    continue; // unknown sender (not a lobby member) — drop
                }

                int sampleCount = Decompress(packet.Value.Data, packet.Value.Data.Length);
                if (sampleCount > 0)
                {
                    playback.Enqueue(_floatScratch, sampleCount);
                }
            }
        }

        /// <summary>Decompress a voice packet into _floatScratch; returns sample count.</summary>
        private int Decompress(byte[] compressed, int length)
        {
            _decompressStream.Position = 0;
            _decompressStream.SetLength(0);

            int pcmBytes;
            try
            {
                using var input = new MemoryStream(compressed, 0, length, false);
                pcmBytes = SteamUser.DecompressVoice(input, length, _decompressStream);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SteamVoice] Decompress failed: {e.Message}");
                return 0;
            }

            if (pcmBytes <= 0)
            {
                return 0;
            }

            byte[] pcm = _decompressStream.GetBuffer();
            int samples = pcmBytes / 2; // 16-bit mono
            if (_floatScratch.Length < samples)
            {
                _floatScratch = new float[samples];
            }

            for (int i = 0; i < samples; i++)
            {
                short s = (short)(pcm[i * 2] | (pcm[i * 2 + 1] << 8));
                _floatScratch[i] = s / 32768f;
            }

            return samples;
        }

        private void UpdateLocalLevel(byte[] compressed, int length)
        {
            int samples = Decompress(compressed, length);
            float peak = 0f;
            for (int i = 0; i < samples; i++)
            {
                float abs = _floatScratch[i] < 0 ? -_floatScratch[i] : _floatScratch[i];
                if (abs > peak)
                {
                    peak = abs;
                }
            }

            if (peak > _localLevel)
            {
                _localLevel = Mathf.Clamp01(peak);
            }
        }

        private void DecayLocalLevel()
        {
            float decay = _config != null ? _config.LevelDecayPerSecond : 6f;
            _localLevel = Mathf.Max(0f, _localLevel - decay * Time.deltaTime * _localLevel);
        }

        private int ResolveSampleRate()
        {
            int fallback = _config != null ? _config.FallbackSampleRate : 48000;
            try
            {
                uint rate = SteamUser.OptimalSampleRate;
                return rate > 0 ? (int)rate : fallback;
            }
            catch
            {
                return fallback;
            }
        }

        private void HandleSessionRequest(SteamId requester)
        {
            // Accept sessions only from known lobby members (peers set by the composition root).
            if (_peers.ContainsKey(requester.Value))
            {
                SteamNetworking.AcceptP2PSessionWithUser(requester);
            }
        }

        private void OnDestroy()
        {
            if (_joined)
            {
                _ = LeaveAsync();
            }
        }
    }
}
