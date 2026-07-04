using UnityEngine;
using UnityEngine.InputSystem;

namespace TableTalkers.UI
{
    /// <summary>
    /// One-line control hints at the bottom of the screen so playtesters can discover the
    /// conversation features without a manual. H toggles. Attach to the App object.
    /// </summary>
    public sealed class HintBar : MonoBehaviour
    {
        [SerializeField] private bool _visible = true;

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.hKey.wasPressedThisFrame
                && GUIUtility.keyboardControl == 0)
            {
                _visible = !_visible;
            }
        }

        private void OnGUI()
        {
            if (!_visible)
            {
                return;
            }

            string text = Loc.Get("hint.bar");
            var content = new GUIContent(text);
            Vector2 size = GUI.skin.box.CalcSize(content);
            GUI.color = new Color(1f, 1f, 1f, 0.75f);
            GUI.Box(new Rect(Screen.width / 2f - size.x / 2f, Screen.height - size.y - 8f, size.x, size.y), content);
            GUI.color = Color.white;
        }
    }
}
