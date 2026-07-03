using System.IO;
using Netcode.Transports.Facepunch;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using TableTalkers.Bootstrap;
using TableTalkers.Core;
using TableTalkers.Moderation;
using TableTalkers.Networking;
using TableTalkers.Platform;
using TableTalkers.Player;
using TableTalkers.Presence;
using TableTalkers.UI;
using TableTalkers.Voice;

namespace TableTalkers.EditorTools
{
    /// <summary>
    /// One-click MVP setup. Builds config assets, the player prefab, and a single "Boot" scene
    /// (table, seats, managers, NetworkManager) with every reference wired — so a non-developer
    /// doesn't have to hand-assemble the scene. Menu: TableTalkers > Setup > Build Everything (MVP).
    /// Re-runnable: it overwrites what it created.
    /// </summary>
    public static class SceneSetupTool
    {
        private const string ConfigDir = "Assets/TableTalkers/Config";
        private const string SceneDir = "Assets/TableTalkers/Scenes";
        private const string PlayerPrefabPath = "Assets/TableTalkers/Player.prefab";
        private const string ScenePath = SceneDir + "/Boot.unity";

        [MenuItem("TableTalkers/Setup/Build Everything (MVP)")]
        public static void BuildEverything()
        {
            EnsureDir(ConfigDir);
            EnsureDir(SceneDir);

            // 1) Config assets (defaults are fine; endpoints filled later).
            var roomConfig = LoadOrCreate<RoomConfig>(ConfigDir + "/RoomConfig.asset");
            var steamConfig = LoadOrCreate<SteamConfig>(ConfigDir + "/SteamConfig.asset");
            var voiceConfig = LoadOrCreate<VoiceConfig>(ConfigDir + "/VoiceConfig.asset");
            var moderationConfig = LoadOrCreate<ModerationConfig>(ConfigDir + "/ModerationConfig.asset");
            var opsConfig = LoadOrCreate<OpsConfig>(ConfigDir + "/OpsConfig.asset");
            AssetDatabase.SaveAssets();

            // 2) Player prefab.
            GameObject playerPrefab = BuildPlayerPrefab(roomConfig);

            // 3) Scene with everything wired.
            BuildScene(playerPrefab, roomConfig, steamConfig, voiceConfig, moderationConfig, opsConfig);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[TableTalkers] Setup complete. Open Scenes/Boot and press Play. " +
                      "Seated view appears after you Create/Join a room (needs Steam running).");
        }

        // ---------------------------------------------------------------- Player prefab

        private static GameObject BuildPlayerPrefab(RoomConfig roomConfig)
        {
            var root = new GameObject("Player");
            root.AddComponent<NetworkObject>();
            root.AddComponent<NetworkParticipant>();
            var head = root.AddComponent<HeadOrientationSync>();
            var controller = root.AddComponent<SeatedFirstPersonController>();
            root.AddComponent<PingProbe>();
            root.AddComponent<EmoteSync>();
            root.AddComponent<ChatChannel>();
            var rig = root.AddComponent<OwnerRigActivator>();

            // Body (capsule)
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = new Vector3(0f, 0.3f, 0f);
            body.transform.localScale = new Vector3(0.4f, 0.5f, 0.4f);
            RemoveCollider(body);

            // Head (sphere) with presence visuals
            var headGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            headGo.name = "Head";
            headGo.transform.SetParent(root.transform, false);
            headGo.transform.localPosition = new Vector3(0f, 1.0f, 0f);
            headGo.transform.localScale = new Vector3(0.35f, 0.35f, 0.35f);
            RemoveCollider(headGo);
            headGo.AddComponent<SpeakingIndicator>();
            headGo.AddComponent<Nameplate>();
            headGo.AddComponent<AmplitudeLipsync>();

            // Camera rig (enabled only for local owner at runtime)
            var camGo = new GameObject("PlayerCamera");
            camGo.transform.SetParent(root.transform, false);
            camGo.transform.localPosition = new Vector3(0f, 1.15f, 0f);
            var cam = camGo.AddComponent<Camera>();
            var listener = camGo.AddComponent<AudioListener>();
            var playerCam = camGo.AddComponent<PlayerCamera>();

            // Wire internal references.
            SetRef(head, "_headBone", headGo.transform);
            SetRef(head, "_config", roomConfig);
            SetRef(playerCam, "_controller", controller);
            SetRef(rig, "_camera", cam);
            SetRef(rig, "_listener", listener);
            SetRef(rig, "_controller", controller);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        // ---------------------------------------------------------------- Scene

        private static void BuildScene(
            GameObject playerPrefab, RoomConfig roomConfig, SteamConfig steamConfig,
            VoiceConfig voiceConfig, ModerationConfig moderationConfig, OpsConfig opsConfig)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Lighting
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // Preview camera (turns off when the local player spawns)
            var previewGo = new GameObject("PreviewCamera");
            previewGo.transform.SetPositionAndRotation(new Vector3(0f, 2.6f, -4.8f), Quaternion.Euler(24f, 0f, 0f));
            previewGo.AddComponent<Camera>();
            previewGo.AddComponent<AudioListener>();
            previewGo.AddComponent<PreviewCamera>();

            // Floor
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.localScale = new Vector3(2f, 1f, 2f);

            // Table
            var table = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            table.name = "Table";
            table.transform.position = new Vector3(0f, 0.4f, 0f);
            table.transform.localScale = new Vector3(1.5f, 0.4f, 1.5f);

            // Seat anchors around the table, each facing the centre.
            var anchorsParent = new GameObject("SeatAnchors");
            var registry = anchorsParent.AddComponent<SeatAnchorRegistry>();
            Vector3[] pos =
            {
                new Vector3(0f, 0.55f, -1.6f),
                new Vector3(1.6f, 0.55f, 0f),
                new Vector3(0f, 0.55f, 1.6f),
                new Vector3(-1.6f, 0.55f, 0f)
            };
            float[] yaw = { 0f, -90f, 180f, 90f };
            var anchors = new Transform[pos.Length];
            for (int i = 0; i < pos.Length; i++)
            {
                var seat = new GameObject("Seat" + i);
                seat.transform.SetParent(anchorsParent.transform, false);
                seat.transform.SetPositionAndRotation(pos[i], Quaternion.Euler(0f, yaw[i], 0f));
                anchors[i] = seat.transform;
            }

            SetArray(registry, "_anchors", anchors);

            // NetworkManager + Facepunch transport
            var nmGo = new GameObject("NetworkManager");
            var nm = nmGo.AddComponent<NetworkManager>();
            var transport = nmGo.AddComponent<FacepunchTransport>();
            if (nm.NetworkConfig == null)
            {
                nm.NetworkConfig = new NetworkConfig();
            }

            nm.NetworkConfig.NetworkTransport = transport;
            nm.NetworkConfig.PlayerPrefab = playerPrefab;
            nm.NetworkConfig.ConnectionApproval = true;
            nm.NetworkConfig.TickRate = (uint)roomConfig.NetworkTickRate;
            EditorUtility.SetDirty(nm);

            // App composition root with all managers
            var app = new GameObject("App");
            var appEntry = app.AddComponent<AppEntry>();
            var session = app.AddComponent<SessionController>();
            var lobby = app.AddComponent<SteamLobby>();
            var dlc = app.AddComponent<SteamDlc>();
            var network = app.AddComponent<NetworkSessionService>();
            var steamVoice = app.AddComponent<SteamVoiceService>();
            var odinVoice = app.AddComponent<OdinVoiceService>();
            var voiceSelector = app.AddComponent<VoiceServiceSelector>();
            app.AddComponent<VoiceInputController>();
            app.AddComponent<BlockController>();
            var reportClient = app.AddComponent<ReportClient>();
            var hostModeration = app.AddComponent<HostModeration>();
            app.AddComponent<MicSettingsPanel>();
            var moderationPanel = app.AddComponent<ModerationPanel>();
            app.AddComponent<ChatPanel>();
            app.AddComponent<OnboardingPanel>();
            app.AddComponent<SettingsPanel>();
            app.AddComponent<DebugOverlay>();
            app.AddComponent<AnalyticsClient>();
            app.AddComponent<RemoteConfigClient>();

            // Wiring
            SetRef(lobby, "_config", steamConfig);
            SetRef(dlc, "_config", steamConfig);
            SetRef(network, "_config", roomConfig);
            SetRef(steamVoice, "_config", voiceConfig);
            SetRef(odinVoice, "_config", voiceConfig);
            SetRef(voiceSelector, "_config", voiceConfig);
            SetRef(voiceSelector, "_steam", steamVoice);
            SetRef(voiceSelector, "_odin", odinVoice);
            SetRef(reportClient, "_config", moderationConfig);
            SetRef(moderationPanel, "_reportClient", reportClient);
            SetRef(moderationPanel, "_hostModeration", hostModeration);

            var analytics = app.GetComponent<AnalyticsClient>();
            var remoteConfig = app.GetComponent<RemoteConfigClient>();
            SetRef(analytics, "_config", opsConfig);
            SetRef(remoteConfig, "_config", opsConfig);
            SetRef(app.GetComponent<SettingsPanel>(), "_opsConfig", opsConfig);
            SetRef(app.GetComponent<OnboardingPanel>(), "_opsConfig", opsConfig);

            SetRef(session, "_appEntry", appEntry);
            SetRef(session, "_lobby", lobby);
            SetRef(session, "_network", network);
            SetRef(session, "_voiceSelector", voiceSelector);
            SetRef(session, "_roomConfig", roomConfig);
            SetRef(session, "_hostModeration", hostModeration);
            SetRef(session, "_dlc", dlc);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings(ScenePath);
        }

        // ---------------------------------------------------------------- Helpers

        private static void AddSceneToBuildSettings(string path)
        {
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(path, true) };
        }

        private static void EnsureDir(string assetDir)
        {
            string full = Path.Combine(Directory.GetCurrentDirectory(), assetDir);
            if (!Directory.Exists(full))
            {
                Directory.CreateDirectory(full);
                AssetDatabase.Refresh();
            }
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null)
            {
                return existing;
            }

            var inst = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(inst, path);
            return inst;
        }

        private static void RemoveCollider(GameObject go)
        {
            var col = go.GetComponent<Collider>();
            if (col != null)
            {
                Object.DestroyImmediate(col);
            }
        }

        private static void SetRef(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(field);
            if (p == null)
            {
                Debug.LogError($"[TableTalkers] Field '{field}' not found on {target.GetType().Name}.");
                return;
            }

            p.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetArray(Object target, string field, Object[] values)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(field);
            if (p == null)
            {
                Debug.LogError($"[TableTalkers] Array field '{field}' not found on {target.GetType().Name}.");
                return;
            }

            p.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
