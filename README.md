# Hush Hunt

3:3 비대칭 온라인 데스매치 (숨는 팀 vs 잡는 팀) 프로토타입. Unity 6.

## 현재 상태 (1단계)

- 평지 맵 + 1인칭 이동 (6명이 돌아다닐 크기, 약 200x200m)
- 분위기: 슬렌더맨 같은 공포 숲 (짙은 안개, 어두운 밤, 키 크고 가는 소나무 실루엣 160그루 + 바위, 낡은 울타리 소품)
- 걸을 때 발소리 (나뭇잎 밟는 소리, 코드가 런타임에 생성)
- 미니맵: 화면 오른쪽 위에 맵 전체가 보이고, 중앙 화살표가 플레이어 위치/방향
- 아직 없는 것: 총, 발소리 표시, 타이머, 승패 판정, 네트워크

## 휴가 때 여는 법 (Unity 6)

> 주의: Unity Hub에서 "New project"로 만들지 말 것. 반드시 이 저장소(또는 ZIP을 푼 hushhunt-main 폴더)를 Add project from disk로 추가. 선택할 폴더 안에 Assets, Packages, ProjectSettings가 바로 보여야 함. 열었을 때 Assets에 Scripts, Editor 폴더가 있고 Scripts/FirstPersonController.cs가 보이면 정상. Tutorials, TextMesh Pro 폴더가 보이면 잘못 연 것(Unity 템플릿).

1. PC에 Unity Hub 설치 → Unity 6 LTS (6000.0.x 아무 버전) 설치
2. 이 저장소 클론
3. Unity Hub → Add → Add project from disk → 클론한 폴더 선택
4. 프로젝트 열기. 버전 다르다고 뜨면 설치된 Unity 6으로 열기 선택해도 됨
5. 처음 열 때 `Assets/Scenes/Main.unity` 가 자동 생성됨 (SetupScene 스크립트가 만들어줌)
   - 씬 생성 코드가 바뀌면 다음에 열 때 자동으로 재생성됨 (수동: 메뉴 Hush Hunt > Setup Main Scene)
6. Play 버튼 누르면 바로 걸어다닐 수 있음

## 조작

- WASD 이동 / Shift 달리기 / Space 점프
- 마우스 시점 / ESC 커서 해제, 클릭하면 다시 잠김
- 미니맵은 오른쪽 위에 항상 표시 (조작 키 없음)

## 구조

- `Assets/Scripts/FirstPersonController.cs` - 1인칭 이동/시점
- `Assets/Scripts/Footsteps.cs` - 발소리 (나뭇잎 소리를 코드로 생성해 재생)
- `Assets/Scripts/Minimap.cs` - 미니맵 (탑다운 카메라 + 화면 오른쪽 위 표시 + 플레이어 화살표)
- `Assets/Editor/SetupScene.cs` - 첫 실행 때 맵 씬 자동 생성 (에디터 전용)
- `Packages/manifest.json`, `ProjectSettings/ProjectVersion.txt` - Unity 프로젝트 인식용
