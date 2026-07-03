using UnityEngine;

namespace TableTalkers.Core
{
    /// <summary>
    /// The only abstraction gameplay/presence code uses to talk about a seat occupant.
    /// Invariant: never assume a participant is a person — that assumption must not leak
    /// past concrete implementations (today: <see cref="HumanParticipant"/>).
    /// </summary>
    public interface IParticipant
    {
        /// <summary>Stable identity for this participant within a session.</summary>
        string Id { get; }

        /// <summary>Assigned seat, or <see cref="SeatManager.Unseated"/> when not seated.</summary>
        int SeatIndex { get; }

        /// <summary>Display name shown on nameplates.</summary>
        string DisplayName { get; }

        /// <summary>Human, Agent, etc.</summary>
        ParticipantKind Kind { get; }

        /// <summary>True for the participant controlled by this client.</summary>
        bool IsLocal { get; }

        /// <summary>True while this participant is currently talking (voice level over threshold).</summary>
        bool IsSpeaking { get; }

        /// <summary>Head yaw/pitch as a rotation — the presence signal for "who is looking where".</summary>
        Quaternion HeadOrientation { get; }
    }
}
