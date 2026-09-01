using UnityEngine;
using UnityEngine.InputSystem;

// Every keyboard and mouse reading in the whole game happens here and nowhere else.
//
// This project is set to Unity's newer Input System, where reading a key looks like
// Keyboard.current.wKey.isPressed rather than the older Input.GetKey(KeyCode.W). Rather
// than spread that noise through the gameplay scripts, all of it is trapped in this one
// file behind plainly named questions. Movement code gets to ask "is sprint held down"
// and never has to care how that is answered.
public static class GameInput
{
    // The new Input System reports mouse movement in raw pixels, which is far too large
    // to use directly. This brings it back to roughly the size the old input system
    // reported, so the camera sensitivity numbers stay sensible.
    private const float MousePixelsToUsefulUnits = 0.05f;

    // ------------------------------------------------------------------------
    // Movement
    // ------------------------------------------------------------------------

    // Minus one for left, plus one for right, zero for neither or both.
    public static float SidewaysAxis()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return 0f;
        }

        float amount = 0f;
        if (keyboard.aKey.isPressed == true)
        {
            amount = amount - 1f;
        }
        if (keyboard.dKey.isPressed == true)
        {
            amount = amount + 1f;
        }
        return amount;
    }

    // Minus one for backwards, plus one for forwards.
    public static float ForwardAxis()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return 0f;
        }

        float amount = 0f;
        if (keyboard.sKey.isPressed == true)
        {
            amount = amount - 1f;
        }
        if (keyboard.wKey.isPressed == true)
        {
            amount = amount + 1f;
        }
        return amount;
    }

    public static bool SprintIsHeld()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return false;
        }
        return keyboard.leftShiftKey.isPressed;
    }

    // Dodge lives on Left Ctrl rather than Space now, because Space became the jump.
    // Both are pressed constantly and in different situations, so sharing one key would
    // mean rolling when you meant to hop over a shockwave.
    public static bool DodgeWasPressed()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return false;
        }
        return keyboard.leftCtrlKey.wasPressedThisFrame;
    }

    public static bool JumpWasPressed()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return false;
        }
        return keyboard.spaceKey.wasPressedThisFrame;
    }

    // ------------------------------------------------------------------------
    // Kit
    // ------------------------------------------------------------------------

    public static bool HealWasPressed()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return false;
        }
        return keyboard.qKey.wasPressedThisFrame;
    }

    // Either F or a flick of the mouse wheel. The wheel is there because swapping mid
    // fight wants a motion the hand is already making, not a key it has to find.
    public static bool SwapWeaponWasPressed()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.fKey.wasPressedThisFrame == true)
        {
            return true;
        }

        Mouse mouse = Mouse.current;
        if (mouse != null)
        {
            float wheel = mouse.scroll.ReadValue().y;
            if (wheel > 0.01f || wheel < -0.01f)
            {
                return true;
            }
        }
        return false;
    }

    // ------------------------------------------------------------------------
    // Camera
    // ------------------------------------------------------------------------

    public static float MouseMovedSideways()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null)
        {
            return 0f;
        }
        return mouse.delta.ReadValue().x * MousePixelsToUsefulUnits;
    }

    public static float MouseMovedVertically()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null)
        {
            return 0f;
        }
        return mouse.delta.ReadValue().y * MousePixelsToUsefulUnits;
    }

    // Swapping between looking over the player's shoulder and looking out of their eyes.
    //
    // V, because every key a hand rests on during a fight was already taken and this one
    // is not something anybody presses in a hurry. It is deliberately NOT on the mouse
    // wheel: the wheel already swaps weapons, and a player scrolling for the bow should
    // not find the whole camera has changed underneath them.
    public static bool ViewToggleWasPressed()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return false;
        }
        return keyboard.vKey.wasPressedThisFrame;
    }

    // ------------------------------------------------------------------------
    // Fighting
    // ------------------------------------------------------------------------

    public static bool AttackWasPressed()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null)
        {
            return false;
        }
        return mouse.leftButton.wasPressedThisFrame;
    }

    public static bool AttackWasReleased()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null)
        {
            return false;
        }
        return mouse.leftButton.wasReleasedThisFrame;
    }

    // ------------------------------------------------------------------------
    // Everything else
    // ------------------------------------------------------------------------

    public static bool StyleChangeWasPressed()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return false;
        }
        return keyboard.tabKey.wasPressedThisFrame;
    }

    public static bool EscapeWasPressed()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return false;
        }
        return keyboard.escapeKey.wasPressedThisFrame;
    }

    // Which of the number keys 1, 2 or 3 was pressed this frame. Returns zero for none,
    // which is what the shrine uses to mean "no offering was chosen".
    public static int WhichUpgradeKeyWasPressed()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return 0;
        }

        if (keyboard.digit1Key.wasPressedThisFrame == true)
        {
            return 1;
        }
        if (keyboard.digit2Key.wasPressedThisFrame == true)
        {
            return 2;
        }
        if (keyboard.digit3Key.wasPressedThisFrame == true)
        {
            return 3;
        }
        return 0;
    }

    // ------------------------------------------------------------------------
    // Talking to people
    // ------------------------------------------------------------------------

    // E is the one key in this game that means "do the thing in front of me". It opens a
    // conversation with the wizard and it takes the gem out of the Warden's chest, and it
    // is deliberately not shared with anything used in a fight.
    public static bool InteractWasPressed()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return false;
        }
        return keyboard.eKey.wasPressedThisFrame;
    }

    // Answering a question that has been asked out loud. Y and N rather than 1 and 2
    // because the shrine already owns the number keys, and a player who has just learned
    // that 1 buys vigour should not find that 1 also means "yes, I will fight the thing
    // that killed me last time".
    public static bool YesWasPressed()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return false;
        }
        return keyboard.yKey.wasPressedThisFrame;
    }

    public static bool NoWasPressed()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return false;
        }
        return keyboard.nKey.wasPressedThisFrame;
    }

    // Moving a conversation on one line. Space, E or a left click all work, because a
    // player who has just been told to press E will press E, and a player who has been
    // clicking through a fight for ten minutes will click.
    public static bool ContinueWasPressed()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.spaceKey.wasPressedThisFrame == true)
            {
                return true;
            }
            if (keyboard.eKey.wasPressedThisFrame == true)
            {
                return true;
            }
        }

        Mouse mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame == true)
        {
            return true;
        }
        return false;
    }
}
