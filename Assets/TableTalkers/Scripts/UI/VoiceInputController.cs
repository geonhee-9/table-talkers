using UnityEngine;
using UnityEngine.InputSystem;
using TableTalkers.Voice;

namespace TableTalkers.UI
{
    /// <summary>
    /// Applies the user's voice input preference every frame: push-to-talk (hold V) or
    /// voice-activated, combined with the local mute. Depends on IVoiceService only.
    /// Attach to a persistent object (e.g. alongside SessionController).
    /// </summary>
    public sealed class VoiceInputController : MonoBehaviour
    {
        private bool _lastApplied = true;

        private void Update()
        {
            IVoiceService voice = VoiceServices.Current;
            if (voice == null)
            {
                return;
            }

            bool enabled;
            if (VoiceSettings.Muted)
            {
                enabled = false;
            }
            else if (VoiceSettings.Mode == VoiceInputMode.PushToTalk)
            {
                enabled = Keyboard.current != null && Keyboard.current.vKey.isPressed;
            }
            else
            {
                enabled = true;
            }

            if (enabled != _lastApplied)
            {
                _lastApplied = enabled;
                voice.SetInputEnabled(enabled);
            }
        }
    }
}
