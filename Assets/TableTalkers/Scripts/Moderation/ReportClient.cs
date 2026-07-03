using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using TableTalkers.Core;

namespace TableTalkers.Moderation
{
    /// <summary>
    /// Sends reports to the lightweight moderation backend (endpoint from ModerationConfig).
    /// Reports queue in memory and retry, so a flaky connection doesn't lose them.
    /// "A report button with nothing behind it" is forbidden — this is the pipe.
    /// </summary>
    public sealed class ReportClient : MonoBehaviour
    {
        [SerializeField] private ModerationConfig _config;

        private readonly Queue<string> _pending = new();
        private bool _sending;

        /// <summary>Queue a report about a participant. Reason is a short category string.</summary>
        public void Report(IParticipant target, string reason)
        {
            if (target == null)
            {
                return;
            }

            string json = BuildReportJson(target, reason);
            _pending.Enqueue(json);
            if (!_sending)
            {
                StartCoroutine(SendLoop());
            }
        }

        private string BuildReportJson(IParticipant target, string reason)
        {
            var sb = new StringBuilder(256);
            sb.Append('{');
            AppendField(sb, "reporterId", LocalPlayerInfo.Id); sb.Append(',');
            AppendField(sb, "targetId", target.Id); sb.Append(',');
            AppendField(sb, "targetName", target.DisplayName); sb.Append(',');
            AppendField(sb, "reason", reason); sb.Append(',');
            AppendField(sb, "appVersion", Application.version); sb.Append(',');
            sb.Append("\"clientUtc\":").Append(System.DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            sb.Append('}');
            return sb.ToString();
        }

        private static void AppendField(StringBuilder sb, string key, string value)
        {
            sb.Append('"').Append(key).Append("\":\"");
            foreach (char c in value ?? "")
            {
                if (c == '"' || c == '\\') sb.Append('\\');
                if (c >= ' ') sb.Append(c);
            }

            sb.Append('"');
        }

        private IEnumerator SendLoop()
        {
            _sending = true;
            while (_pending.Count > 0)
            {
                string endpoint = _config != null ? _config.ReportEndpoint : "";
                if (string.IsNullOrEmpty(endpoint))
                {
                    Debug.LogWarning($"[Report] No endpoint configured — {_pending.Count} report(s) held locally. 🔧 person: set ModerationConfig.ReportEndpoint.");
                    break;
                }

                string json = _pending.Peek();
                using var request = new UnityWebRequest(endpoint, UnityWebRequest.kHttpVerbPOST);
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    _pending.Dequeue();
                }
                else
                {
                    float retry = _config != null ? _config.RetrySeconds : 30f;
                    Debug.LogWarning($"[Report] Send failed ({request.error}); retrying in {retry}s.");
                    yield return new WaitForSeconds(retry);
                }
            }

            _sending = false;
        }
    }
}
