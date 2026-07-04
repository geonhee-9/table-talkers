using System.Collections;
using UnityEngine;
using TableTalkers.Core;

namespace TableTalkers.Presence
{
    /// <summary>
    /// Plays emotes with pure code motion — no animation clips needed for primitive avatars:
    /// nod (head bob), laugh (bounce + head shake), raise hand (hand up, holds until speaking or
    /// timeout — turn-taking aid), thumbs up, clap. Also floats a short text label above the head.
    /// Attach to the avatar root next to EmoteSync; assign the parts.
    /// </summary>
    public sealed class ProceduralEmotePlayer : MonoBehaviour
    {
        [SerializeField] private HeadOrientationSync _head;
        [SerializeField] private Transform _body;
        [SerializeField] private Transform _handLeft;
        [SerializeField] private Transform _handRight;

        [Tooltip("Raised hand lowers automatically after this many seconds (or when speaking).")]
        [SerializeField, Range(2f, 30f)] private float _raiseHandHold = 6f;

        private IParticipant _participant;
        private Coroutine _handRoutine;
        private Vector3 _bodyRestPos;

        private static readonly Vector3 HandRest = new Vector3(0.3f, 0.45f, 0.1f);
        private static readonly Vector3 HandRaised = new Vector3(0.38f, 1.45f, 0.05f);
        private static readonly Vector3 ThumbPos = new Vector3(0.25f, 1.0f, 0.35f);

        private void Awake()
        {
            _participant = GetComponent<IParticipant>();
            if (_body != null)
            {
                _bodyRestPos = _body.localPosition;
            }
        }

        private void OnEnable()
        {
            EmoteSync.EmotePlayed += HandleEmote;
        }

        private void OnDisable()
        {
            EmoteSync.EmotePlayed -= HandleEmote;
        }

        private void HandleEmote(IParticipant participant, EmoteKind kind)
        {
            if (!ReferenceEquals(participant, _participant))
            {
                return;
            }

            switch (kind)
            {
                case EmoteKind.Nod: StartCoroutine(Nod()); break;
                case EmoteKind.Laugh: StartCoroutine(Laugh()); break;
                case EmoteKind.RaiseHand:
                    if (_handRoutine != null) StopCoroutine(_handRoutine);
                    _handRoutine = StartCoroutine(RaiseHand());
                    break;
                case EmoteKind.ThumbsUp: StartCoroutine(ThumbsUp()); break;
                case EmoteKind.Clap: StartCoroutine(Clap()); break;
            }

            FloatLabel(LabelFor(kind));
        }

        private static string LabelFor(EmoteKind kind)
        {
            switch (kind)
            {
                case EmoteKind.Nod: return "끄덕끄덕";
                case EmoteKind.Laugh: return "ㅎㅎㅎ";
                case EmoteKind.RaiseHand: return "손들기!";
                case EmoteKind.ThumbsUp: return "좋아요";
                case EmoteKind.Clap: return "짝짝짝";
                default: return kind.ToString();
            }
        }

        // ---------------------------------------------------------------- Motions

        private IEnumerator Nod()
        {
            const float duration = 0.9f;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float pitch = Mathf.Sin(t / duration * Mathf.PI * 4f) * 16f;
                if (_head != null) _head.EmoteOffset = Quaternion.Euler(pitch, 0f, 0f);
                yield return null;
            }

            if (_head != null) _head.EmoteOffset = Quaternion.identity;
        }

        private IEnumerator Laugh()
        {
            const float duration = 1.1f;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float wave = Mathf.Sin(t * 22f);
                if (_body != null)
                {
                    _body.localPosition = _bodyRestPos + new Vector3(0f, Mathf.Abs(wave) * 0.035f, 0f);
                }

                if (_head != null)
                {
                    _head.EmoteOffset = Quaternion.Euler(-8f * Mathf.Abs(wave), 0f, wave * 4f);
                }

                yield return null;
            }

            if (_body != null) _body.localPosition = _bodyRestPos;
            if (_head != null) _head.EmoteOffset = Quaternion.identity;
        }

        private IEnumerator RaiseHand()
        {
            if (_handRight == null)
            {
                yield break;
            }

            _handRight.gameObject.SetActive(true);
            yield return MoveHand(_handRight, HandRest, HandRaised, 0.25f);

            float raisedAt = Time.time;
            while (Time.time - raisedAt < _raiseHandHold
                   && (_participant == null || !_participant.IsSpeaking))
            {
                yield return null;
            }

            yield return MoveHand(_handRight, HandRaised, HandRest, 0.25f);
            _handRight.gameObject.SetActive(false);
            _handRoutine = null;
        }

        private IEnumerator ThumbsUp()
        {
            if (_handRight == null)
            {
                yield break;
            }

            _handRight.gameObject.SetActive(true);
            Vector3 baseScale = _handRight.localScale;
            _handRight.localPosition = ThumbPos;
            for (float t = 0f; t < 1.2f; t += Time.deltaTime)
            {
                float pop = 1f + 0.4f * Mathf.Exp(-6f * t) * Mathf.Sin(t * 18f);
                _handRight.localScale = baseScale * pop;
                yield return null;
            }

            _handRight.localScale = baseScale;
            _handRight.gameObject.SetActive(false);
        }

        private IEnumerator Clap()
        {
            if (_handLeft == null || _handRight == null)
            {
                yield break;
            }

            _handLeft.gameObject.SetActive(true);
            _handRight.gameObject.SetActive(true);
            const float duration = 1.3f;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float meet = Mathf.Abs(Mathf.Sin(t * 14f)); // 0 = together, 1 = apart
                float spread = 0.06f + 0.16f * meet;
                _handLeft.localPosition = new Vector3(-spread, 0.95f, 0.35f);
                _handRight.localPosition = new Vector3(spread, 0.95f, 0.35f);
                yield return null;
            }

            _handLeft.gameObject.SetActive(false);
            _handRight.gameObject.SetActive(false);
        }

        private static IEnumerator MoveHand(Transform hand, Vector3 from, Vector3 to, float duration)
        {
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                hand.localPosition = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / duration));
                yield return null;
            }

            hand.localPosition = to;
        }

        // ---------------------------------------------------------------- Floating label

        private void FloatLabel(string text)
        {
            var go = new GameObject("EmoteLabel");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 1.9f, 0f);

            var tm = go.AddComponent<TextMesh>();
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            tm.font = font;
            go.GetComponent<MeshRenderer>().material = font.material;
            tm.text = text;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.characterSize = 0.05f;
            tm.fontSize = 72;
            tm.color = new Color(1f, 0.92f, 0.7f, 1f);

            StartCoroutine(RiseAndFade(go, tm));
        }

        private IEnumerator RiseAndFade(GameObject go, TextMesh tm)
        {
            const float duration = 1.6f;
            Vector3 start = go.transform.localPosition;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float k = t / duration;
                go.transform.localPosition = start + new Vector3(0f, 0.45f * k, 0f);
                Camera cam = Camera.main;
                if (cam != null)
                {
                    go.transform.rotation =
                        Quaternion.LookRotation(go.transform.position - cam.transform.position);
                }

                Color c = tm.color;
                c.a = 1f - Mathf.SmoothStep(0f, 1f, k);
                tm.color = c;
                yield return null;
            }

            Destroy(go);
        }
    }
}
