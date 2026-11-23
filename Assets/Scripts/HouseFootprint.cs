using UnityEngine;

// 집의 발자국(폭/깊이). 씬 생성 시 집 루트 오브젝트에 붙는다.
// 미니맵이 이 값을 읽어 실제 집과 같은 위치/크기로 지도에 그린다.
public class HouseFootprint : MonoBehaviour
{
    public Vector2 size; // x: 폭(월드 X, m), y: 깊이(월드 Z, m)
}
