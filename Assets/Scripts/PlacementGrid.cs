using System.Collections.Generic;
using UnityEngine;

// 나무 배치용 공간 격자: 가장 가까운 기존 나무까지의 거리를 빠르게 묻는다.
// "후보 여러 곳 중 기존 나무에서 가장 먼 곳을 고르기"에 쓴다 (여백과 뭉침을 줄인다).
public class PlacementGrid
{
    const float Cell = 8f;
    readonly Dictionary<long, List<Vector2>> cells = new Dictionary<long, List<Vector2>>();

    static long Key(int cx, int cz) { return ((long)cx << 32) ^ (uint)cz; }

    public void Add(Vector2 p)
    {
        long k = Key(Mathf.FloorToInt(p.x / Cell), Mathf.FloorToInt(p.y / Cell));
        List<Vector2> l;
        if (!cells.TryGetValue(k, out l)) { l = new List<Vector2>(); cells[k] = l; }
        l.Add(p);
    }

    // 가장 가까운 점까지 거리. cap 안에 아무것도 없으면 cap을 돌려준다.
    public float Nearest(Vector2 p, float cap)
    {
        int cx = Mathf.FloorToInt(p.x / Cell), cz = Mathf.FloorToInt(p.y / Cell);
        int r = Mathf.CeilToInt(cap / Cell);
        float best = cap;
        for (int dx = -r; dx <= r; dx++)
            for (int dz = -r; dz <= r; dz++)
            {
                List<Vector2> l;
                if (!cells.TryGetValue(Key(cx + dx, cz + dz), out l)) continue;
                foreach (var q in l) best = Mathf.Min(best, Vector2.Distance(p, q));
            }
        return best;
    }
}
