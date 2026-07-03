using UnityEngine;

namespace TableTalkers.Platform
{
    /// <summary>
    /// Operations endpoints and legal URLs, kept out of code. Real-time traffic is P2P/free;
    /// these point at the separate lightweight backend (reports live in ModerationConfig).
    /// Create via Assets > Create > TableTalkers > Ops Config.
    /// </summary>
    [CreateAssetMenu(fileName = "OpsConfig", menuName = "TableTalkers/Ops Config", order = 4)]
    public sealed class OpsConfig : ScriptableObject
    {
        [Header("Analytics / crash")]
        [Tooltip("HTTPS endpoint receiving analytics event JSON. Empty = analytics disabled.")]
        [SerializeField] private string _analyticsEndpoint = "";

        [Tooltip("Seconds between event batch flushes.")]
        [SerializeField, Min(5f)] private float _flushSeconds = 30f;

        [Header("Remote config")]
        [Tooltip("HTTPS URL returning flat JSON config. Empty = local defaults only.")]
        [SerializeField] private string _remoteConfigUrl = "";

        [Header("Legal (shown in onboarding/settings)")]
        [SerializeField] private string _privacyPolicyUrl = "";
        [SerializeField] private string _termsUrl = "";
        [SerializeField] private string _eulaUrl = "";

        public string AnalyticsEndpoint => _analyticsEndpoint;
        public float FlushSeconds => _flushSeconds;
        public string RemoteConfigUrl => _remoteConfigUrl;
        public string PrivacyPolicyUrl => _privacyPolicyUrl;
        public string TermsUrl => _termsUrl;
        public string EulaUrl => _eulaUrl;
    }
}
