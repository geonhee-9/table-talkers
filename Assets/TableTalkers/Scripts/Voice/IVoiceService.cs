using System.Threading.Tasks;

namespace TableTalkers.Voice
{
    /// <summary>
    /// The single seam between gameplay/presence code and the underlying voice backend.
    /// Invariant: no gameplay or presence code calls a voice SDK (Steam Voice, ODIN) directly —
    /// it depends only on this interface, so M0's Steam Voice can be swapped for ODIN in v1
    /// with no changes outside the Voice module.
    ///
    /// Mute, per-peer volume and speaking-level query are part of the contract from day one
    /// because they are hard to retrofit later.
    /// </summary>
    public interface IVoiceService
    {
        /// <summary>True when voice is positional (ODIN); false for non-spatial Steam Voice.</summary>
        bool IsSpatial { get; }

        /// <summary>Join the voice channel for a room.</summary>
        Task JoinAsync(string roomId);

        /// <summary>Leave the current voice channel.</summary>
        Task LeaveAsync();

        /// <summary>Enable/disable local mic capture (local mute or push-to-talk).</summary>
        void SetInputEnabled(bool enabled);

        /// <summary>Set playback volume for one peer, 0..1.</summary>
        void SetPeerVolume(string participantId, float volume01);

        /// <summary>Locally mute/unmute a peer (does not affect others).</summary>
        void SetPeerMuted(string participantId, bool muted);

        /// <summary>Current speaking level for a peer, 0..1 — used to drive IsSpeaking and lipsync.</summary>
        float GetPeerSpeakingLevel(string participantId);
    }
}
