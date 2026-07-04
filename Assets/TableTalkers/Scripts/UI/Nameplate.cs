using UnityEngine;
using TableTalkers.Core;

namespace TableTalkers.UI
{
    /// <summary>
    /// Floating name above an avatar, billboarded to the camera, highlighted while speaking.
    /// Creates its own TextMesh at runtime (no prefab wiring needed). Attach under the avatar.
    /// Names are user data, not UI strings, so no localization key applies here.
    /// </summary>
    public sealed class Nameplate : MonoBehaviour
    {
        [Tooltip("Vertical offset above this transform.")]
        [SerializeField] private float _height = 1.75f;

        [SerializeField] private Color _normalColor = Color.white;
        [SerializeField] private Color _speakingColor = new Color(1f, 0.85f, 0.35f, 1f);
        [SerializeField] private float _characterSize = 0.06f;

        /// <summary>Global option: show nameplates at all times (else only while speaking).</summary>
        public static bool AlwaysShow = true;

        private IParticipant _participant;
        private TextMesh _text;
        private Transform _plate;

        private void Start()
        {
            _participant = GetComponentInParent<IParticipant>();

            var go = new GameObject("Nameplate");
            _plate = go.transform;
            _plate.SetParent(transform, false);
            _plate.localPosition = new Vector3(0f, _height, 0f);

            _text = go.AddComponent<TextMesh>();
            // A runtime-created TextMesh renders nothing until a font + its material are assigned.
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _text.font = font;
            go.GetComponent<MeshRenderer>().material = font.material;
            _text.anchor = TextAnchor.MiddleCenter;
            _text.alignment = TextAlignment.Center;
            _text.characterSize = _characterSize;
            _text.fontSize = 64;
        }

        private void LateUpdate()
        {
            if (_participant == null || _text == null)
            {
                return;
            }

            bool speaking = _participant.IsSpeaking;
            bool visible = AlwaysShow || speaking;
            _text.gameObject.SetActive(visible);
            if (!visible)
            {
                return;
            }

            _text.text = _participant.DisplayName;
            _text.color = speaking ? _speakingColor : _normalColor;

            Camera cam = Camera.main;
            if (cam != null)
            {
                _plate.rotation = Quaternion.LookRotation(_plate.position - cam.transform.position);
            }
        }
    }
}
