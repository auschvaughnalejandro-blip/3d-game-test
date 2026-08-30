using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;

// Menu entry that arms and launches the automated play-through.
//
// The test itself is an ordinary MonoBehaviour, because it has to run frame by frame in
// play mode and editor code cannot do that. All this does is put one into the scene,
// clear out the previous report, and press play.
public static class RunSelfTest
{
    // Must match SelfTest.ReportFolder. Not a const, because Application.dataPath is only
    // known at runtime - the old absolute path was a const on a machine that no longer
    // runs this project.
    private static readonly string ReportFolder = Application.dataPath + "/../Logs/";

    [MenuItem("One Valley/Run Self Test")]
    public static void Run()
    {
        if (EditorApplication.isPlaying == true)
        {
            Debug.LogWarning("Already in play mode. Stop first, then run the self test.");
            return;
        }

        // The done marker is what a waiting reader watches for, so a stale one from the
        // last run would make this run look finished before it had started.
        if (File.Exists(ReportFolder + "selftest_done.txt") == true)
        {
            File.Delete(ReportFolder + "selftest_done.txt");
        }

        PlayerPrefs.SetInt(SelfTest.ArmedFlag, 1);
        PlayerPrefs.Save();

        ValleyBuilder.BuildTheValley();

        GameObject existing = GameObject.Find("SelfTest");
        if (existing != null)
        {
            Object.DestroyImmediate(existing);
        }

        GameObject holder = new GameObject("SelfTest");
        holder.AddComponent<SelfTest>();

        var scene = EditorSceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log("Self test armed. Entering play mode.");
        EditorApplication.EnterPlaymode();
    }

    // Left behind in the scene after a run, the harness would fire again on the next
    // ordinary play. This clears it out once the run is over.
    [MenuItem("One Valley/Remove Self Test Harness")]
    public static void Remove()
    {
        GameObject existing = GameObject.Find("SelfTest");
        if (existing == null)
        {
            Debug.Log("No self test harness in the scene.");
            return;
        }

        Object.DestroyImmediate(existing);
        var scene = EditorSceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Self test harness removed.");
    }
}
