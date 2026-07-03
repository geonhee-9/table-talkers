using System.Collections.Generic;

namespace TableTalkers.Core
{
    /// <summary>
    /// Assigns and releases seats. Deals with occupants only through <see cref="IParticipant"/> —
    /// it never inspects the concrete kind, so it works unchanged when non-human participants exist.
    /// Pure C# (no MonoBehaviour) so it is unit-testable. The seat count comes from RoomConfig.
    /// </summary>
    public sealed class SeatManager
    {
        /// <summary>Sentinel seat index meaning "not seated".</summary>
        public const int Unseated = -1;

        private readonly IParticipant[] _occupants;

        public SeatManager(int seatCount)
        {
            if (seatCount < 0)
            {
                seatCount = 0;
            }

            _occupants = new IParticipant[seatCount];
        }

        public int SeatCount => _occupants.Length;

        /// <summary>Assigns the participant to the first empty seat. Returns false if the room is full.</summary>
        public bool TryAssignSeat(IParticipant participant, out int seatIndex)
        {
            seatIndex = FirstEmptySeat();
            if (seatIndex == Unseated)
            {
                return false;
            }

            _occupants[seatIndex] = participant;
            return true;
        }

        /// <summary>Assigns the participant to a specific seat. Returns false if out of range or occupied.</summary>
        public bool AssignSeat(IParticipant participant, int seatIndex)
        {
            if (!IsValidIndex(seatIndex) || _occupants[seatIndex] != null)
            {
                return false;
            }

            _occupants[seatIndex] = participant;
            return true;
        }

        /// <summary>Frees a seat by index.</summary>
        public void Release(int seatIndex)
        {
            if (IsValidIndex(seatIndex))
            {
                _occupants[seatIndex] = null;
            }
        }

        /// <summary>Frees whatever seat the given participant occupies, if any.</summary>
        public void ReleaseParticipant(IParticipant participant)
        {
            for (int i = 0; i < _occupants.Length; i++)
            {
                if (ReferenceEquals(_occupants[i], participant))
                {
                    _occupants[i] = null;
                    return;
                }
            }
        }

        public IParticipant GetOccupant(int seatIndex)
        {
            return IsValidIndex(seatIndex) ? _occupants[seatIndex] : null;
        }

        public bool IsSeatEmpty(int seatIndex)
        {
            return IsValidIndex(seatIndex) && _occupants[seatIndex] == null;
        }

        /// <summary>Index of the first empty seat, or <see cref="Unseated"/> when the room is full.</summary>
        public int FirstEmptySeat()
        {
            for (int i = 0; i < _occupants.Length; i++)
            {
                if (_occupants[i] == null)
                {
                    return i;
                }
            }

            return Unseated;
        }

        public IReadOnlyList<int> GetEmptySeats()
        {
            var empty = new List<int>();
            for (int i = 0; i < _occupants.Length; i++)
            {
                if (_occupants[i] == null)
                {
                    empty.Add(i);
                }
            }

            return empty;
        }

        private bool IsValidIndex(int seatIndex)
        {
            return seatIndex >= 0 && seatIndex < _occupants.Length;
        }
    }
}
