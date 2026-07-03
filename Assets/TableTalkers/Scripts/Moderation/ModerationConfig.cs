using UnityEngine;

namespace TableTalkers.Moderation
{
    /// <summary>
    /// Moderation endpoints kept out of code. A report button without a receiving backend is
    /// forbidden by design — 🔧 person: stand up a lightweight report endpoint and set its URL.
    /// Create via Assets > Create > TableTalkers > Moderation Config.
    /// </summary>
    [CreateAssetMenu(fileName = "ModerationConfig", menuName = "TableTalkers/Moderation Config", order = 3)]
    public sealed class ModerationConfig : ScriptableObject
    {
        [Tooltip("HTTPS endpoint that receives report JSON. Empty = reports queue locally and warn.")]
        [SerializeField] private string _reportEndpoint = "";

        [Tooltip("Send retry interval in seconds.")]
        [SerializeField, Min(1f)] private float _retrySeconds = 30f;

        public string ReportEndpoint => _reportEndpoint;
        public float RetrySeconds => _retrySeconds;
    }
}
