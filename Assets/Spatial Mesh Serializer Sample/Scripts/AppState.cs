using System;

public static class AppState
{
    [Flags]
    public enum State
    {
        Init = 1,
        Permissions = 2,
        Meshing = 4,
        Gameplay = 8
    }

    public static readonly NotifyingProperty<State> state = new(State.Init);
}