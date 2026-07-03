using UnityEngine;

namespace TableTalkers.Core
{
    /// <summary>
    /// The only <see cref="IParticipant"/> implementation today: a human player.
    /// Mutable state is updated through explicit setters by the systems that own each signal
    /// (SeatManager for the seat, the voice service for speaking, head-sync for orientation),
    /// so the "person" assumption stays contained here and never leaks into gameplay code.
    /// </summary>
    public sealed class HumanParticipant : IParticipant
    {
        public string Id { get; }
        public ParticipantKind Kind => ParticipantKind.Human;
        public bool IsLocal { get; }

        public int SeatIndex { get; private set; } = SeatManager.Unseated;
        public string DisplayName { get; private set; }
        public bool IsSpeaking { get; private set; }
        public Quaternion HeadOrientation { get; private set; } = Quaternion.identity;

        public HumanParticipant(string id, string displayName, bool isLocal)
        {
            Id = id;
            DisplayName = displayName;
            IsLocal = isLocal;
        }

        public void SetSeat(int seatIndex) => SeatIndex = seatIndex;

        public void SetDisplayName(string displayName) => DisplayName = displayName;

        public void SetSpeaking(bool isSpeaking) => IsSpeaking = isSpeaking;

        public void SetHeadOrientation(Quaternion headOrientation) => HeadOrientation = headOrientation;
    }
}
