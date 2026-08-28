// The one question every script asks before it reads the keyboard: is the player
// actually driving right now?
//
// There are two reasons they might not be - somebody is talking to them, or a menu is up -
// and before this existed each script checked only the first of them. Adding the menu
// would have meant editing every one of them again and remembering all of them, which is
// exactly the kind of thing that gets half done and leaves the player able to swing a
// sword through the title screen.
public static class PlayerControl
{
    // True when the game should ignore movement, attacks, weapon swaps, drinking and the
    // camera. Note that this deliberately does NOT cover the dialogue box or the menus
    // themselves: they still have to read the keys that dismiss them.
    public static bool IsBlocked()
    {
        if (MainMenu.IsShowing() == true)
        {
            return true;
        }
        return DialogueBox.ConversationIsOpen();
    }
}
