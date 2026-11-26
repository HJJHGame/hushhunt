using System.Collections;
using UnityEngine;

// 테스트용: WorldConfig.SimplifiedTestMode가 true면 나무와 땅만 남긴다.
// 바위/집/통나무/그루터기/울타리의 렌더러, 충돌체, 조명을 끈다 (오브젝트와 코드는 그대로라 false로 바꾸면 복구).
[DefaultExecutionOrder(1000)] // 다른 시스템의 Start(바위/소품 모델 교체)가 끝난 뒤
public class SimplifiedTestMode : MonoBehaviour
{
    IEnumerator Start()
    {
        if (!WorldConfig.SimplifiedTestMode) yield break;
        yield return null; // 다른 컴포넌트의 Start가 만든 오브젝트까지 포함
        int hidden = 0;
        foreach (var t in FindObjectsByType<Transform>(FindObjectsSortMode.None))
        {
            string n = t.name;
            bool target = t.GetComponent<HouseFootprint>() != null
                || n == WorldConfig.Names.Rock || n == WorldConfig.Names.House
                || n == WorldConfig.Names.FallenLog || n == WorldConfig.Names.Stump
                || n == WorldConfig.Names.OldFence;
            if (!target) continue;
            foreach (var r in t.GetComponentsInChildren<Renderer>()) r.enabled = false;
            foreach (var c in t.GetComponentsInChildren<Collider>()) c.enabled = false;
            foreach (var l in t.GetComponentsInChildren<Light>()) l.enabled = false;
            hidden++;
        }
        Debug.Log("[HushHunt] SimplifiedTestMode 켜짐: 바위/집/소품 " + hidden + "개 숨김 (WorldConfig.SimplifiedTestMode=false로 복구)");
    }
}
