using UnityEngine;

// 소나무를 더 실감 나는 모습으로 교체한다.
// Assets/Resources/Trees 에 모델(.fbx 등)이 있으면 그걸 쓰고, 없으면 코드로 만드는
// 절차적 소나무(ProceduralTree)를 쓴다. 씬을 다시 만들지 않아도 시작할 때 적용된다.
[DefaultExecutionOrder(-50)] // TreePaper 보다 먼저 모델을 단다 (용지는 전나무 모델을 찾는다)
public class TreeModels : MonoBehaviour
{
    Texture2D barkTex;
    Material barkMaterial;
    Material leafInst; // 잎 재질의 GPU 인스턴싱용 복사본
    bool barkTexOwned = true; // Resources 에서 불러온 공유 에셋이면 Destroy 하면 안 된다
    bool farCullingReady;
    const int FarLayer = 30; // 31은 용지 표면 레이캐스트가 임시로 쓴다

    Mesh firProxyMesh;
    Material firProxyMat;

    // 전나무 원본(수백만 삼각형)은 가까울 때만 그린다. 조금 멀어지면 같은 키/폭의 아주 가벼운 대역(원뿔 몇 개, 수백 삼각형)으로
    // 바꾼다. 메시를 줄이는 계산은 없다 (원본 메시를 읽지 않는다). 대역 메시/재질은 한 번만 만들어 모두가 공유한다.
    // 대역은 그림자를 드리운다 (멀리서도 그림자 모양 유지). 원본은 가까울 때만 그림자를 드리운다 (LOD0만 보이므로).
    const float FirLod0Height = 0.30f; // 화면 높이의 30% 이상일 때 원본 (키 9.5m 기준 대략 27m 안)
    const float FirLod1Height = 0.02f; // 이보다 작아지면 안 그림 (거리 컬링 50m가 먼저 걸린다)

    void AddFirProxyLod(Transform tree, GameObject model, Renderer[] rends, Material leafMat)
    {
        if (tree.GetComponent<LODGroup>() != null || rends.Length == 0) return;
        if (firProxyMesh == null) firProxyMesh = BuildFirProxyMesh();
        if (firProxyMat == null)
        {
            firProxyMat = RuntimeMaterials.CreateLit("Fir Proxy (runtime)", new Color(0.03f, 0.05f, 0.035f), 0.05f);
            firProxyMat.enableInstancing = true;
        }
        var b = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
        float height = Mathf.Max(0.5f, b.size.y);
        float radius = Mathf.Max(0.3f, Mathf.Max(b.size.x, b.size.z) * 0.5f);

        var go = new GameObject("FirProxy"); // 이름이 "TreeModel"로 시작하면 TreePaper가 줄기로 착각한다
        go.layer = FarLayer;
        go.transform.SetParent(tree, true);
        go.transform.position = new Vector3(b.center.x, b.min.y, b.center.z);
        go.transform.rotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;
        go.transform.localScale = new Vector3(radius / tree.lossyScale.x, height / tree.lossyScale.y, radius / tree.lossyScale.z);
        go.AddComponent<MeshFilter>().sharedMesh = firProxyMesh;
        var pr = go.AddComponent<MeshRenderer>();
        pr.sharedMaterial = firProxyMat;
        pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;

        foreach (var r in rends)
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On; // 가까운 원본은 그림자 그대로

        var g = tree.gameObject.AddComponent<LODGroup>();
        var lods = new LOD[2];
        lods[0] = new LOD(FirLod0Height, rends);
        lods[1] = new LOD(FirLod1Height, new Renderer[] { pr });
        g.SetLODs(lods);
        g.fadeMode = LODFadeMode.None;
        g.RecalculateBounds();
    }

    // 높이 1, 반지름 1짜리 단위 전나무: 가는 줄기 + 아래로 갈수록 넓은 원뿔 4단 (정점 8각). 약 70 삼각형.
    static Mesh BuildFirProxyMesh()
    {
        const int seg = 8;
        var v = new System.Collections.Generic.List<Vector3>();
        var n = new System.Collections.Generic.List<Vector3>();
        var tri = new System.Collections.Generic.List<int>();
        // 원뿔: 바닥 y0, 꼭대기 y1, 바닥 반지름 r
        System.Action<float, float, float> cone = (y0, y1, r) =>
        {
            int apex = v.Count;
            v.Add(new Vector3(0f, y1, 0f)); n.Add(Vector3.up);
            int ring = v.Count;
            for (int i = 0; i < seg; i++)
            {
                float a = i * Mathf.PI * 2f / seg;
                var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                v.Add(new Vector3(d.x * r, y0, d.z * r));
                n.Add((d + Vector3.up * 0.6f).normalized);
            }
            for (int i = 0; i < seg; i++)
            {
                tri.Add(apex); tri.Add(ring + (i + 1) % seg); tri.Add(ring + i);
            }
        };
        cone(0.00f, 0.35f, 0.05f);  // 줄기
        cone(0.12f, 0.55f, 1.00f);
        cone(0.32f, 0.75f, 0.75f);
        cone(0.52f, 0.90f, 0.50f);
        cone(0.70f, 1.00f, 0.28f);
        var m = new Mesh { name = "Fir Proxy" };
        m.SetVertices(v);
        m.SetNormals(n);
        m.SetTriangles(tri, 0);
        m.RecalculateBounds();
        return m;
    }

    // 안개 시야(약 50m) 밖의 무거운 모델은 카메라 레이어 컬링으로 아예 그리지 않는다.
    void SetupFarCulling(GameObject go)
    {
        foreach (var r in go.GetComponentsInChildren<Renderer>())
            r.gameObject.layer = FarLayer;
        if (farCullingReady) return;
        var cam = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
        if (cam == null) return;
        var d = cam.layerCullDistances;
        if (d == null || d.Length != 32) d = new float[32];
        d[FarLayer] = 50f;
        cam.layerCullDistances = d;
        cam.layerCullSpherical = true;
        farCullingReady = true;
    }

    void Start()
    {
        var prefabs = Resources.LoadAll<GameObject>("Trees");
        bool hasModels = prefabs != null && prefabs.Length > 0;

        var rand = new System.Random(11);
        // 전나무(Fir Tree 01) 모델이 있으면 일부 나무(FirCount그루)에만 단다: 용지는 전나무에만 붙고,
        // 이 모델은 고폴리라 전부 전나무로 만들면 프레임이 크게 떨어진다 (실측: 320그루 전부 전나무일 때 30fps).
        // 전나무는 무작위 후보에서도 빼서 나머지 나무는 절차적 소나무(또는 다른 모델)로 유지한다.
        GameObject firPrefab = null;
        var others = new System.Collections.Generic.List<GameObject>();
        if (hasModels)
            foreach (var pf in prefabs)
            {
                if (pf.name.ToLowerInvariant().Contains("fir_tree_01")) firPrefab = pf;
                else others.Add(pf);
            }
        // 전나무는 숲 전체에 흩어 놓는다 (pines 는 x 순 정렬이라 앞에서부터 쓰면 한쪽 끝에 몰린다).
        // 개수: 안개(약 25~30m 밖은 거의 안 보임) 속에서도 시야에 전나무가 여러 그루 보이는 정도. 모델이 고폴리라 너무 늘리면 fps가 떨어진다.
        const int FirCount = 180;

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
        barkMaterial.enableInstancing = true; // 절차적 나무 줄기를 GPU 인스턴싱으로 그린다
        var all = FindObjectsByType<Transform>(FindObjectsSortMode.None);
        var pines = new System.Collections.Generic.List<Transform>();
        foreach (var cand in all) if (cand.name == WorldConfig.Names.PineTree) pines.Add(cand);
        // 검색 순서가 실행마다 달라도 같은 숲이 나오도록 위치로 정렬한다.
        var firSet = new System.Collections.Generic.HashSet<int>();
        if (firPrefab != null)
        {
            // 매 판 숲 전체에서 무작위로 고른다 (나무 위치는 RandomizeLayout이 집/스폰을 피해 이미 정했다).
            var idx = new System.Collections.Generic.List<int>();
            pines.Sort((p1, p2) =>
            {
                int cmp = p1.position.x.CompareTo(p2.position.x);
                return cmp != 0 ? cmp : p1.position.z.CompareTo(p2.position.z);
            });
            for (int i = 0; i < pines.Count; i++) idx.Add(i);
            var pick = new System.Random(); // 판마다 다른 나무가 전나무가 된다
            for (int i = idx.Count - 1; i > 0; i--)
            {
                int j = pick.Next(i + 1);
                int tmp = idx[i]; idx[i] = idx[j]; idx[j] = tmp;
            }
            for (int i = 0; i < idx.Count && i < FirCount; i++) firSet.Add(idx[i]);
        }
        pines.Sort((p1, p2) =>
        {
            int cmp = p1.position.x.CompareTo(p2.position.x);
            return cmp != 0 ? cmp : p1.position.z.CompareTo(p2.position.z);
        });
        for (int pi = 0; pi < pines.Count; pi++)
        {
            var t = pines[pi];
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

            GameObject prefab = null;
            bool isFir = false;
            if (firSet.Contains(pi)) { prefab = firPrefab; isFir = true; }
            else if (others.Count > 0) prefab = others[rand.Next(others.Count)];
            if (prefab == null)
            {
                // 절차적 소나무: 기존 나무 높이/굵기 배율에 맞춰 코드로 만든다.
                if (leafMat != null)
                {
                    if (leafInst == null)
                    {
                        leafInst = new Material(leafMat) { name = leafMat.name + " (instanced)", enableInstancing = true };
                        ProceduralTree.ConfigureLeafMaterial(leafInst); // 바늘잎 알파 컷아웃
                    }
                    leafMat = leafInst; // GPU 인스턴싱: 같은 재질/메시를 한 번에 그린다
                }
                ProceduralTree.Build(t, WorldConfig.PineHeight * oldScale,
                    0.9f + (float)rand.NextDouble() * 0.25f, barkMaterial, leafMat, rand);
                // 절차적 나무도 안개 시야(약 50m) 밖에서는 그리지 않는다.
                // 320그루를 매 프레임 그리면 드로 콜이 쌓여 느려진다 (실제로 보이는 건 반경 50m 안쪽 몇십 그루뿐).
                var tm = t.Find("TreeModel");
                if (tm != null) SetupFarCulling(tm.gameObject);
                continue;
            }

            var go = Instantiate(prefab, t);
            go.name = isFir ? "TreeModel Fir" : "TreeModel";
            if (isFir) SetupFarCulling(go); // 안개 시야(약 50m) 밖의 무거운 전나무는 그리지 않는다
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

            // 재질: 전나무는 원래 실사 텍스처를 그대로 쓴다 (덮어쓰면 폴리헤이븐 재질이 날아감).
            // 다른 모델은 이름에 wood/bark 가 있으면 줄기색, 나머지는 잎색으로 맞춘다.
            if (isFir) { AddFirProxyLod(t, go, rends, leafMat); continue; }
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
        if (firProxyMesh != null) Destroy(firProxyMesh);
        if (firProxyMat != null) Destroy(firProxyMat);
        if (barkMaterial != null) Destroy(barkMaterial);
        if (leafInst != null) Destroy(leafInst);
        ProceduralTree.ReleaseTexture();
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
