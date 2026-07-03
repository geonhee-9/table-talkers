namespace TableTalkers.Core
{
    /// <summary>
    /// What kind of entity occupies a seat. Only <see cref="Human"/> is implemented today;
    /// <see cref="Agent"/> exists so gameplay code never assumes "participant == person".
    /// </summary>
    public enum ParticipantKind
    {
        Human,
        Agent
    }
}
