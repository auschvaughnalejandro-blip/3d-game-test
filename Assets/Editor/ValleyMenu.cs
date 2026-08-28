using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

// Puts a "One Valley" menu in Unity's menu bar so the scene can be rebuilt with a click.
//
// The valley is generated entirely from ValleyBuilder, which means any change to layout,
// enemy numbers or materials only shows up after that script is run again. Without this
// menu the only way to run it is from outside the editor, which is fragile.
//
// This file lives in an Editor folder, so it is compiled only for the editor and is left
// out of any real build of the game.
public static class ValleyMenu
{
    [MenuItem("One Valley/Rebuild Valley %#r")]
    public static void RebuildValley()
    {
        if (EditorApplication.isPlaying == true)
        {
            Debug.LogWarning("Stop play mode before rebuilding - the scene cannot be saved while running.");
            return;
        }

        ValleyBuilder.BuildTheValley();

        var scene = EditorSceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        EnemyBrain[] enemies = Object.FindObjectsByType<EnemyBrain>(FindObjectsSortMode.None);
        GameObject ground = GameObject.Find("Ground");

        string groundKind = "MISSING";
        if (ground != null)
        {
            MeshFilter groundMesh = ground.GetComponentInChildren<MeshFilter>();
            if (groundMesh != null && groundMesh.sharedMesh != null)
            {
                groundKind = groundMesh.sharedMesh.name + " ("
                    + (groundMesh.sharedMesh.triangles.Length / 3) + " triangles)";
            }
        }

        Debug.Log("One Valley rebuilt and saved. Enemies: " + enemies.Length + ". Ground: " + groundKind);
    }

    [MenuItem("One Valley/Reimport Blender Models")]
    public static void ReimportBlenderModels()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
        Debug.Log("Asset refresh requested - any new FBX files from Blender will import now.");
    }
}
