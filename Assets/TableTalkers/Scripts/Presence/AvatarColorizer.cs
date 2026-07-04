using UnityEngine;
using TableTalkers.Core;

namespace TableTalkers.Presence
{
    /// <summary>
    /// Tints the avatar body by seat index so people are instantly tellable apart before real
    /// avatars exist. Warm, cozy-lounge palette. Attach under the avatar; assign the body renderer.
    /// </summary>
    public sealed class AvatarColorizer : MonoBehaviour
    {
        [SerializeField] private Renderer _body;

        private static readonly Color[] Palette =
        {
            new Color(0.91f, 0.45f, 0.38f), // coral
            new Color(0.35f, 0.62f, 0.60f), // teal
            new Color(0.93f, 0.72f, 0.36f), // amber
            new Color(0.62f, 0.55f, 0.78f), // lavender
            new Color(0.55f, 0.68f, 0.45f), // sage
            new Color(0.44f, 0.62f, 0.80f), // dusk blue
            new Color(0.85f, 0.56f, 0.65f), // rose
            new Color(0.72f, 0.64f, 0.50f)  // sand
        };

        private IParticipant _participant;
        private int _appliedSeat = int.MinValue;

        private void Start()
        {
            _participant = GetComponentInParent<IParticipant>();
        }

        private void LateUpdate()
        {
            if (_participant == null || _body == null)
            {
                return;
            }

            int seat = _participant.SeatIndex;
            if (seat == _appliedSeat)
            {
                return;
            }

            _appliedSeat = seat;
            if (seat >= 0)
            {
                _body.material.color = Palette[seat % Palette.Length];
            }
        }
    }
}
