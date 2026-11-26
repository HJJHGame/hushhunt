#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Hush Hunt: repair static flags for objects that RandomizeLayout moves every round.
public static class HushHuntStaticFlagsFix
{
    [MenuItem("Hush Hunt/Performance/Repair Static Flags In Open Scene")]
    static void Repair()
    {
        int changed = 0;
        var transforms = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);

        foreach (var t in transforms)
        {
            if (t.parent != null) continue;

            string n = t.name;
            bool groundOrBoundary = n == WorldConfig.Names.Ground || n == WorldConfig.Names.EdgeWall;
            bool movableEnvironment =
                n == WorldConfig.Names.Rock ||
                n == WorldConfig.Names.OldFence ||
                n == WorldConfig.Names.FallenLog ||
                n == WorldConfig.Names.Stump;

            if (!groundOrBoundary && !movableEnvironment) continue;

            var flags = groundOrBoundary
                ? StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic
                : StaticEditorFlags.OccludeeStatic;

            foreach (var child in t.GetComponentsInChildren<Transform>(true))
            {
                var go = child.gameObject;
                if (GameObjectUtility.GetStaticEditorFlags(go) == flags) continue;
                GameObjectUtility.SetStaticEditorFlags(go, flags);
                EditorUtility.SetDirty(go);
                changed++;
            }
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("[HushHunt] Static flag repair complete. Updated objects: " + changed);
    }
}
#endif
