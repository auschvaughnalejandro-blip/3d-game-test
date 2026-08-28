using System.IO;
using UnityEngine;

// Everything worth remembering about a run, and how it gets onto disk.
//
// Deliberately a short, flat list of plain numbers rather than a snapshot of the whole
// world. Saving where every enemy was standing would be a great deal of work, would break
// every time the valley changed, and would let somebody reload into the middle of a fight
// they were already losing. Saving the CHECKPOINT instead - which act, which round, what
// the player has earned - is both simpler and kinder.
[System.Serializable]
public class SavedGame
{
    // False in the object handed back when there is no save file at all, so the menu can
    // grey out Continue without having to catch an exception.
    public bool thereIsSomethingSaved = false;

    // Shown on the Continue button, so the player knows what they are going back to
    // rather than trusting a button that says nothing.
    public string whereYouWere = "";
    public string whenYouSavedIt = "";

    // ---- the story ----------------------------------------------------------

    public int act = StoryDirector.ActInTheDungeon;
    public bool hasRefusedOrrin = false;

    // Which round to drop back into. Rounds are re-entered from the beginning: being
    // returned to the middle of one with four enemies already on top of you is not a
    // kindness.
    public int roundToResumeAt = 1;

    // ---- what the player has earned -----------------------------------------

    public int essence = 0;
    public float maximumHealth = 100f;
    public float attackDamage = 20f;
    public float maximumStamina = 100f;

    public bool theEdgeHasBeenWon = false;
    public bool theWardenIsDead = false;

    // ------------------------------------------------------------------------
    // Disk
    // ------------------------------------------------------------------------

    // A plain readable file rather than PlayerPrefs, which on Windows disappears into the
    // registry where nobody can look at it. This can be opened in Notepad, which matters
    // the first time a save goes wrong.
    private static string FilePath()
    {
        return Path.Combine(Application.persistentDataPath, "onevalley-save.json");
    }

    public static bool ASaveExists()
    {
        return File.Exists(FilePath());
    }

    // Always returns something. A missing or unreadable file comes back as a SavedGame
    // with thereIsSomethingSaved left false, so no caller has to handle a null.
    public static SavedGame Load()
    {
        SavedGame nothing = new SavedGame();

        if (ASaveExists() == false)
        {
            return nothing;
        }

        string text = "";
        try
        {
            text = File.ReadAllText(FilePath());
        }
        catch (System.Exception whatWentWrong)
        {
            Debug.LogWarning("Could not read the save file: " + whatWentWrong.Message);
            return nothing;
        }

        SavedGame loaded = JsonUtility.FromJson<SavedGame>(text);
        if (loaded == null)
        {
            Debug.LogWarning("The save file exists but could not be understood. Ignoring it.");
            return nothing;
        }

        loaded.thereIsSomethingSaved = true;
        return loaded;
    }

    public void WriteToDisk()
    {
        thereIsSomethingSaved = true;
        whenYouSavedIt = System.DateTime.Now.ToString("d MMM yyyy, HH:mm");

        try
        {
            File.WriteAllText(FilePath(), JsonUtility.ToJson(this, true));
        }
        catch (System.Exception whatWentWrong)
        {
            // A save that fails must never take the game down with it. The player would
            // rather lose the checkpoint than lose the run they are in the middle of.
            Debug.LogWarning("Could not write the save file: " + whatWentWrong.Message);
        }
    }

    public static void Delete()
    {
        if (ASaveExists() == false)
        {
            return;
        }

        try
        {
            File.Delete(FilePath());
        }
        catch (System.Exception whatWentWrong)
        {
            Debug.LogWarning("Could not delete the save file: " + whatWentWrong.Message);
        }
    }

    // Where the save file actually lives, so it can be found when something needs
    // looking at by hand.
    public static string WhereItLives()
    {
        return FilePath();
    }
}
