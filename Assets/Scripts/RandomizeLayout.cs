using System.Collections.Generic;
using UnityEngine;

// 매 판마다 모든 것(집/나무/바위/통나무/그루터기/울타리)을 새 위치에 다시 배치하고,
// 스폰(네 모서리 중 하나 또는 정중앙)도 다시 고른다.
// 씬 파일의 배치는 초기값일 뿐이다. 플레이어는 고른 스폰으로 옮긴다.
[DefaultExecutionOrder(-100)] // TreeModels/HouseGlow/Minimap보다 먼저 위치를 잡는다
public class RandomizeLayout : MonoBehaviour
{
    // Awake 에서 바로 섞는다: GameSystems 가 씬 로드 직후 이 컴포넌트를 추가하면 Awake 는 즉시 실행돼
    // Minimap(지도 이미지 생성)/TreePaper(용지 부착) 같은 Start 들보다 항상 먼저 끝난다.
    void Awake()
    {
        var rand = new System.Random(); // 시드 고정 없음: 매번 다른 배치

        float corner = WorldConfig.Half - 15f;
        // 숨는 팀 시작 위치: 봉쇄 구역 밖 아무 곳. 구역(5곳)은 나무/소품도 비운다.
        Zones.Init();
        var spawn = Vector2.zero;
        for (int tries = 0; tries < 200; tries++)
        {
            float lim0 = WorldConfig.Half - 20f;
            spawn = new Vector2((float)(rand.NextDouble() * 2 - 1) * lim0, (float)(rand.NextDouble() * 2 - 1) * lim0);
            if (!Zones.Contains(spawn.x, spawn.y, 10f)) break;
        }
        bool NearSpawn(float x, float z, float r) => new Vector2(x - spawn.x, z - spawn.y).magnitude < r || Zones.Contains(x, z, 1f);

        // 플레이어를 스폰으로 옮긴다 (CharacterController를 잠시 꺼야 충돌 상태가 꼬이지 않는다).
        var fpc = FindFirstObjectByType<FirstPersonController>();
        if (fpc != null)
        {
            var cc = fpc.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            var fh = fpc.GetComponent<Health>();
            // 잡는 팀은 처음부터 봉쇄 구역 안에서 시작한다. 숨는 팀은 구역 밖 랜덤 스폰.
            fpc.transform.position = (fh != null && fh.team == Team.Seeker) ? Zones.SeekerSpawnPoint(0) : new Vector3(spawn.x, 1.1f, spawn.y);
            if (cc != null) cc.enabled = true;
        }

        float spread = WorldConfig.Half - 6f;

        // 집부터: 서로/스폰과 겹치지 않게 다시 놓는다 (소품은 뒤에서 같이 옮긴다).
        var houses = FindObjectsByType<HouseFootprint>(FindObjectsSortMode.None);
        var placed = new List<Vector4>(); // x, z, 반폭, 반깊이
        foreach (var fp in houses)
        {
            float hw = fp.size.x * 0.5f, hd = fp.size.y * 0.5f;
            for (int tries = 0; tries < 60; tries++)
            {
                float range = WorldConfig.Half - 14f - Mathf.Max(hw, hd);
                float hx = (float)(rand.NextDouble() * 2.0) * range - range;
                float hz = (float)(rand.NextDouble() * 2.0) * range - range;
                if (NearSpawn(hx, hz, 9f + Mathf.Max(hw, hd))) continue;
                bool overlaps = false;
                foreach (var r in placed)
                    if (Mathf.Abs(hx - r.x) < hw + r.z + 3f && Mathf.Abs(hz - r.y) < hd + r.w + 3f) { overlaps = true; break; }
                if (overlaps) continue;
                fp.transform.position = new Vector3(hx, 0f, hz);
                placed.Add(new Vector4(hx, hz, hw, hd));
                break;
            }
        }

        // 나무: 총 TreeLayout.Total그루 위치를 한 번에 만든다 (봉쇄 구역 5곳/스폰/집을 피하고, 너무 가깝거나 멀지 않게).
        // 씬의 기존 나무 자리(슬롯)가 앞쪽 위치를 가져가 움직이고, 남은 위치는 SpruceForest가 추가 나무로 심는다.
        var pineSlots = new List<Transform>();
        foreach (var t in FindObjectsByType<Transform>(FindObjectsSortMode.None))
            if (t.parent == null && t.name == WorldConfig.Names.PineTree) pineSlots.Add(t);
        bool TreeBlocked(float x, float z)
        {
            if (NearSpawn(x, z, 6f)) return true;
            foreach (var r in placed)
                if (Mathf.Abs(x - r.x) < r.z + 1.5f && Mathf.Abs(z - r.y) < r.w + 1.5f) return true;
            return false;
        }
        var treePositions = TreeLayout.Generate(TreeLayout.Total, TreeBlocked, null, rand);
        int movedTrees = 0;
        var treePos = new List<Vector2>(treePositions); // 소품이 나무를 피하도록 위치 기록
        for (int i = 0; i < pineSlots.Count && i < treePositions.Count; i++)
        {
            var q = treePositions[i];
            pineSlots[i].position = new Vector3(q.x, pineSlots[i].position.y, q.y);
            movedTrees++;
        }
        TreeLayout.Extra = treePositions.Count > pineSlots.Count
            ? treePositions.GetRange(pineSlots.Count, treePositions.Count - pineSlots.Count)
            : new List<Vector2>();

        // 소품(바위/통나무/그루터기/울타리): 스폰/집/나무/다른 소품을 피해 매 판 새 위치로 옮긴다.
        int movedProps = 0;
        var placedProps = new List<Vector3>(); // x, z, 대략 반경
        foreach (var t in FindObjectsByType<Transform>(FindObjectsSortMode.None))
        {
            if (t.parent != null) continue;
            string n = t.name;
            float r = n == WorldConfig.Names.Rock ? 2f
                : n == WorldConfig.Names.FallenLog ? 3.5f
                : n == WorldConfig.Names.Stump ? 1.5f
                : n == WorldConfig.Names.OldFence ? 4f
                : 0f;
            if (r <= 0f) continue;
            for (int tries = 0; tries < 60; tries++)
            {
                float x = (float)(rand.NextDouble() * 2.0) * spread - spread;
                float z = (float)(rand.NextDouble() * 2.0) * spread - spread;
                if (NearSpawn(x, z, 6f + r)) continue;
                bool bad = false;
                foreach (var h in placed)
                    if (Mathf.Abs(x - h.x) < h.z + r && Mathf.Abs(z - h.y) < h.w + r) { bad = true; break; }
                if (bad) continue;
                foreach (var tp in treePos)
                    if (Vector2.Distance(new Vector2(x, z), tp) < r + 1.5f) { bad = true; break; }
                if (bad) continue;
                foreach (var pp in placedProps)
                    if (Vector2.Distance(new Vector2(x, z), new Vector2(pp.x, pp.y)) < r + pp.z + 0.5f) { bad = true; break; }
                if (bad) continue;
                t.position = new Vector3(x, t.position.y, z);
                placedProps.Add(new Vector3(x, z, r));
                movedProps++;
                break;
            }
        }
        Debug.Log("[HushHunt] 랜덤 배치 완료: 스폰 (" + spawn.x.ToString("0") + ", " + spawn.y.ToString("0")
            + "), 집 " + placed.Count + "/" + houses.Length + "채, 나무 " + movedTrees + "그루, 소품 " + movedProps + "개 재배치");
    }
}
