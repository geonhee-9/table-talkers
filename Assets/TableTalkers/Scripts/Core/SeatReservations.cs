using System.Collections.Generic;

namespace TableTalkers.Core
{
    /// <summary>
    /// Host-side seat holds so a briefly disconnected participant returns to the same seat.
    /// Pure C# (time passed in) for testability. Entries expire after a hold duration.
    /// </summary>
    public sealed class SeatReservations
    {
        private struct Hold
        {
            public int SeatIndex;
            public double ExpiresAt;
        }

        private readonly Dictionary<string, Hold> _holds = new();
        private readonly double _holdSeconds;

        public SeatReservations(double holdSeconds)
        {
            _holdSeconds = holdSeconds;
        }

        /// <summary>Hold a seat for a participant that just disconnected.</summary>
        public void Reserve(string participantId, int seatIndex, double now)
        {
            if (string.IsNullOrEmpty(participantId) || seatIndex < 0)
            {
                return;
            }

            _holds[participantId] = new Hold { SeatIndex = seatIndex, ExpiresAt = now + _holdSeconds };
        }

        /// <summary>Claim a previously held seat. Returns false if none or expired.</summary>
        public bool TryClaim(string participantId, double now, out int seatIndex)
        {
            seatIndex = SeatManager.Unseated;
            if (participantId == null || !_holds.TryGetValue(participantId, out Hold hold))
            {
                return false;
            }

            _holds.Remove(participantId);
            if (now > hold.ExpiresAt)
            {
                return false;
            }

            seatIndex = hold.SeatIndex;
            return true;
        }

        /// <summary>Drop expired holds (call occasionally on the host).</summary>
        public void Prune(double now)
        {
            List<string> expired = null;
            foreach (KeyValuePair<string, Hold> pair in _holds)
            {
                if (now > pair.Value.ExpiresAt)
                {
                    (expired ??= new List<string>()).Add(pair.Key);
                }
            }

            if (expired != null)
            {
                foreach (string id in expired)
                {
                    _holds.Remove(id);
                }
            }
        }
    }
}
