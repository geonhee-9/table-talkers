using UnityEngine;
using TableTalkers.Core;
using TableTalkers.Voice;

namespace TableTalkers.Presence
{
    /// <summary>
    /// M0/M1 lipsync: opens the avatar's jaw (or a blendshape) from the participant's voice level
    /// via IVoiceService — works identically for local and remote participants. Viseme-based
    /// lipsync (uLipSync) replaces this later behind the same component boundary.
    /// Attach under the avatar; 🔧 person: assign the jaw bone OR a SkinnedMeshRenderer + blendshape index.
    /// </summary>
    public sealed class AmplitudeLipsync : MonoBehaviour
    {
        [Header("Jaw bone mode")]
        [Tooltip("Jaw transform rotated around local X when speaking. Leave null to use blendshape mode.")]
        [SerializeField] private Transform _jawBone;

        [Tooltip("Max jaw open angle in degrees.")]
        [SerializeField, Range(1f, 45f)] private float _maxJawAngle = 18f;

        [Header("Blendshape mode")]
        [Tooltip("Used when jaw bone is null. Blendshape driven 0-100 by voice level.")]
        [SerializeField] private SkinnedMeshRenderer _face;
        [SerializeField, Min(0)] private int _mouthOpenBlendshape;

        [Header("Response")]
        [Tooltip("Voice level to fully open the mouth.")]
        [SerializeField, Range(0.05f, 1f)] private float _fullOpenLevel = 0.35f;

        [Tooltip("Open/close smoothing speed.")]
        [SerializeField, Range(1f, 30f)] private float _smoothing = 14f;

        private IParticipant _participant;
        private float _open;
        private Quaternion _jawRestRotation;

        private void Start()
        {
            _participant = GetComponentInParent<IParticipant>();
            if (_jawBone != null)
            {
                _jawRestRotation = _jawBone.localRotation;
            }
        }

        private void LateUpdate()
        {
            if (_participant == null || VoiceServices.Current == null)
            {
                return;
            }

            float level = VoiceServices.Current.GetPeerSpeakingLevel(_participant.Id);
            float target = Mathf.Clamp01(level / _fullOpenLevel);
            _open = Mathf.Lerp(_open, target, 1f - Mathf.Exp(-_smoothing * Time.deltaTime));

            if (_jawBone != null)
            {
                _jawBone.localRotation = _jawRestRotation * Quaternion.Euler(_open * _maxJawAngle, 0f, 0f);
            }
            else if (_face != null && _mouthOpenBlendshape < _face.sharedMesh.blendShapeCount)
            {
                _face.SetBlendShapeWeight(_mouthOpenBlendshape, _open * 100f);
            }
        }
    }
}
