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
        var spawnChoices = new[]
        {
            new Vector2(0f, 0f),
            new Vector2(corner, corner), new Vector2(corner, -corner),
            new Vector2(-corner, corner), new Vector2(-corner, -corner),
        };
        var spawn = spawnChoices[rand.Next(spawnChoices.Length)];
        bool NearSpawn(float x, float z, float r) => new Vector2(x - spawn.x, z - spawn.y).magnitude < r;

        // 플레이어를 스폰으로 옮긴다 (CharacterController를 잠시 꺼야 충돌 상태가 꼬이지 않는다).
        var fpc = FindFirstObjectByType<FirstPersonController>();
        if (fpc != null)
        {
            var cc = fpc.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            fpc.transform.position = new Vector3(spawn.x, 1.1f, spawn.y);
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

        // 나무: 스폰/집을 피해 다시 심는다 (크기/기울기는 유지, 위치만 바꾼다).
        int movedTrees = 0;
        var treePos = new List<Vector2>(); // 소품이 나무를 피하도록 위치 기록
        foreach (var t in FindObjectsByType<Transform>(FindObjectsSortMode.None))
        {
            if (t.parent != null || t.name != WorldConfig.Names.PineTree) continue;
            for (int tries = 0; tries < 40; tries++)
            {
                float x = (float)(rand.NextDouble() * 2.0) * spread - spread;
                float z = (float)(rand.NextDouble() * 2.0) * spread - spread;
                if (NearSpawn(x, z, 6f)) continue;
                bool inHouse = false;
                foreach (var r in placed)
                    if (Mathf.Abs(x - r.x) < r.z + 1.5f && Mathf.Abs(z - r.y) < r.w + 1.5f) { inHouse = true; break; }
                if (inHouse) continue;
                t.position = new Vector3(x, t.position.y, z);
                treePos.Add(new Vector2(x, z));
                movedTrees++;
                break;
            }
        }

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
