using UnityEngine;

// 소나무를 더 실감 나는 모습으로 교체한다.
// Assets/Resources/Trees 에 모델(.fbx 등)이 있으면 그걸 쓰고, 없으면 코드로 만드는
// 절차적 소나무(ProceduralTree)를 쓴다. 씬을 다시 만들지 않아도 시작할 때 적용된다.
[DefaultExecutionOrder(-50)] // TreePaper 보다 먼저 모델을 단다 (용지는 전나무 모델을 찾는다)
public class TreeModels : MonoBehaviour
{
    Texture2D barkTex;
    Material barkMaterial;
    bool barkTexOwned = true; // Resources 에서 불러온 공유 에셋이면 Destroy 하면 안 된다

    void Start()
    {
        var prefabs = Resources.LoadAll<GameObject>("Trees");
        bool hasModels = prefabs != null && prefabs.Length > 0;

        var rand = new System.Random(11);
        // 전나무(Fir Tree 01) 모델이 있으면 최소 3그루에는 반드시 단다: 용지는 전나무에만 붙는다.
        GameObject firPrefab = null;
        if (hasModels)
            foreach (var pf in prefabs)
                if (pf.name.ToLowerInvariant().Contains("fir_tree_01")) { firPrefab = pf; break; }
        int firNeed = firPrefab != null ? 3 : 0;

        // 거친 나무껍질 느낌: 코드로 만든 세로 결 노이즈 텍스처. 이미지 파일 없이 적용된다.
        // 어두운 갈색을 입혀 두고, 손전등이 비출 때 결 명암이 드러난다.
        // 실사 껍질 사진(ambientCG CC0)이 있으면 그걸 쓰고, 없으면 코드로 만든 텍스처로 버틴다.
        barkTex = Resources.Load<Texture2D>("Textures/Bark");
        barkTexOwned = barkTex == null;
        if (barkTex == null) barkTex = BuildBarkTexture();
        // 사진은 색이 이미 들어 있으니 흰색, 절차적 텍스처는 회색조라 갈색을 입힌다.
        barkMaterial = RuntimeMaterials.CreateLit("Tree Bark (runtime)",
            barkTexOwned ? new Color(0.28f, 0.22f, 0.18f) : Color.white, 0.05f);
        barkMaterial.mainTexture = barkTex;
        var all = FindObjectsByType<Transform>(FindObjectsSortMode.None);
        var pines = new System.Collections.Generic.List<Transform>();
        foreach (var cand in all) if (cand.name == WorldConfig.Names.PineTree) pines.Add(cand);
        // 검색 순서가 실행마다 달라도 같은 숲이 나오도록 위치로 정렬한다.
        pines.Sort((p1, p2) =>
        {
            int cmp = p1.position.x.CompareTo(p2.position.x);
            return cmp != 0 ? cmp : p1.position.z.CompareTo(p2.position.z);
        });
        foreach (var t in pines)
        {
            // 기존 원뿔 나무의 재질(씬의 어두운 색)을 그대로 써서 안개/밤 분위기와 맞춘다.
            Material leafMat = null, barkMat = null;
            float oldScale = 1f;
            var trunk = t.Find(WorldConfig.Names.PineTrunk);
            if (trunk != null)
            {
                var tr = trunk.GetComponent<Renderer>();
                if (tr != null) { barkMat = tr.sharedMaterial; tr.enabled = false; } // 충돌체는 남김
                oldScale = trunk.localScale.x / WorldConfig.PineTrunkBaseWidth;
            }
            for (int i = 1; i <= 3; i++)
            {
                var f = t.Find(WorldConfig.Names.PineFoliagePrefix + i);
                if (f == null) continue;
                var fr = f.GetComponent<Renderer>();
                if (leafMat == null && fr != null) leafMat = fr.sharedMaterial;
                f.gameObject.SetActive(false);
            }

            if (!hasModels)
            {
                // 절차적 소나무: 기존 나무 높이/굵기 배율에 맞춰 코드로 만든다.
                ProceduralTree.Build(t, WorldConfig.PineHeight * oldScale,
                    0.9f + (float)rand.NextDouble() * 0.25f, barkMaterial, leafMat, rand);
                continue;
            }

            var prefab = firNeed > 0 ? firPrefab : prefabs[rand.Next(prefabs.Length)];
            var go = Instantiate(prefab, t);
            go.name = prefab == firPrefab ? "TreeModel Fir" : "TreeModel";
            if (prefab == firPrefab) firNeed--;
            go.transform.localPosition = Vector3.zero;
            // 방향은 제각각, 살짝 기울어진 나무로 획일적인 줄맞춤 느낌을 없앤다.
            float tiltX = (float)(rand.NextDouble() * 6.0 - 3.0);
            float tiltZ = (float)(rand.NextDouble() * 6.0 - 3.0);
            go.transform.localRotation = Quaternion.Euler(tiltX, (float)rand.NextDouble() * 360f, tiltZ);
            go.transform.localScale = Vector3.one;

            foreach (var c in go.GetComponentsInChildren<Collider>()) Destroy(c);

            // 모델 단위가 파일마다 달라도 되도록 높이를 재서 기존 나무 높이(약 9.5m x 크기)에 맞춘다.
            var rends = go.GetComponentsInChildren<Renderer>();
            if (rends.Length == 0) { Destroy(go); continue; }
            var b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            if (b.size.y > 0.001f)
            {
                float target = WorldConfig.PineHeight * oldScale;
                float k = target / b.size.y;
                float girth = 0.9f + (float)rand.NextDouble() * 0.25f; // 굵기 변화
                go.transform.localScale = new Vector3(k * girth, k, k * girth);

                // 모델 원점이 밑바닥이 아니어도 땅에 박히거나 뜨지 않게 가장 낮은 점을 땅에 맞춘다.
                var nb = rends[0].bounds;
                for (int i = 1; i < rends.Length; i++) nb.Encapsulate(rends[i].bounds);
                go.transform.position += Vector3.up * (t.position.y - nb.min.y);
            }

            // 재질: 이름에 wood/bark 가 있으면 줄기색, 나머지는 잎색.
            foreach (var r in rends)
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    string n = mats[i] != null ? mats[i].name.ToLowerInvariant() : "";
                    bool wood = n.Contains("wood") || n.Contains("bark") || n.Contains("trunk");
                    var pick = wood ? (barkMaterial != null ? barkMaterial : barkMat) : leafMat;
                    if (pick != null) mats[i] = pick;
                }
                r.sharedMaterials = mats;
            }
        }
    }

    void OnDestroy()
    {
        if (barkMaterial != null) Destroy(barkMaterial);
        if (barkTex != null && barkTexOwned) Destroy(barkTex);
    }

    // 가장자리가 이어지는(타일링) 노이즈. GroundTexture와 같은 기법.
    static float TileN(float u, float v, float fx, float fy, float seed)
    {
        float a = Mathf.PerlinNoise(seed + u * fx, seed + v * fy);
        float b = Mathf.PerlinNoise(seed + (u - 1f) * fx, seed + v * fy);
        float c = Mathf.PerlinNoise(seed + u * fx, seed + (v - 1f) * fy);
        float d = Mathf.PerlinNoise(seed + (u - 1f) * fx, seed + (v - 1f) * fy);
        return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
    }

    // 세로로 길게 늘어진 결 + 잔결로 거친 껍질 질감을 만든다. 회색조로 만들고 색은 머티리얼이 입힌다.
    static Texture2D BuildBarkTexture()
    {
        const int W = 128, H = 256;
        var tex = new Texture2D(W, H, TextureFormat.RGB24, true);
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.filterMode = FilterMode.Trilinear;
        var px = new Color[W * H];
        for (int y = 0; y < H; y++)
        {
            for (int x = 0; x < W; x++)
            {
                float u = x / (float)W, v = y / (float)H;
                float ridge = TileN(u, v, 16f, 2f, 3f);      // 가로로 촘촘, 세로로 완만한 결
                float groove = Mathf.Abs(ridge - 0.5f) * 2f; // 골이 패인 느낌
                float fine = TileN(u, v, 48f, 24f, 31f);     // 잔결
                float t = Mathf.Clamp01(1f - groove) * (0.65f + fine * 0.6f);
                float g = Mathf.Lerp(0.22f, 0.90f, t);
                px[y * W + x] = new Color(g, g, g);
            }
        }
        // 옹이: 어두운 타원을 몇 개 박는다.
        var rand = new System.Random(9);
        for (int i = 0; i < 8; i++)
        {
            int kx = rand.Next(W), ky = rand.Next(H);
            int rw = 2 + rand.Next(4), rh = 4 + rand.Next(7);
            for (int dy = -rh; dy <= rh; dy++)
            for (int dx = -rw; dx <= rw; dx++)
            {
                float dd = dx * dx / (float)(rw * rw) + dy * dy / (float)(rh * rh);
                if (dd > 1f) continue;
                int ii = ((ky + dy + H) % H) * W + ((kx + dx + W) % W);
                float k = 1f - 0.7f * (1f - dd);
                px[ii] = new Color(px[ii].r * k, px[ii].g * k, px[ii].b * k);
            }
        }

        tex.SetPixels(px);
        tex.Apply(true);
        return tex;
    }
}
