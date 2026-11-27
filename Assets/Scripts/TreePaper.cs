using UnityEngine;
using System.Collections.Generic;

// 나무마다 깨끗한 흰색 A4 용지를 붙인다 (슬렌더 페이지). 한 나무에 한 장, 맵에 PaperCount장. 매 판 무작위 나무/방향.
// 가까이 가면 용지가 어둠 속에서 환하게 빛나 눈에 띄고, 잡는 팀이 F를 3초 누르면 줍고 총을 받는다 (Armory 규칙).
// 숨는 팀은 용지를 못 줍는다. SpruceForest가 모델로 교체한 뒤에 실행되어야 보이는 줄기 크기를 잴 수 있다.
[DefaultExecutionOrder(50)]
public class TreePaper : MonoBehaviour
{
    // 종이 소리는 잠시 꺼둠. 다시 켜려면 false를 true로 바꾼다.
    static bool PAPER_SOUND = false;

    const int PaperCount = 10;         // 맵에 놓는 종이 수
    const float MinPaperSpacing = 30f; // 종이끼리 최소 거리(m). 자리가 모자라면 줄인다
    const float GlowDistance = 15f;    // 이 거리 안에 오면 용지가 환하게 빛난다(m). 멀리서도 눈에 띄게

    class Paper
    {
        public GameObject go;
        public Renderer renderer;
        public bool glowing;
    }

    readonly List<Paper> papers = new List<Paper>();
    Material litMat;
    Material glowMat;
    Transform playerT;
    GunHolder me;
    Armory armory;
    Paper prompt;     // 지금 줍기 안내를 띄우는 종이
    GUIStyle style;
    float takenAt = -100f;
    public const float HoldSeconds = 3f; // F를 이만큼 꾹 눌러야 줍는다
    float holdStart = -1f;
    Paper holdPaper;
    AudioClip rustle;

    void Start()
    {
        var trees = new List<Transform>();
        var deads = new List<Transform>();
        foreach (var t in FindObjectsByType<Transform>(FindObjectsSortMode.None))
        {
            // 기존 소나무 자리 나무 + SpruceForest가 추가로 심은 나무("Spruce Extra") 둘 다 후보다.
            if (!((t.parent == null && t.name == WorldConfig.Names.PineTree) || t.name == "Spruce Extra")) continue;
            trees.Add(t);
            if (t.Find("TreeModel Spruce " + (SpruceForest.PaperLabel ?? "Dead")) != null) deads.Add(t); // 가장 무거운 잎 있는 모델이 달린 나무(은행나무 80그루 중 10장)
        }
        if (trees.Count == 0) return;

        var rand = new System.Random();
        // 종이는 은행나무(SpruceForest.PaperLabel)에 붙인다. 모자라면 다른 소나무로 채운다. 종이끼리는 서로 멀리 떨어뜨린다.
        var order = new List<Transform>(Shuffled(deads, rand));
        foreach (var t in Shuffled(trees, rand))
            if (!deads.Contains(t)) order.Add(t);

        var picks = new List<Transform>();
        for (float spacing = MinPaperSpacing; picks.Count < PaperCount && spacing >= 0f; spacing -= 10f)
        {
            foreach (var t in order)
            {
                if (picks.Count >= PaperCount) break;
                if (picks.Contains(t)) continue;
                bool close = false;
                foreach (var p in picks)
                    if ((p.position - t.position).magnitude < spacing) { close = true; break; }
                if (!close) picks.Add(t);
            }
        }

        // 무늬 없는 깨끗한 흰색. glowMat는 빛이 없어도 항상 밝은 흰색 (가까이 왔을 때 표시용).
        litMat = RuntimeMaterials.CreateLit("A4 Page", new Color(0.98f, 0.98f, 0.96f), 0.02f);
        var unlit = Shader.Find("Unlit/Color");
        if (unlit == null) unlit = Shader.Find("Unlit/Texture");
        glowMat = new Material(unlit) { name = "A4 Page Glow", color = Color.white };
        if (PAPER_SOUND) rustle = MakeRustle();

        foreach (var tree in picks) PlacePaper(tree, rand);
        Debug.Log("[HushHunt] A4 용지 " + papers.Count + "장 배치 (dead-spruce " + deads.Count + "그루 중 우선)");
    }

    static List<Transform> Shuffled(List<Transform> src, System.Random rand)
    {
        var list = new List<Transform>(src);
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rand.Next(i + 1);
            var tmp = list[i]; list[i] = list[j]; list[j] = tmp;
        }
        return list;
    }

    // 나무 하나에 종이를 붙인다.
    void PlacePaper(Transform tree, System.Random rand)
    {
        float yaw = (float)rand.NextDouble() * 360f;
        var dir = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;

        // 보이는 줄기 메시에 레이캐스트해 실제 표면 위치와 바깥 법선을 구한다.
        // (모델마다 굵기/기울기/면 방향이 달라 추정치를 쓰면 용지가 기둥 속에 묻히거나 옆을 보게 된다.)
        Vector3 surfacePos, outward;
        if (!TryTrunkSurface(tree, dir, tree.position.y + 1.5f, out surfacePos, out outward))
        {
            float surface = TrunkSurfaceDistance(tree, dir, tree.position.y + 1.5f);
            surfacePos = tree.position + Vector3.up * 1.5f + dir * surface;
            outward = dir;
        }

        // 얇은 상자: 어느 쪽에서 봐도 보인다 (Quad는 한쪽 면만 보임).
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "A4 Page";
        Destroy(go.GetComponent<Collider>()); // 걸리적거리지 않게
        // 표면에서 6mm + 두께의 절반만큼 바깥에 띄우고, 면이 바깥(법선)을 보게 세워 껍질에 붙인다.
        go.transform.position = surfacePos + outward * (0.006f + 0.0015f);
        go.transform.rotation = Quaternion.LookRotation(outward, Vector3.up);
        go.transform.localScale = new Vector3(0.21f, 0.297f, 0.003f); // A4 (m)
        go.transform.SetParent(tree, true); // 월드 위치/회전 유지

        var item = new Paper { go = go, renderer = go.GetComponent<Renderer>() };
        item.renderer.sharedMaterial = litMat;
        item.renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        if (PAPER_SOUND)
        {
            // 종이 바스락 소리. 완전 3D라 멀리서도 어느 방향인지 들리고, 가까워질수록 커진다.
            var src = go.AddComponent<AudioSource>();
            src.clip = rustle;
            src.loop = true;
            src.spatialBlend = 1f; // 완전 3D
            src.rolloffMode = AudioRolloffMode.Linear;
            src.minDistance = 2f;  // 이 안에서는 최대 음량
            src.maxDistance = 30f; // 30m 밖에서는 들리지 않는다
            src.dopplerLevel = 0f;
            src.volume = 0.9f;
            src.Play();
        }
        papers.Add(item);
        Debug.Log("[HushHunt] A4 용지 부착: " + tree.name + " 위치 (" + tree.position.x.ToString("0.0")
            + ", " + tree.position.z.ToString("0.0") + ")");
    }

    // 가까이 가면 용지가 눈에 띄고, 잡는 팀이 2.2m 안에서 F를 3초 누르면 줍는다.
    void Update()
    {
        prompt = null;
        if (papers.Count == 0) return;
        if (playerT == null)
        {
            var f = FindFirstObjectByType<FirstPersonController>();
            if (f != null) { playerT = f.transform; me = f.GetComponent<GunHolder>(); }
            return;
        }
        if (armory == null) armory = FindFirstObjectByType<Armory>();
        if (MainMenu.IsMenuOpen) return;

        Paper nearest = null;
        float nearestDist = 2.2f;
        foreach (var p in papers)
        {
            if (p.go == null) continue;
            Vector3 d = p.go.transform.position - playerT.position;
            d.y = 0f;
            float dist = d.magnitude;

            // 가까이 오면 용지만 환하게 빛나 어둠 속에서 눈에 띈다. 빛을 쏘지 않아 주변은 밝아지지 않는다.
            bool near = dist < GlowDistance;
            if (near != p.glowing)
            {
                p.glowing = near;
                p.renderer.sharedMaterial = near ? glowMat : litMat;
            }
            if (dist < nearestDist) { nearest = p; nearestDist = dist; }
        }

        if (nearest == null || armory == null || !armory.CanTakePaper(me)) { holdStart = -1f; return; }
        prompt = nearest;
        // 같은 종이에서 F를 계속 누르고 있어야 시간이 쌓인다. 떼거나 다른 종이로 옮기면 처음부터.
        if (!Input.GetKey(KeyCode.F) || holdPaper != nearest) holdStart = -1f;
        holdPaper = nearest;
        if (Input.GetKeyDown(KeyCode.F)) holdStart = Time.unscaledTime;
        if (holdStart >= 0f && Time.unscaledTime - holdStart >= HoldSeconds)
        {
            holdStart = -1f;
            takenAt = Time.unscaledTime;
            Destroy(nearest.go);
            papers.Remove(nearest);
            prompt = null;
            armory.OnPaperTaken(me); // 총 4발 지급
            Debug.Log("[HushHunt] A4 용지를 찾았다! 남은 용지 " + papers.Count + "장");
        }
    }

    // 줍기 안내와, 주운 직후 "찾았다" 표시. 검정 화면에서도 읽히게 그림자를 깐다.
    void OnGUI()
    {
        if (MainMenu.IsMenuOpen) return;
        if (Event.current.type != EventType.Repaint) return;
        if (style == null)
        {
            style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter };
            style.fontStyle = FontStyle.Bold;
            style.fontSize = Mathf.RoundToInt(26f * Mathf.Clamp(Screen.height / 1080f, 0.75f, 1.5f));
        }
        if (Time.unscaledTime - takenAt < 6f) { ShadowText("A4 용지를 찾았다! 총 " + GunHolder.SeekerRounds + "발"); return; }
        if (prompt != null)
        {
            if (holdStart >= 0f)
            {
                float p01 = Mathf.Clamp01((Time.unscaledTime - holdStart) / HoldSeconds);
                ShadowText("줍는 중... " + Mathf.RoundToInt(p01 * 100f) + "%  (F를 계속 누르세요)");
            }
            else ShadowText("용지 줍기: F를 3초 꾹 누르세요");
        }
    }

    void ShadowText(string msg)
    {
        HudText.Label(new Rect(0f, Screen.height * 0.62f, Screen.width, 44f), msg, style);
    }

    void OnDestroy()
    {
        if (rustle != null) Destroy(rustle);
        if (glowMat != null) Destroy(glowMat);
    }

    // 보이는 줄기 메시에 레이캐스트해 dir 방향의 실제 표면 위치와 바깥 법선을 구한다.
    // 임시 MeshCollider를 전용 레이어(31)에 걸어 기존 충돌체는 무시하고, 끝나면 지운다.
    static bool TryTrunkSurface(Transform tree, Vector3 dir, float worldY, out Vector3 point, out Vector3 normal)
    {
        point = default;
        normal = default;
        Transform model = null; // "TreeModel Spruce Dead" 등
        foreach (Transform c in tree)
            if (c.name.StartsWith("TreeModel")) { model = c; break; }
        if (model == null) return false;

        // 1.5m 부근 띠에 닿는 줄기(나무 재질) 렌더러를 모은다. 재질명으로 못 찾으면 띠의 전체 메시를 쓴다.
        var meshes = new List<MeshRenderer>();
        foreach (var r in model.GetComponentsInChildren<MeshRenderer>())
        {
            var b = r.bounds;
            if (b.max.y < worldY - 0.3f || b.min.y > worldY + 0.3f) continue;
            var m = r.sharedMaterial;
            string n = m != null ? m.name.ToLowerInvariant() : "";
            if (r.name.EndsWith("_Branches")) continue; // 키트 나무의 가지 메시는 줄기가 아니다
            if (n.Contains("bark") || n.Contains("wood") || n.Contains("trunk")) meshes.Add(r);
        }
        if (meshes.Count == 0)
        {
            foreach (var r in model.GetComponentsInChildren<MeshRenderer>())
            {
                var b = r.bounds;
                if (b.max.y < worldY - 0.3f || b.min.y > worldY + 0.3f) continue;
                meshes.Add(r);
            }
        }
        if (meshes.Count == 0) return false;

        // 레이어와 임시 콜라이더를 동일한 Renderer 단위로 묶는다.
        // 일부 Renderer는 MeshFilter가 없거나 메시가 너무 커서 건너뛸 수 있으므로
        // 별도 리스트 인덱스를 맞춰 복구하면 잘못된 오브젝트의 레이어가 바뀔 수 있다.
        var temporary = new List<(MeshRenderer renderer, int oldLayer, MeshCollider collider)>();
        foreach (var r in meshes)
        {
            var mf = r.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) continue;
            if (mf.sharedMesh.vertexCount > 20000) continue;

            int oldLayer = r.gameObject.layer;
            r.gameObject.layer = 31;
            var mc = r.gameObject.AddComponent<MeshCollider>();
            mc.sharedMesh = mf.sharedMesh;
            temporary.Add((r, oldLayer, mc));
        }

        if (temporary.Count == 0) return false;

        bool ok = false;
        var origin = new Vector3(tree.position.x, worldY, tree.position.z) + dir * 3f;
        if (Physics.Raycast(origin, -dir, out var hit, 6f, 1 << 31, QueryTriggerInteraction.Ignore))
        {
            point = hit.point;
            normal = hit.normal;
            if (Vector3.Dot(normal, dir) < 0f) normal = -normal; // 항상 바깥을 보게
            ok = true;
        }

        foreach (var item in temporary)
        {
            if (item.collider != null) Destroy(item.collider);
            if (item.renderer != null) item.renderer.gameObject.layer = item.oldLayer;
        }
        return ok;
    }

    // 나무 축에서 dir 방향으로, 지정 높이 부근(±0.3m)의 줄기 바깥 표면까지의 수평 거리.
    // 실제 모델(TreeModel)의 나무 재질 렌더러 경계상자로 재고, 모델이 없으면 옛 원뿔 기둥 크기로 추정한다.
    static float TrunkSurfaceDistance(Transform tree, Vector3 dir, float worldY)
    {
        var model = tree.Find("TreeModel");
        float best = -1f;
        if (model != null)
        {
            foreach (var r in model.GetComponentsInChildren<Renderer>())
            {
                var b = r.bounds;
                if (b.max.y < worldY - 0.3f || b.min.y > worldY + 0.3f) continue;
                // 줄기(나무 재질)만 잰다. 잎이 끼면 표면이 과대계산되어 용지가 붕 뜬다.
                var m = r.sharedMaterial;
                string n = m != null ? m.name.ToLowerInvariant() : "";
                bool wood = n.Contains("bark") || n.Contains("wood") || n.Contains("trunk");
                if (!wood) continue;
                // 경계상자의 dir 방향 표면: 중심의 방향 성분 + 방향별 반경의 합
                float along = dir.x * (b.center.x - tree.position.x) + dir.z * (b.center.z - tree.position.z);
                float extent = Mathf.Abs(dir.x) * b.extents.x + Mathf.Abs(dir.z) * b.extents.z;
                best = Mathf.Max(best, along + extent);
            }
        }
        if (best < 0f)
        {
            var trunk = tree.Find(WorldConfig.Names.PineTrunk);
            best = trunk != null ? trunk.localScale.x * 0.5f : 0.25f;
        }
        return best;
    }

    // 6초짜리 종이 바스락 소리. 바람에 펄럭이는 종이: 중역 노이즈 + 빠른 떨림 + 가끔 큰 펄럭임.
    // 모든 진동이 6초의 정수배 주기라 반복해도 자연스럽고, 앞뒤 크로스페이드로 이음매를 지운다
    // (AmbientSound.MakeWind 와 같은 방식).
    static AudioClip MakeRustle()
    {
        const int rate = 22050;
        const int n = rate * 6;
        const int xf = rate / 2;
        var buf = new float[n + xf];
        var r = new System.Random(7);
        float lo = 0f, mid = 0f, hi = 0f;

        for (int i = 0; i < n + xf; i++)
        {
            float t = i / (float)rate;
            float noise = (float)r.NextDouble() * 2f - 1f;
            lo += (noise - lo) * 0.02f;
            mid += (noise - mid) * 0.12f;
            hi += (noise - hi) * 0.45f;
            // 플러터: 초당 약 11번 떨림 (6초에 66주기 = 정수배)
            float flutter = 0.6f + 0.4f * Mathf.Sin(2f * Mathf.PI * 66f * t / 6f
                + Mathf.Sin(2f * Mathf.PI * 6f * t / 6f));
            // 펄럭임: 6초에 3번 살짝 더 크게 (정수배)
            float flap = 1f + 0.8f * Mathf.Max(0f, Mathf.Sin(2f * Mathf.PI * 3f * t / 6f + 0.7f));
            buf[i] = ((mid - lo) * 1.1f + hi * 0.5f) * flutter * flap;
        }

        var s = new float[n];
        for (int i = 0; i < n; i++) s[i] = buf[i];
        for (int i = 0; i < xf; i++)
            s[i] = Mathf.Lerp(buf[n + i], buf[i], i / (float)xf);

        float max = 0.001f;
        for (int i = 0; i < n; i++) max = Mathf.Max(max, Mathf.Abs(s[i]));
        float k = 0.8f / max;
        for (int i = 0; i < n; i++) s[i] *= k;

        var clip = AudioClip.Create("Paper Rustle", n, 1, rate, false);
        clip.SetData(s, 0);
        return clip;
    }
}
