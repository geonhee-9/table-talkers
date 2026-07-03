using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace TableTalkers.Platform
{
    /// <summary>
    /// Minimal analytics: batched JSON events (session length, room size, Pro funnel) plus crash
    /// capture from unhandled exceptions. Can't measure → can't improve. Endpoint from OpsConfig;
    /// silently disabled when unset. Attach to a persistent object.
    /// </summary>
    public sealed class AnalyticsClient : MonoBehaviour
    {
        [SerializeField] private OpsConfig _config;

        public static AnalyticsClient Instance { get; private set; }

        private readonly List<string> _batch = new();
        private float _sessionStartedAt;

        private void Awake()
        {
            Instance = this;
            _sessionStartedAt = Time.realtimeSinceStartup;
            Application.logMessageReceived += HandleLog;
        }

        private void Start()
        {
            Track("session_start");
            StartCoroutine(FlushLoop());
        }

        private void OnDestroy()
        {
            Application.logMessageReceived -= HandleLog;
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void OnApplicationQuit()
        {
            Track("session_end", ("length_s", ((int)(Time.realtimeSinceStartup - _sessionStartedAt)).ToString()));
        }

        /// <summary>Queue an event with optional string properties.</summary>
        public void Track(string eventName, params (string key, string value)[] props)
        {
            var sb = new StringBuilder(128);
            sb.Append('{');
            Append(sb, "event", eventName); sb.Append(',');
            Append(sb, "appVersion", Application.version); sb.Append(',');
            sb.Append("\"utc\":").Append(System.DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            foreach ((string key, string value) in props)
            {
                sb.Append(',');
                Append(sb, key, value);
            }

            sb.Append('}');
            _batch.Add(sb.ToString());
        }

        private static void Append(StringBuilder sb, string key, string value)
        {
            sb.Append('"').Append(key).Append("\":\"");
            foreach (char c in value ?? "")
            {
                if (c == '"' || c == '\\') sb.Append('\\');
                if (c >= ' ') sb.Append(c);
            }

            sb.Append('"');
        }

        private void HandleLog(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Exception)
            {
                string trace = stackTrace != null && stackTrace.Length > 500 ? stackTrace.Substring(0, 500) : stackTrace;
                Track("crash", ("message", condition), ("stack", trace));
            }
        }

        private IEnumerator FlushLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(_config != null ? _config.FlushSeconds : 30f);
                yield return Flush();
            }
        }

        private IEnumerator Flush()
        {
            string endpoint = _config != null ? _config.AnalyticsEndpoint : "";
            if (_batch.Count == 0 || string.IsNullOrEmpty(endpoint))
            {
                _batch.Clear(); // disabled: don't grow unbounded
                yield break;
            }

            string payload = "[" + string.Join(",", _batch) + "]";
            _batch.Clear();

            using var request = new UnityWebRequest(endpoint, UnityWebRequest.kHttpVerbPOST);
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(payload));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            yield return request.SendWebRequest();
            // Best-effort: dropped batches are acceptable for MVP analytics.
        }
    }
}
