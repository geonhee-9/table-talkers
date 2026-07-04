using UnityEngine;
using TableTalkers.Core;
using TableTalkers.Voice;

namespace TableTalkers.Presence
{
    /// <summary>
    /// Makes a primitive avatar feel alive with zero animation assets and zero extra network
    /// data (everything is derived locally): mouth opens with voice level, eyes blink, chest
    /// breathes, and the upper body follows the head's yaw a little like real seated posture.
    /// Attach to the avatar root; assign the parts.
    /// </summary>
    public sealed class AvatarLife : MonoBehaviour
    {
        [SerializeField] private Transform _body;
        [SerializeField] private Transform _eyeLeft;
        [SerializeField] private Transform _eyeRight;
        [SerializeField] private Transform _mouth;

        [Tooltip("How much the body turns with the head (0 = rigid, 1 = fully).")]
        [SerializeField, Range(0f, 1f)] private float _bodyYawFollow = 0.25f;

        [Tooltip("Voice level at which the mouth is fully open.")]
        [SerializeField, Range(0.05f, 1f)] private float _fullOpenLevel = 0.3f;

        private IParticipant _participant;
        private Vector3 _eyeScaleL, _eyeScaleR, _mouthScale, _bodyScale;
        private float _mouthOpen;
        private float _nextBlinkAt;
        private float _blinkStartedAt = -1f;
        private float _breathePhase;

        private const float BlinkDuration = 0.12f;

        private void Start()
        {
            _participant = GetComponentInParent<IParticipant>();
            if (_eyeLeft != null) _eyeScaleL = _eyeLeft.localScale;
            if (_eyeRight != null) _eyeScaleR = _eyeRight.localScale;
            if (_mouth != null) _mouthScale = _mouth.localScale;
            if (_body != null) _bodyScale = _body.localScale;
            _breathePhase = Random.Range(0f, Mathf.PI * 2f);
            ScheduleNextBlink();
        }

        private void LateUpdate()
        {
            AnimateMouth();
            AnimateBlink();
            AnimateBreathing();
            FollowHeadYaw();
        }

        private void AnimateMouth()
        {
            if (_mouth == null || _participant == null)
            {
                return;
            }

            float level = VoiceServices.Current != null
                ? VoiceServices.Current.GetPeerSpeakingLevel(_participant.Id)
                : 0f;
            float target = Mathf.Clamp01(level / _fullOpenLevel);
            _mouthOpen = Mathf.Lerp(_mouthOpen, target, 1f - Mathf.Exp(-14f * Time.deltaTime));

            Vector3 s = _mouthScale;
            s.y *= 1f + 4f * _mouthOpen; // opens tall while talking
            _mouth.localScale = s;
        }

        private void AnimateBlink()
        {
            if (_eyeLeft == null && _eyeRight == null)
            {
                return;
            }

            float lidScale = 1f;
            if (_blinkStartedAt >= 0f)
            {
                float t = (Time.time - _blinkStartedAt) / BlinkDuration;
                if (t >= 1f)
                {
                    _blinkStartedAt = -1f;
                    ScheduleNextBlink();
                }
                else
                {
                    lidScale = 1f - Mathf.Sin(t * Mathf.PI); // close then open
                }
            }
            else if (Time.time >= _nextBlinkAt)
            {
                _blinkStartedAt = Time.time;
            }

            if (_eyeLeft != null)
            {
                Vector3 s = _eyeScaleL;
                s.y *= Mathf.Max(0.08f, lidScale);
                _eyeLeft.localScale = s;
            }

            if (_eyeRight != null)
            {
                Vector3 s = _eyeScaleR;
                s.y *= Mathf.Max(0.08f, lidScale);
                _eyeRight.localScale = s;
            }
        }

        private void AnimateBreathing()
        {
            if (_body == null)
            {
                return;
            }

            float breath = 1f + 0.015f * Mathf.Sin(Time.time * 1.9f + _breathePhase);
            Vector3 s = _bodyScale;
            s.y *= breath;
            _body.localScale = s;
        }

        private void FollowHeadYaw()
        {
            if (_body == null || _participant == null)
            {
                return;
            }

            float yaw = _participant.HeadOrientation.eulerAngles.y;
            if (yaw > 180f)
            {
                yaw -= 360f;
            }

            Quaternion target = Quaternion.Euler(0f, yaw * _bodyYawFollow, 0f);
            _body.localRotation = Quaternion.Slerp(
                _body.localRotation, target, 1f - Mathf.Exp(-6f * Time.deltaTime));
        }

        private void ScheduleNextBlink()
        {
            _nextBlinkAt = Time.time + Random.Range(1.5f, 5f);
        }
    }
}
