namespace TableTalkers.Core
{
    /// <summary>
    /// The room's authoritative state on the host: seats and seat holds. Initialized by the
    /// composition root (Bootstrap) from RoomConfig; consumed by the network layer through
    /// Core types only. Meaningful only on the host (host-authoritative invariant).
    /// </summary>
    public static class RoomSession
    {
        public static SeatManager Seats { get; private set; }
        public static SeatReservations Reservations { get; private set; }

        public static bool IsInitialized => Seats != null;

        public static void Initialize(SeatManager seats, SeatReservations reservations)
        {
            Seats = seats;
            Reservations = reservations;
        }

        public static void Reset()
        {
            Seats = null;
            Reservations = null;
        }
    }
}
