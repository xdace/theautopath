using UnityEngine;
#if ENABLE_INPUT_SYSTEM && BETAKNIGHT_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Betaknight.Overworld.Input
{
    /// <summary>
    /// Kapselt Maus- bzw. Touch-Eingaben, damit das Projekt sowohl mit dem neuen Input System
    /// als auch mit dem alten Input Manager funktioniert (Project Settings → Player → Active Input Handling).
    /// </summary>
    public static class PointerInput
    {
        public static bool TryGetScreenPosition(out Vector2 position)
        {
#if ENABLE_INPUT_SYSTEM && BETAKNIGHT_INPUT_SYSTEM
            if (Pointer.current != null)
            {
                position = Pointer.current.position.ReadValue();
                return true;
            }
            position = default;
            return false;
#elif ENABLE_LEGACY_INPUT_MANAGER
            position = UnityEngine.Input.mousePosition;
            return true;
#else
            position = default;
            return false;
#endif
        }

        public static bool PressedThisFrame()
        {
#if ENABLE_INPUT_SYSTEM && BETAKNIGHT_INPUT_SYSTEM
            return Pointer.current != null && Pointer.current.press.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return UnityEngine.Input.GetMouseButtonDown(0);
#else
            return false;
#endif
        }
    }
}
