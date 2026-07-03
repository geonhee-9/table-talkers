namespace TableTalkers.Voice
{
    /// <summary>
    /// Peer membership feed for voice backends that need it (Steam voice sends P2P packets to an
    /// explicit peer set; ODIN manages rooms itself and can no-op). Only the composition root
    /// calls this — gameplay code still sees IVoiceService only.
    /// </summary>
    public interface IVoicePeerRoster
    {
        void AddPeer(ulong platformId);
        void RemovePeer(ulong platformId);
    }
}
