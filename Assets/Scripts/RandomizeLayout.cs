using System.Collections.Generic;
using UnityEngine;

// 매 판마다 집과 나무를 새 위치에 다시 배치하고, 스폰(네 모서리 중 하나 또는 정중앙)도 다시 고른다.
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

        // 소품(바위/통나무/그루터기/울타리)은 씬 그대로 두되, 집/나무가 그 위에 놓이지 않게 피한다.
        var props = new List<Vector3>(); // x, z, 대략 반경
        foreach (var t in FindObjectsByType<Transform>(FindObjectsSortMode.None))
        {
            if (t.parent != null) continue;
            string n = t.name;
            float r = n == WorldConfig.Names.Rock ? 2.5f
                : n == WorldConfig.Names.FallenLog ? 3.5f
                : n == WorldConfig.Names.Stump ? 1.5f
                : n == WorldConfig.Names.OldFence ? 4f
                : 0f;
            if (r > 0f) props.Add(new Vector3(t.position.x, t.position.z, r));
        }
        bool NearProp(float x, float z, float r)
        {
            foreach (var p in props)
                if (Vector2.Distance(new Vector2(x, z), new Vector2(p.x, p.y)) < r + p.z) return true;
            return false;
        }

        // 집부터: 서로/스폰/소품과 겹치지 않게 다시 놓는다.
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
                if (NearProp(hx, hz, Mathf.Sqrt(hw * hw + hd * hd) + 1f)) continue;
                bool overlaps = false;
                foreach (var r in placed)
                    if (Mathf.Abs(hx - r.x) < hw + r.z + 3f && Mathf.Abs(hz - r.y) < hd + r.w + 3f) { overlaps = true; break; }
                if (overlaps) continue;
                fp.transform.position = new Vector3(hx, 0f, hz);
                placed.Add(new Vector4(hx, hz, hw, hd));
                break;
            }
        }

        // 나무: 스폰/집/소품을 피해 다시 심는다 (크기/기울기는 유지, 위치만 바꾼다).
        int movedTrees = 0;
        foreach (var t in FindObjectsByType<Transform>(FindObjectsSortMode.None))
        {
            if (t.parent != null || t.name != WorldConfig.Names.PineTree) continue;
            for (int tries = 0; tries < 40; tries++)
            {
                float x = (float)(rand.NextDouble() * 2.0) * spread - spread;
                float z = (float)(rand.NextDouble() * 2.0) * spread - spread;
                if (NearSpawn(x, z, 6f)) continue;
                if (NearProp(x, z, 1.5f)) continue;
                bool inHouse = false;
                foreach (var r in placed)
                    if (Mathf.Abs(x - r.x) < r.z + 1.5f && Mathf.Abs(z - r.y) < r.w + 1.5f) { inHouse = true; break; }
                if (inHouse) continue;
                t.position = new Vector3(x, t.position.y, z);
                movedTrees++;
                break;
            }
        }
        Debug.Log("[HushHunt] 랜덤 배치 완료: 스폰 (" + spawn.x.ToString("0") + ", " + spawn.y.ToString("0")
            + "), 집 " + placed.Count + "/" + houses.Length + "채 재배치, 나무 " + movedTrees + "그루 이동");
    }
}
