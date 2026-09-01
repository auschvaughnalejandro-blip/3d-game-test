using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;

// Menu entry that arms and launches the restart regression test.
//
// The same shape as RunSelfTest, with two deliberate differences.
//
// It does NOT rebuild the valley. RunSelfTest does, because it is checking the whole
// game including the thing that builds it; this is checking what one run inherits from
// the one before it, and rebuilding would hide exactly that by handing the test a world
// nobody has played in yet.
//
// It does NOT save the scene either. The harness object only has to survive the step into
// play mode, and an object added in edit mode does that on its own - Unity carries the
// scene as it stands in memory. Leaving it unsaved means the scene file is never written,
// and the harness takes itself out of the scene when play mode ends, so there is no
// "Remove Harness" menu item to remember afterwards.
public static class RunRestartRegressionTest
{
    private static readonly string ReportFolder = Application.dataPath + "/../Logs/";

    [MenuItem("One Valley/Run Restart Regression Test")]
    public static void Run()
    {
        if (EditorApplication.isPlaying == true)
        {
            Debug.LogWarning("Already in play mode. Stop first, then run the restart test.");
            return;
        }

        // A stale done-marker from the last run would make this run look finished before
        // it had started, which is the whole reason a waiting reader polls for it.
        if (File.Exists(ReportFolder + "restart_regression_done.txt") == true)
        {
            File.Delete(ReportFolder + "restart_regression_done.txt");
        }

        PlayerPrefs.SetInt(RestartRegressionTest.ArmedFlag, 1);
        PlayerPrefs.Save();

        GameObject existing = GameObject.Find("RestartRegressionTest");
        if (existing != null)
        {
            Object.DestroyImmediate(existing);
        }

        GameObject holder = new GameObject("RestartRegressionTest");
        holder.AddComponent<RestartRegressionTest>();

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        Debug.Log("Restart regression test armed. Entering play mode.");
        EditorApplication.EnterPlaymode();
    }
}
