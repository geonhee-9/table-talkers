using UnityEngine;
using UnityEngine.InputSystem;
using TableTalkers.Core;

namespace TableTalkers.Player
{
    /// <summary>
    /// Seated first-person head look. No locomotion — seating is fixed by design.
    /// Mouse (or right stick) rotates the head with yaw/pitch clamps relative to the seat's
    /// forward. Exposes the pose via <see cref="IHeadPoseSource"/> for head sync and the camera.
    /// Attach to the local player object (owner only drives input).
    /// </summary>
    public sealed class SeatedFirstPersonController : MonoBehaviour, IHeadPoseSource
    {
        [Header("Look")]
        [Tooltip("Degrees per mouse delta unit.")]
        [SerializeField, Min(0.01f)] private float _mouseSensitivity = 0.12f;

        [Tooltip("Max yaw away from seat forward, in degrees (seated — you can't turn fully around).")]
        [SerializeField, Range(30f, 180f)] private float _yawClamp = 150f;

        [Tooltip("Max pitch up/down in degrees.")]
        [SerializeField, Range(10f, 89f)] private float _pitchClamp = 75f;

        [Header("Cursor")]
        [Tooltip("Lock the cursor while looking; Escape releases it.")]
        [SerializeField] private bool _lockCursor = true;

        public float Yaw { get; private set; }
        public float Pitch { get; private set; }

        public Quaternion HeadOrientation => Quaternion.Euler(-Pitch, Yaw, 0f);

        /// <summary>When false (e.g. remote copies, menus open), input is ignored.</summary>
        public bool InputEnabled { get; set; } = true;

        private void OnEnable()
        {
            ApplyCursorLock(_lockCursor);
        }

        private void Update()
        {
            HandleCursorToggle();

            if (!InputEnabled || Cursor.lockState != CursorLockMode.Locked)
            {
                return;
            }

            Vector2 delta = ReadLookDelta();
            Yaw = Mathf.Clamp(Yaw + delta.x * _mouseSensitivity, -_yawClamp, _yawClamp);
            Pitch = Mathf.Clamp(Pitch + delta.y * _mouseSensitivity, -_pitchClamp, _pitchClamp);
        }

        private static Vector2 ReadLookDelta()
        {
            Vector2 delta = Vector2.zero;
            if (Mouse.current != null)
            {
                delta = Mouse.current.delta.ReadValue();
            }

            if (Gamepad.current != null)
            {
                // Right stick as an alternative look input.
                delta += Gamepad.current.rightStick.ReadValue() * 10f;
            }

            return delta;
        }

        private void HandleCursorToggle()
        {
            if (Keyboard.current == null)
            {
                return;
            }

            if (Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                ApplyCursorLock(false);
            }
            else if (_lockCursor && Cursor.lockState != CursorLockMode.Locked
                     && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                ApplyCursorLock(true);
            }
        }

        private static void ApplyCursorLock(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
