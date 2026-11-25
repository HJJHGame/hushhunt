using UnityEngine.SceneManagement;

// 현재 씬을 처음 상태로 다시 불러온다. 라운드 재시작(R)과 타이틀로 돌아가기가 같이 쓴다.
public static class SceneReloader
{
    public static void ReloadActive()
    {
        var scene = SceneManager.GetActiveScene();
        if (scene.buildIndex >= 0)
            SceneManager.LoadScene(scene.buildIndex);
        else
            SceneManager.LoadScene(scene.name);
    }
}
