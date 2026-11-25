using System.Threading;
using UnityEngine;

// 최대 FPS를 매 프레임 다시 확인해서 지킨다.
// 1) 다른 곳(에디터 설정, 다른 스크립트)이 vSync/targetFrameRate를 바꿔도 되돌린다.
// 2) 에디터에서는 targetFrameRate의 대기가 Stats 창 FPS에 안 잡혀 수백 FPS로 보일 수 있어서,
//    LateUpdate에서 직접 남은 시간만큼 기다린다. 빌드에서는 targetFrameRate만 쓴다.
public class FrameLimiter : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Create()
    {
        if (FindFirstObjectByType<FrameLimiter>() != null) return;
        var go = new GameObject("FrameLimiter");
        DontDestroyOnLoad(go);
        go.AddComponent<FrameLimiter>();
    }

#if UNITY_EDITOR
    readonly System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();
    double nextTime;
#endif

    void Update()
    {
        int target = GameSettings.MaxFps == 0 ? -1 : GameSettings.MaxFps;
        if (QualitySettings.vSyncCount != 0) QualitySettings.vSyncCount = 0;
        if (Application.targetFrameRate != target) Application.targetFrameRate = target;
    }

#if UNITY_EDITOR
    void LateUpdate()
    {
        int fps = GameSettings.MaxFps;
        if (fps <= 0) { nextTime = 0; return; }
        double step = 1.0 / fps;
        double now = clock.Elapsed.TotalSeconds;
        if (nextTime == 0) nextTime = now;
        nextTime += step;
        if (nextTime < now) nextTime = now; // 프레임이 늦었으면 몰아서 따라잡지 않는다
        double wait = nextTime - now;
        if (wait > 0.002) Thread.Sleep((int)((wait - 0.001) * 1000.0));
        while (clock.Elapsed.TotalSeconds < nextTime) { }
    }
#endif
}
