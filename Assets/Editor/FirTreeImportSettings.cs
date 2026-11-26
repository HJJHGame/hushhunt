using UnityEditor;
using UnityEngine;

// Assets/Resources/Trees 의 모델은 Read/Write 를 켜 둔다.
// 전나무 LOD(FirLod)가 코드로 메시를 단순화하려면 메시를 읽을 수 있어야 한다.
// 새로 임포트될 때와, 에디터가 컴파일된 직후 이미 있는 모델에 대해 한 번 적용한다.
public class FirTreeImportSettings : AssetPostprocessor
{
    void OnPreprocessModel()
    {
        if (!assetPath.Contains("/Resources/Trees/")) return;
        var mi = assetImporter as ModelImporter;
        if (mi != null && !mi.isReadable) mi.isReadable = true;
    }
}

[InitializeOnLoad]
static class FirTreeImportFix
{
    static FirTreeImportFix()
    {
        EditorApplication.delayCall += Fix;
    }

    static void Fix()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources/Trees")) return;
        foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { "Assets/Resources/Trees" }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var mi = AssetImporter.GetAtPath(path) as ModelImporter;
            if (mi == null || mi.isReadable) continue;
            mi.isReadable = true;
            mi.SaveAndReimport();
            Debug.Log("[HushHunt] " + path + " Read/Write 를 켰다 (전나무 LOD 메시 단순화용).");
        }
    }
}
