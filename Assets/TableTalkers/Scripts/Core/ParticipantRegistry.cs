using System;
using System.Collections.Generic;

namespace TableTalkers.Core
{
    /// <summary>
    /// Session-wide list of participants currently in the room, kept in sync by the network layer.
    /// UI and presence code enumerate participants here through <see cref="IParticipant"/> only.
    /// </summary>
    public static class ParticipantRegistry
    {
        private static readonly List<IParticipant> _participants = new();

        public static event Action<IParticipant> Added;
        public static event Action<IParticipant> Removed;

        public static IReadOnlyList<IParticipant> All => _participants;

        public static IParticipant Local
        {
            get
            {
                for (int i = 0; i < _participants.Count; i++)
                {
                    if (_participants[i].IsLocal)
                    {
                        return _participants[i];
                    }
                }

                return null;
            }
        }

        public static void Add(IParticipant participant)
        {
            if (participant != null && !_participants.Contains(participant))
            {
                _participants.Add(participant);
                Added?.Invoke(participant);
            }
        }

        public static void Remove(IParticipant participant)
        {
            if (_participants.Remove(participant))
            {
                Removed?.Invoke(participant);
            }
        }

        public static void Clear() => _participants.Clear();
    }
}
