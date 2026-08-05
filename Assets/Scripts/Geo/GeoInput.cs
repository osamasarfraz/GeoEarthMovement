using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Key polling that works whether the project is on the legacy Input Manager,
/// the Input System package, or both. This project is set to the Input System,
/// where UnityEngine.Input throws.
/// </summary>
public static class GeoInput
{
    public static bool KeyDown(KeyCode code)
    {
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb == null) return false;

        Key k;
        // KeyCode and Key share names for letters, digits and function keys
        if (!System.Enum.TryParse<Key>(code.ToString(), true, out k)) return false;

        var ctrl = kb[k];
        return ctrl != null && ctrl.wasPressedThisFrame;
#else
        return Input.GetKeyDown(code);
#endif
    }
}
