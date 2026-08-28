using UnityEngine;
using UnityEditor;

// Makes Unity notice changed files without needing its window clicked on.
//
// By default the editor only scans for changed assets when it regains focus. That is
// fine when a person is doing the editing, because they click back into Unity anyway.
// It is not fine when scripts are being written from outside: the editor sits there with
// stale code until somebody remembers to click it, and every change appears to do
// nothing.
//
// This ticks a few times a second and asks the editor to look, but only when it is
// genuinely safe and worth doing.
[InitializeOnLoad]
public static class KeepAssetsFresh
{
    // Often enough that a change is picked up before it is missed, rarely enough that
    // the editor is not constantly rescanning the project.
    private const double SecondsBetweenChecks = 1.5;

    private static double nextCheckTime = 0.0;

    // Switchable from the menu, and remembered between sessions, so it can be turned off
    // if it ever gets in the way.
    private const string EnabledSetting = "OneValley.KeepAssetsFresh";

    static KeepAssetsFresh()
    {
        EditorApplication.update += Tick;
    }

    private static bool IsEnabled()
    {
        return EditorPrefs.GetBool(EnabledSetting, true);
    }

    private static void Tick()
    {
        if (IsEnabled() == false)
        {
            return;
        }

        // Never during play. Refreshing mid-game triggers a domain reload, which would
        // throw the player out of whatever they were doing.
        if (EditorApplication.isPlaying == true || EditorApplication.isPlayingOrWillChangePlaymode == true)
        {
            return;
        }

        // Never while the editor is already busy. Asking it to refresh while it is
        // compiling or importing does nothing useful and can stall it.
        if (EditorApplication.isCompiling == true || EditorApplication.isUpdating == true)
        {
            return;
        }

        if (EditorApplication.timeSinceStartup < nextCheckTime)
        {
            return;
        }
        nextCheckTime = EditorApplication.timeSinceStartup + SecondsBetweenChecks;

        AssetDatabase.Refresh();
    }

    [MenuItem("One Valley/Auto Refresh Assets", false, 100)]
    private static void Toggle()
    {
        bool nowOn = IsEnabled() == false;
        EditorPrefs.SetBool(EnabledSetting, nowOn);
        Debug.Log("Auto refresh is now " + (nowOn ? "ON" : "OFF"));
    }

    [MenuItem("One Valley/Auto Refresh Assets", true)]
    private static bool ToggleTick()
    {
        Menu.SetChecked("One Valley/Auto Refresh Assets", IsEnabled());
        return true;
    }
}
