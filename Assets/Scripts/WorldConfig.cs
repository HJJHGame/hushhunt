// 씬 생성 코드(SetupScene)와 런타임 시스템이 함께 쓰는 맵 상수와 오브젝트 이름.
// 문자열/숫자가 여러 파일에 흩어져 있으면 한쪽만 바뀌었을 때 조용히 어긋나므로 한 곳에 모은다.
public static class WorldConfig
{
    // 맵 절반 크기(m). 경계 벽 안쪽 면은 ±(Half - 1), 미니맵은 ±Half로 그린다.
    public const float Half = 200f; // 400x400m 맵

    // 테스트용: true면 나무와 땅만 남기고 바위/집/통나무/그루터기/울타리를 숨긴다 (코드는 그대로, false로 바꾸면 복구).
    public static bool SimplifiedTestMode = true;

    // 소나무 규격. SetupScene이 만들고 TreeModels가 3D 모델 크기를 맞출 때 같은 값을 쓴다.
    public const float PineTrunkBaseWidth = 0.32f; // 크기 배율 1일 때 기둥 굵기(m)
    public const float PineHeight = 9.5f;          // 크기 배율 1일 때 나무 전체 높이(m)

    // 런타임 시스템이 이름으로 찾는 오브젝트들. SetupScene이 이 이름으로 만든다.
    public static class Names
    {
        public const string Ground = "Ground";
        public const string PineTree = "Pine Tree";
        public const string PineTrunk = "Trunk";
        public const string PineFoliagePrefix = "Foliage"; // Foliage1 ~ Foliage3
        public const string Rock = "Rock";
        public const string EdgeWall = "Edge Wall";
        public const string OldFence = "Old Fence";
        public const string House = "House";
        public const string FallenLog = "Fallen Log";
        public const string Stump = "Stump";
    }
}
