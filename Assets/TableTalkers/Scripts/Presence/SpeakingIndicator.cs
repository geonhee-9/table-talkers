using UnityEngine;
using TableTalkers.Core;

namespace TableTalkers.Presence
{
    /// <summary>
    /// Subtle "this person is talking" highlight — reacts within ~0.1s of IsSpeaking.
    /// Toggles an indicator object (e.g. a soft ring/glow under the avatar) and can tint a renderer.
    /// Attach under the avatar; 🔧 person: assign the indicator object and/or renderer.
    /// </summary>
    public sealed class SpeakingIndicator : MonoBehaviour
    {
        [Tooltip("Enabled while the participant speaks (e.g. a glow quad). Optional.")]
        [SerializeField] private GameObject _indicator;

        [Tooltip("Renderer tinted while speaking. Optional.")]
        [SerializeField] private Renderer _tintTarget;

        [SerializeField] private Color _speakingTint = new Color(1f, 0.92f, 0.6f, 1f);

        [Tooltip("Fade in/out speed. High enough to feel instant (<0.1s).")]
        [SerializeField, Range(5f, 40f)] private float _fadeSpeed = 20f;

        private IParticipant _participant;
        private Color _restColor;
        private float _weight;

        private void Start()
        {
            _participant = GetComponentInParent<IParticipant>();
            if (_tintTarget != null)
            {
                _restColor = _tintTarget.material.color;
            }

            if (_indicator != null)
            {
                _indicator.SetActive(false);
            }
        }

        private void LateUpdate()
        {
            if (_participant == null)
            {
                return;
            }

            float target = _participant.IsSpeaking ? 1f : 0f;
            _weight = Mathf.MoveTowards(_weight, target, _fadeSpeed * Time.deltaTime);

            if (_indicator != null)
            {
                _indicator.SetActive(_weight > 0.01f);
            }

            if (_tintTarget != null)
            {
                _tintTarget.material.color = Color.Lerp(_restColor, _speakingTint, _weight);
            }
        }
    }
}
