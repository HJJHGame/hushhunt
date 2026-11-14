# Hush Hunt

3:3 비대칭 온라인 데스매치 (숨는 팀 vs 잡는 팀) 프로토타입. Unity 6.

## 현재 상태 (1단계)

- 평지 맵 + 1인칭 이동만 구현 (6명이 돌아다닐 크기, 약 60x60m)
- 분위기: 어둡고 축축한 숲 (안개, 어두운 조명, 나무 기둥 오브젝트)
- 아직 없는 것: 총, 발소리 표시, 타이머, 승패 판정, 네트워크

## 휴가 때 여는 법 (Unity 6)

1. PC에 Unity Hub 설치 → Unity 6 LTS (6000.0.x 아무 버전) 설치
2. 이 저장소 클론
3. Unity Hub → Add → Add project from disk → 클론한 폴더 선택
4. 프로젝트 열기. 버전 다르다고 뜨면 설치된 Unity 6으로 열기 선택해도 됨
5. 처음 열 때 `Assets/Scenes/Main.unity` 가 자동 생성됨 (SetupScene 스크립트가 만들어줌)
6. Play 버튼 누르면 바로 걸어다닐 수 있음

## 조작

- WASD 이동 / Shift 달리기 / Space 점프
- 마우스 시점 / ESC 커서 해제, 클릭하면 다시 잠김

## 구조

- `Assets/Scripts/FirstPersonController.cs` - 1인칭 이동/시점
- `Assets/Editor/SetupScene.cs` - 첫 실행 때 맵 씬 자동 생성 (에디터 전용)
- `Packages/manifest.json`, `ProjectSettings/ProjectVersion.txt` - Unity 프로젝트 인식용
