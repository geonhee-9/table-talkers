using UnityEngine;

namespace TableTalkers.Core
{
    /// <summary>
    /// Provides the local player's head pose each frame. Implemented by the seated controller
    /// (Player module) and consumed by head sync (Presence module) — the interface lives in Core
    /// so neither module needs to reference the other.
    /// </summary>
    public interface IHeadPoseSource
    {
        /// <summary>Head yaw in degrees, relative to the seat's forward direction.</summary>
        float Yaw { get; }

        /// <summary>Head pitch in degrees (positive = looking up).</summary>
        float Pitch { get; }

        /// <summary>Combined head rotation.</summary>
        Quaternion HeadOrientation { get; }
    }
}
