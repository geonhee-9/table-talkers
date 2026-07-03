using UnityEngine;

namespace TableTalkers.Platform
{
    /// <summary>
    /// Steam settings kept out of code. The App ID must not be hardcoded; set it here (or ship a
    /// steam_appid.txt next to the executable for local testing). 480 is Valve's public test AppID —
    /// used as a fallback so the game can boot before a real App ID is registered.
    /// Create via Assets > Create > TableTalkers > Steam Config.
    /// </summary>
    [CreateAssetMenu(fileName = "SteamConfig", menuName = "TableTalkers/Steam Config", order = 1)]
    public sealed class SteamConfig : ScriptableObject
    {
        [Tooltip("Steam App ID. 0 = fall back to the Spacewar test AppID (480) for early testing.")]
        [SerializeField] private uint _appId = 0;

        /// <summary>Valve's public "Spacewar" test AppID, usable without a registered app.</summary>
        public const uint SpacewarTestAppId = 480;

        public uint AppId => _appId != 0 ? _appId : SpacewarTestAppId;
    }
}
