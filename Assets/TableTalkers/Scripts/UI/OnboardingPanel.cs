using UnityEngine;
using TableTalkers.Voice;

namespace TableTalkers.UI
{
    /// <summary>
    /// First-run mic check: permission → device pick → live level test → "all set" confirmation.
    /// The single biggest drop-off point, so it must be short and reassuring. Shows once
    /// (PlayerPrefs flag), skippable afterwards. IMGUI for MVP.
    /// Note: device pick is informational for Steam voice (system default); it becomes a real
    /// selector with ODIN in v1.
    /// </summary>
    public sealed class OnboardingPanel : MonoBehaviour
    {
        private const string DoneKey = "tt.onboarding.done";
        private const int TestClipSeconds = 10;

        private bool _visible;
        private string[] _devices = new string[0];
        private int _deviceIndex;
        private AudioClip _testClip;
        private string _testingDevice;
        private float _level;

        private void Start()
        {
            _visible = PlayerPrefs.GetInt(DoneKey, 0) == 0;
            if (_visible)
            {
                RequestPermissionAndListDevices();
            }
        }

        private void RequestPermissionAndListDevices()
        {
            // Desktop grants mic by default; the call still surfaces the OS prompt where needed.
            Application.RequestUserAuthorization(UserAuthorization.Microphone);
            _devices = Microphone.devices;
        }

        private void Update()
        {
            if (_testClip == null || string.IsNullOrEmpty(_testingDevice))
            {
                return;
            }

            // Live input level from the mic test loop.
            int pos = Microphone.GetPosition(_testingDevice);
            const int window = 256;
            if (pos < window)
            {
                return;
            }

            var samples = new float[window];
            _testClip.GetData(samples, pos - window);
            float peak = 0f;
            for (int i = 0; i < window; i++)
            {
                float abs = samples[i] < 0 ? -samples[i] : samples[i];
                if (abs > peak)
                {
                    peak = abs;
                }
            }

            _level = Mathf.Lerp(_level, peak, 0.3f);
        }

        private void OnGUI()
        {
            if (!_visible)
            {
                return;
            }

            const float w = 360f;
            const float h = 260f;
            GUILayout.BeginArea(new Rect(Screen.width / 2f - w / 2f, Screen.height / 2f - h / 2f, w, h), GUI.skin.box);
            GUILayout.Label(Loc.Get("onboard.title"));
            GUILayout.Space(6f);

            GUILayout.Label(Loc.Get("onboard.mic"));
            if (_devices.Length == 0)
            {
                _devices = Microphone.devices;
            }

            for (int i = 0; i < _devices.Length; i++)
            {
                if (GUILayout.Toggle(_deviceIndex == i, _devices[i]) && _deviceIndex != i)
                {
                    _deviceIndex = i;
                    StopTest();
                }
            }

            GUILayout.Space(6f);
            if (_testClip == null)
            {
                if (GUILayout.Button(Loc.Get("onboard.test")) && _devices.Length > 0)
                {
                    _testingDevice = _devices[_deviceIndex];
                    _testClip = Microphone.Start(_testingDevice, true, TestClipSeconds, AudioSettings.outputSampleRate);
                }
            }
            else
            {
                GUILayout.Label(Loc.Get("onboard.testing"));
                // Simple level bar.
                Rect bar = GUILayoutUtility.GetRect(w - 40f, 16f);
                GUI.Box(bar, GUIContent.none);
                GUI.Box(new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01(_level * 4f), bar.height), GUIContent.none);
                GUILayout.Label(Loc.Get("onboard.hear"));
            }

            GUILayout.FlexibleSpace();
            if (GUILayout.Button(Loc.Get("onboard.done")))
            {
                StopTest();
                PlayerPrefs.SetInt(DoneKey, 1);
                PlayerPrefs.Save();
                _visible = false;
            }

            GUILayout.EndArea();
        }

        private void StopTest()
        {
            if (!string.IsNullOrEmpty(_testingDevice))
            {
                Microphone.End(_testingDevice);
            }

            _testClip = null;
            _testingDevice = null;
            _level = 0f;
        }

        private void OnDestroy() => StopTest();
    }
}
