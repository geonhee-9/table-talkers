using UnityEngine;
using TableTalkers.Core;

namespace TableTalkers.Player
{
    /// <summary>
    /// First-person camera pinned to the seated position: follows the seat anchor (player root)
    /// at eye height and applies the controller's head orientation. Attach to the Camera and
    /// assign the local controller. 🔧 person: only the LOCAL player's camera should be enabled.
    /// </summary>
    public sealed class PlayerCamera : MonoBehaviour
    {
        [SerializeField] private SeatedFirstPersonController _controller;

        [Tooltip("Eye offset from the player root (seated eye height).")]
        [SerializeField] private Vector3 _eyeOffset = new Vector3(0f, 1.15f, 0f);

        private void LateUpdate()
        {
            if (_controller == null)
            {
                return;
            }

            Transform root = _controller.transform;
            transform.position = root.position + root.rotation * _eyeOffset;
            transform.rotation = root.rotation * _controller.HeadOrientation;
        }

        public void Bind(SeatedFirstPersonController controller) => _controller = controller;
    }
}
