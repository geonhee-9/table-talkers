using System;
using Steamworks;
using UnityEngine;

namespace TableTalkers.Platform
{
    /// <summary>
    /// Steam DLC ownership checks — Pro (host-pays room upgrade) and cosmetics. Steam syncs DLC
    /// ownership itself, so no license server is needed. Guarded for Steam-not-running.
    /// </summary>
    public sealed class SteamDlc : MonoBehaviour
    {
        [SerializeField] private SteamConfig _config;

        /// <summary>Does the LOCAL user own the Pro DLC? False when Steam is unavailable or unset.</summary>
        public bool OwnsPro()
        {
            if (_config == null)
            {
                _config = Resources.Load<SteamConfig>("SteamConfig");
            }

            if (!SteamClient.IsValid || _config == null || _config.ProDlcAppId == 0)
            {
                return false;
            }

            try
            {
                return SteamApps.IsDlcInstalled(_config.ProDlcAppId);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SteamDlc] DLC check failed: {e.Message}");
                return false;
            }
        }

        /// <summary>Cosmetic pack ownership by its DLC App ID (cosmetic catalog comes later).</summary>
        public bool OwnsCosmetic(uint dlcAppId)
        {
            if (!SteamClient.IsValid || dlcAppId == 0)
            {
                return false;
            }

            try
            {
                return SteamApps.IsDlcInstalled(dlcAppId);
            }
            catch
            {
                return false;
            }
        }
    }
}
