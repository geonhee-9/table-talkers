using Unity.Netcode;
using UnityEngine;

namespace TableTalkers.Player
{
    /// <summary>
    /// Enables the first-person rig (camera, audio listener, look controller) only on the local
    /// player. Remote avatars keep their camera/listener/input off so there is exactly one active
    /// camera and listener. Attach to the player prefab root; assign its own camera rig.
    /// </summary>
    public sealed class OwnerRigActivator : NetworkBehaviour
    {
        [SerializeField] private Camera _camera;
        [SerializeField] private AudioListener _listener;
        [SerializeField] private SeatedFirstPersonController _controller;

        public override void OnNetworkSpawn()
        {
            bool owner = IsOwner;
            if (_camera != null) _camera.enabled = owner;
            if (_listener != null) _listener.enabled = owner;
            if (_controller != null) _controller.enabled = owner;
        }
    }
}
