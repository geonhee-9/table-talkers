namespace TableTalkers.Core
{
    /// <summary>
    /// Identity of the local player, set once by the composition root (Bootstrap) from the
    /// platform layer. Lets Presence/UI read "who am I" without referencing Steam.
    /// </summary>
    public static class LocalPlayerInfo
    {
        /// <summary>Platform id (SteamId for Steam builds). 0 until set.</summary>
        public static ulong PlatformId { get; private set; }

        public static string DisplayName { get; private set; } = string.Empty;

        public static string Id => PlatformId.ToString();

        public static void Set(ulong platformId, string displayName)
        {
            PlatformId = platformId;
            DisplayName = displayName ?? string.Empty;
        }
    }
}
