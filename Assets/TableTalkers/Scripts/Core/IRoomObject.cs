namespace TableTalkers.Core
{
    /// <summary>
    /// Marker for a shared surface object a room can hold besides its participants
    /// (future: shared screen, document, board). No implementation today — this exists so
    /// a room is modeled as a collection of surfaces, not hardcoded as "avatars + table".
    /// </summary>
    public interface IRoomObject
    {
        string Id { get; }
    }
}
