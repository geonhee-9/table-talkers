using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace TableTalkers.Platform
{
    /// <summary>
    /// Fetches a flat JSON dictionary at boot (capacity limits, feature flags) so values can change
    /// without shipping a build. Local ScriptableObject values remain the defaults; remote wins
    /// when present. Only flat {"key": "value"|number|bool} JSON is supported on purpose.
    /// </summary>
    public sealed class RemoteConfigClient : MonoBehaviour
    {
        [SerializeField] private OpsConfig _config;

        public static RemoteConfigClient Instance { get; private set; }

        private readonly Dictionary<string, string> _values = new();

        public bool Loaded { get; private set; }

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            if (_config != null && !string.IsNullOrEmpty(_config.RemoteConfigUrl))
            {
                StartCoroutine(Fetch(_config.RemoteConfigUrl));
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public bool TryGetInt(string key, out int value)
        {
            value = 0;
            return _values.TryGetValue(key, out string raw) && int.TryParse(raw, out value);
        }

        public bool TryGetBool(string key, out bool value)
        {
            value = false;
            if (!_values.TryGetValue(key, out string raw))
            {
                return false;
            }

            value = raw == "true" || raw == "1";
            return true;
        }

        public bool TryGetString(string key, out string value) => _values.TryGetValue(key, out value);

        private IEnumerator Fetch(string url)
        {
            using UnityWebRequest request = UnityWebRequest.Get(url);
            yield return request.SendWebRequest();
            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[RemoteConfig] Fetch failed ({request.error}) — using local defaults.");
                yield break;
            }

            ParseFlatJson(request.downloadHandler.text);
            Loaded = true;
        }

        /// <summary>Tiny parser for flat {"k":"v","n":1,"b":true} JSON — no nesting.</summary>
        private void ParseFlatJson(string json)
        {
            _values.Clear();
            int i = 0;
            while (i < json.Length)
            {
                int keyStart = json.IndexOf('"', i);
                if (keyStart < 0) break;
                int keyEnd = json.IndexOf('"', keyStart + 1);
                if (keyEnd < 0) break;
                string key = json.Substring(keyStart + 1, keyEnd - keyStart - 1);

                int colon = json.IndexOf(':', keyEnd);
                if (colon < 0) break;

                int valueStart = colon + 1;
                while (valueStart < json.Length && (json[valueStart] == ' ' || json[valueStart] == '\t')) valueStart++;

                string value;
                int next;
                if (valueStart < json.Length && json[valueStart] == '"')
                {
                    int valueEnd = json.IndexOf('"', valueStart + 1);
                    if (valueEnd < 0) break;
                    value = json.Substring(valueStart + 1, valueEnd - valueStart - 1);
                    next = valueEnd + 1;
                }
                else
                {
                    int comma = json.IndexOfAny(new[] { ',', '}' }, valueStart);
                    if (comma < 0) comma = json.Length;
                    value = json.Substring(valueStart, comma - valueStart).Trim();
                    next = comma;
                }

                _values[key] = value;
                i = next + 1;
            }
        }
    }
}
