using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Procedural pine LOD and shadow optimization for Hush Hunt.
// Runs after TreeModels (-50) and before TreePaper (+50).
[DefaultExecutionOrder(0)]
public sealed class ForestPerformance : MonoBehaviour
{
    [Header("LOD thresholds (screen-relative height)")]
    [Range(0.01f, 0.5f)] public float highDetailThreshold = 0.12f;
    [Range(0.001f, 0.2f)] public float proxyThreshold = 0.025f;
    [Range(0.001f, 0.1f)] public float cullThreshold = 0.008f;

    [Header("Shadows")]
    public bool disableFoliageShadows = true;
    public bool proxyCastsShadows = false;

    static Mesh proxyTrunkMesh;
    static Mesh proxyFoliageMesh;
    static int processed;
    Material proxyBarkMaterial;
    Material proxyLeafMaterial;

    void Start()
    {
        var all = FindObjectsByType<Transform>(FindObjectsSortMode.None);
        int proceduralCount = 0;

        foreach (var tree in all)
        {
            if (tree == null || tree.name != WorldConfig.Names.PineTree) continue;

            // TreeModels creates this hierarchy only for the built-in procedural tree.
            var model = tree.Find("TreeModel");
            if (model == null || tree.Find("TreeModel Fir") != null) continue;

            var trunk = model.Find("Trunk");
            var foliage = model.Find("Foliage");
            if (trunk == null || foliage == null) continue;
            if (tree.GetComponent<LODGroup>() != null) continue;

            var trunkRenderer = trunk.GetComponent<MeshRenderer>();
            var foliageRenderer = foliage.GetComponent<MeshRenderer>();
            if (trunkRenderer == null || foliageRenderer == null) continue;

            ApplyInstancing(trunkRenderer.sharedMaterial);
            ApplyInstancing(foliageRenderer.sharedMaterial);

            if (disableFoliageShadows)
            {
                foliageRenderer.shadowCastingMode = ShadowCastingMode.Off;
                foliageRenderer.receiveShadows = false;
            }

            AddProxy(model, trunkRenderer.sharedMaterial, foliageRenderer.sharedMaterial);
            var proxyTrunk = model.Find("LODProxy/ProxyTrunk").GetComponent<Renderer>();
            var proxyFoliage = model.Find("LODProxy/ProxyFoliage").GetComponent<Renderer>();

            proxyTrunk.shadowCastingMode = proxyCastsShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            proxyFoliage.shadowCastingMode = ShadowCastingMode.Off;
            proxyTrunk.receiveShadows = false;
            proxyFoliage.receiveShadows = false;

            var lodGroup = tree.gameObject.AddComponent<LODGroup>();
            lodGroup.fadeMode = LODFadeMode.None;
            lodGroup.animateCrossFading = false;

            var lod0 = new Renderer[] { trunkRenderer, foliageRenderer };
            var lod1 = new Renderer[] { proxyTrunk, proxyFoliage };
            var lods = new[]
            {
                new LOD(highDetailThreshold, lod0),
                new LOD(proxyThreshold, lod1),
                new LOD(cullThreshold, new Renderer[0])
            };

            lodGroup.SetLODs(lods);
            lodGroup.RecalculateBounds();
            proceduralCount++;
        }

        processed = proceduralCount;
        Debug.Log("[HushHunt] ForestPerformance: procedural trees optimized = " + processed);
    }

    static void ApplyInstancing(Material material)
    {
        if (material != null && material.enableInstancing == false)
            material.enableInstancing = true;
    }

    void AddProxy(Transform model, Material bark, Material foliage)
    {
        EnsureProxyMeshes();

        var root = new GameObject("LODProxy");
        root.transform.SetParent(model, false);
        root.transform.localPosition = Vector3.zero;
        root.transform.localRotation = Quaternion.identity;
        root.transform.localScale = Vector3.one;

        // TreeModels는 원본 렌더러를 전용 원거리 컬링 레이어(30)에 둔다.
        // 프록시가 기본 레이어(0)에 남으면 50m 레이어 컬링을 우회하므로 계층 전체에 레이어를 맞춘다.
        var trunkT = model.Find("Trunk");
        int renderLayer = trunkT != null ? trunkT.gameObject.layer : model.gameObject.layer;
        root.layer = renderLayer;

        CreateProxyPart(root.transform, "ProxyTrunk", proxyTrunkMesh, bark, renderLayer);
        CreateProxyPart(root.transform, "ProxyFoliage", proxyFoliageMesh, foliage, renderLayer);
    }

    static void CreateProxyPart(Transform parent, string name, Mesh mesh, Material material, int layer)
    {
        var go = new GameObject(name);
        go.layer = layer;
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    static void EnsureProxyMeshes()
    {
        if (proxyTrunkMesh == null)
        {
            proxyTrunkMesh = BuildTrunkMesh();
            proxyTrunkMesh.name = "HushHunt Pine LOD Trunk";
            proxyTrunkMesh.UploadMeshData(true);
        }

        if (proxyFoliageMesh == null)
        {
            proxyFoliageMesh = BuildFoliageMesh();
            proxyFoliageMesh.name = "HushHunt Pine LOD Foliage";
            proxyFoliageMesh.UploadMeshData(true);
        }
    }

    static Mesh BuildTrunkMesh()
    {
        const int sides = 6;
        const float bottomRadius = 0.22f;
        const float topRadius = 0.025f;
        const float height = 9.5f;

        var vertices = new List<Vector3>();
        var triangles = new List<int>();

        for (int ring = 0; ring < 2; ring++)
        {
            float radius = ring == 0 ? bottomRadius : topRadius;
            float y = ring == 0 ? 0f : height;
            for (int i = 0; i < sides; i++)
            {
                float angle = i * Mathf.PI * 2f / sides;
                vertices.Add(new Vector3(Mathf.Cos(angle) * radius, y, Mathf.Sin(angle) * radius));
            }
        }

        for (int i = 0; i < sides; i++)
        {
            int next = (i + 1) % sides;
            int a = i;
            int b = next;
            int c = sides + next;
            int d = sides + i;
            triangles.AddRange(new[] { a, d, c, a, c, b });
        }

        var mesh = new Mesh();
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    static Mesh BuildFoliageMesh()
    {
        const int sides = 7;
        var vertices = new List<Vector3>();
        var triangles = new List<int>();

        AddCone(vertices, triangles, sides, 3.4f, 6.2f, 1.15f);
        AddCone(vertices, triangles, sides, 4.9f, 7.4f, 0.82f);
        AddCone(vertices, triangles, sides, 6.5f, 8.7f, 0.52f);
        AddCone(vertices, triangles, sides, 7.9f, 9.5f, 0.28f);

        var mesh = new Mesh();
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    static void AddCone(List<Vector3> vertices, List<int> triangles,
        int sides, float baseY, float tipY, float radius)
    {
        int start = vertices.Count;
        for (int i = 0; i < sides; i++)
        {
            float angle = i * Mathf.PI * 2f / sides;
            vertices.Add(new Vector3(Mathf.Cos(angle) * radius, baseY,
                Mathf.Sin(angle) * radius));
        }

        int tip = vertices.Count;
        vertices.Add(new Vector3(0f, tipY, 0f));

        for (int i = 0; i < sides; i++)
        {
            int next = (i + 1) % sides;
            triangles.Add(tip);
            triangles.Add(start + next);
            triangles.Add(start + i);
        }
    }
}
