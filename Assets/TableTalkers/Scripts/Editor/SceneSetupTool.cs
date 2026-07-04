using System.IO;
using Netcode.Transports.Facepunch;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
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
    /// One-click MVP setup, v2: cozy-lounge room (warm lights, walls, rug, chairs, hanging lamp)
    /// and presence-forward avatars (eyes for gaze, seat colors, speaking ring). Builds config
    /// assets (under Resources for runtime fallback), the player prefab, and the Boot scene with
    /// every reference wired. Re-runnable: overwrites what it created.
    /// Menu: TableTalkers > Setup > Build Everything (MVP).
    /// </summary>
    public static class SceneSetupTool
    {
        // Configs live under a Resources folder so components can load them at runtime even if a
        // scene reference is missing (belt-and-suspenders against scene wiring loss).
        private const string ConfigDir = "Assets/TableTalkers/Resources";
        private const string MaterialDir = "Assets/TableTalkers/Materials";
        private const string SceneDir = "Assets/TableTalkers/Scenes";
        private const string PlayerPrefabPath = "Assets/TableTalkers/Player.prefab";
        private const string ScenePath = SceneDir + "/Boot.unity";

        [MenuItem("TableTalkers/Setup/Build Everything (MVP)")]
        public static void BuildEverything()
        {
            EnsureDir(ConfigDir);
            EnsureDir(MaterialDir);
            EnsureDir(SceneDir);

            // 1) Config assets (defaults are fine; endpoints filled later).
            CreateIfMissing<RoomConfig>(ConfigDir + "/RoomConfig.asset");
            CreateIfMissing<SteamConfig>(ConfigDir + "/SteamConfig.asset");
            CreateIfMissing<VoiceConfig>(ConfigDir + "/VoiceConfig.asset");
            CreateIfMissing<ModerationConfig>(ConfigDir + "/ModerationConfig.asset");
            CreateIfMissing<OpsConfig>(ConfigDir + "/OpsConfig.asset");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Re-load from disk so references resolve to fully-imported assets (freshly created
            // assets can serialize as null references in a scene saved in the same tool run).
            var roomConfig = AssetDatabase.LoadAssetAtPath<RoomConfig>(ConfigDir + "/RoomConfig.asset");
            var steamConfig = AssetDatabase.LoadAssetAtPath<SteamConfig>(ConfigDir + "/SteamConfig.asset");
            var voiceConfig = AssetDatabase.LoadAssetAtPath<VoiceConfig>(ConfigDir + "/VoiceConfig.asset");
            var moderationConfig = AssetDatabase.LoadAssetAtPath<ModerationConfig>(ConfigDir + "/ModerationConfig.asset");
            var opsConfig = AssetDatabase.LoadAssetAtPath<OpsConfig>(ConfigDir + "/OpsConfig.asset");

            // 2) Player prefab.
            GameObject playerPrefab = BuildPlayerPrefab(roomConfig);

            // 3) Scene with everything wired.
            BuildScene(playerPrefab, roomConfig, steamConfig, voiceConfig, moderationConfig, opsConfig);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[TableTalkers] Setup complete (cozy lounge v2). Open Scenes/Boot and press Play.");
        }

        // ---------------------------------------------------------------- Materials

        private static Material Mat(string name, Color color, float smoothness = 0.25f, Color? emission = null)
        {
            string path = MaterialDir + "/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Standard"));
                AssetDatabase.CreateAsset(m, path);
            }

            m.color = color;
            m.SetFloat("_Glossiness", smoothness);
            if (emission.HasValue)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emission.Value);
            }
            else
            {
                m.DisableKeyword("_EMISSION");
            }

            EditorUtility.SetDirty(m);
            return m;
        }

        // ---------------------------------------------------------------- Player prefab

        private static GameObject BuildPlayerPrefab(RoomConfig roomConfig)
        {
            Material bodyMat = Mat("AvatarBody", new Color(0.75f, 0.70f, 0.66f));
            Material headMat = Mat("AvatarHead", new Color(0.92f, 0.80f, 0.68f));
            Material eyeMat = Mat("AvatarEye", new Color(0.12f, 0.10f, 0.10f), 0.6f);
            Material ringMat = Mat("SpeakingRing", new Color(1f, 0.85f, 0.4f), 0.2f,
                                   new Color(1f, 0.75f, 0.25f) * 1.4f);

            var root = new GameObject("Player");
            root.AddComponent<NetworkObject>();
            root.AddComponent<NetworkParticipant>();
            var head = root.AddComponent<HeadOrientationSync>();
            var controller = root.AddComponent<SeatedFirstPersonController>();
            root.AddComponent<PingProbe>();
            root.AddComponent<EmoteSync>();
            root.AddComponent<ChatChannel>();
            var rig = root.AddComponent<OwnerRigActivator>();

            // Body (capsule, seat-colored at runtime)
            var body = Primitive(PrimitiveType.Capsule, "Body", root.transform,
                new Vector3(0f, 0.3f, 0f), new Vector3(0.4f, 0.5f, 0.4f), bodyMat);

            // Head (sphere) with eyes so gaze direction reads at a glance
            var headGo = Primitive(PrimitiveType.Sphere, "Head", root.transform,
                new Vector3(0f, 1.0f, 0f), new Vector3(0.35f, 0.35f, 0.35f), headMat);
            Primitive(PrimitiveType.Sphere, "EyeL", headGo.transform,
                new Vector3(-0.28f, 0.15f, 0.42f), new Vector3(0.16f, 0.16f, 0.16f), eyeMat);
            Primitive(PrimitiveType.Sphere, "EyeR", headGo.transform,
                new Vector3(0.28f, 0.15f, 0.42f), new Vector3(0.16f, 0.16f, 0.16f), eyeMat);

            var indicator = headGo.AddComponent<SpeakingIndicator>();
            headGo.AddComponent<Nameplate>();
            headGo.AddComponent<AmplitudeLipsync>();
            var colorizer = root.AddComponent<AvatarColorizer>();

            // Speaking ring on the floor under the avatar (toggled by SpeakingIndicator)
            var ring = Primitive(PrimitiveType.Cylinder, "SpeakingRing", root.transform,
                new Vector3(0f, -0.52f, 0f), new Vector3(0.8f, 0.012f, 0.8f), ringMat);

            // Camera rig (enabled only for local owner at runtime)
            var camGo = new GameObject("PlayerCamera");
            camGo.transform.SetParent(root.transform, false);
            camGo.transform.localPosition = new Vector3(0f, 1.15f, 0f);
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.09f, 0.07f, 0.06f);
            var listener = camGo.AddComponent<AudioListener>();
            var playerCam = camGo.AddComponent<PlayerCamera>();

            // Wire internal references.
            SetRef(head, "_headBone", headGo.transform);
            SetRef(head, "_config", roomConfig);
            SetRef(playerCam, "_controller", controller);
            SetRef(rig, "_camera", cam);
            SetRef(rig, "_listener", listener);
            SetRef(rig, "_controller", controller);
            SetRef(indicator, "_indicator", ring);
            SetRef(indicator, "_tintTarget", headGo.GetComponent<Renderer>());
            SetRef(colorizer, "_body", body.GetComponent<Renderer>());

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

            BuildCozyLounge();

            // Preview camera (turns off when the local player spawns)
            var previewGo = new GameObject("PreviewCamera");
            previewGo.transform.SetPositionAndRotation(new Vector3(0f, 2.4f, -4.6f), Quaternion.Euler(22f, 0f, 0f));
            var previewCam = previewGo.AddComponent<Camera>();
            previewCam.clearFlags = CameraClearFlags.SolidColor;
            previewCam.backgroundColor = new Color(0.09f, 0.07f, 0.06f);
            previewGo.AddComponent<AudioListener>();
            previewGo.AddComponent<PreviewCamera>();

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
            var onboarding = app.AddComponent<OnboardingPanel>();
            var settings = app.AddComponent<SettingsPanel>();
            app.AddComponent<DebugOverlay>();
            var analytics = app.AddComponent<AnalyticsClient>();
            var remoteConfig = app.AddComponent<RemoteConfigClient>();

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
            SetRef(analytics, "_config", opsConfig);
            SetRef(remoteConfig, "_config", opsConfig);
            SetRef(settings, "_opsConfig", opsConfig);
            SetRef(onboarding, "_opsConfig", opsConfig);
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

        /// <summary>Warm, dim, intimate: dark walls, wood floor, rug, chairs, hanging lamp.</summary>
        private static void BuildCozyLounge()
        {
            Material floorMat = Mat("FloorWood", new Color(0.33f, 0.24f, 0.18f), 0.35f);
            Material wallMat = Mat("Wall", new Color(0.24f, 0.19f, 0.16f), 0.1f);
            Material rugMat = Mat("Rug", new Color(0.42f, 0.20f, 0.16f), 0.05f);
            Material tableMat = Mat("TableWood", new Color(0.45f, 0.30f, 0.19f), 0.5f);
            Material chairMat = Mat("Chair", new Color(0.30f, 0.22f, 0.17f), 0.3f);
            Material bulbMat = Mat("LampBulb", new Color(1f, 0.85f, 0.6f), 0.3f,
                                    new Color(1f, 0.72f, 0.40f) * 2.2f);
            Material cordMat = Mat("LampCord", new Color(0.1f, 0.1f, 0.1f), 0.2f);

            // Mood lighting: dim warm ambient + soft fog.
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.30f, 0.26f, 0.23f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.10f, 0.08f, 0.07f);
            RenderSettings.fogDensity = 0.028f;

            var sun = new GameObject("Directional Light");
            var light = sun.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.88f, 0.72f);
            light.intensity = 0.35f;
            light.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(45f, -30f, 0f);

            // Hanging lamp over the table — the room's warm heart.
            var lamp = new GameObject("Lamp");
            var lampLight = lamp.AddComponent<Light>();
            lampLight.type = LightType.Point;
            lampLight.color = new Color(1f, 0.72f, 0.45f);
            lampLight.intensity = 2.0f;
            lampLight.range = 7f;
            lamp.transform.position = new Vector3(0f, 2.05f, 0f);
            Primitive(PrimitiveType.Sphere, "Bulb", lamp.transform,
                new Vector3(0f, 0.1f, 0f), new Vector3(0.22f, 0.22f, 0.22f), bulbMat);
            Primitive(PrimitiveType.Cylinder, "Cord", lamp.transform,
                new Vector3(0f, 0.6f, 0f), new Vector3(0.02f, 0.45f, 0.02f), cordMat);

            // Room shell: floor, rug, four walls, ceiling.
            Primitive(PrimitiveType.Plane, "Floor", null,
                Vector3.zero, new Vector3(1.2f, 1f, 1.2f), floorMat, keepCollider: true);
            Primitive(PrimitiveType.Cylinder, "Rug", null,
                new Vector3(0f, 0.012f, 0f), new Vector3(6f, 0.01f, 6f), rugMat);
            const float half = 6f, wallH = 3f;
            Primitive(PrimitiveType.Cube, "WallN", null, new Vector3(0, wallH / 2f, half), new Vector3(half * 2, wallH, 0.2f), wallMat, keepCollider: true);
            Primitive(PrimitiveType.Cube, "WallS", null, new Vector3(0, wallH / 2f, -half), new Vector3(half * 2, wallH, 0.2f), wallMat, keepCollider: true);
            Primitive(PrimitiveType.Cube, "WallE", null, new Vector3(half, wallH / 2f, 0), new Vector3(0.2f, wallH, half * 2), wallMat, keepCollider: true);
            Primitive(PrimitiveType.Cube, "WallW", null, new Vector3(-half, wallH / 2f, 0), new Vector3(0.2f, wallH, half * 2), wallMat, keepCollider: true);
            Primitive(PrimitiveType.Cube, "Ceiling", null, new Vector3(0, wallH + 0.05f, 0), new Vector3(half * 2, 0.1f, half * 2), wallMat);

            // Round table.
            Primitive(PrimitiveType.Cylinder, "Table", null,
                new Vector3(0f, 0.4f, 0f), new Vector3(1.5f, 0.4f, 1.5f), tableMat, keepCollider: true);

            // Chairs at each seat.
            Vector3[] pos =
            {
                new Vector3(0f, 0f, -1.6f), new Vector3(1.6f, 0f, 0f),
                new Vector3(0f, 0f, 1.6f), new Vector3(-1.6f, 0f, 0f)
            };
            float[] yaw = { 0f, -90f, 180f, 90f };
            for (int i = 0; i < pos.Length; i++)
            {
                var chair = new GameObject("Chair" + i);
                chair.transform.SetPositionAndRotation(pos[i], Quaternion.Euler(0f, yaw[i], 0f));
                Primitive(PrimitiveType.Cube, "Seat", chair.transform,
                    new Vector3(0f, 0.45f, 0f), new Vector3(0.52f, 0.08f, 0.52f), chairMat);
                Primitive(PrimitiveType.Cube, "Back", chair.transform,
                    new Vector3(0f, 0.85f, -0.26f), new Vector3(0.52f, 0.7f, 0.07f), chairMat);
                Primitive(PrimitiveType.Cube, "Legs", chair.transform,
                    new Vector3(0f, 0.2f, 0f), new Vector3(0.46f, 0.4f, 0.46f), chairMat);
            }
        }

        // ---------------------------------------------------------------- Helpers

        private static GameObject Primitive(
            PrimitiveType type, string name, Transform parent,
            Vector3 localPos, Vector3 localScale, Material material, bool keepCollider = false)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            if (parent != null)
            {
                go.transform.SetParent(parent, false);
            }

            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            go.GetComponent<Renderer>().sharedMaterial = material;
            if (!keepCollider)
            {
                var col = go.GetComponent<Collider>();
                if (col != null)
                {
                    Object.DestroyImmediate(col);
                }
            }

            return go;
        }

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

        private static void CreateIfMissing<T>(string path) where T : ScriptableObject
        {
            if (AssetDatabase.LoadAssetAtPath<T>(path) != null)
            {
                return;
            }

            var inst = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(inst, path);
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
