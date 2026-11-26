using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// 전나무(Fir Tree 01) 한 그루마다 LODGroup을 만든다.
//  LOD0: 원본 (가까이)
//  LOD1: 삼각형 약 40% (중간 거리, 그림자 유지)
//  LOD2: 삼각형 약 10% (먼 거리, 그림자 끔)
//  그 아래는 아예 그리지 않는다. LOD 사이는 크로스페이드.
// 줄인 메시는 원본 메시를 코드로 단순화해서(정점 클러스터링) 한 번만 만들고 모든 나무가 공유한다.
// 모델이 Read/Write 꺼진 상태면 메시를 읽을 수 없어서(Assets/Editor/FirTreeImportSettings 가 켜 준다)
// LOD1은 원본을 그대로 쓰고 LOD2는 없앤다 (멀리서는 안 그림).
// 재질은 런타임 복사본에 GPU 인스턴싱을 켠다 (프로젝트의 재질 에셋은 건드리지 않는다).
public static class FirLod
{
    // 화면에서 나무가 차지하는 높이 비율 기준. 키 9.5m, 시야각 60~75도면 대략 17m / 30m / 55m 이상 거리.
    const float Lod0Height = 0.40f;
    const float Lod1Height = 0.22f;
    const float Lod2Height = 0.10f;
    const float Lod1Ratio = 0.40f;
    const float Lod2Ratio = 0.10f;

    static readonly Dictionary<Mesh, Mesh[]> meshCache = new Dictionary<Mesh, Mesh[]>();
    static readonly Dictionary<Material, Material> matCopies = new Dictionary<Material, Material>();
    static bool warned;

    public static void Release()
    {
        foreach (var kv in meshCache)
            if (kv.Value != null)
                foreach (var m in kv.Value) if (m != null) Object.Destroy(m);
        meshCache.Clear();
        foreach (var kv in matCopies) if (kv.Value != null) Object.Destroy(kv.Value);
        matCopies.Clear();
        warned = false;
    }

    static Material Instanced(Material m)
    {
        if (m == null) return null;
        if (!matCopies.TryGetValue(m, out var c) || c == null)
        {
            c = new Material(m) { name = m.name + " (instanced)" };
            c.enableInstancing = true;
            matCopies[m] = c;
        }
        return c;
    }

    // tree: 나무 루트(LODGroup이 여기 붙는다), model: 전나무 모델 오브젝트(이미 크기/위치가 맞춰진 상태).
    public static void Apply(Transform tree, GameObject model)
    {
        if (tree.GetComponent<LODGroup>() != null || model.GetComponentInChildren<LODGroup>(true) != null) return;
        var rends = model.GetComponentsInChildren<MeshRenderer>();
        if (rends.Length == 0) return;

        // 줄인 메시 렌더러는 "TreeModel" 로 시작하지 않는 별도 오브젝트에 둔다
        // (TreePaper가 TreeModel 아래 렌더러로 줄기 표면을 재므로 섞이면 안 된다).
        var root = new GameObject("FirLODs");
        root.transform.SetParent(tree, false);
        root.transform.localPosition = model.transform.localPosition;
        root.transform.localRotation = model.transform.localRotation;
        root.transform.localScale = model.transform.localScale;

        var lod0 = new List<Renderer>();
        var lod1 = new List<Renderer>();
        var lod2 = new List<Renderer>();
        bool readable = true;

        foreach (var r in rends)
        {
            var mf = r.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) continue;

            var shared = r.sharedMaterials;
            var inst = new Material[shared.Length];
            for (int i = 0; i < shared.Length; i++) inst[i] = Instanced(shared[i]);
            r.sharedMaterials = inst;
            lod0.Add(r);

            var lods = GetLods(mf.sharedMesh);
            if (lods == null) { readable = false; continue; }

            var rel = model.transform.worldToLocalMatrix * r.transform.localToWorldMatrix;
            for (int level = 0; level < 2; level++)
            {
                var go = new GameObject("LOD" + (level + 1) + " " + r.name);
                go.transform.SetParent(root.transform, false);
                go.layer = r.gameObject.layer;
                go.transform.localPosition = rel.GetColumn(3);
                go.transform.localRotation = rel.rotation;
                go.transform.localScale = rel.lossyScale;
                go.AddComponent<MeshFilter>().sharedMesh = lods[level];
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterials = inst;
                mr.shadowCastingMode = level == 0 ? ShadowCastingMode.On : ShadowCastingMode.Off;
                mr.receiveShadows = r.receiveShadows;
                (level == 0 ? lod1 : lod2).Add(mr);
            }
        }

        if (lod0.Count == 0) { Object.Destroy(root); return; }
        if (!readable)
        {
            if (!warned)
            {
                warned = true;
                Debug.LogWarning("[HushHunt] 전나무 메시를 읽을 수 없어 LOD를 줄이지 못했다 (모델 Import 설정의 Read/Write를 켜야 한다). 원본을 중간 거리까지 쓰고 그 뒤는 그리지 않는다.");
            }
            foreach (var r in lod0) lod1.Add(r); // 중간 거리까지 원본 유지
            lod2.Clear();
            foreach (var mr in root.GetComponentsInChildren<Renderer>()) Object.Destroy(mr.gameObject);
        }

        var g = tree.gameObject.AddComponent<LODGroup>();
        var arr = new LOD[3];
        arr[0] = new LOD(Lod0Height, lod0.ToArray());
        arr[1] = new LOD(Lod1Height, lod1.ToArray());
        arr[2] = new LOD(Lod2Height, lod2.ToArray());
        for (int i = 0; i < arr.Length; i++) arr[i].fadeTransitionWidth = 0.25f;
        g.SetLODs(arr);
        g.fadeMode = LODFadeMode.CrossFade;
        g.animateCrossFading = true;
        g.RecalculateBounds();
    }

    // [0]=LOD1용, [1]=LOD2용. 메시를 읽을 수 없으면 null.
    static Mesh[] GetLods(Mesh src)
    {
        if (meshCache.TryGetValue(src, out var cached)) return cached;
        Mesh[] result = null;
        if (src.isReadable && src.vertexCount > 0)
            result = new[] { Decimate(src, Lod1Ratio, "LOD1"), Decimate(src, Lod2Ratio, "LOD2") };
        meshCache[src] = result;
        return result;
    }

    struct Key
    {
        public int x, y, z, u, v;
    }

    class Built
    {
        public List<Vector3> pos = new List<Vector3>();
        public List<Vector3> nrm = new List<Vector3>();
        public List<Vector2> uv = new List<Vector2>();
        public List<Vector4> tan = new List<Vector4>();
        public List<int>[] tris;
        public int triCount;
    }

    // 정점 클러스터링: 격자 칸(위치 + UV 칸)마다 정점을 하나로 합치고, 찌그러진 삼각형은 버린다.
    // 목표 삼각형 수가 될 때까지 칸 크기를 이분 탐색한다. UV 칸을 키에 넣어서 텍스처 섬이 섞이지 않게 한다.
    static Mesh Decimate(Mesh src, float ratio, string suffix)
    {
        var verts = src.vertices;
        var norms = src.normals;
        var uvs = src.uv;
        var tans = src.tangents;
        int vc = verts.Length;
        bool hasN = norms.Length == vc, hasUV = uvs.Length == vc, hasT = tans.Length == vc;
        int subs = src.subMeshCount;
        var subTris = new int[subs][];
        int total = 0;
        for (int s = 0; s < subs; s++) { subTris[s] = src.GetTriangles(s); total += subTris[s].Length / 3; }
        int target = Mathf.Max(12, Mathf.RoundToInt(total * ratio));

        float diag = Mathf.Max(0.01f, src.bounds.size.magnitude);
        float lo = diag / 3000f, hi = diag / 2f;
        Built best = null;
        for (int it = 0; it < 9; it++)
        {
            float mid = Mathf.Sqrt(lo * hi);
            var b = Cluster(verts, norms, uvs, tans, hasN, hasUV, hasT, subTris, mid);
            if (b.triCount <= target) { best = b; hi = mid; }
            else lo = mid;
        }
        if (best == null) best = Cluster(verts, norms, uvs, tans, hasN, hasUV, hasT, subTris, hi);

        var mesh = new Mesh { name = src.name + " " + suffix };
        if (best.pos.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
        mesh.SetVertices(best.pos);
        if (hasN) mesh.SetNormals(best.nrm);
        if (hasUV) mesh.SetUVs(0, best.uv);
        if (hasT) mesh.SetTangents(best.tan);
        mesh.subMeshCount = subs;
        for (int s = 0; s < subs; s++) mesh.SetTriangles(best.tris[s], s, false);
        mesh.RecalculateBounds();
        return mesh;
    }

    static Built Cluster(Vector3[] verts, Vector3[] norms, Vector2[] uvs, Vector4[] tans,
        bool hasN, bool hasUV, bool hasT, int[][] subTris, float cell)
    {
        var b = new Built { tris = new List<int>[subTris.Length] };
        var sumPos = new List<Vector3>();
        var sumN = new List<Vector3>();
        var sumUV = new List<Vector2>();
        var sumT = new List<Vector4>();
        var cnt = new List<int>();
        float inv = 1f / cell;
        for (int s = 0; s < subTris.Length; s++)
        {
            var map = new Dictionary<Key, int>();
            var vmap = new Dictionary<int, int>(); // 원본 정점 -> 새 정점 (이 서브메시 안에서)
            var tris = new List<int>();
            var src = subTris[s];
            for (int i = 0; i < src.Length; i++)
            {
                int vi = src[i];
                if (!vmap.TryGetValue(vi, out int ni))
                {
                    var p = verts[vi];
                    var k = new Key
                    {
                        x = Mathf.FloorToInt(p.x * inv), y = Mathf.FloorToInt(p.y * inv), z = Mathf.FloorToInt(p.z * inv),
                        u = hasUV ? Mathf.FloorToInt(uvs[vi].x * 24f) : 0,
                        v = hasUV ? Mathf.FloorToInt(uvs[vi].y * 24f) : 0
                    };
                    if (!map.TryGetValue(k, out ni))
                    {
                        ni = sumPos.Count;
                        map[k] = ni;
                        sumPos.Add(Vector3.zero);
                        sumN.Add(Vector3.zero);
                        sumUV.Add(Vector2.zero);
                        sumT.Add(hasT ? new Vector4(0f, 0f, 0f, tans[vi].w) : Vector4.zero);
                        cnt.Add(0);
                    }
                    vmap[vi] = ni;
                    sumPos[ni] += p;
                    if (hasN) sumN[ni] += norms[vi];
                    if (hasUV) sumUV[ni] += uvs[vi];
                    if (hasT) { var t = tans[vi]; sumT[ni] += new Vector4(t.x, t.y, t.z, 0f); }
                    cnt[ni]++;
                }
                tris.Add(ni);
            }
            var kept = new List<int>();
            for (int i = 0; i + 2 < tris.Count; i += 3)
            {
                int a = tris[i], c1 = tris[i + 1], c2 = tris[i + 2];
                if (a == c1 || c1 == c2 || a == c2) continue;
                kept.Add(a); kept.Add(c1); kept.Add(c2);
            }
            b.tris[s] = kept;
            b.triCount += kept.Count / 3;
        }
        for (int i = 0; i < sumPos.Count; i++)
        {
            float f = 1f / Mathf.Max(1, cnt[i]);
            b.pos.Add(sumPos[i] * f);
            b.nrm.Add(hasN ? sumN[i].normalized : Vector3.up);
            b.uv.Add(sumUV[i] * f);
            var t = sumT[i];
            var tv = new Vector3(t.x, t.y, t.z);
            tv = tv.sqrMagnitude > 1e-8f ? tv.normalized : Vector3.right;
            b.tan.Add(new Vector4(tv.x, tv.y, tv.z, t.w));
        }
        return b;
    }
}
