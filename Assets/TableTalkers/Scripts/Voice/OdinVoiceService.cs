using System.Threading.Tasks;
using UnityEngine;

namespace TableTalkers.Voice
{
    /// <summary>
    /// v1 spatial voice behind the same IVoiceService seam — swapping this in must not touch any
    /// gameplay/presence code. The real implementation compiles only when the ODIN SDK is imported
    /// and the TT_ODIN scripting define is set (🔧 person: Project Settings > Player > Scripting
    /// Define Symbols after importing ODIN + dashboard credentials). Without it, this is an inert
    /// stub so the project always compiles.
    /// </summary>
    public sealed class OdinVoiceService : MonoBehaviour, IVoiceService, IVoicePeerRoster
    {
        [SerializeField] private VoiceConfig _config;

        public bool IsSpatial => true;

        /// <summary>Self-heal path: lets the selector supply the config when the ref is missing.</summary>
        public void SetConfigIfMissing(VoiceConfig config)
        {
            if (_config == null)
            {
                _config = config;
            }
        }

#if TT_ODIN
        // Real ODIN integration lands here when the SDK is imported:
        // - JoinAsync: OdinHandler.Instance.JoinRoom(roomId) with APM settings from _config
        //   (EchoCanceller, NoiseSuppression, GainController)
        // - Playback objects attached to each participant's avatar for positional audio
        // - GetPeerSpeakingLevel from ODIN media stream activity
        public Task JoinAsync(string roomId) { throw new System.NotImplementedException("Wire ODIN SDK calls."); }
        public Task LeaveAsync() { throw new System.NotImplementedException(); }
        public void SetInputEnabled(bool enabled) { }
        public void SetPeerVolume(string participantId, float volume01) { }
        public void SetPeerMuted(string participantId, bool muted) { }
        public float GetPeerSpeakingLevel(string participantId) => 0f;
#else
        public Task JoinAsync(string roomId)
        {
            Debug.LogWarning("[OdinVoice] ODIN SDK not imported (TT_ODIN not defined) — voice inactive.");
            return Task.CompletedTask;
        }

        public Task LeaveAsync() => Task.CompletedTask;
        public void SetInputEnabled(bool enabled) { }
        public void SetPeerVolume(string participantId, float volume01) { }
        public void SetPeerMuted(string participantId, bool muted) { }
        public float GetPeerSpeakingLevel(string participantId) => 0f;
#endif

        // ODIN manages room membership itself — the roster is a no-op.
        public void AddPeer(ulong platformId) { }
        public void RemovePeer(ulong platformId) { }
    }
}
