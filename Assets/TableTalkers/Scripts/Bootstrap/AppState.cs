namespace TableTalkers.Bootstrap
{
    /// <summary>
    /// High-level app phases. Core loop: Boot -> AvatarSetup -> Lobby -> InRoom.
    /// These are app states, not one-to-one with scenes (AvatarSetup/Lobby are UI in the Boot scene).
    /// </summary>
    public enum AppState
    {
        Boot,
        AvatarSetup,
        Lobby,
        InRoom
    }
}
