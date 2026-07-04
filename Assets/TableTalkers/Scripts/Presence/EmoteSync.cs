using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using TableTalkers.Core;

namespace TableTalkers.Presence
{
    /// <summary>
    /// One-shot emote events over RPC (never synced state). Owner presses 1–5 → ServerRpc →
    /// ClientRpc on everyone → Animator trigger named after the emote + a static event for UI.
    /// Attach to the player prefab. 🔧 person: add an Animator with triggers matching EmoteKind
    /// names (Nod, Laugh, RaiseHand, ThumbsUp, Clap).
    /// </summary>
    public sealed class EmoteSync : NetworkBehaviour
    {
        [Tooltip("Animator with a trigger per EmoteKind name. Optional until clips exist.")]
        [SerializeField] private Animator _animator;

        /// <summary>Raised on every client when any participant plays an emote.</summary>
        public static event Action<IParticipant, EmoteKind> EmotePlayed;

        private IParticipant _participant;

        public override void OnNetworkSpawn()
        {
            _participant = GetComponent<IParticipant>();
        }

        private void Update()
        {
            if (!IsSpawned || !IsOwner || Keyboard.current == null)
            {
                return;
            }

            // Don't fire emotes while the user is typing in an IMGUI field (e.g. chat).
            if (GUIUtility.keyboardControl != 0)
            {
                return;
            }

            if (Keyboard.current.digit1Key.wasPressedThisFrame) Play(EmoteKind.Nod);
            else if (Keyboard.current.digit2Key.wasPressedThisFrame) Play(EmoteKind.Laugh);
            else if (Keyboard.current.digit3Key.wasPressedThisFrame) Play(EmoteKind.RaiseHand);
            else if (Keyboard.current.digit4Key.wasPressedThisFrame) Play(EmoteKind.ThumbsUp);
            else if (Keyboard.current.digit5Key.wasPressedThisFrame) Play(EmoteKind.Clap);
        }

        /// <summary>Local API for UI buttons as well as hotkeys.</summary>
        public void Play(EmoteKind kind)
        {
            if (IsSpawned && IsOwner)
            {
                PlayEmoteServerRpc((byte)kind);
            }
        }

        [ServerRpc]
        private void PlayEmoteServerRpc(byte kind)
        {
            PlayEmoteClientRpc(kind);
        }

        [ClientRpc]
        private void PlayEmoteClientRpc(byte kind)
        {
            var emote = (EmoteKind)kind;
            if (_animator != null)
            {
                _animator.SetTrigger(emote.ToString());
            }

            EmotePlayed?.Invoke(_participant, emote);
        }
    }
}
