using System;

namespace TableTalkers.Bootstrap
{
    /// <summary>
    /// Thin state machine driving the app phase (see <see cref="AppState"/>).
    /// Pure C# and side-effect free by itself: actual scene loading is done by whoever listens to
    /// <see cref="StateChanged"/> using <see cref="SceneNames"/> constants. Per-state enter/exit
    /// hooks are intentionally left empty for later tasks to fill in.
    /// </summary>
    public sealed class SceneFlow
    {
        /// <summary>Fired after a transition completes: (previous, current).</summary>
        public event Action<AppState, AppState> StateChanged;

        public AppState Current { get; private set; } = AppState.Boot;

        /// <summary>Transition to a new state, running exit then enter hooks. No-op if unchanged.</summary>
        public void TransitionTo(AppState next)
        {
            if (next == Current)
            {
                return;
            }

            AppState previous = Current;
            OnExit(previous);
            Current = next;
            OnEnter(next);
            StateChanged?.Invoke(previous, next);
        }

        private void OnEnter(AppState state)
        {
            switch (state)
            {
                case AppState.Boot:
                    break;
                case AppState.AvatarSetup:
                    break;
                case AppState.Lobby:
                    break;
                case AppState.InRoom:
                    break;
            }
        }

        private void OnExit(AppState state)
        {
            switch (state)
            {
                case AppState.Boot:
                    break;
                case AppState.AvatarSetup:
                    break;
                case AppState.Lobby:
                    break;
                case AppState.InRoom:
                    break;
            }
        }
    }
}
