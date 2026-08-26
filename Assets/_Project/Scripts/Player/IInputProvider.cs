using UnityEngine;

namespace Survival.Player
{
    /// <summary>
    /// Everything the player can ask the game to do, independent of how it was pressed.
    /// <para>
    /// The prototype reads the legacy Input Manager, which needs no package and no editor
    /// restart. Moving to the Input System package, a gamepad, or rebindable controls means
    /// writing one more implementation of this interface and nothing else.
    /// </para>
    /// </summary>
    public interface IInputProvider
    {
        /// <summary>Desired movement, interpreted in camera space. x = right, y = forward.
        /// Magnitude never exceeds 1, but may be less: a partly pressed stick should walk.</summary>
        Vector2 Move { get; }

        /// <summary>Mouse delta for this frame, already scaled by sensitivity.</summary>
        Vector2 Look { get; }

        /// <summary>Metres to add to camera distance this frame. Positive pulls the camera back.</summary>
        float Zoom { get; }

        bool SprintHeld { get; }

        bool JumpPressed { get; }

        /// <summary>Held state, not the press. Releasing early cuts the jump short, which is the
        /// difference between a jump you control and a jump you commit to.</summary>
        bool JumpHeld { get; }

        bool InteractPressed { get; }
        bool AttackPressed { get; }
    }
}
