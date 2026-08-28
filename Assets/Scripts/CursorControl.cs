using UnityEngine;

// The one and only place that decides whether the mouse is captured by the game.
//
// It used to be shared out between four scripts: the camera grabbed the mouse the moment
// it started, the dialogue box let go of it and took it back, the menu let go of it, and
// Escape released it. Every one of those was correct on its own, and they contradicted
// each other whenever two of them happened in the same frame. Two real faults came out of
// that, and both of them looked like the menu was broken:
//
//   - The title screen opened with no pointer on it at all, because the camera's Start
//     happened to run after the menu's Start and took the mouse straight back. Pressing
//     Escape was the only way to get a pointer, which is not something a player should
//     ever have to work out for themselves.
//   - Clicking a menu button could hide the pointer and leave every remaining button
//     dead. The same click was also read as "advance the conversation", the conversation
//     ended, and the dialogue box dutifully handed the mouse back to a game that was not
//     running. With the mouse locked to the middle of the screen, no button can be
//     clicked ever again.
//
// So the cursor code has been taken out of all of those scripts and the decision is made
// here instead, once a frame, purely from the state of the game. LateUpdate rather than
// Update, so that it runs after everything else has had its say.
public class CursorControl : MonoBehaviour
{
    void LateUpdate()
    {
        if (TheMouseShouldBeFree() == true)
        {
            // Written every frame rather than only on the frame the answer changes.
            // Unity does not mind being told the same thing repeatedly, and it means
            // anything that grabs the mouse behind this script's back can only keep hold
            // of it for a single frame.
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            return;
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private bool TheMouseShouldBeFree()
    {
        // A menu is up, and it has buttons on it that have to stay clickable. This covers
        // the title screen and the pause screen, and it is why Escape no longer has to
        // release the mouse by hand any more: Escape opens the pause screen, and the
        // pause screen frees the mouse.
        if (MainMenu.IsShowing() == true)
        {
            return true;
        }

        // Somebody is talking. The pointer is shown so the player can see what they are
        // being asked, and so that a hand resting on the mouse does not spin the camera
        // while they read. Hints murmured along the bottom of the screen deliberately do
        // NOT count - those arrive in the middle of a fight and must not disturb the
        // controls.
        if (DialogueBox.ConversationIsOpen() == true)
        {
            return true;
        }

        return false;
    }
}
