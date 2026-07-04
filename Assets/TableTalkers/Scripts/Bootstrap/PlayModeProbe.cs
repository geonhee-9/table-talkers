using UnityEngine;

namespace TableTalkers.Bootstrap
{
    /// <summary>
    /// Diagnostic: logs unconditionally when Play mode starts, independent of any scene object,
    /// then reports whether the App composition root is present and active. Confirms whether our
    /// assemblies run at all vs. a scene-object problem.
    /// </summary>
    public static class PlayModeProbe
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Probe()
        {
            var app = GameObject.Find("App");
            Debug.Log($"[TableTalkers] ▶ Play mode is running. " +
                      $"App object found: {(app != null)}, active: {(app != null && app.activeInHierarchy)}. " +
                      $"NetworkManager present: {(GameObject.Find("NetworkManager") != null)}.");
        }
    }
}
