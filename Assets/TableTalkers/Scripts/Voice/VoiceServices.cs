namespace TableTalkers.Voice
{
    /// <summary>
    /// Locator for the active <see cref="IVoiceService"/>, set by the composition root (Bootstrap).
    /// Gameplay/presence code reads voice state through this — never a concrete SDK.
    /// </summary>
    public static class VoiceServices
    {
        public static IVoiceService Current { get; private set; }

        public static void Set(IVoiceService service) => Current = service;
    }
}
