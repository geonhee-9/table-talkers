using UnityEngine;
using TableTalkers.Core;

namespace TableTalkers.Player
{
    /// <summary>
    /// Scene overview camera shown before the local player is seated (so pressing Play isn't a
    /// black screen). Turns itself off the moment the local participant spawns, handing the view
    /// to the seated player camera. Attach to a scene Camera with an AudioListener.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class PreviewCamera : MonoBehaviour
    {
        private Camera _camera;
        private AudioListener _listener;

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            _listener = GetComponent<AudioListener>();
        }

        private void OnEnable()
        {
            ParticipantRegistry.Added += HandleParticipantAdded;
            ParticipantRegistry.Removed += HandleParticipantRemoved;
        }

        private void OnDisable()
        {
            ParticipantRegistry.Added -= HandleParticipantAdded;
            ParticipantRegistry.Removed -= HandleParticipantRemoved;
        }

        private void HandleParticipantAdded(IParticipant participant)
        {
            if (participant.IsLocal)
            {
                if (_listener != null) _listener.enabled = false;
                _camera.enabled = false;
            }
        }

        private void HandleParticipantRemoved(IParticipant participant)
        {
            // Local player left the room (or was despawned) — hand the view back to the preview
            // camera so leaving a room never ends on a black screen.
            if (participant.IsLocal)
            {
                if (_listener != null) _listener.enabled = true;
                _camera.enabled = true;
            }
        }
    }
}
