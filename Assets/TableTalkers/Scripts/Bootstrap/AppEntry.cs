using UnityEngine;

namespace TableTalkers.Bootstrap
{
    /// <summary>
    /// Composition root. Place this on a single GameObject in the Boot scene
    /// (🔧 person: create the Boot scene and add this component). Keeps the MonoBehaviour thin —
    /// it owns the <see cref="SceneFlow"/> and starts the app in <see cref="AppState.Boot"/>.
    /// Later tasks wire networking, voice and UI to the flow's state changes.
    /// </summary>
    public sealed class AppEntry : MonoBehaviour
    {
        public SceneFlow Flow { get; private set; }

        private void Awake()
        {
            Flow = new SceneFlow();
            Flow.StateChanged += OnStateChanged;
        }

        private void Start()
        {
            // App begins in Boot; subsequent transitions are driven by UI/session as they are built.
            Flow.TransitionTo(AppState.AvatarSetup);
        }

        private void OnDestroy()
        {
            if (Flow != null)
            {
                Flow.StateChanged -= OnStateChanged;
            }
        }

        private void OnStateChanged(AppState previous, AppState current)
        {
            // Scene loading hook. Uses SceneNames constants; scenes authored by a person in the Editor.
            // Left minimal for now — later tasks (lobby/session) trigger Room load on entering InRoom.
        }
    }
}
