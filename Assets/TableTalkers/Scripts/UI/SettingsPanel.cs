using UnityEngine;
using UnityEngine.InputSystem;
using TableTalkers.Platform;

namespace TableTalkers.UI
{
    /// <summary>
    /// Settings (O toggles): nameplate visibility, app version, and the legally required links
    /// (privacy policy / terms / EULA — Steam launch blockers if missing). URLs from OpsConfig.
    /// All strings via Loc keys. IMGUI for MVP.
    /// </summary>
    public sealed class SettingsPanel : MonoBehaviour
    {
        [SerializeField] private OpsConfig _opsConfig;

        private bool _visible;

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.oKey.wasPressedThisFrame)
            {
                _visible = !_visible;
            }
        }

        private void OnGUI()
        {
            if (!_visible)
            {
                return;
            }

            const float w = 280f;
            GUILayout.BeginArea(new Rect(Screen.width / 2f - w / 2f, 60, w, 260), GUI.skin.box);
            GUILayout.Label(Loc.Get("settings.title"));
            GUILayout.Label($"v{Application.version}");
            GUILayout.Space(6f);

            Nameplate.AlwaysShow = GUILayout.Toggle(Nameplate.AlwaysShow, Loc.Get("settings.nameplates"));

            GUILayout.Space(10f);
            LegalLink("settings.privacy", _opsConfig != null ? _opsConfig.PrivacyPolicyUrl : "");
            LegalLink("settings.terms", _opsConfig != null ? _opsConfig.TermsUrl : "");
            LegalLink("settings.eula", _opsConfig != null ? _opsConfig.EulaUrl : "");

            GUILayout.EndArea();
        }

        private static void LegalLink(string locKey, string url)
        {
            GUI.enabled = !string.IsNullOrEmpty(url);
            if (GUILayout.Button(Loc.Get(locKey)))
            {
                Application.OpenURL(url);
            }

            GUI.enabled = true;
        }
    }
}
