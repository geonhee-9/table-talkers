using UnityEngine;

namespace TableTalkers.Core
{
    /// <summary>
    /// Scene-side list of seat anchor transforms around the table, ordered by seat index.
    /// 🔧 person: place empty Transforms at each chair (position = seat, forward = facing the
    /// table center) and assign them here in the Room scene.
    /// </summary>
    public sealed class SeatAnchorRegistry : MonoBehaviour
    {
        [Tooltip("Seat anchors ordered by seat index. Forward should face the table center.")]
        [SerializeField] private Transform[] _anchors = new Transform[0];

        /// <summary>Singleton-style access for the active room scene.</summary>
        public static SeatAnchorRegistry Instance { get; private set; }

        public int Count => _anchors.Length;

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public Transform GetAnchor(int seatIndex)
        {
            if (seatIndex < 0 || seatIndex >= _anchors.Length)
            {
                return null;
            }

            return _anchors[seatIndex];
        }
    }
}
