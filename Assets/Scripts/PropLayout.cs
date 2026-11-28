using System.Collections.Generic;
using UnityEngine;

// 나무가 아닌 소품(돌, 통나무 더미, 상자, 드럼통, 묘비, 토템, 물 그릇, 뿌리, 목책)의 고정 배치.
// 시드가 고정이라 매 판 같은 구조로 나온다. 다만 집과 플레이어 시작 위치는 매 판 바뀌므로, 그것과 겹치는 소품만 건너뛴다.
// 모든 소품은 모델 원본 크기(tevy 페이지의 치수) 그대로 쓰고, 겹침은 회전된 직사각형(OBB) 검사로 막는다.
// 나무는 이 소품들을 피해서 심는다 (BlocksCircle).
public static class PropLayout
{
    public class Spec
    {
        public string slug;
        public float w, d, h;          // 원본 치수(m): 폭(x), 깊이(z), 높이(y)
        public float near, mid, far;   // LOD0/LOD1/안 그림 거리(m)
        public bool collider;          // 걷는 데 걸리는 물건인가 (납작한 것은 콜라이더 없음)
        public float scale = 1f;       // 모델 크기 배율 (w/d/h는 이미 배율을 곱한 값)
        public bool meshCol;           // true면 상자 대신 모델 모양(단순한 먼 LOD 메시)으로 막는다: 통로가 있는 목책, 둥근 드럼통 등
    }

    public class Item
    {
        public Spec spec;
        public Obb box;
        public float yaw;
    }

    public struct Obb
    {
        public float cx, cz, hw, hd, c, s;
        public static Obb Make(float x, float z, float hw, float hd, float yawDeg)
        {
            float r = yawDeg * Mathf.Deg2Rad;
            return new Obb { cx = x, cz = z, hw = hw, hd = hd, c = Mathf.Cos(r), s = Mathf.Sin(r) };
        }
        // Quaternion.Euler(0, yaw, 0) 기준: 로컬 x축 = (cos, -sin), 로컬 z축 = (sin, cos) (월드 x,z)
        public Vector2 AxisX { get { return new Vector2(c, -s); } }
        public Vector2 AxisZ { get { return new Vector2(s, c); } }
    }

    // 모델 목록. 치수는 각 tevy 모델 페이지의 Dimensions 값.
    static Spec S(string slug, float w, float d, float h, float near, float mid, float far, bool collider)
    {
        return new Spec { slug = slug, w = w, d = d, h = h, near = near, mid = mid, far = far, collider = collider };
    }
    public static readonly Spec Pond1 = S("granite-pond-edge-stone-1", 0.84f, 0.66f, 0.28f, 10f, 25f, 70f, false);
    public static readonly Spec River = S("granite-river-stone-veined", 0.40f, 0.41f, 0.23f, 8f, 20f, 60f, false);
    public static readonly Spec Mossy = S("mossy-rocks", 1.14f, 0.73f, 0.34f, 10f, 25f, 70f, true);
    public static readonly Spec LogPile = S("log-pile", 7.76f, 3.21f, 1.76f, 30f, 65f, 120f, true); // 원본 5.17 x 2.14 x 1.17m의 1.5배
    public static readonly Spec Crate = S("pine-supply-crate-broken", 0.96f, 0.96f, 0.90f, 12f, 30f, 80f, true); // 원본 0.64 x 0.64 x 0.60m의 1.5배
    public static readonly Spec Drum = S("steel-drum-yellow", 0.59f, 0.59f, 0.88f, 10f, 25f, 70f, true);
    public static readonly Spec Totem = S("stone-face-totem", 1.17f, 1.14f, 3.64f, 25f, 55f, 140f, true);
    public static readonly Spec Basin = S("stone-water-basin-bamboo-spout", 0.80f, 1.56f, 0.76f, 12f, 30f, 80f, true);
    public static readonly Spec Roots = S("swamp-root-tangle", 6.06f, 6.14f, 0.74f, 15f, 35f, 80f, true); // 원본 3.03 x 3.07 x 0.37m의 2배
    public static readonly Spec GraveLedger = S("weathered-ledger-gravestone", 0.93f, 1.96f, 0.37f, 12f, 30f, 80f, true);
    public static readonly Spec GraveCross = S("weathered-stone-cross-gravestone", 0.56f, 0.38f, 1.26f, 12f, 30f, 80f, true);
    public static readonly Spec GravePointed = S("weathered-pointed-top-gravestone", 0.62f, 0.16f, 1.31f, 12f, 30f, 80f, true);
    public static readonly Spec Walkway = S("wooden-palisade-walkway", 3.99f, 2.01f, 2.81f, 20f, 45f, 100f, true);
    public static readonly Spec Palisade = S("wooden-palisade-straight", 4.20f, 1.60f, 3.98f, 20f, 45f, 100f, true);

    // 모델 모양 콜라이더를 쓰는 종류 (나머지 콜라이더 종류는 상자 하나)
    static PropLayout()
    {
        LogPile.scale = 1.5f; Crate.scale = 1.5f; Roots.scale = 2f;
        LogPile.meshCol = true; Crate.meshCol = true; Drum.meshCol = true; Totem.meshCol = true; Basin.meshCol = true;
        GraveCross.meshCol = true; GravePointed.meshCol = true; Walkway.meshCol = true; Palisade.meshCol = true;
    }

    public static readonly Spec[] All =
    {
        Pond1, River, Mossy, LogPile, Crate, Drum, Totem, Basin, Roots, GraveLedger, GraveCross, GravePointed, Walkway, Palisade,
    };

    const int Seed = 20261128;
    const float Gap = 1.0f;          // 소품끼리/집과의 최소 간격(m)
    const float ZoneGap = 2.5f;      // 봉쇄 구역 벽 바깥 여유(m)
    const float SpawnGap = 6f;       // 플레이어 시작 위치 주변 비움(m)
    const float EdgeGap = 4f;        // 맵 가장자리 벽 안쪽 여유(m)
    const float MaxQueryR = 4f;      // 나무 검사에 쓰는 최대 반경(색인 여유)
    const float CellSize = 16f;

    public static List<Item> Items = new List<Item>();
    static readonly List<Obb> fixedBoxes = new List<Obb>(); // 집 + 봉쇄 구역
    static Vector2 spawnPos;
    static readonly Dictionary<long, List<int>> index = new Dictionary<long, List<int>>();

    // ---- 겹침 검사(수학) ----

    // 두 회전 직사각형이 margin 이상 떨어져 있지 않으면 true (분리축 검사)
    public static bool Overlap(Obb a, Obb b, float margin)
    {
        float dx = b.cx - a.cx, dz = b.cz - a.cz;
        return !Separated(a, b, a.AxisX, dx, dz, margin) && !Separated(a, b, a.AxisZ, dx, dz, margin)
            && !Separated(a, b, b.AxisX, dx, dz, margin) && !Separated(a, b, b.AxisZ, dx, dz, margin);
    }

    // ax 축에 투영했을 때 두 직사각형 사이가 벌어져 있으면 true (분리축)
    static bool Separated(Obb a, Obb b, Vector2 ax, float dx, float dz, float margin)
    {
        float ra = a.hw * Mathf.Abs(Vector2.Dot(a.AxisX, ax)) + a.hd * Mathf.Abs(Vector2.Dot(a.AxisZ, ax)) + margin;
        float rb = b.hw * Mathf.Abs(Vector2.Dot(b.AxisX, ax)) + b.hd * Mathf.Abs(Vector2.Dot(b.AxisZ, ax));
        return Mathf.Abs(dx * ax.x + dz * ax.y) > ra + rb;
    }

    // 원(나무 줄기 + 여유)과 회전 직사각형이 겹치면 true
    public static bool CircleHitsObb(float x, float z, float r, Obb b)
    {
        float dx = x - b.cx, dz = z - b.cz;
        float lx = Mathf.Abs(dx * b.c - dz * b.s);
        float lz = Mathf.Abs(dx * b.s + dz * b.c);
        float ex = Mathf.Max(lx - b.hw, 0f), ez = Mathf.Max(lz - b.hd, 0f);
        return ex * ex + ez * ez < r * r;
    }

    static long Key(int cx, int cz) { return ((long)cx << 32) ^ (uint)cz; }

    static void AddToIndex(int id, Obb b)
    {
        float R = Mathf.Sqrt(b.hw * b.hw + b.hd * b.hd) + MaxQueryR;
        int x0 = Mathf.FloorToInt((b.cx - R) / CellSize), x1 = Mathf.FloorToInt((b.cx + R) / CellSize);
        int z0 = Mathf.FloorToInt((b.cz - R) / CellSize), z1 = Mathf.FloorToInt((b.cz + R) / CellSize);
        for (int ix = x0; ix <= x1; ix++)
            for (int iz = z0; iz <= z1; iz++)
            {
                List<int> l;
                if (!index.TryGetValue(Key(ix, iz), out l)) { l = new List<int>(); index[Key(ix, iz)] = l; }
                l.Add(id);
            }
    }

    // 나무를 (x,z)에 심으면 소품과 겹치는가 (r: 줄기 + 여유 반경)
    public static bool BlocksCircle(float x, float z, float r)
    {
        List<int> l;
        if (!index.TryGetValue(Key(Mathf.FloorToInt(x / CellSize), Mathf.FloorToInt(z / CellSize)), out l)) return false;
        foreach (var id in l)
            if (CircleHitsObb(x, z, r, Items[id].box)) return true;
        return false;
    }

    // ---- 배치 ----

    static bool Free(Obb box, float margin)
    {
        float lim = WorldConfig.Half - EdgeGap;
        float ext = Mathf.Abs(box.c) * box.hw + Mathf.Abs(box.s) * box.hd; // 회전 후 x 방향 반폭
        float extZ = Mathf.Abs(box.s) * box.hw + Mathf.Abs(box.c) * box.hd;
        if (Mathf.Abs(box.cx) + ext > lim || Mathf.Abs(box.cz) + extZ > lim) return false;
        if (CircleHitsObb(spawnPos.x, spawnPos.y, SpawnGap, box)) return false;
        foreach (var f in fixedBoxes) if (Overlap(box, f, margin)) return false;
        foreach (var it in Items) if (Overlap(box, it.box, margin)) return false;
        return true;
    }

    static bool TryPlace(Spec spec, float x, float z, float yaw, float margin)
    {
        var box = Obb.Make(x, z, spec.w * 0.5f, spec.d * 0.5f, yaw);
        if (!Free(box, margin)) return false;
        Items.Add(new Item { spec = spec, box = box, yaw = yaw });
        AddToIndex(Items.Count - 1, box);
        return true;
    }

    // houses: x, z, 반폭, 반깊이 (월드 축 정렬). spawn: 이번 판 숨는 팀 시작 위치(x, z).
    public static void Generate(List<Vector4> houses, Vector2 spawn)
    {
        Items = new List<Item>();
        index.Clear();
        fixedBoxes.Clear();
        spawnPos = spawn;
        foreach (var h in houses) fixedBoxes.Add(Obb.Make(h.x, h.y, h.z, h.w, 0f));
        Zones.Init();
        foreach (var c in Zones.Centers)
            fixedBoxes.Add(Obb.Make(c.x, c.y, Zones.Size * 0.5f + Zones.WallThick + ZoneGap, Zones.Size * 0.5f + Zones.WallThick + ZoneGap, 0f));
        var rand = new System.Random(Seed);
        float R(float a, float b) { return a + (float)rand.NextDouble() * (b - a); }

        // 1) 작은 공동묘지: 후보 자리를 차례로 시도해서 15기 중 12기 이상 놓이는 첫 자리를 쓴다.
        float[,] sites = { { -80f, 70f }, { 80f, -70f }, { -70f, -85f }, { 85f, 80f }, { -105f, -25f }, { 40f, 100f } };
        for (int si = 0; si < sites.GetLength(0); si++)
        {
            int before = Items.Count;
            float sx = sites[si, 0], sz = sites[si, 1];
            Spec[] rows = { GravePointed, GraveCross, GraveLedger };
            float[] rowZ = { 0f, 3.2f, 6.6f };
            int placedHere = 0;
            for (int row = 0; row < 3; row++)
                for (int col = 0; col < 5; col++)
                {
                    float jx = R(-0.15f, 0.15f), jz = R(-0.15f, 0.15f), jy = R(-6f, 6f);
                    if (TryPlace(rows[row], sx + col * 2.3f + jx, sz + rowZ[row] + jz, jy, 0.4f)) placedHere++;
                }
            if (placedHere >= 12) break;
            // 실패: 이 자리 롤백
            while (Items.Count > before) Items.RemoveAt(Items.Count - 1);
            index.Clear();
            for (int i = 0; i < Items.Count; i++) AddToIndex(i, Items[i].box);
        }

        // 2) 집 가까이 목책: 앞 4채. 한 변(남/북/동/서 중 되는 곳)에 일자 목책 + 가운데 통로.
        int fenced = 0;
        for (int hi = 0; hi < houses.Count && fenced < 4; hi++)
        {
            var h = houses[hi];
            for (int side = 0; side < 4; side++)
            {
                bool alongX = side < 2;               // 남/북 변은 x 방향으로 늘어놓는다
                float sign = (side % 2 == 0) ? -1f : 1f;
                float off = (alongX ? h.w : h.z) + 3.5f;
                Spec[] seq = { Palisade, Palisade, Walkway, Palisade, Palisade };
                float[] len = { Palisade.w, Palisade.w, Walkway.w, Palisade.w, Palisade.w };
                float total = 0f; foreach (var l in len) total += l + 0.2f;
                float cursor = -total * 0.5f;
                int before = Items.Count, ok = 0;
                for (int k = 0; k < seq.Length; k++)
                {
                    float c = cursor + len[k] * 0.5f; cursor += len[k] + 0.2f;
                    float px = alongX ? h.x + c : h.x + sign * off;
                    float pz = alongX ? h.y + sign * off : h.y + c;
                    if (TryPlace(seq[k], px, pz, alongX ? 0f : 90f, 0.1f)) ok++;
                }
                if (ok >= 3) { fenced++; break; }
                while (Items.Count > before) Items.RemoveAt(Items.Count - 1);
                index.Clear();
                for (int i = 0; i < Items.Count; i++) AddToIndex(i, Items[i].box);
            }
        }

        // 3) 이정표: 토템 4개(사분면마다 하나), 물 그릇 2개(서쪽/동쪽)
        for (int q = 0; q < 4; q++)
        {
            float sx = (q % 2 == 0) ? 1f : -1f, sz = (q < 2) ? 1f : -1f;
            for (int t = 0; t < 60; t++)
                if (TryPlace(Totem, sx * R(30f, 110f), sz * R(30f, 110f), R(0f, 360f), Gap + 1f)) break;
        }
        for (int q = 0; q < 2; q++)
        {
            float sx = q == 0 ? -1f : 1f;
            for (int t = 0; t < 60; t++)
                if (TryPlace(Basin, sx * R(30f, 100f), R(-35f, 35f), R(0f, 360f), Gap + 1f)) break;
        }

        // 4) 흩뿌리기 (큰 것부터): 뿌리 덩이, 통나무 더미, 돌무더기, 드럼통(2~3개 묶음), 상자(1~3개 묶음), 돌
        Scatter(rand, Roots, 15, 1.5f);
        Scatter(rand, LogPile, 10, 2f);
        Scatter(rand, Mossy, 25, 1.5f);
        Cluster(rand, Drum, 8, 3);
        Cluster(rand, Crate, 10, 3);
        Scatter(rand, Pond1, 40, 1.5f);
        Scatter(rand, River, 50, 1.5f);

        // 검사: 소품끼리, 소품과 집/구역이 정말 안 겹치는지 다시 센다
        int bad = CountOverlaps();
        var counts = new Dictionary<string, int>();
        foreach (var it in Items) counts[it.spec.slug] = (counts.TryGetValue(it.spec.slug, out var n) ? n : 0) + 1;
        var sb = new System.Text.StringBuilder();
        foreach (var kv in counts) sb.Append(kv.Key).Append('=').Append(kv.Value).Append(' ');
        Debug.Log("[HushHunt] PropLayout: 소품 " + Items.Count + "개 (" + sb + ") / 겹침 검사(소품-소품, 소품-집/구역): 겹침 " + bad + "건");
    }

    static void Scatter(System.Random rand, Spec spec, int count, float margin)
    {
        float lim = WorldConfig.Half - EdgeGap;
        int made = 0;
        for (int t = 0; t < count * 60 && made < count; t++)
        {
            float x = ((float)rand.NextDouble() * 2f - 1f) * lim, z = ((float)rand.NextDouble() * 2f - 1f) * lim;
            if (TryPlace(spec, x, z, (float)rand.NextDouble() * 360f, margin)) made++;
        }
    }

    // 묶음: 기준점 하나를 놓고 그 둘레 1~2.5m 안에 1~(max-1)개를 더 놓는다.
    static void Cluster(System.Random rand, Spec spec, int groups, int max)
    {
        float lim = WorldConfig.Half - EdgeGap;
        int made = 0;
        for (int t = 0; t < groups * 60 && made < groups; t++)
        {
            float x = ((float)rand.NextDouble() * 2f - 1f) * lim, z = ((float)rand.NextDouble() * 2f - 1f) * lim;
            if (!TryPlace(spec, x, z, (float)rand.NextDouble() * 360f, 1.5f)) continue;
            made++;
            int extra = 1 + rand.Next(max - 1);
            for (int e = 0; e < extra; e++)
                for (int tries = 0; tries < 10; tries++)
                {
                    float a = (float)rand.NextDouble() * Mathf.PI * 2f, d = 1f + (float)rand.NextDouble() * 1.5f;
                    if (TryPlace(spec, x + Mathf.Cos(a) * d, z + Mathf.Sin(a) * d, (float)rand.NextDouble() * 360f, 0.15f)) break;
                }
        }
    }

    // 소품끼리, 소품과 집/구역 사이의 겹침 수 (margin 0 = 닿기만 해도 셈)
    public static int CountOverlaps()
    {
        int bad = 0;
        for (int i = 0; i < Items.Count; i++)
        {
            for (int j = i + 1; j < Items.Count; j++) if (Overlap(Items[i].box, Items[j].box, 0f)) bad++;
            foreach (var f in fixedBoxes) if (Overlap(Items[i].box, f, 0f)) bad++;
        }
        return bad;
    }

    // 나무 위치가 소품과 겹치는 수 (RandomizeLayout이 나무를 다 만든 뒤 호출)
    public static int CountTreeHits(List<Vector2> trees, float r)
    {
        int n = 0;
        foreach (var p in trees) if (BlocksCircle(p.x, p.y, r)) n++;
        return n;
    }
}
