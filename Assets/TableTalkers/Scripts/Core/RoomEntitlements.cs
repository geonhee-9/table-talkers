namespace TableTalkers.Core
{
    /// <summary>
    /// What the current room is entitled to, decided by the HOST's ownership (host-pays model:
    /// one Pro owner upgrades the whole room; guests always enter free). Set by the composition
    /// root when a room starts; read by UI/session code without touching Steam.
    /// </summary>
    public static class RoomEntitlements
    {
        /// <summary>True when the room host owns Pro — larger capacity, unlimited time, extras.</summary>
        public static bool ProActive { get; private set; }

        /// <summary>Seats this room is allowed (free tier = RoomConfig.SeatCount, Pro = MaxCapacity).</summary>
        public static int RoomCapacity { get; private set; }

        public static void Set(bool proActive, int roomCapacity)
        {
            ProActive = proActive;
            RoomCapacity = roomCapacity;
        }

        public static void Reset()
        {
            ProActive = false;
            RoomCapacity = 0;
        }
    }
}
