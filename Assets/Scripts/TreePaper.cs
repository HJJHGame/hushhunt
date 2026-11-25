using UnityEngine;

// 나무 한 그루에 A4 용지 한 장을 붙인다 (슬렌더 페이지). 매 판 무작위 나무/무작위 방향.
// 발광은 없고, 어둠 속에서 손전등에 비쳐야 선명하게 보인다.
public class TreePaper : MonoBehaviour
{
    void Start()
    {
        var trees = new System.Collections.Generic.List<Transform>();
        foreach (var t in FindObjectsByType<Transform>(FindObjectsSortMode.None))
            if (t.parent == null && t.name == WorldConfig.Names.PineTree) trees.Add(t);
        if (trees.Count == 0) return;

        var rand = new System.Random();
        var tree = trees[rand.Next(trees.Count)];

        // 줄기 굵기에 맞춰 껍질에서 살짝 띄운다.
        float radius = 0.25f;
        var trunk = tree.Find(WorldConfig.Names.PineTrunk);
        if (trunk != null) radius = trunk.localScale.x * 0.5f + 0.03f;

        float yaw = (float)rand.NextDouble() * 360f;
        var dir = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;

        // 얇은 상자: 어느 쪽에서 봐도 보인다 (Quad는 한쪽 면만 보임).
        var paper = GameObject.CreatePrimitive(PrimitiveType.Cube);
        paper.name = "A4 Page";
        Destroy(paper.GetComponent<Collider>()); // 걸리적거리지 않게
        paper.transform.SetParent(tree, false);
        paper.transform.localPosition = new Vector3(dir.x * radius, 1.5f, dir.z * radius);
        paper.transform.localRotation = Quaternion.Euler(
            (float)(rand.NextDouble() * 8.0 - 4.0), yaw, (float)(rand.NextDouble() * 6.0 - 3.0));
        paper.transform.localScale = new Vector3(0.21f, 0.297f, 0.003f); // A4 (m)

        var mat = RuntimeMaterials.CreateLit("A4 Page", new Color(0.9f, 0.88f, 0.82f), 0.02f);
        paper.GetComponent<Renderer>().sharedMaterial = mat;
        paper.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }
}
