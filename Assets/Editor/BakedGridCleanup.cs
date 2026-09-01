#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class BakedGridCleanup
{
    private const string CombatScenePath = "Assets/Scenes/Combat.unity";

    static BakedGridCleanup()
    {
        EditorSceneManager.sceneOpened += OnSceneOpened;
        EditorApplication.delayCall += CleanOpenCombatScene;
    }

    [MenuItem("Tools/Hex Roguelike/Remove Baked Default Grid")]
    public static void CleanOpenCombatScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (scene.path == CombatScenePath) Clean(scene);
        }
    }

    private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
    {
        if (scene.path == CombatScenePath) Clean(scene);
    }

    private static void Clean(Scene scene)
    {
        HexGridManager grid = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            grid = root.GetComponentInChildren<HexGridManager>(true);
            if (grid != null) break;
        }
        if (grid == null || grid.tileParent == null || grid.tileParent.childCount == 0) return;

        int removed = grid.tileParent.childCount;
        for (int i = grid.tileParent.childCount - 1; i >= 0; i--)
            Object.DestroyImmediate(grid.tileParent.GetChild(i).gameObject);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"Removed {removed} obsolete baked tiles from Combat scene. Runtime rooms are template-only.");
    }
}
#endif
