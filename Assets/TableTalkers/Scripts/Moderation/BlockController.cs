using UnityEngine;
using TableTalkers.Core;
using TableTalkers.Voice;

namespace TableTalkers.Moderation
{
    /// <summary>
    /// Enforces blocks at runtime: a blocked participant is muted (inaudible) and their avatar
    /// hidden (invisible) on this client. Works purely through IParticipant/IVoiceService.
    /// Attach to a persistent object.
    /// </summary>
    public sealed class BlockController : MonoBehaviour
    {
        private void OnEnable()
        {
            ParticipantRegistry.Added += Apply;
            BlockList.Changed += HandleBlockChanged;
        }

        private void OnDisable()
        {
            ParticipantRegistry.Added -= Apply;
            BlockList.Changed -= HandleBlockChanged;
        }

        private void HandleBlockChanged(string participantId, bool blocked)
        {
            var all = ParticipantRegistry.All;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].Id == participantId)
                {
                    Apply(all[i]);
                    return;
                }
            }
        }

        private void Apply(IParticipant participant)
        {
            if (participant.IsLocal)
            {
                return;
            }

            bool blocked = BlockList.IsBlocked(participant.Id);
            VoiceServices.Current?.SetPeerMuted(participant.Id, blocked);

            // Participants are scene components; hide renderers without knowing the concrete type.
            if (participant is Component component)
            {
                foreach (Renderer r in component.GetComponentsInChildren<Renderer>(true))
                {
                    r.enabled = !blocked;
                }
            }
        }
    }
}
