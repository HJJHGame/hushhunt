using System.Collections.Generic;
using UnityEngine;

// 나무 위치 만들기. "서로 너무 가깝지도, 너무 멀지도 않게" 정확히 count개를 뽑는다.
// 1단계(바둑판 한 칸에 한 그루): 맵을 CellSize 칸으로 나누고 칸마다 중앙 근처에 한 그루. 비는 큰 공터가 안 생긴다.
//   칸 안에서 흔드는 폭을 제한해서 이웃과의 거리는 최소 (1-Jitter)*CellSize = 3.2m 이상이다.
// 2단계(나머지): 후보 여러 곳 중 기존 나무에서 가장 먼 곳을 고르되, MinDist보다 가까운 곳은 버린다.
// 막힌 곳(blocked: 봉쇄 구역 5곳, 집, 스폰 주변)에는 심지 않는다.
public static class TreeLayout
{
    public const int Total = 1800;        // 맵의 나무 총 수
    public const float CellSize = 8f;     // 1단계 칸 크기(m). 클수록 듬성듬성, 작을수록 촘촘
    const float Jitter = 0.6f;            // 칸 중앙에서 흔드는 폭 비율
    public const float MinDist = 3.2f;    // 나무끼리 최소 거리(m)
    const int Candidates = 30;            // 2단계에서 한 그루마다 뽑는 후보 수
    const float NearCap = 12f;

    // RandomizeLayout이 정한, 씬의 기존 나무 자리(슬롯) 밖의 나머지 위치. SpruceForest가 가져가 쓴다.
    public static List<Vector2> Extra;

    public delegate bool Blocked(float x, float z);

    // existing: 이미 놓인 나무 위치(있으면 피한다). 못 채우면 가능한 만큼만 돌려준다.
    public static List<Vector2> Generate(int count, Blocked blocked, List<Vector2> existing, System.Random rand)
    {
        var result = new List<Vector2>(count);
        if (count <= 0) return result;
        var grid = new PlacementGrid();
        if (existing != null) foreach (var e in existing) grid.Add(e);

        float lim = WorldConfig.Half - 3f;
        int n = Mathf.FloorToInt(2f * lim / CellSize);
        float origin = -lim + (2f * lim - n * CellSize) * 0.5f;

        // 1단계
        var stage1 = new List<Vector2>();
        for (int ix = 0; ix < n; ix++)
            for (int iz = 0; iz < n; iz++)
            {
                float cx = origin + (ix + 0.5f) * CellSize, cz = origin + (iz + 0.5f) * CellSize;
                for (int t = 0; t < 6; t++)
                {
                    float x = cx + (float)(rand.NextDouble() * 2.0 - 1.0) * Jitter * CellSize * 0.5f;
                    float z = cz + (float)(rand.NextDouble() * 2.0 - 1.0) * Jitter * CellSize * 0.5f;
                    if (blocked(x, z)) continue;
                    if (grid.Nearest(new Vector2(x, z), MinDist) < MinDist) continue; // 기존 나무와 너무 가까움
                    stage1.Add(new Vector2(x, z));
                    break;
                }
            }
        // 칸 수가 count보다 많으면 무작위로 count개만 쓴다.
        for (int i = stage1.Count - 1; i > 0; i--)
        {
            int j = rand.Next(i + 1);
            var tmp = stage1[i]; stage1[i] = stage1[j]; stage1[j] = tmp;
        }
        foreach (var p in stage1)
        {
            if (result.Count >= count) break;
            result.Add(p);
            grid.Add(p);
        }

        // 2단계
        int fails = 0;
        while (result.Count < count && fails < 2000)
        {
            bool found = false;
            Vector2 best = Vector2.zero;
            float bestD = -1f;
            for (int c = 0; c < Candidates; c++)
            {
                float x = (float)(rand.NextDouble() * 2.0 - 1.0) * lim;
                float z = (float)(rand.NextDouble() * 2.0 - 1.0) * lim;
                if (blocked(x, z)) continue;
                float d = grid.Nearest(new Vector2(x, z), NearCap);
                if (d < MinDist) continue;
                if (d > bestD) { bestD = d; best = new Vector2(x, z); found = true; }
            }
            if (!found) { fails++; continue; }
            result.Add(best);
            grid.Add(best);
        }

        // 위치 순서를 섞는다 (슬롯/추가, 종류 배정이 한쪽으로 쏠리지 않게).
        for (int i = result.Count - 1; i > 0; i--)
        {
            int j = rand.Next(i + 1);
            var tmp = result[i]; result[i] = result[j]; result[j] = tmp;
        }
        return result;
    }
}
